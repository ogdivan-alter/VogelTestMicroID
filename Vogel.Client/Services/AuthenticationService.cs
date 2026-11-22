using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Vogel.Shared.DTOs;
using Vogel.Shared.Mappers;
using Vogel.Shared.Models;

namespace Vogel.Client.Services;

/// <summary>
/// Implementación del servicio de autenticación en Vogel.Client.
/// Consume exclusivamente el endpoint BFF en Vogel.Server ('api/auth/login').
/// La resolución y gestión del proveedor de autenticación es 100% transparente para el cliente
/// y es gestionada exclusivamente por Vogel.Server.
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    public const string SessionUserKey = "vogel_user_session";
    public const string BackendTokenKey = "vogel_backend_token";

    private readonly HttpClient _httpClient;
    private readonly SessionState _sessionState;
    private readonly ISecureSessionStorageService _sessionStorage;
    private readonly ISessionValidationService _sessionValidation;
    private readonly IClientLogger _logger;
    private readonly IConfiguration _configuration;

    public AuthenticationService(
        HttpClient httpClient,
        SessionState sessionState,
        ISecureSessionStorageService sessionStorage,
        ISessionValidationService sessionValidation,
        IClientLogger logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _sessionState = sessionState;
        _sessionStorage = sessionStorage;
        _sessionValidation = sessionValidation;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Inicia sesión enviando las credenciales al BFF (Vogel.Server) de forma transparente.
    /// </summary>
    public async Task<(bool Success, string ErrorMessage)> LoginAsync(string username, string password, bool rememberMe = false)
    {
        try
        {
            var request = new LoginRequestDto
            {
                Username = username.Trim(),
                Password = password,
                RememberMe = rememberMe
            };

            var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);
            var statusCode = (int)response.StatusCode;

            string? token = null;
            if (response.Headers.TryGetValues("X-Session-Token", out var sessionTokens))
            {
                token = sessionTokens.FirstOrDefault();
            }

            LoginResponseDto? loginResponse = null;
            try
            {
                loginResponse = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
            }
            catch (Exception)
            {
                // En caso de respuesta de error no JSON
            }

            if (!response.IsSuccessStatusCode || loginResponse == null || !loginResponse.Success || loginResponse.User == null)
            {
                var errorMsg = loginResponse?.Message;
                if (string.IsNullOrWhiteSpace(errorMsg))
                {
                    errorMsg = statusCode == 401 ? "Usuario o Clave incorrectos" : "Error en el servicio de autenticación";
                }

                await _logger.LogWarningAsync($"Login rechazado en BFF para usuario '{username}' con código {statusCode}: {errorMsg}", nameof(AuthenticationService));
                return (false, errorMsg);
            }

            var user = loginResponse.User;
            user.BaseRole = "Único";

            // Persistir sesión cifrada, token y establecer marcador de servidor concurrentemente
            var persistUserTask = _sessionStorage.SetItemEncryptedObjectAsync(SessionUserKey, user).AsTask();
            var persistTokenTask = !string.IsNullOrWhiteSpace(token)
                ? _sessionStorage.SetItemEncryptedAsync(BackendTokenKey, token).AsTask()
                : Task.CompletedTask;
            var markerTask = _sessionValidation.EstablishSessionAsync();

            await Task.WhenAll(persistUserTask, persistTokenTask, markerTask);

            var markerEstablished = await markerTask;
            if (!markerEstablished)
            {
                await _sessionStorage.ClearAsync();
                _sessionState.ClearUser();
                await _logger.LogWarningAsync("No se pudo establecer el marcador de sesión con el servidor.", nameof(AuthenticationService));
                return (false, "No fue posible validar la sesión con el servidor. Intente nuevamente.");
            }

            _sessionState.SetUser(user);
            await _logger.LogInformationAsync($"Login exitoso en BFF para usuario '{username}'.", nameof(AuthenticationService));
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _sessionState.ClearUser();
            await _logger.LogErrorAsync("Error inesperado durante la autenticación con el BFF.", ex, nameof(AuthenticationService));
            return (false, "Ha ocurrido un error inesperado al conectar con el servidor. Por favor intente nuevamente.");
        }
    }

    /// <summary>
    /// Cierra la sesión activa en el cliente y notifica al BFF.
    /// </summary>
    public async Task LogoutAsync()
    {
        try
        {
            var username = _sessionState.CurrentUser?.Username ?? "desconocido";

            if (_httpClient.BaseAddress != null)
            {
                try
                {
                    await _httpClient.PostAsync("api/auth/logout", null);
                }
                catch
                {
                    // Falla silenciosa si el servidor no responde al momento del logout
                }
            }

            _sessionState.ClearUser();
            await _sessionStorage.ClearAsync();
            await _logger.LogInformationAsync($"Sesión cerrada para usuario '{username}'.", nameof(AuthenticationService));
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync("Error durante el cierre de sesión.", ex, nameof(AuthenticationService));
        }
    }

    /// <summary>
    /// Mapea los DTOs devueltos por el backend hacia el modelo User de dominio de Vogel.
    /// Mantenido por retrocompatibilidad delegando en BackendUserMapper.
    /// </summary>
    public static User MapToUser(BackendLoginResponseDto profile, IEnumerable<BackendPrivilegeItemDto> backendPrivileges, string backendBaseUrl)
        => BackendUserMapper.MapToUser(profile, backendPrivileges, backendBaseUrl);
}
