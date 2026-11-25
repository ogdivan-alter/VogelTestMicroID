using System.Collections.Concurrent;

namespace Vogel.Server.Services;

/// <summary>
/// Almacén en memoria (thread-safe) que asocia tokens de sesión Mock con el nombre de usuario autenticado.
/// </summary>
public sealed class MockSessionStore
{
    private readonly ConcurrentDictionary<string, string> _sessions = new(StringComparer.Ordinal);

    /// <summary>
    /// Registra o actualiza la asociación entre un token de sesión Mock y un nombre de usuario.
    /// </summary>
    public void RegisterSession(string token, string username)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        _sessions[token] = username;
    }

    /// <summary>
    /// Intenta resolver el nombre de usuario asociado a un token de sesión Mock.
    /// </summary>
    public bool TryGetUsername(string token, out string username)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            username = string.Empty;
            return false;
        }

        return _sessions.TryGetValue(token, out username!);
    }

    /// <summary>
    /// Elimina una sesión Mock registrada.
    /// </summary>
    public void RemoveSession(string token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            _sessions.TryRemove(token, out _);
        }
    }
}
