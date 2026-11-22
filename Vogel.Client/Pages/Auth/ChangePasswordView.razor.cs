using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Vogel.Client.Services;

namespace Vogel.Client.Pages;

/// <summary>
/// Code-behind de la vista de cambio de contraseña.
/// Disponible para usuarios autenticados que tengan permitido el cambio de contraseña (allow_password_change = true).
/// Corresponde a la sección 2.2.1 del Requerimiento Maestro.
/// </summary>
public class ChangePasswordViewBase : ComponentBase
{
    [Inject] protected SessionState SessionState { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
    [Inject] protected IAuthenticationService AuthenticationService { get; set; } = default!;
    [Inject] protected IClientLogger Logger { get; set; } = default!;

    protected ChangePasswordModel Model { get; set; } = new();
    protected InputText? CurrentPasswordInput { get; set; }
    protected bool IsLoading { get; set; }
    protected string? ErrorMessage { get; set; }
    protected string? SuccessMessage { get; set; }

    protected override void OnInitialized()
    {
        if (!SessionState.IsAuthenticated)
        {
            NavigationManager.NavigateTo("/login", replace: true);
            return;
        }

        if (SessionState.CurrentUser != null && !SessionState.CurrentUser.AllowPasswordChange)
        {
            // Usuario no tiene permitido cambiar contraseña (ej. externo)
            NavigationManager.NavigateTo("/", replace: true);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && CurrentPasswordInput?.Element.HasValue == true)
        {
            try
            {
                await CurrentPasswordInput.Element.Value.FocusAsync();
            }
            catch
            {
                // Manejo de foco nativo
            }
        }
    }

    protected async Task HandleChangePasswordAsync()
    {
        if (IsLoading) return;

        ErrorMessage = null;
        SuccessMessage = null;

        if (Model.NewPassword != Model.ConfirmPassword)
        {
            ErrorMessage = "La confirmación de la contraseña no coincide.";
            return;
        }

        IsLoading = true;
        StateHasChanged();

        try
        {
            var (success, errorMessage) = await AuthenticationService.ChangePasswordAsync(
                Model.CurrentPassword,
                Model.NewPassword);

            if (!success)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
                    ? "No fue posible actualizar la contraseña."
                    : errorMessage;
                return;
            }

            Model = new();
            SuccessMessage = "Contraseña actualizada exitosamente. Redirigiendo a inicio...";
            await Logger.LogInformationAsync($"Contraseña actualizada para usuario '{SessionState.CurrentUser?.Username}'.", nameof(ChangePasswordViewBase));
            StateHasChanged();

            await Task.Delay(1000);
            NavigationManager.NavigateTo("/");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Ocurrió un error al actualizar la contraseña.";
            await Logger.LogErrorAsync("Error al cambiar contraseña.", ex, nameof(ChangePasswordViewBase));
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    public class ChangePasswordModel
    {
        [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "La nueva contraseña debe tener al menos 6 caracteres.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe confirmar la nueva contraseña.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
