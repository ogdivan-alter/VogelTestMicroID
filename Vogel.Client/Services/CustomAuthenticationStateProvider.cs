using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Vogel.Client.Services;

/// <summary>
/// Proveedor de estado de autenticación para Blazor WebAssembly (CustomAuthenticationStateProvider).
/// Integra el SessionState reactivo con el subsistema nativo de autorización de Blazor (AuthorizeView).
/// Asigna al usuario autenticado el rol base 'Único' según la sección 4.1 del Requerimiento Maestro.
/// </summary>
public class CustomAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly SessionState _sessionState;

    public CustomAuthenticationStateProvider(SessionState sessionState)
    {
        _sessionState = sessionState;
        _sessionState.OnChange += HandleSessionChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        ClaimsIdentity identity;

        if (_sessionState.IsAuthenticated && _sessionState.CurrentUser != null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, _sessionState.CurrentUser.Username),
                new(ClaimTypes.Role, "Único")
            };

            if (!string.IsNullOrWhiteSpace(_sessionState.CurrentUser.BaseRole) &&
                !string.Equals(_sessionState.CurrentUser.BaseRole, "Único", StringComparison.OrdinalIgnoreCase))
            {
                claims.Add(new Claim(ClaimTypes.Role, _sessionState.CurrentUser.BaseRole));
            }

            identity = new ClaimsIdentity(claims, "VogelAuth");
        }
        else
        {
            identity = new ClaimsIdentity();
        }

        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(new AuthenticationState(principal));
    }

    private void HandleSessionChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void Dispose()
    {
        _sessionState.OnChange -= HandleSessionChanged;
        GC.SuppressFinalize(this);
    }
}
