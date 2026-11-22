using Microsoft.AspNetCore.Components;

namespace Vogel.Client.Components;

/// <summary>
/// Code-behind del componente SuboptionMenu.
/// Corresponde a cada una de las subopciones subordinadas dentro de un OptionMenu.
/// Al hacer clic, invoca y ejecuta la acción / ruta asignada en el sistema.
/// Corresponde a la sección 2.3.4 del Requerimiento Maestro.
/// </summary>
public class SuboptionMenuBase : ComponentBase
{
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Identificador del privilegio padre asociado.
    /// </summary>
    [Parameter] public int IdParent { get; set; }

    /// <summary>
    /// Nombre clave o identificador mnemotécnico de la subopción.
    /// </summary>
    [Parameter] public string Codename { get; set; } = string.Empty;

    /// <summary>
    /// Texto que se despliega en la subopción.
    /// </summary>
    [Parameter] public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Ruta relativa del icono SVG a mostrar.
    /// </summary>
    [Parameter] public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Acción o ruta relativa que se ejecuta al seleccionar la subopción.
    /// </summary>
    [Parameter] public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Evento opcional para notificar la selección al contenedor padre.
    /// </summary>
    [Parameter] public EventCallback<string> OnSuboptionSelected { get; set; }

    /// <summary>
    /// Indica si la ruta de esta subopción coincide con la URL activa en el navegador.
    /// </summary>
    protected bool IsActive
    {
        get
        {
            if (string.IsNullOrEmpty(Action)) return false;
            var currentUri = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
            var normalizedAction = Action.TrimStart('/');
            return string.Equals(currentUri, normalizedAction, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Maneja el clic en la subopción y realiza la navegación.
    /// </summary>
    protected async Task HandleClickAsync()
    {
        if (!string.IsNullOrWhiteSpace(Action))
        {
            await OnSuboptionSelected.InvokeAsync(Action);
            var targetUrl = Action.StartsWith('/') ? Action : $"/{Action}";
            NavigationManager.NavigateTo(targetUrl);
        }
    }
}
