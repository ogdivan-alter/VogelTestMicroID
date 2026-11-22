using Microsoft.AspNetCore.Components;
using Vogel.Shared.Models;

namespace Vogel.Client.Services;

/// <summary>
/// Estado de sesión del usuario autenticado compartido entre los componentes Blazor.
/// Actúa como servicio Singleton para la propagación del estado de autenticación a través de la aplicación.
/// </summary>
public class SessionState
{
    private User? _currentUser;

    /// <summary>
    /// Usuario actualmente autenticado o null si no hay sesión activa.
    /// </summary>
    public User? CurrentUser
    {
        get => _currentUser;
        private set
        {
            _currentUser = value;
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Indica si hay un usuario autenticado en la sesión actual.
    /// </summary>
    public bool IsAuthenticated => _currentUser != null;

    /// <summary>
    /// Evento disparado cuando el estado de autenticación cambia.
    /// Permite que los componentes se re-rendericen automáticamente.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Establece al usuario autenticado en el estado de sesión.
    /// </summary>
    public void SetUser(User user)
    {
        CurrentUser = user ?? throw new ArgumentNullException(nameof(user));
    }

    /// <summary>
    /// Limpia el estado de sesión al cerrar sesión.
    /// </summary>
    public void ClearUser()
    {
        CurrentUser = null;
    }

    /// <summary>
    /// Verifica si el usuario tiene un privilegio específico por codename.
    /// Retorna "Único" si lo tiene o "Negado" si no (compatible con AuthorizeView).
    /// </summary>
    public string PrivilegeFunction(string codename)
    {
        if (!IsAuthenticated || CurrentUser?.Privileges == null) return "Negado";

        var normalizedTarget = codename.Replace("_", "-");
        var found = CurrentUser.Privileges.Any(p =>
            string.Equals(p.Codename, codename, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Codename.Replace("_", "-"), normalizedTarget, StringComparison.OrdinalIgnoreCase));

        return found ? "Único" : "Negado";
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
