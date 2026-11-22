using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Vogel.Client.Services;
using Vogel.Shared.Services;

namespace Vogel.Client.Pages;

/// <summary>
/// Code-behind de la vista secundaria (SecondaryView).
/// Despliega el encabezado HeaderWork y el tablero de opciones SecondaryMenu.
/// Ocupa la totalidad del cuerpo del layout, sustituyendo la vista principal.
/// Corresponde a la sección 2.2.4 del Requerimiento Maestro.
/// </summary>
public partial class SecondaryView : ComponentBase
{
    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    protected SessionState SessionState { get; set; } = default!;

    [Inject]
    protected IMockDataService MockDataService { get; set; } = default!;

    [Inject]
    protected IConfiguration Configuration { get; set; } = default!;

    /// <summary>
    /// Identificador del privilegio padre obtenido desde la ruta de navegación.
    /// </summary>
    [Parameter]
    public int IdParent { get; set; }

    /// <summary>
    /// Título dinámico que se muestra en el HeaderWork.
    /// </summary>
    protected string Title { get; set; } = "Menú Secundario";

    /// <summary>
    /// Color de franja del encabezado de trabajo.
    /// </summary>
    protected string HeaderColor { get; set; } = "#1e293b";

    /// <summary>
    /// Texto del botón de salida.
    /// </summary>
    protected string TextExit { get; set; } = "Salir";

    /// <summary>
    /// Acción de salida (retorna a MainView /).
    /// </summary>
    protected string ActionExit { get; set; } = "/";

    protected override async Task OnInitializedAsync()
    {
        if (!SessionState.IsAuthenticated)
        {
            NavigationManager.NavigateTo("/login");
            return;
        }

        await LoadTitleAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadTitleAsync();
    }

    /// <summary>
    /// Obtiene el nombre del privilegio padre para personalizar el título de la vista.
    /// Prioriza la búsqueda en SessionState; si no se encuentra y estamos en modo Mock, consulta MockDataService.
    /// </summary>
    private async Task LoadTitleAsync()
    {
        // 1. Buscar en privilegios de la sesión activa
        var parentPrivilege = SessionState.CurrentUser?.Privileges?
            .FirstOrDefault(p => p.Id == IdParent);

        // 2. Si no se encuentra en la sesión activa, fallback a MockDataService
        if (parentPrivilege == null && MockDataService != null)
        {
            parentPrivilege = await MockDataService.GetPrivilegeByIdAsync(IdParent);
        }

        if (parentPrivilege != null)
        {
            Title = $"Menú Secundario - {parentPrivilege.Title}";
        }
        else
        {
            Title = $"Menú Secundario ({IdParent})";
        }
    }
}
