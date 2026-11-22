using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Vogel.Server.Configuration;
using Vogel.Server.Models.MicroId;

namespace Vogel.Server.Services.MicroId;

/// <summary>
/// Implementación Singleton thread-safe que gestiona la llave X-System-Key en memoria
/// consumiendo POST /creds/create de MicroID.
/// </summary>
public sealed class MicroIdSystemKeyProvider : IMicroIdSystemKeyProvider, IDisposable
{
    public const string HttpClientName = "MicroIdCredsClient";
    private static readonly TimeSpan ExpirationSafetyMargin = TimeSpan.FromMinutes(1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MicroIdOptions _options;
    private readonly ILogger<MicroIdSystemKeyProvider> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private string? _plainApiKey;
    private DateTimeOffset _dateExpiration = DateTimeOffset.MinValue;

    public MicroIdSystemKeyProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<MicroIdOptions> options,
        ILogger<MicroIdSystemKeyProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetKeyAsync(CancellationToken cancellationToken = default)
    {
        if (HasValidCachedKey())
        {
            return _plainApiKey!;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (HasValidCachedKey())
            {
                return _plainApiKey!;
            }

            return await RefreshKeyInternalAsync(cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void InvalidateKey()
    {
        _plainApiKey = null;
        _dateExpiration = DateTimeOffset.MinValue;
    }

    private bool HasValidCachedKey()
    {
        return !string.IsNullOrEmpty(_plainApiKey) &&
               DateTimeOffset.UtcNow.Add(ExpirationSafetyMargin) < _dateExpiration;
    }

    private async Task<string> RefreshKeyInternalAsync(CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString();
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "creds/create");
        request.Headers.Add("X-Request-Id", requestId);
        request.Headers.Add("X-Timestamp", DateTimeOffset.UtcNow.ToString("O"));

        var envelope = new MicroIdRequestMessage<CreateCredRequest>(
            new CreateCredRequest(
                IdSystem: _options.SystemNumericId,
                Renovation: _options.CredRenovation));

        request.Content = JsonContent.Create(envelope);

        _logger.LogInformation(
            "Solicitando nueva llave de sistema MicroID (RequestId: {RequestId}, SystemId: {SystemNumericId}, Renovation: {Renovation})",
            requestId,
            _options.SystemNumericId,
            _options.CredRenovation);

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<MicroIdResponseMessage<CreateCredResponse>>(
            cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode ||
            body is null ||
            !string.Equals(body.Code, MicroIdResponseCodes.Success, StringComparison.Ordinal) ||
            body.Data is null ||
            string.IsNullOrWhiteSpace(body.Data.PlainApiKey))
        {
            var code = body?.Code ?? response.StatusCode.ToString();
            _logger.LogError(
                "Error al obtener llave de sistema MicroID (RequestId: {RequestId}, StatusCode: {StatusCode}, Code: {Code})",
                requestId,
                (int)response.StatusCode,
                code);

            throw new InvalidOperationException(
                $"No se pudo obtener la llave de sistema de MicroID (Code: {code}).");
        }

        _plainApiKey = body.Data.PlainApiKey;
        _dateExpiration = body.Data.DateExpiration;

        _logger.LogInformation(
            "Llave de sistema MicroID renovada exitosamente (RequestId: {RequestId}, CredId: {CredId}, ExpirationUtc: {ExpirationUtc})",
            requestId,
            body.Data.IdCred,
            _dateExpiration.UtcDateTime);

        return _plainApiKey;
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
