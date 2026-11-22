namespace Vogel.Shared.Enums;

/// <summary>
/// Define los tipos de autenticación admitidos por la plataforma Vogel.
/// </summary>
public enum AuthenticationType
{
    /// <summary>
    /// Autenticación interna propia del sistema (permite cambio de contraseña).
    /// </summary>
    Internal,

    /// <summary>
    /// Proveedor de autenticación externo (Google, Microsoft, LDAP, OAuth).
    /// </summary>
    External
}
