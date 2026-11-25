using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Vogel.Shared.DTOs;
using Vogel.Shared.Enums;

namespace Vogel.Client.Services;

/// <summary>
/// Implementación del estado compartido para la administración de privilegios (AdmPriv).
/// Consume exclusivamente el endpoint BFF 'api/privileges' adjuntando el token Bearer de sesión.
/// </summary>
public sealed class AdmPrivStateService : IAdmPrivStateService
{
    private const string DefaultSystemCode = "MICROID";
    private const string ModulePrivilegeCodename = "adm_priv";
    private const int DefaultPageSize = 10;

    private readonly HttpClient _httpClient;
    private readonly ISecureSessionStorageService _sessionStorage;
    private readonly IClientLogger _logger;
    private readonly SessionState _sessionState;

    private List<PrivilegeAdminItemDto> _allItems = new();
    private List<PrivilegeAdminItemDto> _filteredItems = new();
    private List<PrivilegeAdminItemDto> _pagedItems = new();

    public AdmPrivStateService(
        HttpClient httpClient,
        ISecureSessionStorageService sessionStorage,
        IClientLogger logger,
        SessionState sessionState)
    {
        _httpClient = httpClient;
        _sessionStorage = sessionStorage;
        _logger = logger;
        _sessionState = sessionState;
    }

    public string SystemCode { get; private set; } = DefaultSystemCode;

    public int IdSystem { get; private set; }

    public bool IsLoading { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? SuccessMessage { get; private set; }

    public string StatusFilter { get; private set; } = "A";

    public string TitleFilter { get; private set; } = string.Empty;

    public string IdParentFilter { get; private set; } = string.Empty;

    public IReadOnlyList<PrivilegeAdminItemDto> AllItems => _allItems;

    public IReadOnlyList<PrivilegeAdminItemDto> FilteredItems => _filteredItems;

    public int CurrentPage { get; private set; } = 1;

    public int PageSize => DefaultPageSize;

    public IReadOnlyList<PrivilegeAdminItemDto> PagedItems => _pagedItems;

    public int TotalFilteredCount => _filteredItems.Count;

    public int? SelectedEditPrivilegeId { get; private set; }

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

                ErrorMessage = await ExtractErrorMessageAsync(
                    response,
                    "No fue posible cargar los privilegios del sistema. Intente nuevamente.");

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

    public void SetSelectedEditPrivilegeId(int id)
    {
        var normalized = id > 0 ? id : (int?)null;
        if (SelectedEditPrivilegeId == normalized)
        {
            return;
        }

        SelectedEditPrivilegeId = normalized;
        NotifyStateChanged();
    }

    public void ClearSelectedEditPrivilegeId()
    {
        if (!SelectedEditPrivilegeId.HasValue)
        {
            return;
        }

        SelectedEditPrivilegeId = null;
        NotifyStateChanged();
    }

    public async Task<PrivilegeOperationResult<PrivilegeAdminItemDto>> GetByIdAsync(int id)
    {
        if (id <= 0)
        {
            return new PrivilegeOperationResult<PrivilegeAdminItemDto>(
                false,
                null,
                (int)HttpStatusCode.BadRequest,
                "El identificador del privilegio no es válido.");
        }

        try
        {
            using var request = await CreateAuthenticatedRequestAsync(HttpMethod.Get, $"api/privileges/{id}");
            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await ExtractErrorMessageAsync(
                    response,
                    "No fue posible consultar el detalle del privilegio.");

                await _logger.LogWarningAsync(
                    $"Consulta a api/privileges/{id} rechazada con código {(int)response.StatusCode}.",
                    nameof(AdmPrivStateService));

                return new PrivilegeOperationResult<PrivilegeAdminItemDto>(
                    false,
                    null,
                    (int)response.StatusCode,
                    errorMessage);
            }

            var data = await response.Content.ReadFromJsonAsync<PrivilegeAdminItemDto>();
            if (data is null)
            {
                return new PrivilegeOperationResult<PrivilegeAdminItemDto>(
                    false,
                    null,
                    (int)HttpStatusCode.InternalServerError,
                    "Respuesta vacía del servidor al consultar el privilegio.");
            }

            return new PrivilegeOperationResult<PrivilegeAdminItemDto>(
                true,
                data,
                (int)response.StatusCode,
                null);
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync(
                $"Error inesperado al consultar api/privileges/{id}.",
                ex,
                nameof(AdmPrivStateService));

            return new PrivilegeOperationResult<PrivilegeAdminItemDto>(
                false,
                null,
                (int)HttpStatusCode.InternalServerError,
                "Ocurrió un error al consultar el detalle del privilegio.");
        }
    }

    public async Task<PrivilegeOperationResult<CreatePrivilegeResponseDto>> CreateAsync(CreatePrivilegeRequestDto requestDto)
    {
        if (requestDto is null)
        {
            return new PrivilegeOperationResult<CreatePrivilegeResponseDto>(
                false,
                null,
                (int)HttpStatusCode.BadRequest,
                "Los datos del privilegio son obligatorios.");
        }

        try
        {
            using var request = await CreateAuthenticatedRequestAsync(HttpMethod.Post, "api/privileges", requestDto);
            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await ExtractErrorMessageAsync(
                    response,
                    "No fue posible crear el privilegio. Verifique los datos e intente nuevamente.");

                await _logger.LogWarningAsync(
                    $"Solicitud POST api/privileges rechazada con código {(int)response.StatusCode}.",
                    nameof(AdmPrivStateService));

                return new PrivilegeOperationResult<CreatePrivilegeResponseDto>(
                    false,
                    null,
                    (int)response.StatusCode,
                    errorMessage);
            }

            var data = await response.Content.ReadFromJsonAsync<CreatePrivilegeResponseDto>();
            if (data is null)
            {
                return new PrivilegeOperationResult<CreatePrivilegeResponseDto>(
                    false,
                    null,
                    (int)HttpStatusCode.InternalServerError,
                    "Respuesta vacía del servidor al crear el privilegio.");
            }

            if (_sessionState.HasPrivilegeAccess(ModulePrivilegeCodename, PrivilegeAction.Read))
            {
                await LoadAsync();
            }

            return new PrivilegeOperationResult<CreatePrivilegeResponseDto>(
                true,
                data,
                (int)response.StatusCode,
                null);
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync(
                "Error inesperado al crear privilegio en api/privileges.",
                ex,
                nameof(AdmPrivStateService));

            return new PrivilegeOperationResult<CreatePrivilegeResponseDto>(
                false,
                null,
                (int)HttpStatusCode.InternalServerError,
                "Ocurrió un error al crear el privilegio.");
        }
    }

    public async Task<PrivilegeOperationResult<UpdatePrivilegeResponseDto>> UpdateAsync(int id, UpdatePrivilegeRequestDto requestDto)
    {
        if (id <= 0)
        {
            return new PrivilegeOperationResult<UpdatePrivilegeResponseDto>(
                false,
                null,
                (int)HttpStatusCode.BadRequest,
                "El identificador del privilegio no es válido.");
        }

        if (requestDto is null)
        {
            return new PrivilegeOperationResult<UpdatePrivilegeResponseDto>(
                false,
                null,
                (int)HttpStatusCode.BadRequest,
                "Los datos del privilegio son obligatorios.");
        }

        try
        {
            using var request = await CreateAuthenticatedRequestAsync(HttpMethod.Put, $"api/privileges/{id}", requestDto);
            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await ExtractErrorMessageAsync(
                    response,
                    "No fue posible actualizar el privilegio. Verifique los datos e intente nuevamente.");

                await _logger.LogWarningAsync(
                    $"Solicitud PUT api/privileges/{id} rechazada con código {(int)response.StatusCode}.",
                    nameof(AdmPrivStateService));

                return new PrivilegeOperationResult<UpdatePrivilegeResponseDto>(
                    false,
                    null,
                    (int)response.StatusCode,
                    errorMessage);
            }

            var data = await response.Content.ReadFromJsonAsync<UpdatePrivilegeResponseDto>();
            if (data is null)
            {
                return new PrivilegeOperationResult<UpdatePrivilegeResponseDto>(
                    false,
                    null,
                    (int)HttpStatusCode.InternalServerError,
                    "Respuesta vacía del servidor al actualizar el privilegio.");
            }

            if (_sessionState.HasPrivilegeAccess(ModulePrivilegeCodename, PrivilegeAction.Read))
            {
                await LoadAsync();
            }

            return new PrivilegeOperationResult<UpdatePrivilegeResponseDto>(
                true,
                data,
                (int)response.StatusCode,
                null);
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync(
                $"Error inesperado al actualizar privilegio en api/privileges/{id}.",
                ex,
                nameof(AdmPrivStateService));

            return new PrivilegeOperationResult<UpdatePrivilegeResponseDto>(
                false,
                null,
                (int)HttpStatusCode.InternalServerError,
                "Ocurrió un error al actualizar el privilegio.");
        }
    }

    public async Task<PrivilegeOperationResult<DeactivatePrivilegeResponseDto>> DeactivateAsync(int id, DeactivatePrivilegeRequestDto? requestDto = null)
    {
        if (id <= 0)
        {
            return new PrivilegeOperationResult<DeactivatePrivilegeResponseDto>(
                false,
                null,
                (int)HttpStatusCode.BadRequest,
                "El identificador del privilegio no es válido.");
        }

        try
        {
            using var request = await CreateAuthenticatedRequestAsync(HttpMethod.Delete, $"api/privileges/{id}", requestDto);
            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await ExtractErrorMessageAsync(
                    response,
                    "No fue posible inactivar el privilegio.");

                await _logger.LogWarningAsync(
                    $"Solicitud DELETE api/privileges/{id} rechazada con código {(int)response.StatusCode}.",
                    nameof(AdmPrivStateService));

                return new PrivilegeOperationResult<DeactivatePrivilegeResponseDto>(
                    false,
                    null,
                    (int)response.StatusCode,
                    errorMessage);
            }

            var data = await response.Content.ReadFromJsonAsync<DeactivatePrivilegeResponseDto>();
            if (data is null)
            {
                return new PrivilegeOperationResult<DeactivatePrivilegeResponseDto>(
                    false,
                    null,
                    (int)HttpStatusCode.InternalServerError,
                    "Respuesta vacía del servidor al inactivar el privilegio.");
            }

            if (_sessionState.HasPrivilegeAccess(ModulePrivilegeCodename, PrivilegeAction.Read))
            {
                await LoadAsync();
            }

            return new PrivilegeOperationResult<DeactivatePrivilegeResponseDto>(
                true,
                data,
                (int)response.StatusCode,
                null);
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync(
                $"Error inesperado al inactivar privilegio en api/privileges/{id}.",
                ex,
                nameof(AdmPrivStateService));

            return new PrivilegeOperationResult<DeactivatePrivilegeResponseDto>(
                false,
                null,
                (int)HttpStatusCode.InternalServerError,
                "Ocurrió un error al inactivar el privilegio.");
        }
    }

    public async Task<PrivilegeOperationResult<ReactivatePrivilegeResponseDto>> ReactivateAsync(int id)
    {
        SuccessMessage = null;

        if (id <= 0)
        {
            ErrorMessage = "El identificador del privilegio no es válido.";
            NotifyStateChanged();
            return new PrivilegeOperationResult<ReactivatePrivilegeResponseDto>(
                false,
                null,
                (int)HttpStatusCode.BadRequest,
                ErrorMessage);
        }

        try
        {
            using var request = await CreateAuthenticatedRequestAsync(HttpMethod.Post, $"api/privileges/{id}/reactivate");
            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await ExtractErrorMessageAsync(
                    response,
                    "No fue posible reactivar el privilegio.");

                await _logger.LogWarningAsync(
                    $"Solicitud POST api/privileges/{id}/reactivate rechazada con código {(int)response.StatusCode}.",
                    nameof(AdmPrivStateService));

                ErrorMessage = errorMessage;
                SuccessMessage = null;
                NotifyStateChanged();

                return new PrivilegeOperationResult<ReactivatePrivilegeResponseDto>(
                    false,
                    null,
                    (int)response.StatusCode,
                    errorMessage);
            }

            var data = await response.Content.ReadFromJsonAsync<ReactivatePrivilegeResponseDto>();
            if (data is null)
            {
                ErrorMessage = "Respuesta vacía del servidor al reactivar el privilegio.";
                SuccessMessage = null;
                NotifyStateChanged();

                return new PrivilegeOperationResult<ReactivatePrivilegeResponseDto>(
                    false,
                    null,
                    (int)HttpStatusCode.InternalServerError,
                    ErrorMessage);
            }

            if (_sessionState.HasPrivilegeAccess(ModulePrivilegeCodename, PrivilegeAction.Read))
            {
                await LoadAsync();
            }

            if (string.IsNullOrWhiteSpace(ErrorMessage))
            {
                SuccessMessage = "Privilegio reactivado exitosamente.";
                NotifyStateChanged();
            }

            return new PrivilegeOperationResult<ReactivatePrivilegeResponseDto>(
                true,
                data,
                (int)response.StatusCode,
                null);
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync(
                $"Error inesperado al reactivar privilegio en api/privileges/{id}/reactivate.",
                ex,
                nameof(AdmPrivStateService));

            ErrorMessage = "Ocurrió un error al reactivar el privilegio.";
            SuccessMessage = null;
            NotifyStateChanged();

            return new PrivilegeOperationResult<ReactivatePrivilegeResponseDto>(
                false,
                null,
                (int)HttpStatusCode.InternalServerError,
                ErrorMessage);
        }
    }

    public void ClearMessages()
    {
        if (ErrorMessage is null && SuccessMessage is null)
        {
            return;
        }

        ErrorMessage = null;
        SuccessMessage = null;
        NotifyStateChanged();
    }

    public void SetErrorMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ErrorMessage = message.Trim();
        SuccessMessage = null;
        NotifyStateChanged();
    }

    public void ClearCatalog()
    {
        _allItems = new List<PrivilegeAdminItemDto>();
        _filteredItems = new List<PrivilegeAdminItemDto>();
        _pagedItems = new List<PrivilegeAdminItemDto>();
        CurrentPage = 1;
        IsLoading = false;
        NotifyStateChanged();
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

    private async Task<HttpRequestMessage> CreateAuthenticatedRequestAsync(
        HttpMethod method,
        string relativeUri,
        object? payload = null)
    {
        var token = await _sessionStorage.GetItemDecryptedAsync(AuthenticationService.BackendTokenKey);
        var request = new HttpRequestMessage(method, relativeUri);

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        return request;
    }

    private static async Task<string> ExtractErrorMessageAsync(HttpResponseMessage response, string defaultMessage)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return "Su sesión ha expirado o no es válida. Por favor inicie sesión nuevamente.";
        }

        try
        {
            var errorDto = await response.Content.ReadFromJsonAsync<BackendErrorDto>();
            if (!string.IsNullOrWhiteSpace(errorDto?.Message))
            {
                return errorDto.Message;
            }
        }
        catch
        {
            // Ignorar errores de deserialización y retornar el mensaje genérico sanitizado.
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return "No cuenta con permisos suficientes para realizar esta operación.";
        }

        return defaultMessage;
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
