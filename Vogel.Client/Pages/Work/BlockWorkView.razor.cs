using Microsoft.AspNetCore.Components;
using Vogel.Client.Services;
using Vogel.Shared.Models;
using Vogel.Shared.Services;

namespace Vogel.Client.Pages;

/// <summary>
/// Code-behind de la página BlockWorkView.
/// Vista análoga a WorkView, pero que no implementa elementos internos manuales (como InfoFrame o ListFrame).
/// Toda la construcción visual y funcional depende exclusivamente de la instancia recibida en el parámetro BlockWork.
/// </summary>
public partial class BlockWorkView : ComponentBase
{
    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    protected SessionState SessionState { get; set; } = default!;

    [Inject]
    protected IMockDataService MockDataService { get; set; } = default!;

    /// <summary>
    /// Identificador / codename de la entidad BlockWork recibido desde la URL.
    /// </summary>
    [Parameter]
    public string Codename { get; set; } = string.Empty;

    protected BlockWork? _blockWork;
    protected bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        if (!SessionState.IsAuthenticated)
        {
            NavigationManager.NavigateTo("/login");
            return;
        }

        await LoadBlockWorkAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadBlockWorkAsync();
    }

    /// <summary>
    /// Consulta y asigna la instancia de BlockWork para el codename recibido.
    /// </summary>
    private async Task LoadBlockWorkAsync()
    {
        _isLoading = true;

        try
        {
            _blockWork = await MockDataService.GetBlockWorkAsync(Codename);
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Gestiona el retorno hacia el menú secundario o la ruta indicada en ActionSet.Exit.
    /// </summary>
    protected void GoBackToSecondary()
    {
        if (_blockWork?.ActionSet?.Exit != null && !string.IsNullOrWhiteSpace(_blockWork.ActionSet.Exit))
        {
            var target = _blockWork.ActionSet.Exit.StartsWith("/")
                ? _blockWork.ActionSet.Exit
                : $"/{_blockWork.ActionSet.Exit}";
            NavigationManager.NavigateTo(target);
        }
        else
        {
            NavigationManager.NavigateTo("/");
        }
    }
}
