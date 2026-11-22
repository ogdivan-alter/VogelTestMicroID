using System.Net;
using Vogel.Server.Models.MicroId;

namespace Vogel.Server.Services.MicroId;

/// <summary>
/// Resultado normalizado de una llamada HTTP POST hacia MicroID.
/// </summary>
public sealed record MicroIdApiResult<T>(
    HttpStatusCode StatusCode,
    string Code,
    string Message,
    T? Data)
{
    public bool IsSuccess =>
        StatusCode == HttpStatusCode.OK &&
        string.Equals(Code, MicroIdResponseCodes.Success, StringComparison.Ordinal);
}

/// <summary>
/// Cliente HTTP de bajo nivel para comunicarse con los endpoints protegidos de MicroID.
/// </summary>
public interface IMicroIdApiClient
{
    /// <summary>
    /// Envía una petición HTTP POST al endpoint de MicroID inyectando las cabeceras obligatorias
    /// y reintentando una vez si MicroID responde ERR_INVALID_SYSTEM_KEY.
    /// </summary>
    Task<MicroIdApiResult<TResponse>> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest payload,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);
}
