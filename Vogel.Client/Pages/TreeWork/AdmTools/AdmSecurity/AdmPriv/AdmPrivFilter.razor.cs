using Microsoft.AspNetCore.Components;
using Vogel.Client.Services;
using Vogel.Shared.Models;

namespace Vogel.Client.Pages.TreeWork.AdmTools.AdmSecurity.AdmPriv;

/// <summary>
/// Code-behind de la vista Filter de Administración de Privilegios.
/// Gestiona el radio button de estado (Activos/Inactivos/Todos),
/// filtro ignorecase por title y filtro numérico por id_parent.
/// </summary>
public partial class AdmPrivFilter : ComponentBase, IDisposable
{
    [Inject]
    protected IAdmPrivStateService AdmPrivState { get; set; } = default!;

    [Parameter]
    public BlockWork? BlockWork { get; set; }

    protected bool HasTitleFilter => !string.IsNullOrWhiteSpace(AdmPrivState.TitleFilter);

    protected bool HasIdParentFilter => !string.IsNullOrWhiteSpace(AdmPrivState.IdParentFilter);

    protected override void OnInitialized()
    {
        AdmPrivState.OnStateChanged += HandleStateChanged;
    }

    protected bool IsStatusSelected(string status)
    {
        return string.Equals(AdmPrivState.StatusFilter, status, StringComparison.OrdinalIgnoreCase);
    }

    protected void HandleStatusChanged(string status)
    {
        AdmPrivState.SetStatusFilter(status);
    }

    protected void HandleTitleInput(ChangeEventArgs e)
    {
        AdmPrivState.SetTitleFilter(e.Value?.ToString() ?? string.Empty);
    }

    protected void ClearTitleFilter()
    {
        AdmPrivState.SetTitleFilter(string.Empty);
    }

    protected void HandleIdParentInput(ChangeEventArgs e)
    {
        AdmPrivState.SetIdParentFilter(e.Value?.ToString() ?? string.Empty);
    }

    protected void ClearIdParentFilter()
    {
        AdmPrivState.SetIdParentFilter(string.Empty);
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
