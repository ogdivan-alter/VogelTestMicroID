using Vogel.Shared.Models;

namespace Vogel.Shared.DTOs;

/// <summary>
/// Objeto de transferencia de datos con el resultado de una solicitud de autenticación.
/// </summary>
public class LoginResponseDto
{
    /// <summary>
    /// Indica si las credenciales fueron validadas satisfactoriamente.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Mensaje descriptivo o informativo del resultado de autenticación.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Datos del usuario autenticado y su árbol de privilegios asociado.
    /// </summary>
    public User? User { get; set; }
}
