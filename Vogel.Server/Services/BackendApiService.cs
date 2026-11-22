using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Vogel.Shared.DTOs;

namespace Vogel.Server.Services;

/// <summary>
/// Implementación del servicio de comunicación entre el BFF (Vogel.Server) y el Backend de negocio / autenticación externo.
/// </summary>
public class BackendApiService : IBackendApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BackendApiService> _logger;
    private readonly IConfiguration _configuration;

    public BackendApiService(HttpClient httpClient, ILogger<BackendApiService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<(BackendLoginResponseDto? Profile, string? Token, BackendErrorDto? Error, int StatusCode)> LoginAsync(
        string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var requestDto = new BackendLoginRequestDto(username, password);
            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/login", requestDto, cancellationToken);
            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                string? token = null;

                if (response.Headers.TryGetValues("X-Session-Token", out var sessionTokens))
                {
                    token = sessionTokens.FirstOrDefault();
                }

                if (string.IsNullOrWhiteSpace(token) && response.Headers.TryGetValues("Authorization", out var authHeaders))
                {
                    var authVal = authHeaders.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(authVal) && authVal.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        token = authVal["Bearer ".Length..].Trim();
                    }
                }

                var profile = await response.Content.ReadFromJsonAsync<BackendLoginResponseDto>(cancellationToken: cancellationToken);
                return (profile, token, null, statusCode);
            }

            BackendErrorDto? errorDto = null;
            try
            {
                errorDto = await response.Content.ReadFromJsonAsync<BackendErrorDto>(cancellationToken: cancellationToken);
            }
            catch (Exception)
            {
                // Fallback si la respuesta no es un JSON con BackendErrorDto
            }

            errorDto ??= new BackendErrorDto(statusCode, response.ReasonPhrase ?? "Error en la autenticación con el backend");
            return (null, null, errorDto, statusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error de red al conectar con el backend externo en LoginAsync para usuario {Username}.", SanitizeForLog(username));
            var netError = new BackendErrorDto((int)(ex.StatusCode ?? HttpStatusCode.ServiceUnavailable), "No fue posible conectar con el backend externo de negocio");
            return (null, null, netError, netError.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en LoginAsync para usuario {Username}.", SanitizeForLog(username));
            var unexpected = new BackendErrorDto((int)HttpStatusCode.InternalServerError, "Error inesperado en el BFF al procesar la autenticación");
            return (null, null, unexpected, unexpected.Status);
        }
    }

    public async Task<(BackendPrivilegesResponseDto? Data, BackendErrorDto? Error, int StatusCode)> GetPrivilegesAsync(
        string token, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/auth/privileges");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<BackendPrivilegesResponseDto>(cancellationToken: cancellationToken);
                return (data, null, statusCode);
            }

            BackendErrorDto? errorDto = null;
            try
            {
                errorDto = await response.Content.ReadFromJsonAsync<BackendErrorDto>(cancellationToken: cancellationToken);
            }
            catch (Exception)
            {
            }

            errorDto ??= new BackendErrorDto(statusCode, response.ReasonPhrase ?? "Error al obtener privilegios desde el backend externo");
            return (null, errorDto, statusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error de red en GetPrivilegesAsync contra el backend externo.");
            var netError = new BackendErrorDto((int)(ex.StatusCode ?? HttpStatusCode.ServiceUnavailable), "No fue posible conectar con el backend externo para consultar privilegios");
            return (null, netError, netError.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en GetPrivilegesAsync.");
            var unexpected = new BackendErrorDto((int)HttpStatusCode.InternalServerError, "Error inesperado en el BFF al consultar privilegios");
            return (null, unexpected, unexpected.Status);
        }
    }

    public async Task<(bool Success, object? Data, string? Error, int StatusCode)> ExecuteBlockWorkActionAsync(
        string codename, string action, object? payload = null, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var formattedAction = action.ToLowerInvariant();
            var relativeUrl = $"api/v1/{codename}/{formattedAction}";

            using var request = new HttpRequestMessage(HttpMethod.Post, relativeUrl);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            if (payload != null)
            {
                request.Content = JsonContent.Create(payload);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<object>(cancellationToken: cancellationToken);
                return (true, data, null, statusCode);
            }

            var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
            return (false, null, errorText, statusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "No se pudo conectar con el backend externo para la acción {Action} de {Codename}.", action, SanitizeForLog(codename));
            return (false, null, ex.Message, (int)(ex.StatusCode ?? HttpStatusCode.ServiceUnavailable));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al ejecutar la acción {Action} de {Codename} en el backend externo.", action, SanitizeForLog(codename));
            return (false, null, ex.Message, (int)HttpStatusCode.InternalServerError);
        }
    }

    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\r", string.Empty, StringComparison.Ordinal)
                    .Replace("\n", string.Empty, StringComparison.Ordinal);
    }
}

