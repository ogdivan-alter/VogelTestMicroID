using Microsoft.AspNetCore.Components;

namespace Vogel.Client.Components;

/// <summary>
/// Code-behind del componente DefaultFooter.
/// Despliega la información global del sistema y la versión actual de la aplicación.
/// Corresponde a la sección 2.2.1 del Requerimiento Maestro.
/// </summary>
public class DefaultFooterBase : ComponentBase
{
    /// <summary>
    /// Nombre de la empresa o sistema desplegado en el pie de página.
    /// </summary>
    [Parameter]
    public string CompanyName { get; set; } = "Vogel";

    /// <summary>
    /// Título descriptivo de la aplicación.
    /// </summary>
    [Parameter]
    public string ApplicationTitle { get; set; } = "Sistema de Gestión";

    /// <summary>
    /// Versión actual de la aplicación.
    /// </summary>
    [Parameter]
    public string Version { get; set; } = "v1.0.0";

    /// <summary>
    /// Indica si el pie de página se presenta sobre un fondo claro,
    /// adaptando el contraste y color de fuente del badge de versión a negro.
    /// </summary>
    [Parameter]
    public bool IsLightBackground { get; set; } = false;

    /// <summary>
    /// Clases CSS adicionales para personalización del contenedor de pie de página.
    /// </summary>
    [Parameter]
    public string? AdditionalCssClass { get; set; }
}
