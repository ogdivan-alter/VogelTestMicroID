using System;

namespace Vogel.Shared.DTOs;

/// <summary>
/// DTO para la validación de instancia de servidor entre cliente y servidor.
/// </summary>
public class SessionValidationDto
{
    /// <summary>
    /// Fecha y hora de arranque del servidor en UTC.
    /// </summary>
    public DateTime ServerStartUtc { get; set; }

    /// <summary>
    /// Identificador único de la instancia del servidor.
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Indica si la respuesta es válida.
    /// </summary>
    public bool Valid { get; set; }

    /// <summary>
    /// Tiempo de vida (en segundos) que el cliente puede cachear el resultado de la validación.
    /// Un valor de 0 indica tolerancia cero (validar en cada ingreso a una opción).
    /// </summary>
    public int TtlSeconds { get; set; }
}
