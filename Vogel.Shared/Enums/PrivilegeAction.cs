namespace Vogel.Shared.Enums;

/// <summary>
/// Define las acciones granulares de autorización asociadas a un privilegio en Vogel.
/// </summary>
public enum PrivilegeAction
{
    /// <summary>
    /// Permite consultar o visualizar registros del módulo.
    /// </summary>
    Read,

    /// <summary>
    /// Permite crear nuevos registros y reactivar registros inactivos.
    /// </summary>
    Create,

    /// <summary>
    /// Permite modificar registros existentes.
    /// </summary>
    Modify,

    /// <summary>
    /// Permite inactivar o eliminar registros.
    /// </summary>
    Delete,

    /// <summary>
    /// Permite validar registros o procesos.
    /// </summary>
    Validate,

    /// <summary>
    /// Permite ejecutar operaciones o acciones específicas.
    /// </summary>
    Execute,

    /// <summary>
    /// Permite aprobar operaciones o flujos.
    /// </summary>
    Approve
}
