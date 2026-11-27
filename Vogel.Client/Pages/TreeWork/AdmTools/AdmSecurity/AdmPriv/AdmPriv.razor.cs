using Microsoft.AspNetCore.Components;
using Vogel.Client.Services;
using Vogel.Shared.Enums;
using Vogel.Shared.Models;

namespace Vogel.Client.Pages.TreeWork.AdmTools.AdmSecurity.AdmPriv;

/// <summary>
/// Code-behind de la vista principal de Administración de Privilegios (/admsec/priv).
/// Inicializa el BlockWork del módulo (fuente única del codename en el frontend) y
/// valida la sesión autenticada y el permiso de lectura antes de cargar el catálogo.
/// </summary>
public partial class AdmPriv : ComponentBase
{
    [Inject]
    protected SessionState SessionState { get; set; } = default!;

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    protected IAdmPrivStateService AdmPrivState { get; set; } = default!;

    protected BlockWork ModuleBlockWork { get; private set; } = new();

    protected bool CanRead => SessionState.HasPrivilegeAccess(AdmPrivState.ModuleCodename, PrivilegeAction.Read);

    protected override async Task OnInitializedAsync()
    {
        ModuleBlockWork = CreateBlockWork();
        AdmPrivState.SetModuleCodename(ModuleBlockWork.Codename);

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
