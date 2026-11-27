using Microsoft.AspNetCore.Components;
using Vogel.Client.Services;
using Vogel.Shared.Enums;
using Vogel.Shared.Models;

namespace Vogel.Client.Pages.TreeWork.AdmTools.AdmSecurity.AdmPriv;

/// <summary>
/// Code-behind de la vista Capture de Administración de Privilegios.
/// Expone el botón "Crear privilegio" exclusivamente cuando el usuario posee permiso Create sobre el módulo.
/// </summary>
public partial class AdmPrivCapture : ComponentBase
{
    [Inject]
    protected SessionState SessionState { get; set; } = default!;

    [Inject]
    protected IAdmPrivStateService AdmPrivState { get; set; } = default!;

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Parameter]
    public BlockWork? BlockWork { get; set; }

    private string EffectiveCodename =>
        !string.IsNullOrWhiteSpace(BlockWork?.Codename) ? BlockWork.Codename : AdmPrivState.ModuleCodename;

    protected bool CanCreate => SessionState.HasPrivilegeAccess(EffectiveCodename, PrivilegeAction.Create);

    /// <summary>
    /// Limpia cualquier privilegio seleccionado para edición y navega al formulario de creación.
    /// </summary>
    protected void HandleCreatePrivilegeClick()
    {
        if (!CanCreate)
        {
            return;
        }

        AdmPrivState.ClearMessages();
        AdmPrivState.ClearSelectedEditPrivilegeId();
        NavigationManager.NavigateTo("/admsec/priv/create");
    }
}
