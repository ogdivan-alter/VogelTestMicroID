using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Vogel.Shared.DTOs;

namespace Vogel.Client.Services.Api;

/// <summary>
/// Implementación del cliente HTTP tipado para interactuar con los endpoints de MockBackend API.
/// </summary>
public class ApiBackendClient : IApiBackendClient
{
    public const string HttpClientName = "ApiBackendHttpClient";
    private readonly HttpClient _httpClient;
    private readonly IClientLogger _logger;

    public ApiBackendClient(HttpClient httpClient, IClientLogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
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
                // En caso de que el cuerpo de error no tenga formato JSON esperado
            }

            errorDto ??= new BackendErrorDto(statusCode, response.ReasonPhrase ?? "Error en la autenticación");
            return (null, null, errorDto, statusCode);
        }
        catch (HttpRequestException ex)
        {
            await _logger.LogErrorAsync("Error de red al conectar con el backend en LoginAsync.", ex, nameof(ApiBackendClient));
            var netError = new BackendErrorDto((int)(ex.StatusCode ?? HttpStatusCode.ServiceUnavailable), "No fue posible conectar con el servicio de autenticación");
            return (null, null, netError, netError.Status);
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync("Error inesperado en LoginAsync.", ex, nameof(ApiBackendClient));
            var unexpected = new BackendErrorDto((int)HttpStatusCode.InternalServerError, "Error inesperado al conectar con el servicio de autenticación");
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

            errorDto ??= new BackendErrorDto(statusCode, response.ReasonPhrase ?? "Error al obtener privilegios");
            return (null, errorDto, statusCode);
        }
        catch (HttpRequestException ex)
        {
            await _logger.LogErrorAsync("Error de red en GetPrivilegesAsync.", ex, nameof(ApiBackendClient));
            var netError = new BackendErrorDto((int)(ex.StatusCode ?? HttpStatusCode.ServiceUnavailable), "No fue posible conectar con el servicio de privilegios");
            return (null, netError, netError.Status);
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync("Error inesperado en GetPrivilegesAsync.", ex, nameof(ApiBackendClient));
            var unexpected = new BackendErrorDto((int)HttpStatusCode.InternalServerError, "Error inesperado al obtener privilegios");
            return (null, unexpected, unexpected.Status);
        }
    }
}

