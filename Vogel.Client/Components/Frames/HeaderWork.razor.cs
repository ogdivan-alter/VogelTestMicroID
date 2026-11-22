using Microsoft.AspNetCore.Components;

namespace Vogel.Client.Components;

/// <summary>
/// Componente de encabezado de trabajo (HeaderWork).
/// Franja superior con color configurable, título y botón de salida alineado a la derecha.
/// Corresponde a la sección 2.3.6 del Requerimiento Maestro.
/// </summary>
public partial class HeaderWork : ComponentBase
{
    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Color de fondo de la franja superior (parámetro @color). Por defecto oscuro integrado.
    /// </summary>
    [Parameter]
    public string Color { get; set; } = "#0c0e14";

    /// <summary>
    /// Título de la sección o vista de trabajo (parámetro @title).
    /// </summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Texto del botón de salida (parámetro @textExit). Por defecto: "Salir".
    /// </summary>
    [Parameter]
    public string TextExit { get; set; } = "Salir";

    /// <summary>
    /// Ruta o acción de salida (parámetro @actionExit). Por defecto navega a la vista principal "/".
    /// </summary>
    [Parameter]
    public string ActionExit { get; set; } = "/";

    /// <summary>
    /// EventCallback opcional para interceptar y personalizar la acción de salida.
    /// </summary>
    [Parameter]
    public EventCallback OnExit { get; set; }

    /// <summary>
    /// Control de estado ocupado para evitar doble clic accidental en el botón de salida.
    /// </summary>
    protected bool _isProcessing = false;

    /// <summary>
    /// Gestiona la invocación segura de la salida hacia la vista previa o principal.
    /// </summary>
    protected async Task HandleExitAsync()
    {
        if (_isProcessing) return;

        try
        {
            _isProcessing = true;

            if (OnExit.HasDelegate)
            {
                await OnExit.InvokeAsync();
                return;
            }

            if (!string.IsNullOrWhiteSpace(ActionExit))
            {
                var targetUrl = ActionExit.StartsWith("/") ? ActionExit : $"/{ActionExit}";
                NavigationManager.NavigateTo(targetUrl);
            }
            else
            {
                NavigationManager.NavigateTo("/");
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }
}
