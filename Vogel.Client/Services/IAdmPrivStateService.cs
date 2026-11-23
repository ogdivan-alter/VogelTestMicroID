using Vogel.Shared.DTOs;

namespace Vogel.Client.Services;

/// <summary>
/// Resultado tipado de operaciones individuales (lectura, creación, edición e inactivación)
/// contra los endpoints BFF de administración de privilegios.
/// </summary>
public sealed record PrivilegeOperationResult<T>(
    bool Success,
    T? Data,
    int StatusCode,
    string? ErrorMessage);

/// <summary>
/// Servicio de estado compartido (Scoped) para coordinar las vistas dinámicas
/// de Administración de Privilegios (AdmPrivInfo, AdmPrivCapture, AdmPrivFilter, AdmPrivList y AdmPrivForm).
/// </summary>
public interface IAdmPrivStateService
{
    /// <summary>
    /// Código del sistema al que pertenecen los privilegios cargados (p. ej. "MICROID").
    /// </summary>
    string SystemCode { get; }

    /// <summary>
    /// Identificador numérico del sistema al que pertenecen los privilegios.
    /// </summary>
    int IdSystem { get; }

    /// <summary>
    /// Indica si existe una consulta en curso contra el BFF.
    /// </summary>
    bool IsLoading { get; }

    /// <summary>
    /// Mensaje de error en caso de falla al consultar o operar sobre el catálogo de privilegios.
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// Mensaje de confirmación tras completar una operación exitosa en la vista de lista (p. ej. reactivación).
    /// </summary>
    string? SuccessMessage { get; }

    /// <summary>
    /// Filtro de estado activo: "A" (Activos, por defecto), "I" (Inactivos) o "ALL" (Todos).
    /// </summary>
    string StatusFilter { get; }

    /// <summary>
    /// Filtro de texto por título (búsqueda ignorecase).
    /// </summary>
    string TitleFilter { get; }

    /// <summary>
    /// Filtro de texto numérico por id_parent.
    /// </summary>
    string IdParentFilter { get; }

    /// <summary>
    /// Catálogo completo de privilegios recibido del BFF.
    /// </summary>
    IReadOnlyList<PrivilegeAdminItemDto> AllItems { get; }

    /// <summary>
    /// Lista de privilegios resultante tras aplicar todos los filtros activos.
    /// </summary>
    IReadOnlyList<PrivilegeAdminItemDto> FilteredItems { get; }

    /// <summary>
    /// Página actual (1-based) de la vista de listado.
    /// </summary>
    int CurrentPage { get; }

    /// <summary>
    /// Cantidad de registros por página.
    /// </summary>
    int PageSize { get; }

    /// <summary>
    /// Subconjunto paginado correspondiente a la página actual.
    /// </summary>
    IReadOnlyList<PrivilegeAdminItemDto> PagedItems { get; }

    /// <summary>
    /// Cantidad total de registros tras aplicar los filtros activos.
    /// </summary>
    int TotalFilteredCount { get; }

    /// <summary>
    /// Identificador del privilegio seleccionado en memoria para la vista de edición (/admsec/priv/edit).
    /// Se mantiene únicamente en memoria para evitar exponer IDs en la URL.
    /// </summary>
    int? SelectedEditPrivilegeId { get; }

    /// <summary>
    /// Evento disparado cada vez que cambia el estado, los datos o los filtros.
    /// </summary>
    event Action? OnStateChanged;

    /// <summary>
    /// Consulta el catálogo administrativo de privilegios al BFF (GET api/privileges).
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// Almacena en memoria el identificador del privilegio que se editará en /admsec/priv/edit.
    /// </summary>
    void SetSelectedEditPrivilegeId(int id);

    /// <summary>
    /// Limpia el identificador de privilegio seleccionado para edición.
    /// </summary>
    void ClearSelectedEditPrivilegeId();

    /// <summary>
    /// Obtiene el detalle fresco de un privilegio activo desde el BFF (GET api/privileges/{id}).
    /// </summary>
    Task<PrivilegeOperationResult<PrivilegeAdminItemDto>> GetByIdAsync(int id);

    /// <summary>
    /// Crea un nuevo privilegio en el BFF (POST api/privileges) y refresca el catálogo local si tiene éxito.
    /// </summary>
    Task<PrivilegeOperationResult<CreatePrivilegeResponseDto>> CreateAsync(CreatePrivilegeRequestDto request);

    /// <summary>
    /// Actualiza un privilegio activo en el BFF (PUT api/privileges/{id}) y refresca el catálogo local si tiene éxito.
    /// </summary>
    Task<PrivilegeOperationResult<UpdatePrivilegeResponseDto>> UpdateAsync(int id, UpdatePrivilegeRequestDto request);

    /// <summary>
    /// Inactiva lógicamente un privilegio activo en el BFF (DELETE api/privileges/{id}) y refresca el catálogo local si tiene éxito.
    /// </summary>
    Task<PrivilegeOperationResult<DeactivatePrivilegeResponseDto>> DeactivateAsync(int id, DeactivatePrivilegeRequestDto? request = null);

    /// <summary>
    /// Reactiva lógicamente un privilegio inactivo en el BFF (POST api/privileges/{id}/reactivate) y refresca el catálogo local si tiene éxito.
    /// </summary>
    Task<PrivilegeOperationResult<ReactivatePrivilegeResponseDto>> ReactivateAsync(int id);

    /// <summary>
    /// Limpia los mensajes de éxito y error publicados en el estado compartido.
    /// </summary>
    void ClearMessages();

    /// <summary>
    /// Actualiza el filtro por estado ("A", "I" o "ALL") y reinicia la paginación.
    /// </summary>
    void SetStatusFilter(string statusFilter);

    /// <summary>
    /// Actualiza el filtro por título (ignorecase) y reinicia la paginación.
    /// </summary>
    void SetTitleFilter(string titleFilter);

    /// <summary>
    /// Actualiza el filtro por id_parent y reinicia la paginación.
    /// </summary>
    void SetIdParentFilter(string idParentFilter);

    /// <summary>
    /// Cambia la página actual de la lista filtrada.
    /// </summary>
    void SetPage(int page);

    /// <summary>
    /// Restablece los filtros a sus valores por defecto (Activos, sin título ni id_parent).
    /// </summary>
    void ResetFilters();
}
