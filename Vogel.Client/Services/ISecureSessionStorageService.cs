namespace Vogel.Client.Services;

/// <summary>
/// Contrato de servicio para el almacenamiento cifrado en sesión
/// (sessionStorage nativo del navegador + CryptoHelper).
/// Corresponde a la sección 5.2 del Requerimiento Maestro.
/// </summary>
public interface ISecureSessionStorageService
{
    /// <summary>
    /// Guarda un valor de texto plano en sesión, cifrándolo previamente con AES-256.
    /// </summary>
    ValueTask SetItemEncryptedAsync(string key, string plainValue);

    /// <summary>
    /// Guarda un objeto complejo en sesión, serializándolo en JSON y cifrándolo con AES-256.
    /// </summary>
    ValueTask SetItemEncryptedObjectAsync<T>(string key, T value);

    /// <summary>
    /// Recupera y descifra un valor de texto plano almacenado en sesión.
    /// </summary>
    ValueTask<string?> GetItemDecryptedAsync(string key);

    /// <summary>
    /// Recupera, descifra y deserializa un objeto complejo almacenado en sesión.
    /// </summary>
    ValueTask<T?> GetItemDecryptedObjectAsync<T>(string key);

    /// <summary>
    /// Elimina un elemento de la sesión por su clave.
    /// </summary>
    ValueTask RemoveItemAsync(string key);

    /// <summary>
    /// Limpia todos los elementos de la sesión.
    /// </summary>
    ValueTask ClearAsync();

    /// <summary>
    /// Guarda de forma cifrada el IdParent activo en la navegación actual.
    /// </summary>
    ValueTask SetActiveIdParentAsync(int idParent);

    /// <summary>
    /// Recupera el IdParent activo descifrado de la sesión (retorna 0 si no existe).
    /// </summary>
    ValueTask<int> GetActiveIdParentAsync();
}
