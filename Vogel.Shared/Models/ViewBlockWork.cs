namespace Vogel.Shared.Models;

/// <summary>
/// Contiene los nombres de componentes o URLs relativas de las vistas internas que componen un BlockWork.
/// Corresponde a la sección 2.4.3 del Requerimiento Maestro.
/// </summary>
public class ViewBlockWork
{
    /// <summary>
    /// Identificador o ruta de la vista de despliegue de información de la cabecera maestro.
    /// </summary>
    public string Info { get; set; } = string.Empty;

    /// <summary>
    /// Identificador o ruta de la vista para captura y asociación de elementos detalle.
    /// </summary>
    public string Capture { get; set; } = string.Empty;

    /// <summary>
    /// Identificador o ruta de la vista con los filtros de búsqueda para elementos detalle.
    /// </summary>
    public string Filter { get; set; } = string.Empty;

    /// <summary>
    /// Identificador o ruta de la vista con la tabla paginada de registros detalle.
    /// </summary>
    public string List { get; set; } = string.Empty;
}
