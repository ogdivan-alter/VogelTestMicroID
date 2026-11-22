using Microsoft.AspNetCore.Components;
using Vogel.Client.Services;
using Vogel.Shared.Models;

namespace Vogel.Client.Components;

/// <summary>
/// Code-behind del componente MainMenu.
/// Construye la barra lateral de navegación con comportamiento de acordeón de 2 niveles
/// a partir de los privilegios del usuario autenticado.
/// Corresponde a la sección 2.3.2 del Requerimiento Maestro.
/// </summary>
public class MainMenuBase : ComponentBase, IDisposable
{
    [Inject] protected SessionState SessionState { get; set; } = default!;

    /// <summary>
    /// Lista de privilegios a renderizar (por defecto toma los del usuario en SessionState).
    /// </summary>
    [Parameter] public List<Privilege>? Privileges { get; set; }

    /// <summary>
    /// Distintivo o badge opcional para la cabecera del menú.
    /// Si no se especifica ni existe título, la cabecera no tiene altura.
    /// </summary>
    [Parameter] public string? HeaderBadge { get; set; }

    /// <summary>
    /// Título opcional para la cabecera del menú.
    /// Si no se especifica ni existe badge, la cabecera no tiene altura.
    /// </summary>
    [Parameter] public string? HeaderTitle { get; set; }

    /// <summary>
    /// Identificador de la opción actualmente expandida en el acordeón (null = todas colapsadas).
    /// Siempre inicializa con todas las opciones colapsadas.
    /// </summary>
    public int? ExpandedOptionId { get; protected set; }

    /// <summary>
    /// Opciones de nivel raíz (IdParent == 0).
    /// </summary>
    protected List<Privilege> RootOptions { get; private set; } = new();

    protected override void OnInitialized()
    {
        SessionState.OnChange += HandleSessionStateChanged;
        LoadMenuStructure();
    }

    protected override void OnParametersSet()
    {
        LoadMenuStructure();
    }

    private void HandleSessionStateChanged()
    {
        LoadMenuStructure();
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Carga y clasifica los privilegios del usuario para la construcción del menú.
    /// </summary>
    protected void LoadMenuStructure()
    {
        var sourcePrivileges = Privileges ?? SessionState.CurrentUser?.Privileges ?? new List<Privilege>();

        RootOptions = sourcePrivileges
            .Where(p => p.IdParent == 0)
            .OrderBy(p => p.Order)
            .ToList();
    }

    /// <summary>
    /// Obtiene las subopciones subordinadas a una opción padre específica.
    /// </summary>
    protected List<Privilege> GetSuboptions(int parentId)
    {
        var sourcePrivileges = Privileges ?? SessionState.CurrentUser?.Privileges ?? new List<Privilege>();

        return sourcePrivileges
            .Where(p => p.IdParent == parentId)
            .OrderBy(p => p.Order)
            .ToList();
    }

    /// <summary>
    /// Maneja el clic en una opción raíz asegurando el comportamiento de acordeón exclusivo:
    /// abrir una opción colapsa automáticamente todas las demás.
    /// Si se pulsa la opción ya abierta, se colapsa.
    /// </summary>
    /// <param name="optionId">Identificador de la opción pulsada.</param>
    public void HandleOptionToggle(int optionId)
    {
        if (ExpandedOptionId == optionId)
        {
            // Colapsar la opción actualmente abierta
            ExpandedOptionId = null;
        }
        else
        {
            // Abrir la opción seleccionada y colapsar automáticamente las demás
            ExpandedOptionId = optionId;
        }

        StateHasChanged();
    }

    public void Dispose()
    {
        SessionState.OnChange -= HandleSessionStateChanged;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Resuelve la acción de navegación para una subopción del menú.
    /// Si el privilegio posee opciones subordinadas (hijos), genera la ruta dinámica hacia el menú secundario (/menu/{Codename}).
    /// En caso contrario, retorna la acción directa configurada en el privilegio (o la raíz '/' por defecto).
    /// </summary>
    /// <param name="codename">Nombre clave (codename) o identificador numérico de la subopción.</param>
    /// <returns>Ruta URL de destino para la navegación.</returns>
    public string Submenu(string? codename)
    {
        if (string.IsNullOrWhiteSpace(codename))
        {
            return "/";
        }

        var privileges = Privileges ?? SessionState.CurrentUser?.Privileges;
        if (privileges == null || privileges.Count == 0)
        {
            return "/";
        }

        // 1. Buscar por Codename (comparación insensible a mayúsculas/minúsculas)
        var privilege = privileges.FirstOrDefault(p =>
            string.Equals(p.Codename, codename, StringComparison.OrdinalIgnoreCase));

        // 2. Fallback: buscar por Id si el parámetro provisto es un identificador numérico
        if (privilege == null && int.TryParse(codename, out var numericId))
        {
            privilege = privileges.FirstOrDefault(p => p.Id == numericId);
        }

        if (privilege == null)
        {
            return "/";
        }

        // Si existen privilegios subordinados (hijos), dirigir a la vista de plantilla de menú secundario
        var hasChildren = privileges.Any(x => x.IdParent == privilege.Id);
        if (hasChildren)
        {
            var routeKey = !string.IsNullOrWhiteSpace(privilege.Codename) ? privilege.Codename : privilege.Id.ToString();
            return $"/menu/{routeKey}";
        }

        // Opción terminal: retornar su acción directa configurada
        return !string.IsNullOrWhiteSpace(privilege.Action) ? privilege.Action : "/";
    }

    /// <summary>
    /// Sobrecarga que resuelve la acción de navegación directamente a partir de la entidad Privilege.
    /// </summary>
    /// <param name="sub">Instancia del privilegio a evaluar.</param>
    /// <returns>Ruta URL de destino para la navegación.</returns>
    public string Submenu(Privilege? sub)
    {
        if (sub == null)
        {
            return "/";
        }

        return Submenu(!string.IsNullOrWhiteSpace(sub.Codename) ? sub.Codename : sub.Id.ToString());
    }
}
