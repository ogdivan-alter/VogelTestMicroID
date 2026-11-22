using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Vogel.Shared.DTOs;

namespace Vogel.Client.Services;

/// <summary>
/// Implementación del estado compartido para la administración de privilegios (AdmPriv).
/// Consume exclusivamente el endpoint BFF 'api/privileges' adjuntando el token Bearer de sesión.
/// </summary>
public sealed class AdmPrivStateService : IAdmPrivStateService
{
    private const string DefaultSystemCode = "MICROID";
    private const int DefaultPageSize = 10;

    private readonly HttpClient _httpClient;
    private readonly ISecureSessionStorageService _sessionStorage;
    private readonly IClientLogger _logger;

    private List<PrivilegeAdminItemDto> _allItems = new();
    private List<PrivilegeAdminItemDto> _filteredItems = new();
    private List<PrivilegeAdminItemDto> _pagedItems = new();

    public AdmPrivStateService(
        HttpClient httpClient,
        ISecureSessionStorageService sessionStorage,
        IClientLogger logger)
    {
        _httpClient = httpClient;
        _sessionStorage = sessionStorage;
        _logger = logger;
    }

    public string SystemCode { get; private set; } = DefaultSystemCode;

    public int IdSystem { get; private set; }

    public bool IsLoading { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string StatusFilter { get; private set; } = "A";

    public string TitleFilter { get; private set; } = string.Empty;

    public string IdParentFilter { get; private set; } = string.Empty;

    public IReadOnlyList<PrivilegeAdminItemDto> AllItems => _allItems;

    public IReadOnlyList<PrivilegeAdminItemDto> FilteredItems => _filteredItems;

    public int CurrentPage { get; private set; } = 1;

    public int PageSize => DefaultPageSize;

    public IReadOnlyList<PrivilegeAdminItemDto> PagedItems => _pagedItems;

    public int TotalFilteredCount => _filteredItems.Count;

    public event Action? OnStateChanged;

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        NotifyStateChanged();

        try
        {
            var token = await _sessionStorage.GetItemDecryptedAsync(AuthenticationService.BackendTokenKey);

            using var request = new HttpRequestMessage(HttpMethod.Get, "api/privileges");
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _allItems = new List<PrivilegeAdminItemDto>();
                ApplyFiltersAndPagination(resetPage: true);

                ErrorMessage = response.StatusCode == HttpStatusCode.Unauthorized
                    ? "Su sesión ha expirado o no es válida. Por favor inicie sesión nuevamente."
                    : "No fue posible cargar los privilegios del sistema. Intente nuevamente.";

                await _logger.LogWarningAsync(
                    $"Consulta a api/privileges rechazada con código {(int)response.StatusCode}.",
                    nameof(AdmPrivStateService));
                return;
            }

            var payload = await response.Content.ReadFromJsonAsync<PrivilegeAdminListResponseDto>();
            if (payload == null)
            {
                _allItems = new List<PrivilegeAdminItemDto>();
                ApplyFiltersAndPagination(resetPage: true);
                ErrorMessage = "Respuesta vacía del servidor al consultar privilegios.";
                return;
            }

            SystemCode = !string.IsNullOrWhiteSpace(payload.SystemCode)
                ? payload.SystemCode
                : DefaultSystemCode;
            IdSystem = payload.IdSystem;
            _allItems = payload.Items ?? new List<PrivilegeAdminItemDto>();

            ApplyFiltersAndPagination(resetPage: true);
        }
        catch (Exception ex)
        {
            _allItems = new List<PrivilegeAdminItemDto>();
            ApplyFiltersAndPagination(resetPage: true);
            ErrorMessage = "Ocurrió un error al consultar los privilegios del sistema.";
            await _logger.LogErrorAsync(
                "Error inesperado al consultar api/privileges.",
                ex,
                nameof(AdmPrivStateService));
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public void SetStatusFilter(string statusFilter)
    {
        var normalized = (statusFilter ?? "A").Trim().ToUpperInvariant();
        if (normalized != "A" && normalized != "I" && normalized != "ALL")
        {
            normalized = "A";
        }

        if (string.Equals(StatusFilter, normalized, StringComparison.Ordinal))
        {
            return;
        }

        StatusFilter = normalized;
        ApplyFiltersAndPagination(resetPage: true);
        NotifyStateChanged();
    }

    public void SetTitleFilter(string titleFilter)
    {
        var value = titleFilter ?? string.Empty;
        if (string.Equals(TitleFilter, value, StringComparison.Ordinal))
        {
            return;
        }

        TitleFilter = value;
        ApplyFiltersAndPagination(resetPage: true);
        NotifyStateChanged();
    }

    public void SetIdParentFilter(string idParentFilter)
    {
        var value = idParentFilter ?? string.Empty;
        if (string.Equals(IdParentFilter, value, StringComparison.Ordinal))
        {
            return;
        }

        IdParentFilter = value;
        ApplyFiltersAndPagination(resetPage: true);
        NotifyStateChanged();
    }

    public void SetPage(int page)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)_filteredItems.Count / PageSize));
        var targetPage = Math.Clamp(page, 1, totalPages);

        if (CurrentPage == targetPage)
        {
            return;
        }

        CurrentPage = targetPage;
        UpdatePagedItems();
        NotifyStateChanged();
    }

    public void ResetFilters()
    {
        StatusFilter = "A";
        TitleFilter = string.Empty;
        IdParentFilter = string.Empty;
        ApplyFiltersAndPagination(resetPage: true);
        NotifyStateChanged();
    }

    private void ApplyFiltersAndPagination(bool resetPage)
    {
        IEnumerable<PrivilegeAdminItemDto> query = _allItems;

        if (!string.Equals(StatusFilter, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(item =>
                string.Equals(item.Status, StatusFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(TitleFilter))
        {
            var titleTerm = TitleFilter.Trim();
            query = query.Where(item =>
                !string.IsNullOrEmpty(item.Title) &&
                item.Title.Contains(titleTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(IdParentFilter) &&
            int.TryParse(IdParentFilter.Trim(), out var parsedIdParent))
        {
            query = query.Where(item => item.IdParent == parsedIdParent);
        }

        _filteredItems = query.ToList();

        if (resetPage)
        {
            CurrentPage = 1;
        }
        else
        {
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)_filteredItems.Count / PageSize));
            CurrentPage = Math.Clamp(CurrentPage, 1, totalPages);
        }

        UpdatePagedItems();
    }

    private void UpdatePagedItems()
    {
        _pagedItems = _filteredItems
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
