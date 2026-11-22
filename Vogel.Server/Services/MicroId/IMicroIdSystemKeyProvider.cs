namespace Vogel.Server.Services.MicroId;

/// <summary>
/// Proveedor Singleton en memoria de la llave de sistema (X-System-Key) para MicroID.
/// </summary>
public interface IMicroIdSystemKeyProvider
{
    /// <summary>
    /// Obtiene la llave de sistema vigente en memoria o solicita una nueva a POST /creds/create.
    /// </summary>
    Task<string> GetKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalida la llave en caché para forzar su renovación en la siguiente solicitud.
    /// </summary>
    void InvalidateKey();
}
