using System.ComponentModel.DataAnnotations;

namespace Vogel.Shared.DTOs;

/// <summary>
/// Solicitud de cambio o recuperación de contraseña enviada desde Vogel.Client hacia el BFF.
/// </summary>
public class ChangePasswordRequestDto
{
    /// <summary>
    /// Contraseña actual del usuario (requerida en cambio autenticado sin código de verificación).
    /// </summary>
    [MaxLength(256, ErrorMessage = "La contraseña actual no puede exceder los 256 caracteres.")]
    public string? CurrentPassword { get; set; }

    /// <summary>
    /// Nueva contraseña propuesta por el usuario.
    /// </summary>
    [Required(ErrorMessage = "La nueva contraseña es requerida.")]
    [MaxLength(256, ErrorMessage = "La nueva contraseña no puede exceder los 256 caracteres.")]
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>
    /// Código de verificación opcional para flujos de recuperación de contraseña.
    /// </summary>
    [MaxLength(128, ErrorMessage = "El código de verificación no puede exceder los 128 caracteres.")]
    public string? VerificationCode { get; set; }
}
