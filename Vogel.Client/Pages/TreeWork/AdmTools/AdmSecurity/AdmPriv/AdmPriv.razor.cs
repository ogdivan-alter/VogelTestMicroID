using Microsoft.AspNetCore.Components;
using Vogel.Client.Services;

namespace Vogel.Client.Pages.TreeWork.AdmTools.AdmSecurity.AdmPriv;

/// <summary>
/// Code-behind de la vista principal de Administración de Privilegios (/admsec/priv).
/// Valida la sesión autenticada e inicia la carga de privilegios en el estado compartido.
/// </summary>
public partial class AdmPriv : ComponentBase
{
    [Inject]
    protected SessionState SessionState { get; set; } = default!;

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    protected IAdmPrivStateService AdmPrivState { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        if (!SessionState.IsAuthenticated)
        {
            NavigationManager.NavigateTo("/login");
            return;
        }

        await AdmPrivState.LoadAsync();
    }
}
