using Microsoft.AspNetCore.Components;

namespace Vogel.Client.Components;

/// <summary>
/// Code-behind del componente genérico ListViewFrame.
/// Soporta paginación conectada, navegación rápida («, ‹, números, ›, »),
/// renderizado de encabezado y filas dinámicas y estado de carga.
/// Corresponde a la sección 2.3.11 del Requerimiento Maestro.
/// </summary>
public partial class ListViewFrame<TItem> : ComponentBase
{
    /// <summary>
    /// Cantidad máxima de registros por página (parámetro PageSize). Por defecto: 10.
    /// </summary>
    [Parameter]
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Número de página actual activa (1-based). Por defecto: 1.
    /// </summary>
    [Parameter]
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Total general de registros disponibles en el origen de datos.
    /// </summary>
    [Parameter]
    public int TotalCount { get; set; } = 0;

    /// <summary>
    /// Colección de elementos a visualizar en la página actual.
    /// </summary>
    [Parameter]
    public IReadOnlyList<TItem>? Data { get; set; }

    /// <summary>
    /// Método / EventCallback invocado al cambiar de página (parámetro PageMethod).
    /// </summary>
    [Parameter]
    public EventCallback<int> PageMethod { get; set; }

    /// <summary>
    /// Plantilla para renderizar las cabeceras de columnas (th).
    /// </summary>
    [Parameter]
    public RenderFragment? HeaderContent { get; set; }

    /// <summary>
    /// Plantilla para renderizar cada fila (td) correspondiente al elemento actual.
    /// </summary>
    [Parameter]
    public RenderFragment<TItem>? RowTemplate { get; set; }

    /// <summary>
    /// Contenido alternativo / anidado libre.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Indica si el componente se encuentra en estado de carga asíncrona.
    /// </summary>
    [Parameter]
    public bool IsLoading { get; set; } = false;

    /// <summary>
    /// Cantidad máxima de botones numéricos de página visibles (por defecto 10 según requerimiento).
    /// </summary>
    [Parameter]
    public int MaxVisiblePages { get; set; } = 10;

    /// <summary>
    /// Clases CSS personalizadas para el contenedor de la tabla.
    /// </summary>
    [Parameter]
    public string CustomClass { get; set; } = string.Empty;

    /// <summary>
    /// Cantidad total de páginas calculadas a partir del TotalCount y PageSize.
    /// </summary>
    public int TotalPages => PageSize > 0 ? Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize)) : 1;

    /// <summary>
    /// Índice del primer registro visualizado en la página actual.
    /// </summary>
    public int StartRecordIndex => TotalCount == 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;

    /// <summary>
    /// Índice del último registro visualizado en la página actual.
    /// </summary>
    public int EndRecordIndex => Math.Min(CurrentPage * PageSize, TotalCount);

    /// <summary>
    /// Notifica el cambio de página seleccionada hacia el consumidor externo.
    /// </summary>
    public async Task ChangePageAsync(int targetPage)
    {
        if (IsLoading) return;

        if (targetPage < 1) targetPage = 1;
        if (targetPage > TotalPages) targetPage = TotalPages;

        if (targetPage == CurrentPage) return;

        CurrentPage = targetPage;

        if (PageMethod.HasDelegate)
        {
            await PageMethod.InvokeAsync(targetPage);
        }
    }

    /// <summary>
    /// Calcula la lista de números de página visibles según el límite máximo permitido.
    /// </summary>
    public IEnumerable<int> GetVisiblePages()
    {
        if (TotalPages <= MaxVisiblePages)
        {
            return Enumerable.Range(1, TotalPages);
        }

        int half = MaxVisiblePages / 2;
        int start = Math.Max(1, CurrentPage - half);
        int end = Math.Min(TotalPages, start + MaxVisiblePages - 1);

        if (end - start + 1 < MaxVisiblePages)
        {
            start = Math.Max(1, end - MaxVisiblePages + 1);
        }

        return Enumerable.Range(start, end - start + 1);
    }
}
