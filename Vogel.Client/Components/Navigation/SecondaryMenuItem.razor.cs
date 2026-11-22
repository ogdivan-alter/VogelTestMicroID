using Microsoft.AspNetCore.Components;

namespace Vogel.Client.Components;

/// <summary>
/// Componente de tarjeta de opción de menú secundario (SecondaryMenuItem).
/// Estructurado en dos secciones: ícono en parte superior y título en parte inferior.
/// Seguro contra doble clic accidental.
/// Corresponde a la sección 2.3.8 del Requerimiento Maestro.
/// </summary>
public partial class SecondaryMenuItem : ComponentBase
{
    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Título descriptivo de la opción secundaria.
    /// </summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Ruta del ícono vectorial SVG asociado.
    /// </summary>
    [Parameter]
    public string? Icon { get; set; }

    /// <summary>
    /// Ruta o acción que se invoca al hacer clic sobre la tarjeta.
    /// </summary>
    [Parameter]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Callback opcional para interceptar y gestionar el clic en la tarjeta.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnItemClick { get; set; }

    /// <summary>
    /// Control de estado ocupado para protección contra doble clic.
    /// </summary>
    protected bool _isProcessing = false;

    /// <summary>
    /// Maneja el clic en la tarjeta de forma asíncrona y segura.
    /// </summary>
    protected async Task HandleClickAsync()
    {
        if (_isProcessing) return;

        try
        {
            _isProcessing = true;

            if (OnItemClick.HasDelegate)
            {
                await OnItemClick.InvokeAsync(Action);
                return;
            }

            if (!string.IsNullOrWhiteSpace(Action))
            {
                var targetUrl = Action.StartsWith("/") ? Action : $"/{Action}";
                NavigationManager.NavigateTo(targetUrl);
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }
}
