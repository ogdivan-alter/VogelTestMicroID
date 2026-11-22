namespace Vogel.Shared.DTOs;

/// <summary>
/// Respuesta del BFF ante una solicitud de cambio de contraseña.
/// </summary>
public class ChangePasswordResponseDto
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;
}
