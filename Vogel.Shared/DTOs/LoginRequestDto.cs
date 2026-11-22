using System.ComponentModel.DataAnnotations;

namespace Vogel.Shared.DTOs;

/// <summary>
/// Objeto de transferencia de datos para la solicitud de inicio de sesión.
/// </summary>
public class LoginRequestDto
{
    /// <summary>
    /// Nombre de usuario para autenticación. Campo requerido.
    /// </summary>
    [Required(ErrorMessage = "El nombre de usuario es requerido.")]
    [MaxLength(150, ErrorMessage = "El nombre de usuario no puede exceder los 150 caracteres.")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Contraseña del usuario. Campo requerido.
    /// </summary>
    [Required(ErrorMessage = "La contraseña es requerida.")]
    [MaxLength(256, ErrorMessage = "La contraseña no puede exceder los 256 caracteres.")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Indica si el usuario solicita persistir su sesión.
    /// </summary>
    public bool RememberMe { get; set; } = false;

    /// <summary>
    /// Token opcional generado por reCAPTCHA para validación anti-bot.
    /// </summary>
    public string? RecaptchaToken { get; set; }
}
