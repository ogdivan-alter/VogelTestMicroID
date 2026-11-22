using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Vogel.Client.Components;
using Vogel.Client.Services;

namespace Vogel.Client.Pages;

/// <summary>
/// Code-behind de la vista de autenticación (LoginView).
/// Hospeda el componente Login, lee la configuración de RememberMe y reCAPTCHA,
/// gestiona el flujo de autenticación y la redirección post-login.
/// Corresponde a la sección 2.2.2 del Requerimiento Maestro.
/// </summary>
public class LoginViewBase : ComponentBase
{
    // ——— Servicios inyectados ———
    [Inject] protected IAuthenticationService AuthService      { get; set; } = default!;
    [Inject] protected SessionState           SessionState     { get; set; } = default!;
    [Inject] protected NavigationManager      NavigationManager { get; set; } = default!;
    [Inject] protected IClientLogger          Logger           { get; set; } = default!;
    [Inject] protected IConfiguration         Configuration    { get; set; } = default!;

    // ——— Referencias y estado de la vista ———
    protected Login? LoginComponent { get; set; }

    /// <summary>Indica si se está ejecutando la solicitud de autenticación.</summary>
    protected bool IsLoading { get; private set; }

    /// <summary>Mensaje de error global a mostrar en la vista.</summary>
    protected string? ErrorMessage { get; private set; }

    /// <summary>Configuración de Recordar Sesión obtenida de appsettings.</summary>
    protected bool ShowRememberMe { get; private set; } = true;
    protected string RememberMeText { get; private set; } = "Recordar Sesión";

    /// <summary>Configuración de reCAPTCHA obtenida de appsettings.</summary>
    protected string RecaptchaMode { get; private set; } = "Disabled";
    protected string RecaptchaSiteKey { get; private set; } = string.Empty;
    protected bool RecaptchaConditionMet { get; private set; } = false;

    /// <summary>Contador de intentos fallidos para reCAPTCHA condicional.</summary>
    private int _failedAttempts = 0;

    // ——————————————————————————————————————————
    // Ciclo de vida
    // ——————————————————————————————————————————

    protected override void OnInitialized()
    {
        // Si el usuario ya está autenticado, redirigir directamente al home
        if (SessionState.IsAuthenticated)
        {
            NavigationManager.NavigateTo("/", replace: true);
            return;
        }

        // Leer configuraciones desde appsettings.json
        var rememberSection = Configuration.GetSection("RememberMe");
        if (rememberSection.Exists())
        {
            ShowRememberMe = rememberSection.GetValue<bool>("Enabled", true);
            RememberMeText = rememberSection.GetValue<string>("DisplayText", "Recordar Sesión") ?? "Recordar Sesión";
        }

        var recaptchaSection = Configuration.GetSection("Recaptcha");
        if (recaptchaSection.Exists())
        {
            RecaptchaMode = recaptchaSection.GetValue<string>("Mode", "Disabled") ?? "Disabled";
            RecaptchaSiteKey = recaptchaSection.GetValue<string>("SiteKey", string.Empty) ?? string.Empty;
        }
    }

    // ——————————————————————————————————————————
    // Manejadores de eventos
    // ——————————————————————————————————————————

    /// <summary>
    /// Procesa el evento de envío desde el componente Login.
    /// </summary>
    protected async Task HandleLoginSubmitAsync(LoginBase.LoginCredentials credentials)
    {
        if (IsLoading) return;

        ErrorMessage = null;
        IsLoading = true;
        StateHasChanged();

        try
        {
            var (success, errorMsg) = await AuthService.LoginAsync(
                credentials.Username,
                credentials.Password,
                credentials.RememberMe);

            if (success)
            {
                _failedAttempts = 0;
                RecaptchaConditionMet = false;
                NavigationManager.NavigateTo("/", replace: true);
            }
            else
            {
                _failedAttempts++;
                if (_failedAttempts >= 3)
                {
                    RecaptchaConditionMet = true;
                }
                ErrorMessage = errorMsg;
                credentials.Username = string.Empty;
                credentials.Password = string.Empty;
                LoginComponent?.ClearForm();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Ha ocurrido un error inesperado al iniciar sesión.";
            await Logger.LogErrorAsync("Error inesperado en el submit de login.", ex, nameof(LoginViewBase));
            credentials.Username = string.Empty;
            credentials.Password = string.Empty;
            LoginComponent?.ClearForm();
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Manejador en caso de cancelación del formulario de login.
    /// </summary>
    protected void HandleCancel()
    {
        ErrorMessage = null;
        StateHasChanged();
    }
}
