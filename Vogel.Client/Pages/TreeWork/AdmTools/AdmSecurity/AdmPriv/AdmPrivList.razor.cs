using Microsoft.AspNetCore.Components;
using Vogel.Client.Services;
using Vogel.Shared.DTOs;
using Vogel.Shared.Models;

namespace Vogel.Client.Pages.TreeWork.AdmTools.AdmSecurity.AdmPriv;

/// <summary>
/// Code-behind de la vista List de Administración de Privilegios.
/// Renderiza los privilegios filtrados en ListViewFrame y se suscribe reactivamente a AdmPrivState.
/// </summary>
public partial class AdmPrivList : ComponentBase, IDisposable
{
    [Inject]
    protected IAdmPrivStateService AdmPrivState { get; set; } = default!;

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Parameter]
    public BlockWork? BlockWork { get; set; }

    protected override void OnInitialized()
    {
        AdmPrivState.OnStateChanged += HandleStateChanged;
    }

    protected Task HandlePageChangedAsync(int page)
    {
        AdmPrivState.SetPage(page);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Almacena en memoria el identificador del privilegio activo y navega a la ruta limpia /admsec/priv/edit.
    /// </summary>
    protected void HandleEditClick(PrivilegeAdminItemDto item)
    {
        if (item is null || !string.Equals(item.Status, "A", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AdmPrivState.SetSelectedEditPrivilegeId(item.IdPrivilege);
        NavigationManager.NavigateTo("/admsec/priv/edit");
    }

    private void HandleStateChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        AdmPrivState.OnStateChanged -= HandleStateChanged;
    }
}
