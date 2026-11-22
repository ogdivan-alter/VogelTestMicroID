using Microsoft.AspNetCore.Components;
using Vogel.Shared.Models;

namespace Vogel.Client.Components;

/// <summary>
/// Code-behind de OpSecundariaInfoFrame.
/// Vista referenciada en BlockWork.ViewSet.Info para la entidad 'op-secundaria'.
/// Cumple con la sección 2.3.9, 2.3.10 y 2.4.3 del Requerimiento Maestro.
/// </summary>
public partial class OpSecundariaInfoFrame : ComponentBase
{
    /// <summary>
    /// Instancia de BlockWork inyectada opcionalmente por WorkViewFrame al resolver la vista dinámica.
    /// </summary>
    [Parameter]
    public BlockWork? BlockWork { get; set; }
}
