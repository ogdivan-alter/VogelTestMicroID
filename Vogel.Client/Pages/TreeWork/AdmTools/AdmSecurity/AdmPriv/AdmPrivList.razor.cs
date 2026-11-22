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
    /// Handler reservado para abrir el formulario de edición del privilegio en la siguiente fase.
    /// </summary>
    protected void HandleEditClick(PrivilegeAdminItemDto item)
    {
        // Pendiente de implementación del formulario de edición de privilegio.
        _ = item;
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
