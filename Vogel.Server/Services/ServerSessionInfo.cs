using System;

namespace Vogel.Server.Services;

/// <summary>
/// Almacena información de arranque del servidor para invalidar sesiones
/// de cliente tras reinicio del servicio (Ajustes Fase 2).
/// </summary>
public class ServerSessionInfo
{
    /// <summary>
    /// Momento exacto de arranque del servidor (UTC).
    /// </summary>
    public DateTime StartTimeUtc { get; } = DateTime.UtcNow;

    /// <summary>
    /// Identificador único de esta instancia de servidor.
    /// </summary>
    public string InstanceId { get; } = Guid.NewGuid().ToString("N");
}
