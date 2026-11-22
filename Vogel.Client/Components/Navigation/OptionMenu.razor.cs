using Microsoft.AspNetCore.Components;

namespace Vogel.Client.Components;

/// <summary>
/// Code-behind del componente OptionMenu.
/// Representa cada ítem raíz (nivel 1) en el menú principal.
/// Administra el despliegue de sus subopciones de acuerdo con el comportamiento de acordeón.
/// Corresponde a la sección 2.3.3 del Requerimiento Maestro.
/// </summary>
public class OptionMenuBase : ComponentBase
{
    /// <summary>
    /// Identificador único del privilegio/opción.
    /// </summary>
    [Parameter] public int Id { get; set; }

    /// <summary>
    /// Nombre clave o identificador mnemotécnico.
    /// </summary>
    [Parameter] public string Codename { get; set; } = string.Empty;

    /// <summary>
    /// Título descriptivo desplegado en el menú.
    /// </summary>
    [Parameter] public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Ruta relativa del icono SVG representativo.
    /// </summary>
    [Parameter] public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Acción asignada (para opciones raíz no genera ninguna acción directa).
    /// </summary>
    [Parameter] public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Indica si este nodo de menú se encuentra expandido actualmente.
    /// </summary>
    [Parameter] public bool IsExpanded { get; set; }

    /// <summary>
    /// Evento disparado al hacer clic en la opción para conmutar el acordeón.
    /// </summary>
    [Parameter] public EventCallback<int> OnToggle { get; set; }

    /// <summary>
    /// Fragmento de renderizado con los componentes SuboptionMenu hijos.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Manejador de evento al pulsar la cabecera de la opción.
    /// </summary>
    protected async Task HandleToggleAsync()
    {
        await OnToggle.InvokeAsync(Id);
    }
}
