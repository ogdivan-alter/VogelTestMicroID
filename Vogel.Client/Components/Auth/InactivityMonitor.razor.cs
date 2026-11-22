using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Vogel.Client.Services;

namespace Vogel.Client.Components;

/// <summary>
/// Componente Code-Behind que monitorea la actividad del usuario en el navegador
/// y gestiona el temporizador de cierre de sesión automático por inactividad.
/// Corresponde a la sección 5.1 del Requerimiento Maestro.
/// </summary>
public partial class InactivityMonitor : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IClientLogger Logger { get; set; } = default!;
    [Inject] private ISecureSessionStorageService SecureSessionStorage { get; set; } = default!;

    /// <summary>
    /// Tiempo máximo en minutos de inactividad antes de forzar el cierre de sesión.
    /// Configurable por defecto en 15 minutos.
    /// </summary>
    [Parameter] public int TimeoutMinutes { get; set; } = 15;

    /// <summary>
    /// Evento opcional invocado al producirse el vencimiento por inactividad.
    /// </summary>
    [Parameter] public EventCallback OnTimeout { get; set; }

    private DotNetObjectReference<InactivityMonitor>? _dotNetRef;
    private System.Timers.Timer? _inactivityTimer;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync("vogelInactivity.init", _dotNetRef);
            StartTimer();
        }
    }

    /// <summary>
    /// Método invocado desde JavaScript cuando se detecta actividad del usuario (teclado, mouse, touch).
    /// </summary>
    [JSInvokable]
    public void OnUserActivity()
    {
        ResetTimer();
    }

    private void StartTimer()
    {
        var intervalMs = Math.Max(TimeoutMinutes, 1) * 60 * 1000;
        _inactivityTimer = new System.Timers.Timer(intervalMs)
        {
            AutoReset = false
        };
        _inactivityTimer.Elapsed += async (_, _) => await HandleInactivityTimeoutAsync();
        _inactivityTimer.Start();
    }

    private void ResetTimer()
    {
        if (_inactivityTimer != null)
        {
            _inactivityTimer.Stop();
            _inactivityTimer.Start();
        }
    }

    private async Task HandleInactivityTimeoutAsync()
    {
        await Logger.LogWarningAsync($"Sesión cerrada automáticamente tras {TimeoutMinutes} minutos de inactividad.", nameof(InactivityMonitor));
        await SecureSessionStorage.ClearAsync();

        if (OnTimeout.HasDelegate)
        {
            await InvokeAsync(async () => await OnTimeout.InvokeAsync());
        }

        await InvokeAsync(() =>
        {
            Navigation.NavigateTo("login", forceLoad: false);
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_inactivityTimer != null)
        {
            _inactivityTimer.Stop();
            _inactivityTimer.Dispose();
            _inactivityTimer = null;
        }

        if (_dotNetRef != null)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("vogelInactivity.dispose");
            }
            catch
            {
                // Ignorar si el runtime de JS ya fue desmontado al cerrar la página
            }
            _dotNetRef.Dispose();
            _dotNetRef = null;
        }
    }
}
