using Microsoft.AspNetCore.Components;
using Vogel.Shared.Models;

namespace Vogel.Client.Pages.TreeWork.AdmTools.AdmSecurity.AdmPriv;

/// <summary>
/// Code-behind de la vista Capture de Administración de Privilegios.
/// Expone el botón "Crear privilegio" cuya acción de formulario queda pendiente para la siguiente etapa.
/// </summary>
public partial class AdmPrivCapture : ComponentBase
{
    [Parameter]
    public BlockWork? BlockWork { get; set; }

    /// <summary>
    /// Handler reservado para abrir el formulario de creación de privilegio en la siguiente fase.
    /// </summary>
    protected void HandleCreatePrivilegeClick()
    {
        // Pendiente de implementación del formulario de creación de privilegio.
    }
}
