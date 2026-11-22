namespace Vogel.Shared.Models;

/// <summary>
/// Representa a un usuario autenticado dentro del sistema Vogel.
/// Corresponde a la sección 2.1.1 del Requerimiento Maestro.
/// </summary>
public class User
{
    /// <summary>
    /// Nombre de usuario único para inicio de sesión e identificación.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Nombre completo o alias para despliegue en la interfaz. Opcional.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Ruta o datos de la imagen de avatar del usuario (JPG/PNG). Opcional.
    /// </summary>
    public string? Avatar { get; set; }

    /// <summary>
    /// Tipo de mecanismo de autenticación empleado (ejemplo: "INTERNA", "EXTERNAL").
    /// </summary>
    public string TypeAuthentication { get; set; } = "INTERNA";

    /// <summary>
    /// Indica si el usuario está facultado para modificar su contraseña.
    /// Por defecto, si el tipo de autenticación es "INTERNA" se habilita.
    /// </summary>
    public bool AllowPasswordChange { get; set; } = true;

    /// <summary>
    /// Colección de privilegios asignados al usuario durante la sesión activa.
    /// </summary>
    public List<Privilege> Privileges { get; set; } = new();

    /// <summary>
    /// Rol base asignado en la sesión (por defecto "Único").
    /// </summary>
    public string BaseRole { get; set; } = "Único";
}
