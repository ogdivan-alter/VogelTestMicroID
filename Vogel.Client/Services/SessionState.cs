using Microsoft.AspNetCore.Components;
using Vogel.Shared.Enums;
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
    /// Obtiene el privilegio del usuario autenticado por su codename (insensible a mayúsculas/minúsculas
    /// y normalizando '_' y '-'). Retorna null si no hay sesión activa o el privilegio no existe.
    /// </summary>
    public Privilege? GetPrivilege(string codename)
    {
        if (!IsAuthenticated || CurrentUser?.Privileges is null || string.IsNullOrWhiteSpace(codename))
        {
            return null;
        }

        var trimmedCodename = codename.Trim();
        var normalizedTarget = trimmedCodename.Replace("_", "-");

        return CurrentUser.Privileges.FirstOrDefault(p =>
            !string.IsNullOrWhiteSpace(p.Codename) &&
            (string.Equals(p.Codename, trimmedCodename, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(p.Codename.Replace("_", "-"), normalizedTarget, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Obtiene el privilegio del usuario autenticado cuya ruta/acción coincide con <paramref name="action"/>
    /// (normalizando '/' inicial/final e insensible a mayúsculas/minúsculas), independientemente del codename
    /// configurado en el proveedor de identidad.
    /// </summary>
    public Privilege? GetPrivilegeByAction(string action)
    {
        if (!IsAuthenticated || CurrentUser?.Privileges is null || string.IsNullOrWhiteSpace(action))
        {
            return null;
        }

        var normalizedTarget = NormalizeAction(action);
        if (string.IsNullOrEmpty(normalizedTarget))
        {
            return null;
        }

        return CurrentUser.Privileges.FirstOrDefault(p =>
            !string.IsNullOrWhiteSpace(p.Action) &&
            string.Equals(NormalizeAction(p.Action), normalizedTarget, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Evalúa de forma estricta (fail-closed) si el usuario autenticado posee una acción granular
    /// específica sobre un privilegio identificado por su ruta/acción (p. ej. "/admsec/priv").
    /// </summary>
    public bool HasPrivilegeAccessByAction(string action, PrivilegeAction requiredAction)
    {
        var privilege = GetPrivilegeByAction(action);
        return HasAccess(privilege, requiredAction);
    }

    /// <summary>
    /// Evalúa de forma estricta (fail-closed) si el usuario autenticado posee una acción granular
    /// específica sobre un privilegio identificado por codename.
    /// Retorna false si no está autenticado, si el privilegio no existe o si Access es null.
    /// </summary>
    public bool HasPrivilegeAccess(string codename, PrivilegeAction action)
    {
        var privilege = GetPrivilege(codename);
        return HasAccess(privilege, action);
    }

    private static bool HasAccess(Privilege? privilege, PrivilegeAction action)
    {
        if (privilege?.Access is null)
        {
            return false;
        }

        return action switch
        {
            PrivilegeAction.Read => privilege.Access.Read,
            PrivilegeAction.Create => privilege.Access.Create,
            PrivilegeAction.Modify => privilege.Access.Modify,
            PrivilegeAction.Delete => privilege.Access.Delete,
            PrivilegeAction.Validate => privilege.Access.Validate,
            PrivilegeAction.Execute => privilege.Access.Execute,
            PrivilegeAction.Approve => privilege.Access.Approve,
            _ => false
        };
    }

    private static string NormalizeAction(string? action) =>
        string.IsNullOrWhiteSpace(action)
            ? string.Empty
            : action.Trim().Trim('/');

    /// <summary>
    /// Verifica si el usuario tiene un privilegio específico por codename.
    /// Retorna "Único" si lo tiene o "Negado" si no (compatible con AuthorizeView).
    /// </summary>
    public string PrivilegeFunction(string codename)
    {
        return GetPrivilege(codename) is not null ? "Único" : "Negado";
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
