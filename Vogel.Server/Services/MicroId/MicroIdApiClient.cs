using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Vogel.Server.Configuration;
using Vogel.Server.Models.MicroId;

namespace Vogel.Server.Services.MicroId;

/// <summary>
/// Implementación de IMicroIdApiClient sobre HttpClientFactory con soporte para
/// X-Request-Id, X-Timestamp, X-System-Id, X-System-Key, Bearer opcional y
/// reintento único automático ante ERR_INVALID_SYSTEM_KEY.
/// </summary>
public sealed class MicroIdApiClient : IMicroIdApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IMicroIdSystemKeyProvider _systemKeyProvider;
    private readonly MicroIdOptions _options;
    private readonly ILogger<MicroIdApiClient> _logger;

    public MicroIdApiClient(
        HttpClient httpClient,
        IMicroIdSystemKeyProvider systemKeyProvider,
        IOptions<MicroIdOptions> options,
        ILogger<MicroIdApiClient> logger)
    {
        _httpClient = httpClient;
        _systemKeyProvider = systemKeyProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MicroIdApiResult<TResponse>> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest payload,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        var result = await SendOnceAsync<TRequest, TResponse>(endpoint, payload, bearerToken, cancellationToken);

        if (string.Equals(result.Code, MicroIdResponseCodes.InvalidSystemKey, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "MicroID respondió ERR_INVALID_SYSTEM_KEY en {Endpoint}. Invalidando llave y reintentando una vez.",
                endpoint);

            _systemKeyProvider.InvalidateKey();
            result = await SendOnceAsync<TRequest, TResponse>(endpoint, payload, bearerToken, cancellationToken);
        }

        return result;
    }

    private async Task<MicroIdApiResult<TResponse>> SendOnceAsync<TRequest, TResponse>(
        string endpoint,
        TRequest payload,
        string? bearerToken,
        CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString();
        var systemKey = await _systemKeyProvider.GetKeyAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.TrimStart('/'));
        request.Headers.Add("X-Request-Id", requestId);
        request.Headers.Add("X-Timestamp", DateTimeOffset.UtcNow.ToString("O"));
        request.Headers.Add("X-System-Id", _options.SystemId);
        request.Headers.Add("X-System-Key", systemKey);

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        request.Content = JsonContent.Create(new MicroIdRequestMessage<TRequest>(payload));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await TryReadResponseAsync<TResponse>(response, cancellationToken);

        var code = body?.Code ?? (response.IsSuccessStatusCode
            ? MicroIdResponseCodes.Success
            : response.StatusCode.ToString());
        var message = body?.Message ?? (response.ReasonPhrase ?? "Error en comunicación con MicroID.");

        _logger.LogDebug(
            "MicroID POST {Endpoint} (RequestId: {RequestId}) -> HTTP {StatusCode}, Code: {Code}",
            endpoint,
            requestId,
            (int)response.StatusCode,
            code);

        return new MicroIdApiResult<TResponse>(
            StatusCode: response.StatusCode,
            Code: code,
            Message: message,
            Data: body is not null ? body.Data : default);
    }

    private async Task<MicroIdResponseMessage<TResponse>?> TryReadResponseAsync<TResponse>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength == 0)
        {
            return null;
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<MicroIdResponseMessage<TResponse>>(
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            _logger.LogWarning(
                "No se pudo deserializar el cuerpo de respuesta de MicroID (HTTP {StatusCode}).",
                (int)response.StatusCode);
            return null;
        }
    }
}
