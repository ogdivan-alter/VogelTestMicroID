using Microsoft.AspNetCore.Components;
using Vogel.Client.Services;
using Vogel.Shared.Models;
using Vogel.Shared.Services;

namespace Vogel.Client.Pages;

/// <summary>
/// Code-behind de la página WorkView.
/// Integra el contenedor WorkViewFrame, InfoViewFrame y ListViewFrame para la gestión maestro-detalle.
/// Corresponde al Sprint 6 y a las fases de desarrollo del Requerimiento Maestro.
/// </summary>
public partial class WorkView : ComponentBase
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
    protected bool _isLoadingData = false;

    // Estado de paginación
    protected int _currentPage = 1;
    protected int _pageSize = 10;
    protected int _totalCount = 0;
    protected List<Dictionary<string, object>> _detailItems = new();
    protected string _filterTerm = string.Empty;

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
    /// Carga la configuración de la entidad BlockWork a partir del codename provisto.
    /// </summary>
    private async Task LoadBlockWorkAsync()
    {
        _isLoading = true;

        try
        {
            _blockWork = await MockDataService.GetBlockWorkAsync(Codename);

            if (_blockWork != null)
            {
                await LoadPagedDataAsync(1);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Consulta los registros de detalle paginados correspondientes a la página indicada.
    /// </summary>
    public async Task LoadPagedDataAsync(int page)
    {
        _isLoadingData = true;
        _currentPage = page;

        try
        {
            var result = await MockDataService.GetDetailDataPagedAsync(Codename, _currentPage, _pageSize);

            if (string.IsNullOrWhiteSpace(_filterTerm))
            {
                _detailItems = result.Items;
                _totalCount = result.TotalCount;
            }
            else
            {
                // Filtrado en memoria de prueba si el usuario ingresó un término de búsqueda
                var allResult = await MockDataService.GetDetailDataPagedAsync(Codename, 1, 1000);
                var filtered = allResult.Items.Where(item =>
                    item.Values.Any(v => v?.ToString()?.Contains(_filterTerm, StringComparison.OrdinalIgnoreCase) == true)
                ).ToList();

                _totalCount = filtered.Count;
                _detailItems = filtered
                    .Skip((_currentPage - 1) * _pageSize)
                    .Take(_pageSize)
                    .ToList();
            }
        }
        finally
        {
            _isLoadingData = false;
        }
    }

    /// <summary>
    /// Event handler para cuando se pulsa un botón de cambio de página en ListViewFrame.
    /// </summary>
    protected async Task HandlePageChangedAsync(int newPage)
    {
        await LoadPagedDataAsync(newPage);
    }

    /// <summary>
    /// Manejador de evento al escribir en el campo de búsqueda.
    /// </summary>
    protected async Task HandleSearchInput(ChangeEventArgs e)
    {
        _filterTerm = e.Value?.ToString() ?? string.Empty;
        await LoadPagedDataAsync(1);
    }

    /// <summary>
    /// Limpia el filtro de búsqueda actual y recarga la primera página.
    /// </summary>
    protected async Task ClearFilter()
    {
        _filterTerm = string.Empty;
        await LoadPagedDataAsync(1);
    }

    /// <summary>
    /// Regresa a la vista secundaria de procedencia o a la vista principal.
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
