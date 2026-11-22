using Microsoft.AspNetCore.Components;

namespace Vogel.Client.Components;

/// <summary>
/// Code-behind del componente ReCaptcha.
/// Permite simular y gestionar la verificación reCAPTCHA en modos: Deshabilitado, Siempre exigido, Exigido por condición.
/// Corresponde a la sección 2.2.2 del Requerimiento Maestro.
/// </summary>
public class ReCaptchaBase : ComponentBase
{
    /// <summary>
    /// Modo de operación del reCAPTCHA: "Disabled", "Always", "Conditional".
    /// </summary>
    [Parameter]
    public string Mode { get; set; } = "Disabled";

    /// <summary>
    /// Clave de sitio para integración real externa (Fase 3).
    /// </summary>
    [Parameter]
    public string SiteKey { get; set; } = string.Empty;

    /// <summary>
    /// Si el modo es "Conditional", indica si la condición de activación se cumple (ej. tras N intentos fallidos).
    /// </summary>
    [Parameter]
    public bool ConditionMet { get; set; } = false;

    /// <summary>
    /// Callback que notifica si la verificación ha sido completada exitosamente.
    /// </summary>
    [Parameter]
    public EventCallback<bool> OnVerifiedChanged { get; set; }

    /// <summary>
    /// Indica si el reCAPTCHA está verificado actualmente.
    /// </summary>
    public bool IsVerified { get; protected set; }

    /// <summary>
    /// Determina si el widget de reCAPTCHA debe mostrarse en la interfaz.
    /// </summary>
    public bool IsVisible => Mode.Equals("Always", StringComparison.OrdinalIgnoreCase) ||
                            (Mode.Equals("Conditional", StringComparison.OrdinalIgnoreCase) && ConditionMet);

    /// <summary>
    /// Conmuta la verificación del usuario al hacer clic en el widget.
    /// </summary>
    protected async Task HandleVerificationToggle()
    {
        IsVerified = !IsVerified;
        await OnVerifiedChanged.InvokeAsync(IsVerified);
    }

    /// <summary>
    /// Reinicia el estado de verificación a no verificado.
    /// </summary>
    public void Reset()
    {
        IsVerified = false;
        StateHasChanged();
    }
}
