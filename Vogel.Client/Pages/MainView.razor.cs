using Microsoft.AspNetCore.Components;
using Vogel.Client.Services;

namespace Vogel.Client.Pages;

/// <summary>
/// Code-behind de la Vista Principal (MainView).
/// Integra horizontalmente el MainMenu y el MainContent (apilándose en responsive sm).
/// MainMenu pertenece exclusivamente a esta vista y no se visualiza en vistas secundarias ni de trabajo.
/// Corresponde a la sección 2.2.3 del Requerimiento Maestro.
/// </summary>
public class MainViewBase : ComponentBase
{
    [Inject] protected SessionState SessionState { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;

    protected override void OnInitialized()
    {
        // Si no está autenticado, el AuthGuard y esta verificación garantizan el desvío a login
        if (!SessionState.IsAuthenticated)
        {
            NavigationManager.NavigateTo("/login", replace: true);
        }
    }
}
