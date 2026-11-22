using Microsoft.AspNetCore.Components;
using Vogel.Client.Services;
using Vogel.Client.Pages;

namespace Vogel.Client.Components;

/// <summary>
/// Code-behind para AuthGuard. Controla el acceso a páginas según el estado de autenticación
/// y valida la vigencia de la sesión al ingresar a opciones controladas por privilegios.
/// </summary>
public class AuthGuardBase : ComponentBase
{
    [Inject] protected SessionState SessionState { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
    [Inject] protected ISessionValidationService SessionValidationService { get; set; } = default!;
    [Inject] protected ISecureSessionStorageService SessionStorage { get; set; } = default!;

    /// <summary>
    /// Ruta activa que se va a renderizar.
    /// </summary>
    [Parameter]
    public RouteData RouteData { get; set; } = default!;

    /// <summary>
    /// Contenido a renderizar cuando el usuario está autenticado.
    /// </summary>
    [Parameter]
    public RenderFragment ChildContent { get; set; } = default!;

    // Páginas públicas que no requieren autenticación
    private static readonly HashSet<Type> _publicPages = new()
    {
        typeof(LoginView)
    };

    /// <summary>
    /// Indica que la validación/verificación terminó y el contenido puede mostrarse.
    /// Evita desplegar brevemente una vista protegida antes de redirigir al login.
    /// </summary>
    protected bool _ready;

    private bool _checking;

    protected override async Task OnParametersSetAsync()
    {
        if (_checking) return;
        _checking = true;

        try
        {
            var isPublic = _publicPages.Contains(RouteData.PageType);

            if (isPublic)
            {
                if (SessionState.IsAuthenticated)
                {
                    NavigationManager.NavigateTo("/", replace: true);
                    return;
                }

                _ready = true;
                return;
            }

            // Validar vigencia de la sesión antes de desplegar cualquier opción protegida.
            var sessionValid = await SessionValidationService.EnsureValidAsync();
            if (!sessionValid)
            {
                try { await SessionStorage.ClearAsync(); } catch { }
                SessionState.ClearUser();
                _ready = false;
                NavigationManager.NavigateTo("/login", replace: true);
                return;
            }

            if (!SessionState.IsAuthenticated)
            {
                _ready = false;
                NavigationManager.NavigateTo("/login", replace: true);
                return;
            }

            _ready = true;
        }
        finally
        {
            _checking = false;
        }
    }
}
