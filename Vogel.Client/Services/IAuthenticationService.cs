namespace Vogel.Client.Services;

/// <summary>
/// Contrato del servicio de autenticación de la aplicación Vogel.
/// En Fase 2 se conecta al MockDataService. En Fase 3 se integrará con servicios externos.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Intenta autenticar al usuario con las credenciales provistas.
    /// Retorna true si la autenticación fue exitosa, false y mensaje de error si no.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> LoginAsync(string username, string password, bool rememberMe = false);

    /// <summary>
    /// Cambia la contraseña del usuario autenticado (o mediante código de verificación) a través del BFF.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> ChangePasswordAsync(string currentPassword, string newPassword, string? verificationCode = null);

    /// <summary>
    /// Cierra la sesión actual, limpia el estado y el almacenamiento cifrado.
    /// </summary>
    Task LogoutAsync();
}
