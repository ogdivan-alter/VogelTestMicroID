using Vogel.Shared.DTOs;

namespace Vogel.Client.Services;

/// <summary>
/// Servicio de estado compartido (Scoped) para coordinar las vistas dinámicas
/// de Administración de Privilegios (AdmPrivInfo, AdmPrivCapture, AdmPrivFilter y AdmPrivList).
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
    /// Mensaje de error en caso de falla al consultar el catálogo de privilegios.
    /// </summary>
    string? ErrorMessage { get; }

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
    /// Evento disparado cada vez que cambia el estado, los datos o los filtros.
    /// </summary>
    event Action? OnStateChanged;

    /// <summary>
    /// Consulta el catálogo administrativo de privilegios al BFF (GET api/privileges).
    /// </summary>
    Task LoadAsync();

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
