using Microsoft.AspNetCore.Components;
using Vogel.Client.Services;
using Vogel.Shared.Models;

namespace Vogel.Client.Pages.Template;

/// <summary>
/// Vista plantilla para la navegación de menús secundarios mediante ruta paramétrica (/menu/{Codename}).
/// Resuelve el identificador de privilegio padre a partir del Codename o Id provisto en la URL
/// y delega la renderización en el componente SecondaryMenuPage.
/// Corresponde a la sección 2.2.4 del Requerimiento Maestro.
/// </summary>
public partial class SecondaryMenu : ComponentBase
{
    [Inject]
    protected SessionState SessionState { get; set; } = default!;

    [Inject]
    protected Vogel.Shared.Services.IMockDataService? MockDataService { get; set; }

    [Inject]
    protected Microsoft.Extensions.Configuration.IConfiguration? Configuration { get; set; }

    /// <summary>
    /// Identificador textual (Codename) o numérico obtenido desde la ruta de navegación.
    /// </summary>
    [Parameter]
    public string Codename { get; set; } = string.Empty;

    /// <summary>
    /// Obtiene el identificador numérico (Id) del privilegio padre correspondiente al Codename recibido.
    /// </summary>
    /// <param name="codename">Codename textual o Id numérico del privilegio.</param>
    /// <returns>Id del privilegio padre si existe; de lo contrario, 0.</returns>
    public int IdParentCode(string? codename)
    {
        if (string.IsNullOrWhiteSpace(codename))
        {
            return 0;
        }

        var privileges = SessionState.CurrentUser?.Privileges;
        if (privileges == null || privileges.Count == 0)
        {
            return 0;
        }

        // 1. Buscar coincidencia por Codename
        var privilege = privileges.FirstOrDefault(p =>
            string.Equals(p.Codename, codename, StringComparison.OrdinalIgnoreCase));

        // 2. Fallback: buscar por Id numérico
        if (privilege == null && int.TryParse(codename, out var numericId))
        {
            privilege = privileges.FirstOrDefault(p => p.Id == numericId);
        }

        // 3. Fallback a MockDataService si no se encuentra en los privilegios de sesión
        if (privilege == null && MockDataService != null)
        {
            var mockPrivilege = MockDataService.GetPrivilegeByCodenameAsync(codename).GetAwaiter().GetResult();
            if (mockPrivilege == null && int.TryParse(codename, out var numId))
            {
                mockPrivilege = MockDataService.GetPrivilegeByIdAsync(numId).GetAwaiter().GetResult();
            }
            return mockPrivilege?.Id ?? 0;
        }

        return privilege?.Id ?? 0;
    }
}
