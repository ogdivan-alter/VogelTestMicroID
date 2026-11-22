using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Vogel.Client.Components;

/// <summary>
/// Code-behind del componente Login.
/// Incluye la captura de credenciales (username, password), soporte para recordar sesión,
/// reCAPTCHA y botones de Ingresar y Cancelar (configurable).
/// Corresponde a la sección 2.3.1 del Requerimiento Maestro.
/// </summary>
public class LoginBase : ComponentBase
{
    /// <summary>
    /// Modelo de datos del formulario de autenticación.
    /// </summary>
    protected LoginCredentials FormModel { get; set; } = new();

    /// <summary>
    /// Referencia al componente de reCAPTCHA incrustado.
    /// </summary>
    protected ReCaptcha? ReCaptchaComponent { get; set; }

    /// <summary>
    /// Indica si el reCAPTCHA ha sido verificado.
    /// </summary>
    protected bool IsRecaptchaVerified { get; set; }

    /// <summary>
    /// Indica si se está ejecutando la acción de autenticación.
    /// </summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>
    /// Mensaje de error visual para desplegar en caso de fallo.
    /// </summary>
    [Parameter]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Determina si se visualiza el botón de Cancelar (según sección 2.3.1).
    /// </summary>
    [Parameter]
    public bool ShowCancelButton { get; set; } = false;

    /// <summary>
    /// Determina si se visualiza el botón de limpiar campos.
    /// </summary>
    [Parameter]
    public bool ShowClearButton { get; set; } = true;

    /// <summary>
    /// Determina si se visualiza la opción de recordar sesión.
    /// </summary>
    [Parameter]
    public bool ShowRememberMe { get; set; } = true;

    /// <summary>
    /// Texto de la etiqueta de recordar sesión.
    /// </summary>
    [Parameter]
    public string RememberMeText { get; set; } = "Recordar Sesión";

    /// <summary>
    /// Modo de reCAPTCHA: "Disabled", "Always", "Conditional".
    /// </summary>
    [Parameter]
    public string RecaptchaMode { get; set; } = "Disabled";

    /// <summary>
    /// Clave de sitio para reCAPTCHA.
    /// </summary>
    [Parameter]
    public string RecaptchaSiteKey { get; set; } = string.Empty;

    /// <summary>
    /// Indica si se cumplió la condición para mostrar el reCAPTCHA en modo condicional.
    /// </summary>
    [Parameter]
    public bool RecaptchaConditionMet { get; set; } = false;

    /// <summary>
    /// Evento disparado al enviar válidamente las credenciales.
    /// </summary>
    [Parameter]
    public EventCallback<LoginCredentials> OnSubmit { get; set; }

    /// <summary>
    /// Evento disparado al pulsar el botón de Cancelar.
    /// </summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    /// <summary>
    /// Callback al cambiar el estado de verificación del reCAPTCHA.
    /// </summary>
    protected void HandleRecaptchaVerifiedChanged(bool verified)
    {
        IsRecaptchaVerified = verified;
    }

    /// <summary>
    /// Ejecuta el callback OnSubmit cuando el formulario es válido.
    /// </summary>
    protected async Task HandleSubmitAsync()
    {
        if (IsLoading) return;
        await OnSubmit.InvokeAsync(FormModel);
    }

    /// <summary>
    /// Ejecuta el callback OnCancel.
    /// </summary>
    protected async Task HandleCancelAsync()
    {
        ClearForm();
        await OnCancel.InvokeAsync();
    }

    /// <summary>
    /// Referencia al campo de texto del usuario para asignación de foco.
    /// </summary>
    protected InputText? UsernameInput { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && UsernameInput?.Element.HasValue == true)
        {
            try
            {
                await UsernameInput.Element.Value.FocusAsync();
            }
            catch
            {
                // El navegador gestiona el foco nativo en caso de no disponibilidad
            }
        }
    }

    /// <summary>
    /// Limpia los campos del formulario y reasigna el foco al campo de usuario.
    /// </summary>
    public void ClearForm()
    {
        FormModel.Username = string.Empty;
        FormModel.Password = string.Empty;
        FormModel.RememberMe = false;
        ReCaptchaComponent?.Reset();
        IsRecaptchaVerified = false;
        StateHasChanged();

        if (UsernameInput?.Element.HasValue == true)
        {
            _ = UsernameInput.Element.Value.FocusAsync().AsTask();
        }
    }

    /// <summary>
    /// Clase para encapsular las credenciales ingresadas.
    /// </summary>
    public class LoginCredentials
    {
        [Required(ErrorMessage = "El usuario es obligatorio.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "El usuario debe tener entre 3 y 50 caracteres.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [StringLength(100, MinimumLength = 4, ErrorMessage = "La contraseña debe tener al menos 4 caracteres.")]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
