namespace Vogel.Shared.DTOs;

/// <summary>
/// Objeto de transferencia de datos para el envío asíncrono de registros de log desde el cliente hacia el servidor.
/// Corresponde a la sección 6.2 del Requerimiento Maestro.
/// </summary>
public class ClientLogDto
{
    /// <summary>
    /// Nivel del registro de log (ejemplo: "Information", "Warning", "Error").
    /// </summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Mensaje descriptivo del evento o excepción.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Componente, vista o servicio de origen donde se originó el registro.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Traza de la pila o detalles adicionales de la excepción, si aplica.
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Fecha y hora UTC en la que ocurrió el evento en el cliente.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Usuario autenticado en el momento de generar el log, si existe sesión.
    /// </summary>
    public string? Username { get; set; }
}
