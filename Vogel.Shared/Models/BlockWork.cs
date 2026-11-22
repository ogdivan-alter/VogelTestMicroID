namespace Vogel.Shared.Models;

/// <summary>
/// Representa una entidad compuesta tipo maestro-detalle orientada a la construcción dinámica de vistas de trabajo.
/// Corresponde a la sección 2.4.1 del Requerimiento Maestro.
/// </summary>
public class BlockWork
{
    /// <summary>
    /// Nombre clave que identifica a la entidad de trabajo.
    /// </summary>
    public string Codename { get; set; } = string.Empty;

    /// <summary>
    /// Determina si la sección de información maestro (InfoFrame) debe ser visible en pantalla.
    /// </summary>
    public bool ShowInfo { get; set; } = true;

    /// <summary>
    /// Título descriptivo que se presenta en el encabezado (HeaderWork) de la vista de trabajo.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del atributo que actúa como clave primaria del objeto maestro.
    /// </summary>
    public string KeyMasterAttribute { get; set; } = "Id";

    /// <summary>
    /// Nombre del atributo que actúa como clave primaria del objeto detalle.
    /// </summary>
    public string KeyDetailAttribute { get; set; } = "Id";

    /// <summary>
    /// Conjunto de acciones operativas disponibles para la entidad.
    /// </summary>
    public ActionBlockWork ActionSet { get; set; } = new();

    /// <summary>
    /// Conjunto de vistas internas configuradas para las secciones del bloque de trabajo.
    /// </summary>
    public ViewBlockWork ViewSet { get; set; } = new();
}
