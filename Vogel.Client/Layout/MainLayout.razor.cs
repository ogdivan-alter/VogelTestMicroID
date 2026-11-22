using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Vogel.Client.Services;

namespace Vogel.Client.Layout;

/// <summary>
/// Code-behind de MainLayout. Administra el estado de sesión compartido,
/// reaccionando a cambios de autenticación, menú burger responsive y ejecutando el cierre de sesión.
/// Corresponde a la sección 2.2.1 del Requerimiento Maestro.
/// </summary>
public class MainLayoutBase : LayoutComponentBase, IDisposable
{
    // ——— Servicios inyectados ———
    [Inject] protected SessionState SessionState { get; set; } = default!;
    [Inject] protected IAuthenticationService AuthService { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
    [Inject] protected IClientLogger Logger { get; set; } = default!;
    [Inject] protected IConfiguration Configuration { get; set; } = default!;

    // ——— Estado interno ———

    /// <summary>Indica si se está ejecutando el proceso de logout (muestra overlay y spinner).</summary>
    protected bool IsLoggingOut { get; private set; }

    /// <summary>Indica si el menú desplegable móvil (burger) está abierto.</summary>
    protected bool IsMobileMenuOpen { get; private set; }

    /// <summary>Tiempo en minutos para inactividad leído desde configuración.</summary>
    protected int InactivityTimeoutMinutes { get; private set; } = 15;

    // ——————————————————————————————————————————
    // Ciclo de vida del componente
    // ——————————————————————————————————————————

    protected override void OnInitialized()
    {
        // Suscribirse a cambios del estado de sesión para re-renderizar el header
        SessionState.OnChange += HandleSessionStateChange;

        // Leer tiempo de inactividad de configuración
        InactivityTimeoutMinutes = Configuration.GetValue<int>("InactivityTimeoutMinutes", 15);
    }

    // ——————————————————————————————————————————
    // Métodos de apoyo
    // ——————————————————————————————————————————

    /// <summary>
    /// Conmuta la visibilidad del menú de usuario en modo responsive / móvil.
    /// </summary>
    protected void ToggleMobileMenu()
    {
        IsMobileMenuOpen = !IsMobileMenuOpen;
    }

    /// <summary>
    /// Obtiene las iniciales del nombre completo del usuario para el avatar.
    /// Máximo 2 letras en mayúscula.
    /// </summary>
    protected string GetUserInitials()
    {
        if (SessionState.CurrentUser == null) return "?";
        var parts = (SessionState.CurrentUser.Name ?? SessionState.CurrentUser.Username ?? "?")
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            : parts[0][0].ToString().ToUpperInvariant();
    }

    // ——————————————————————————————————————————
    // Manejadores de eventos
    // ——————————————————————————————————————————

    /// <summary>
    /// Ejecuta el proceso de cierre de sesión con feedback visual al usuario.
    /// </summary>
    protected async Task HandleLogoutAsync()
    {
        if (IsLoggingOut) return;

        IsLoggingOut = true;
        IsMobileMenuOpen = false;
        StateHasChanged();

        try
        {
            await AuthService.LogoutAsync();
        }
        catch (Exception ex)
        {
            await Logger.LogErrorAsync("Error inesperado durante el logout desde el layout.", ex, nameof(MainLayoutBase));
        }
        finally
        {
            IsLoggingOut = false;
            // Navegar a login después del logout
            NavigationManager.NavigateTo("/login", forceLoad: false);
        }
    }

    /// <summary>
    /// Invocado por el estado de sesión cuando cambia la autenticación.
    /// Re-renderiza el componente desde el hilo de UI.
    /// </summary>
    private void HandleSessionStateChange()
    {
        InvokeAsync(StateHasChanged);
    }

    // ——————————————————————————————————————————
    // IDisposable
    // ——————————————————————————————————————————

    public void Dispose()
    {
        SessionState.OnChange -= HandleSessionStateChange;
        GC.SuppressFinalize(this);
    }
}
