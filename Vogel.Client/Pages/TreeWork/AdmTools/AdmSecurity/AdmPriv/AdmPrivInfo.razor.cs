using Microsoft.AspNetCore.Components;
using Vogel.Client.Services;
using Vogel.Shared.Models;

namespace Vogel.Client.Pages.TreeWork.AdmTools.AdmSecurity.AdmPriv;

/// <summary>
/// Code-behind de la vista Info de Administración de Privilegios.
/// Muestra el sistema al que pertenecen los privilegios administrados.
/// </summary>
public partial class AdmPrivInfo : ComponentBase, IDisposable
{
    [Inject]
    protected IAdmPrivStateService AdmPrivState { get; set; } = default!;

    [Parameter]
    public BlockWork? BlockWork { get; set; }

    protected string DisplaySystemCode =>
        string.IsNullOrWhiteSpace(AdmPrivState.SystemCode) ? "MICROID" : AdmPrivState.SystemCode;

    protected override void OnInitialized()
    {
        AdmPrivState.OnStateChanged += HandleStateChanged;
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
