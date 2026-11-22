using System.ComponentModel.DataAnnotations;

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
    [Required(ErrorMessage = "El nivel del log es requerido.")]
    [MaxLength(32, ErrorMessage = "El nivel del log no puede exceder los 32 caracteres.")]
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Mensaje descriptivo del evento o excepción.
    /// </summary>
    [Required(ErrorMessage = "El mensaje del log es requerido.")]
    [MaxLength(2000, ErrorMessage = "El mensaje del log no puede exceder los 2000 caracteres.")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Componente, vista o servicio de origen donde se originó el registro.
    /// </summary>
    [MaxLength(200, ErrorMessage = "El origen del log no puede exceder los 200 caracteres.")]
    public string? Source { get; set; }

    /// <summary>
    /// Traza de la pila o detalles adicionales de la excepción, si aplica.
    /// </summary>
    [MaxLength(4000, ErrorMessage = "La traza de pila no puede exceder los 4000 caracteres.")]
    public string? StackTrace { get; set; }

    /// <summary>
    /// Fecha y hora UTC en la que ocurrió el evento en el cliente.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Usuario autenticado en el momento de generar el log, si existe sesión.
    /// </summary>
    [MaxLength(150, ErrorMessage = "El nombre de usuario no puede exceder los 150 caracteres.")]
    public string? Username { get; set; }
}
