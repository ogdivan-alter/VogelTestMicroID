using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Vogel.Shared.DTOs;
using Vogel.Shared.Models;

namespace Vogel.Client.Services;

/// <summary>
/// Implementación del servicio de validación de instancia y sesión en Vogel.Client.
/// Consulta exclusivamente el endpoint del BFF (api/session/validate), eliminando
/// cualquier llamada directa desde el navegador hacia APIs o microservicios externos.
/// </summary>
public class SessionValidationService : ISessionValidationService
{
    private const string ServerMarkerKey = "vogel_server_session_marker";
    private const string SessionUserKey = "vogel_user_session";
    private const string BackendTokenKey = "vogel_backend_token";

    private readonly HttpClient _httpClient;
    private readonly ISecureSessionStorageService _sessionStorage;
    private readonly IClientLogger _logger;
    private readonly IConfiguration _configuration;
    private readonly SessionState _sessionState;

    private readonly SemaphoreSlim _validationLock = new(1, 1);
    private bool _lastResult = true;
    private DateTime _lastCheckedUtc = DateTime.MinValue;

    private TimeSpan _cacheTtl;

    public SessionValidationService(
        HttpClient httpClient,
        ISecureSessionStorageService sessionStorage,
        IClientLogger logger,
        IConfiguration configuration,
        SessionState sessionState)
    {
        _httpClient = httpClient;
        _sessionStorage = sessionStorage;
        _logger = logger;
        _configuration = configuration;
        _sessionState = sessionState;

        var ttlSeconds = configuration.GetValue<int>("SessionValidation:TtlSeconds", 10);
        _cacheTtl = TimeSpan.FromSeconds(ttlSeconds < 0 ? 0 : ttlSeconds);
    }

    /// <summary>
    /// Constructor de compatibilidad para pruebas unitarias existentes.
    /// </summary>
    public SessionValidationService(
        HttpClient httpClient,
        ISecureSessionStorageService sessionStorage,
        IClientLogger logger,
        IConfiguration configuration,
        Api.IApiBackendClient? apiBackendClient,
        SessionState sessionState)
        : this(httpClient, sessionStorage, logger, configuration, sessionState)
    {
    }

    public async Task<bool> EnsureValidAsync()
    {
        // Tolerancia cero (TTL = 0): validar siempre contra el servidor BFF.
        if (_cacheTtl > TimeSpan.Zero
            && _lastResult
            && DateTime.UtcNow - _lastCheckedUtc < _cacheTtl)
        {
            return true;
        }

        await _validationLock.WaitAsync();
        try
        {
            if (_cacheTtl > TimeSpan.Zero
                && _lastResult
                && DateTime.UtcNow - _lastCheckedUtc < _cacheTtl)
            {
                return true;
            }

            var result = await ValidateServerInstanceAsync();
            _lastResult = result;
            _lastCheckedUtc = result ? DateTime.UtcNow : DateTime.MinValue;
            return result;
        }
        finally
        {
            _validationLock.Release();
        }
    }

    public async Task<bool> EstablishSessionAsync()
    {
        try
        {
            var token = await _sessionStorage.GetItemDecryptedAsync(BackendTokenKey);

            using var request = new HttpRequestMessage(HttpMethod.Get, "api/session/validate");
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                await _logger.LogWarningAsync("Respuesta no exitosa al establecer marcador de sesión en BFF.", nameof(SessionValidationService));
                return false;
            }

            var validationResponse = await response.Content.ReadFromJsonAsync<SessionValidationDto>();
            if (validationResponse == null)
            {
                await _logger.LogWarningAsync("Respuesta nula al establecer marcador de sesión en BFF.", nameof(SessionValidationService));
                return false;
            }

            _cacheTtl = TimeSpan.FromSeconds(validationResponse.TtlSeconds < 0 ? 0 : validationResponse.TtlSeconds);

            var newMarker = $"{validationResponse.ServerStartUtc.Ticks}:{validationResponse.InstanceId}";
            await _sessionStorage.SetItemEncryptedAsync(ServerMarkerKey, newMarker);

            _lastResult = true;
            _lastCheckedUtc = DateTime.UtcNow;

            Console.WriteLine($"[SessionValidation] Marcador establecido en BFF: '{newMarker}' | ttl: {validationResponse.TtlSeconds}s");
            return true;
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync("Error al establecer marcador de sesión.", ex, nameof(SessionValidationService));
            return false;
        }
    }

    public async Task<bool> ValidateServerInstanceAsync()
    {
        try
        {
            var markerTask = _sessionStorage.GetItemDecryptedAsync(ServerMarkerKey).AsTask();
            var tokenTask = _sessionStorage.GetItemDecryptedAsync(BackendTokenKey).AsTask();

            await Task.WhenAll(markerTask, tokenTask);

            var currentMarker = await markerTask;
            var currentToken = await tokenTask;

            using var request = new HttpRequestMessage(HttpMethod.Get, "api/session/validate");
            if (!string.IsNullOrWhiteSpace(currentToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", currentToken);
            }

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _lastResult = false;
                _lastCheckedUtc = DateTime.MinValue;
                return false;
            }

            var validationResponse = await response.Content.ReadFromJsonAsync<SessionValidationDto>();

            if (validationResponse == null || !validationResponse.Valid)
            {
                Console.WriteLine("[SessionValidation] Sesión invalidada por respuesta del BFF.");
                _ = _logger.LogWarningAsync("Sesión invalidada por respuesta del servidor BFF.", nameof(SessionValidationService));
                await ClearSessionAsync();
                _lastResult = false;
                _lastCheckedUtc = DateTime.MinValue;
                return false;
            }

            // El servidor define la política de caché vigente.
            _cacheTtl = TimeSpan.FromSeconds(validationResponse.TtlSeconds < 0 ? 0 : validationResponse.TtlSeconds);

            var newMarker = $"{validationResponse.ServerStartUtc.Ticks}:{validationResponse.InstanceId}";

            Console.WriteLine($"[SessionValidation] currentMarker: '{currentMarker ?? "(null)"}' | newMarker: '{newMarker}' | ttl: {validationResponse.TtlSeconds}s");

            // 1) Si existe marcador previo
            if (!string.IsNullOrWhiteSpace(currentMarker))
            {
                // Si es distinto -> servidor reiniciado con otra instancia
                if (!string.Equals(currentMarker, newMarker, StringComparison.Ordinal))
                {
                    Console.WriteLine("[SessionValidation] Marcas DIFERENTES → INVALIDANDO sesión");
                    _ = _logger.LogInformationAsync("Cambio de instancia del servidor detectado. Se invalida la sesión.", nameof(SessionValidationService));
                    await ClearSessionAsync();
                    _lastResult = false;
                    _lastCheckedUtc = DateTime.MinValue;
                    return false;
                }

                Console.WriteLine("[SessionValidation] Marcas coinciden y sesión válida");
                _lastResult = true;
                _lastCheckedUtc = DateTime.UtcNow;
                return true;
            }

            // 2) No existe marcador previo
            var oldUserSession = await _sessionStorage.GetItemDecryptedAsync(SessionUserKey);
            if (!string.IsNullOrWhiteSpace(oldUserSession))
            {
                Console.WriteLine("[SessionValidation] Sin marcador pero CON sesión de usuario → INVALIDANDO por reinicio");
                _ = _logger.LogInformationAsync("Marcador de servidor ausente con sesión existente. Se invalida por posible reinicio del servicio.", nameof(SessionValidationService));
                await ClearSessionAsync();
                _lastResult = false;
                _lastCheckedUtc = DateTime.MinValue;
                return false;
            }

            // 3) Sin marcador y sin sesión → primera vez con esta instancia
            Console.WriteLine("[SessionValidation] Sin marcador ni sesión → guardando marcador e iniciando limpio");
            await _sessionStorage.SetItemEncryptedAsync(ServerMarkerKey, newMarker);
            _lastResult = true;
            _lastCheckedUtc = DateTime.UtcNow;
            return true;
        }
        catch (Exception ex)
        {
            _ = _logger.LogErrorAsync("Error al validar instancia del servidor en BFF.", ex, nameof(SessionValidationService));
            _lastResult = false;
            _lastCheckedUtc = DateTime.MinValue;
            return false;
        }
    }

    /// <summary>
    /// Limpia el marcador de instancia, el token y la sesión de usuario del almacenamiento del cliente.
    /// </summary>
    private async Task ClearSessionAsync()
    {
        try { await _sessionStorage.RemoveItemAsync(ServerMarkerKey); } catch { }
        try { await _sessionStorage.RemoveItemAsync(SessionUserKey); } catch { }
        try { await _sessionStorage.RemoveItemAsync(BackendTokenKey); } catch { }
        _sessionState.ClearUser();
    }
}
