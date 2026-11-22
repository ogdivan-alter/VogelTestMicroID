using Microsoft.AspNetCore.Components;
using Vogel.Shared.Models;
using Vogel.Shared.Services;

namespace Vogel.Client.Components;

/// <summary>
/// Code-behind de OpSecundariaListFrame.
/// Vista referenciada en BlockWork.ViewSet.List para la entidad 'op-secundaria'.
/// Encapsula la consulta de datos de detalle y paginación mediante ListViewFrame.
/// Cumple con las secciones 2.3.9, 2.3.11 y 2.4.3 del Requerimiento Maestro.
/// </summary>
public partial class OpSecundariaListFrame : ComponentBase
{
    [Inject]
    protected IMockDataService MockDataService { get; set; } = default!;

    [Parameter]
    public BlockWork? BlockWork { get; set; }

    protected int _currentPage = 1;
    protected int _pageSize = 10;
    protected int _totalCount = 0;
    protected List<Dictionary<string, object>> _items = new();
    protected bool _isLoading = false;
    protected string _filterTerm = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync(1);
    }

    public async Task LoadDataAsync(int page)
    {
        _isLoading = true;
        _currentPage = page;

        try
        {
            var codename = BlockWork?.Codename ?? "op-secundaria";
            var result = await MockDataService.GetDetailDataPagedAsync(codename, _currentPage, _pageSize);

            if (string.IsNullOrWhiteSpace(_filterTerm))
            {
                _items = result.Items;
                _totalCount = result.TotalCount;
            }
            else
            {
                var allResult = await MockDataService.GetDetailDataPagedAsync(codename, 1, 1000);
                var filtered = allResult.Items.Where(item =>
                    item.Values.Any(v => v?.ToString()?.Contains(_filterTerm, StringComparison.OrdinalIgnoreCase) == true)
                ).ToList();

                _totalCount = filtered.Count;
                _items = filtered
                    .Skip((_currentPage - 1) * _pageSize)
                    .Take(_pageSize)
                    .ToList();
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    public async Task HandlePageChangedAsync(int page)
    {
        await LoadDataAsync(page);
    }

    public async Task HandleSearchInput(ChangeEventArgs e)
    {
        _filterTerm = e.Value?.ToString() ?? string.Empty;
        await LoadDataAsync(1);
    }

    public async Task ClearFilter()
    {
        _filterTerm = string.Empty;
        await LoadDataAsync(1);
    }
}
