using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Vogel.Client.Services;
using Vogel.Shared.Models;
using Vogel.Shared.Services;

namespace Vogel.Client.Components;

/// <summary>
/// Code-behind del componente SecondaryMenu.
/// Estructura y visualiza el tablero de opciones habilitadas para el usuario bajo un IdParent.
/// Agrupa los privilegios por 'Group', los ordena por 'Order' y los presenta en una cuadrícula responsiva (máximo 4 por fila).
/// Corresponde a la sección 2.3.7 del Requerimiento Maestro.
/// </summary>
public partial class SecondaryMenu : ComponentBase
{
    [Inject]
    protected SessionState SessionState { get; set; } = default!;

    [Inject]
    protected IMockDataService MockDataService { get; set; } = default!;

    [Inject]
    protected IConfiguration Configuration { get; set; } = default!;

    /// <summary>
    /// Identificador del privilegio padre sobre el cual se cargan las opciones subordinadas.
    /// </summary>
    [Parameter]
    public int IdParent { get; set; }

    /// <summary>
    /// Lista opcional de privilegios ya provista externamente (para pruebas o inyección directa).
    /// </summary>
    [Parameter]
    public List<Privilege>? Privileges { get; set; }

    /// <summary>
    /// Diccionario de privilegios agrupados por categoría y ordenados por su número de orden.
    /// </summary>
    protected Dictionary<string, List<Privilege>> _groupedPrivileges = new();

    /// <summary>
    /// Bandera de estado de carga.
    /// </summary>
    protected bool _isLoading = true;

    protected override async Task OnParametersSetAsync()
    {
        await LoadPrivilegesAsync();
    }

    /// <summary>
    /// Carga los privilegios correspondientes al IdParent, los agrupa por categoría y los ordena.
    /// Prioriza los privilegios en memoria de SessionState; si no se hallan y el proveedor es Mock, consulta MockDataService.
    /// </summary>
    public async Task LoadPrivilegesAsync()
    {
        _isLoading = true;

        try
        {
            List<Privilege> source = new();

            if (Privileges != null && Privileges.Any())
            {
                source = Privileges.Where(p => p.IdParent == IdParent).ToList();
            }
            else if (SessionState.CurrentUser?.Privileges != null)
            {
                source = SessionState.CurrentUser.Privileges
                    .Where(p => p.IdParent == IdParent)
                    .ToList();

                // Si no hay hijos en sesión y estamos en modo Mock, consultar MockDataService como fallback
                if (!source.Any())
                {
                    var authProvider = Configuration.GetValue<string>("AuthProvider", "Api") ?? "Api";
                    if (string.Equals(authProvider, "Mock", StringComparison.OrdinalIgnoreCase))
                    {
                        source = await MockDataService.GetSubPrivilegesAsync(IdParent);
                    }
                }
            }
            else
            {
                var authProvider = Configuration.GetValue<string>("AuthProvider", "Api") ?? "Api";
                if (string.Equals(authProvider, "Mock", StringComparison.OrdinalIgnoreCase))
                {
                    source = await MockDataService.GetSubPrivilegesAsync(IdParent);
                }
            }

            // Agrupar por 'Group' (o "General" si viene vacío/nulo) y ordenar cada grupo por 'Order'
            _groupedPrivileges = source
                .GroupBy(p => string.IsNullOrWhiteSpace(p.Group) ? "General" : p.Group)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(p => p.Order).ToList()
                );
        }
        finally
        {
            _isLoading = false;
        }
    }
}
