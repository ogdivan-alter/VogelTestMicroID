namespace Vogel.Client.Services;

/// <summary>
/// Servicio para validar la instancia del servidor y detectar reinicios del servicio.
/// Permite invalidar sesiones almacenadas en el cliente cuando el servidor se reinicia.
/// </summary>
public interface ISessionValidationService
{
    /// <summary>
    /// Valida si la instancia actual del servidor coincide con la última instancia conocida.
    /// </summary>
    /// <returns>True si la instancia del servidor es válida (no ha reiniciado), False si ha reiniciado o no existe registro.</returns>
    Task<bool> ValidateServerInstanceAsync();

    /// <summary>
    /// Asegura la validez de la sesión contra la instancia del servidor aplicando la política TTL.
    /// Debe invocarse al ingresar a cada opción controlada por privilegios.
    /// </summary>
    Task<bool> EnsureValidAsync();

    /// <summary>
    /// Registra el marcador de la instancia actual del servidor al establecer una nueva sesión
    /// (por ejemplo, tras un login exitoso), sin aplicar invalidación previa.
    /// </summary>
    /// <returns>True si el marcador fue registrado correctamente.</returns>
    Task<bool> EstablishSessionAsync();
}
