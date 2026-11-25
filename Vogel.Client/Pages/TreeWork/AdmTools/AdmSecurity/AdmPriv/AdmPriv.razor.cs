using Microsoft.AspNetCore.Components;
using Vogel.Client.Services;
using Vogel.Shared.Enums;

namespace Vogel.Client.Pages.TreeWork.AdmTools.AdmSecurity.AdmPriv;

/// <summary>
/// Code-behind de la vista principal de Administración de Privilegios (/admsec/priv).
/// Valida la sesión autenticada y el permiso de lectura antes de iniciar la carga en el estado compartido.
/// </summary>
public partial class AdmPriv : ComponentBase
{
    private const string ModulePrivilegeCodename = "adm_priv";

    [Inject]
    protected SessionState SessionState { get; set; } = default!;

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    protected IAdmPrivStateService AdmPrivState { get; set; } = default!;

    protected bool CanRead => SessionState.HasPrivilegeAccess(ModulePrivilegeCodename, PrivilegeAction.Read);

    protected override async Task OnInitializedAsync()
    {
        if (!SessionState.IsAuthenticated)
        {
            NavigationManager.NavigateTo("/login");
            return;
        }

        if (!CanRead)
        {
            AdmPrivState.ClearCatalog();
            return;
        }

        var pendingError = AdmPrivState.ErrorMessage;
        await AdmPrivState.LoadAsync();
        if (!string.IsNullOrWhiteSpace(pendingError) && string.IsNullOrWhiteSpace(AdmPrivState.ErrorMessage))
        {
            AdmPrivState.SetErrorMessage(pendingError);
        }
    }
}
