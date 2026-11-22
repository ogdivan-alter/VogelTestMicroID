using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components;
using Vogel.Client.Services;
using Vogel.Shared.Models;

namespace Vogel.Client.Components;

/// <summary>
/// Code-behind del componente WorkViewFrame.
/// Contenedor maestro-detalle tripartito:
/// - Header: HeaderWork asociado a action_set.exit
/// - Body: InfoFrame, CaptureFrame, FilterFrame, ListFrame
/// - Footer: FooterFrame
/// Corresponde a la sección 2.3.9 del Requerimiento Maestro.
/// </summary>
public partial class WorkViewFrame : ComponentBase
{
    private static readonly ConcurrentDictionary<string, Type?> _viewTypeCache = new(StringComparer.OrdinalIgnoreCase);

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    protected SessionState SessionState { get; set; } = default!;

    /// <summary>
    /// Instancia de la entidad maestro-detalle BlockWork para inicializar dinámicamente la vista.
    /// </summary>
    [Parameter]
    public BlockWork? BlockWork { get; set; }

    /// <summary>
    /// Vista para la sección InfoFrame (información maestro de la entidad).
    /// Si no se suministra, se carga la vista indicada en BlockWork.ViewSet.Info.
    /// </summary>
    [Parameter]
    public RenderFragment? InfoFrame { get; set; }

    /// <summary>
    /// Vista para la sección CaptureFrame (captura de nuevos elementos detalle).
    /// Si no se suministra, se carga la vista indicada en BlockWork.ViewSet.Capture.
    /// </summary>
    [Parameter]
    public RenderFragment? CaptureFrame { get; set; }

    /// <summary>
    /// Vista para la sección FilterFrame (criterios de filtrado y búsqueda).
    /// Si no se suministra, se carga la vista indicada en BlockWork.ViewSet.Filter.
    /// </summary>
    [Parameter]
    public RenderFragment? FilterFrame { get; set; }

    /// <summary>
    /// Vista para la sección ListFrame (grilla o tabla de registros detalle con paginación).
    /// Si no se suministra, se carga la vista indicada en BlockWork.ViewSet.List.
    /// </summary>
    [Parameter]
    public RenderFragment? ListFrame { get; set; }

    /// <summary>
    /// Vista para la sección FooterFrame (pie de página específico de la vista de trabajo).
    /// </summary>
    [Parameter]
    public RenderFragment? FooterFrame { get; set; }

    /// <summary>
    /// Contenido libre anidado alternativo.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Título de reserva si no se suministra BlockWork.
    /// </summary>
    [Parameter]
    public string HeaderTitle { get; set; } = "Vista de Trabajo";

    /// <summary>
    /// Color de franja del encabezado.
    /// </summary>
    [Parameter]
    public string HeaderColor { get; set; } = "#0c0e14";

    /// <summary>
    /// Texto del botón de salida en el encabezado.
    /// </summary>
    [Parameter]
    public string HeaderExitText { get; set; } = "Salir";

    /// <summary>
    /// Ruta de salida de reserva si no se suministra BlockWork.ActionSet.Exit.
    /// </summary>
    [Parameter]
    public string HeaderExitAction { get; set; } = "/";

    /// <summary>
    /// Callback opcional para interceptar la salida.
    /// </summary>
    [Parameter]
    public EventCallback OnExit { get; set; }

    /// <summary>
    /// Callback notificado cuando se acciona un botón del ActionSet.
    /// </summary>
    [Parameter]
    public EventCallback<(string ActionKey, string Url)> OnActionTriggered { get; set; }

    /// <summary>
    /// Determina si se visualiza la barra de acciones de BlockWork.
    /// </summary>
    [Parameter]
    public bool ShowActionToolbar { get; set; } = true;

    /// <summary>
    /// Clases CSS personalizadas.
    /// </summary>
    [Parameter]
    public string CustomClass { get; set; } = string.Empty;

    /// <summary>
    /// Estado de procesamiento para evitar doble clic o peticiones concurrentes en la botonera de acciones.
    /// </summary>
    protected bool _isActionBusy = false;

    /// <summary>
    /// Mensaje temporal de retroalimentación de acción.
    /// </summary>
    protected string? _actionMessage;

    /// <summary>
    /// Resuelve dinámicamente el Type de un componente Blazor a partir del nombre o identificador indicado en ViewBlockWork.
    /// </summary>
    protected Type? ResolveViewType(string? viewName)
    {
        if (string.IsNullOrWhiteSpace(viewName)) return null;

        return _viewTypeCache.GetOrAdd(viewName, name =>
        {
            var asm = typeof(WorkViewFrame).Assembly;
            return asm.GetType(name)
                ?? asm.GetType($"Vogel.Client.Components.{name}")
                ?? asm.GetType($"Vogel.Client.Views.{name}")
                ?? asm.GetType($"Vogel.Client.Pages.{name}")
                ?? asm.GetTypes().FirstOrDefault(t =>
                    string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)
                    && typeof(IComponent).IsAssignableFrom(t));
        });
    }

    /// <summary>
    /// Construye los parámetros que la vista dinámica puede recibir (p. ej. la instancia de BlockWork).
    /// </summary>
    protected Dictionary<string, object> GetViewParameters(Type componentType)
    {
        var parameters = new Dictionary<string, object>();
        if (BlockWork != null)
        {
            var prop = componentType.GetProperty(nameof(BlockWork));
            if (prop != null && Attribute.IsDefined(prop, typeof(ParameterAttribute)))
            {
                parameters[nameof(BlockWork)] = BlockWork;
            }
        }
        return parameters;
    }

    /// <summary>
    /// Procesa de manera segura la invocación de acciones del ActionSet con retroalimentación visual.
    /// </summary>
    public async Task HandleActionClickAsync(string actionKey, string actionUrl)
    {
        if (_isActionBusy) return;

        try
        {
            _isActionBusy = true;
            _actionMessage = $"Acción ejecutada: {actionKey} ({actionUrl})";

            if (OnActionTriggered.HasDelegate)
            {
                await OnActionTriggered.InvokeAsync((actionKey, actionUrl));
            }
        }
        finally
        {
            _isActionBusy = false;
        }
    }
}
