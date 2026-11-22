using Microsoft.AspNetCore.Components;

namespace Vogel.Client.Components;

/// <summary>
/// Componente contenedor semántico para bloques de información maestro (InfoViewFrame).
/// Se comporta como etiqueta HTML que encapsula estilos y anida otros elementos.
/// Corresponde a la sección 2.3.10 del Requerimiento Maestro.
/// </summary>
public partial class InfoViewFrame : ComponentBase
{
    /// <summary>
    /// Título opcional que se visualiza en la cabecera del contenedor.
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    /// Fragmento de cabecera personalizado para sobrescribir el título estándar.
    /// </summary>
    [Parameter]
    public RenderFragment? HeaderContent { get; set; }

    /// <summary>
    /// Contenido anidado del componente.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Clases CSS adicionales personalizadas para el contenedor.
    /// </summary>
    [Parameter]
    public string CustomClass { get; set; } = string.Empty;
}
