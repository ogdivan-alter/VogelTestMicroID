namespace Vogel.Client.Services;

/// <summary>
/// Contrato del servicio de registro de logs del cliente para envío asíncrono al servidor.
/// Corresponde a la sección 6.2 del Requerimiento Maestro.
/// </summary>
public interface IClientLogger
{
    /// <summary>
    /// Envía un registro de información al servidor.
    /// </summary>
    Task LogInformationAsync(string message, string? source = null);

    /// <summary>
    /// Envía un registro de advertencia al servidor.
    /// </summary>
    Task LogWarningAsync(string message, string? source = null);

    /// <summary>
    /// Envía un registro de error o excepción capturada al servidor.
    /// </summary>
    Task LogErrorAsync(string message, Exception? exception = null, string? source = null);
}
