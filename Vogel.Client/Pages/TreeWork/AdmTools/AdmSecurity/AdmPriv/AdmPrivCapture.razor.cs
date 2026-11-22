using Microsoft.AspNetCore.Components;
using Vogel.Client.Services;
using Vogel.Shared.Models;

namespace Vogel.Client.Pages.TreeWork.AdmTools.AdmSecurity.AdmPriv;

/// <summary>
/// Code-behind de la vista Capture de Administración de Privilegios.
/// Expone el botón "Crear privilegio" que limpia cualquier selección de edición y navega a /admsec/priv/create.
/// </summary>
public partial class AdmPrivCapture : ComponentBase
{
    [Inject]
    protected IAdmPrivStateService AdmPrivState { get; set; } = default!;

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Parameter]
    public BlockWork? BlockWork { get; set; }

    /// <summary>
    /// Limpia cualquier privilegio seleccionado para edición y navega al formulario de creación.
    /// </summary>
    protected void HandleCreatePrivilegeClick()
    {
        AdmPrivState.ClearSelectedEditPrivilegeId();
        NavigationManager.NavigateTo("/admsec/priv/create");
    }
}
