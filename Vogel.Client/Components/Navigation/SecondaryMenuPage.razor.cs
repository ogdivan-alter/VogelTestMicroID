using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Vogel.Client.Services;
using Vogel.Shared.Services;

namespace Vogel.Client.Components;

/// <summary>
/// Code-behind de la vista secundaria (SecondaryView).
/// Despliega el encabezado HeaderWork y el tablero de opciones SecondaryMenu.
/// Ocupa la totalidad del cuerpo del layout, sustituyendo la vista principal.
/// Corresponde a la sección 2.2.4 del Requerimiento Maestro.
/// </summary>
public partial class SecondaryMenuPage : ComponentBase
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
    public int IdParent { get; set; } = 0;

    /// <summary>
    /// Título personalizado opcional para el HeaderWork (si no se proporciona, se calcula dinámicamente).
    /// </summary>
    [Parameter]
    public string? CustomTitle { get; set; }

    /// <summary>
    /// Color de franja del encabezado de trabajo.
    /// </summary>
    [Parameter]
    public string HeaderColor { get; set; } = "#0c0e14";

    /// <summary>
    /// Texto del botón de salida.
    /// </summary>
    [Parameter]
    public string TextExit { get; set; } = "Salir";

    /// <summary>
    /// Acción de salida (retorna a MainView '/' por defecto).
    /// </summary>
    [Parameter]
    public string ActionExit { get; set; } = "/";

    /// <summary>
    /// Título dinámico que se muestra en el HeaderWork.
    /// </summary>
    protected string Title { get; set; } = "Menú Secundario";

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
        if (!string.IsNullOrWhiteSpace(CustomTitle))
        {
            Title = CustomTitle;
            return;
        }

        // 1. Buscar en privilegios de la sesión activa por Id
        var parentPrivilege = SessionState.CurrentUser?.Privileges?
            .FirstOrDefault(p => p.Id == IdParent);

        // 2. Si no se encuentra y el proveedor es Mock, consultar MockDataService
        if (parentPrivilege == null)
        {
            var authProvider = Configuration.GetValue<string>("AuthProvider", "Api") ?? "Api";
            if (string.Equals(authProvider, "Mock", StringComparison.OrdinalIgnoreCase))
            {
                parentPrivilege = await MockDataService.GetPrivilegeByIdAsync(IdParent);
            }
        }

        if (parentPrivilege != null && !string.IsNullOrWhiteSpace(parentPrivilege.Title))
        {
            Title = $"{parentPrivilege.Title}";
        }
        else
        {
            Title = IdParent > 0 ? $"Menú Secundario ({IdParent})" : "Menú Secundario";
        }
    }
}
