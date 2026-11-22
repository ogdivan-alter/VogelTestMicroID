namespace Vogel.Shared.Models;

/// <summary>
/// Contiene las URLs relativas o identificadores de acciones para una entidad maestro-detalle (BlockWork).
/// Corresponde a la sección 2.4.2 del Requerimiento Maestro.
/// </summary>
public class ActionBlockWork
{
    /// <summary>
    /// Acción de inicio o vista principal de gestión de la entidad.
    /// </summary>
    public string Start { get; set; } = string.Empty;

    /// <summary>
    /// Acción de captura de objetos detalle ya asociados o por asociar al objeto maestro.
    /// </summary>
    public string Capture { get; set; } = string.Empty;

    /// <summary>
    /// Acción de desasociación o remoción de objetos detalle del objeto maestro.
    /// </summary>
    public string Remove { get; set; } = string.Empty;

    /// <summary>
    /// Acción de cancelación de cambios sobre el objeto maestro (incluye cancelación de creación).
    /// </summary>
    public string CancelMaster { get; set; } = string.Empty;

    /// <summary>
    /// Acción de ejecución o aplicación definitiva de cambios sobre el objeto maestro.
    /// </summary>
    public string ApplyMaster { get; set; } = string.Empty;

    /// <summary>
    /// Acción para reversar cambios aplicados previamente sobre el objeto maestro.
    /// </summary>
    public string RevApplyMaster { get; set; } = string.Empty;

    /// <summary>
    /// Acción para aplicar filtros sobre la lista de objetos detalle o generar el listado inicial.
    /// </summary>
    public string ListDetailMaster { get; set; } = string.Empty;

    /// <summary>
    /// Acción para la descarga de un documento representativo de la entidad (ejemplo: PDF).
    /// </summary>
    public string DocMaster { get; set; } = string.Empty;

    /// <summary>
    /// Acción para la descarga de un documento tipo reporte detallado de la entidad (ejemplo: Excel o CSV).
    /// </summary>
    public string RepMaster { get; set; } = string.Empty;

    /// <summary>
    /// Acción de salida de la vista principal de gestión de la entidad hacia el menú anterior.
    /// </summary>
    public string Exit { get; set; } = string.Empty;
}
