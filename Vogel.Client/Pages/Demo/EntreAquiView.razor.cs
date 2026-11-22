using Microsoft.AspNetCore.Components;
using Vogel.Client.Services;

namespace Vogel.Client.Pages;

/// <summary>
/// Code-behind de la vista de demostración de la subopción "Entre Aquí" (demo/entre-aqui).
/// Valida el flujo de salida de MainView y el regreso seguro a la vista principal.
/// Corresponde a la Fase 2 del Requerimiento Maestro.
/// </summary>
public class EntreAquiViewBase : ComponentBase
{
    [Inject] protected SessionState SessionState { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;

    protected override void OnInitialized()
    {
        if (!SessionState.IsAuthenticated)
        {
            NavigationManager.NavigateTo("/login", replace: true);
        }
    }
}
