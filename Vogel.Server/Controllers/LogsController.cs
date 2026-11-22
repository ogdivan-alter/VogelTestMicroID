using Microsoft.AspNetCore.Mvc;
using Vogel.Shared.DTOs;

namespace Vogel.Server.Controllers;

/// <summary>
/// Controlador API para la recepción y consolidación de eventos de log emitidos desde el cliente Blazor WebAssembly.
/// Corresponde a la sección 6.2 del Requerimiento Maestro.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly ILogger<LogsController> _logger;

    public LogsController(ILogger<LogsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Recibe un registro de log desde el cliente y lo almacena de forma estructurada en el archivo de log diario del servidor.
    /// </summary>
    /// <param name="logDto">Detalles del evento de log (nivel, mensaje, origen, pila de errores).</param>
    /// <returns>Resultado de la operación.</returns>
    [HttpPost]
    public IActionResult RecordClientLog([FromBody] ClientLogDto logDto)
    {
        if (logDto == null || string.IsNullOrWhiteSpace(logDto.Message))
        {
            return BadRequest("El contenido del log no puede estar vacío.");
        }

        var source = SanitizeForLog(string.IsNullOrWhiteSpace(logDto.Source) ? "Vogel.Client" : logDto.Source);
        var message = SanitizeForLog(logDto.Message);
        var stack = string.IsNullOrWhiteSpace(logDto.StackTrace) ? string.Empty : $"\nStack: {SanitizeForLog(logDto.StackTrace)}";

        switch (logDto.LogLevel?.ToUpperInvariant())
        {
            case "ERROR":
            case "CRITICAL":
                _logger.LogError("[Client] [{Source}] {Message} {Stack}", source, message, stack);
                break;

            case "WARNING":
            case "WARN":
                _logger.LogWarning("[Client] [{Source}] {Message}", source, message);
                break;

            case "DEBUG":
                _logger.LogDebug("[Client] [{Source}] {Message}", source, message);
                break;

            default:
                _logger.LogInformation("[Client] [{Source}] {Message}", source, message);
                break;
        }

        return Ok(new { success = true, timestamp = DateTime.UtcNow });
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
