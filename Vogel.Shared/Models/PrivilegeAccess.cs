namespace Vogel.Shared.Models;

/// <summary>
/// Representa los permisos y acciones específicas permitidas para un privilegio de usuario.
/// Corresponde a la sección 2.1.2 y 4.2 del Requerimiento Maestro.
/// </summary>
public class PrivilegeAccess
{
    /// <summary>
    /// Permiso para crear nuevos registros o elementos.
    /// </summary>
    public bool Create { get; set; } = false;

    /// <summary>
    /// Permiso para modificar registros existentes.
    /// </summary>
    public bool Modify { get; set; } = false;

    /// <summary>
    /// Permiso para lectura y consulta de datos.
    /// </summary>
    public bool Read { get; set; } = true;

    /// <summary>
    /// Permiso para eliminación de registros.
    /// </summary>
    public bool Delete { get; set; } = false;

    /// <summary>
    /// Permiso para validación técnica u operativa de registros.
    /// </summary>
    public bool Validate { get; set; } = false;

    /// <summary>
    /// Permiso para ejecutar procesos o acciones de negocio.
    /// </summary>
    public bool Execute { get; set; } = false;

    /// <summary>
    /// Permiso para aprobación de registros o flujos de trabajo.
    /// </summary>
    public bool Approve { get; set; } = false;
}
