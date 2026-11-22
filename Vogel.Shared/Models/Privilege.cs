namespace Vogel.Shared.Models;

/// <summary>
/// Representa una opción o elemento dentro del árbol jerárquico de privilegios del sistema.
/// Corresponde a la sección 2.1.2 del Requerimiento Maestro.
/// </summary>
public class Privilege
{
    /// <summary>
    /// Identificador del privilegio padre. Valor obligatorio; 0 indica que es un nodo raíz (sin padre).
    /// </summary>
    public int IdParent { get; set; } = 0;

    /// <summary>
    /// Identificador único del privilegio.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Nombre clave unívoco del privilegio para comprobaciones programáticas.
    /// </summary>
    public string Codename { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del grupo de pertenencia; utilizado para agrupar tarjetas en el menú secundario.
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Orden de visualización dentro de su nivel o grupo.
    /// </summary>
    public int Order { get; set; } = 0;

    /// <summary>
    /// Texto descriptivo que se muestra en la interfaz (menú o botón).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Ruta o acción que se ejecuta al seleccionar el elemento.
    /// Para opciones raíz (IdParent == 0) no genera navegación directa.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Ruta relativa del recurso de ícono vectorial SVG asociado.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Conjunto de permisos granulares asociados al privilegio (lectura, escritura, borrado, etc.).
    /// </summary>
    public PrivilegeAccess? Access { get; set; }

    /// <summary>
    /// Indica si el privilegio es una opción raíz (sin nodo padre).
    /// </summary>
    public bool IsRoot => IdParent == 0;
}
