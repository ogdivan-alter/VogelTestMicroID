using Microsoft.JSInterop;
using Vogel.Shared.Security;

namespace Vogel.Client.Services;

/// <summary>
/// Implementación de almacenamiento cifrado en sesión utilizando el `sessionStorage` nativo
/// del navegador mediante JS Interop (sin dependencias de terceros) y CryptoHelper.
/// Asegura que la información de navegación y estado sensible no quede expuesta en texto claro.
/// </summary>
public class SecureSessionStorageService : ISecureSessionStorageService
{
    private const string ActiveIdParentKey = "vogel_nav_active_id_parent";
    private readonly IJSRuntime _jsRuntime;

    public SecureSessionStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async ValueTask SetItemEncryptedAsync(string key, string plainValue)
    {
        var cipherText = CryptoHelper.Encrypt(plainValue);
        await SetRawAsync(key, cipherText);
    }

    public async ValueTask SetItemEncryptedObjectAsync<T>(string key, T value)
    {
        var cipherText = CryptoHelper.EncryptObject(value);
        await SetRawAsync(key, cipherText);
    }

    public async ValueTask<string?> GetItemDecryptedAsync(string key)
    {
        var cipherText = await GetRawAsync(key);
        if (string.IsNullOrEmpty(cipherText))
        {
            return null;
        }

        try
        {
            return CryptoHelper.Decrypt(cipherText);
        }
        catch
        {
            // Si el valor no puede descifrarse (corrupción, formato previo o manipulación), se descarta.
            await RemoveItemAsync(key);
            return null;
        }
    }

    public async ValueTask<T?> GetItemDecryptedObjectAsync<T>(string key)
    {
        var cipherText = await GetRawAsync(key);
        if (string.IsNullOrEmpty(cipherText))
        {
            return default;
        }

        try
        {
            return CryptoHelper.DecryptObject<T>(cipherText);
        }
        catch
        {
            await RemoveItemAsync(key);
            return default;
        }
    }

    public async ValueTask RemoveItemAsync(string key)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("vogelSessionStorage.remove", key);
        }
        catch (JSException)
        {
            // El almacenamiento no está disponible; no hay nada que eliminar.
        }
    }

    public async ValueTask ClearAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("vogelSessionStorage.clear");
        }
        catch (JSException)
        {
            // El almacenamiento no está disponible; no hay nada que limpiar.
        }
    }

    public async ValueTask SetActiveIdParentAsync(int idParent)
    {
        await SetItemEncryptedAsync(ActiveIdParentKey, idParent.ToString());
    }

    public async ValueTask<int> GetActiveIdParentAsync()
    {
        var value = await GetItemDecryptedAsync(ActiveIdParentKey);
        if (int.TryParse(value, out var result))
        {
            return result;
        }
        return 0;
    }

    /// <summary>
    /// Recupera el valor crudo (cifrado) desde sessionStorage, sin descifrar.
    /// </summary>
    private async ValueTask<string?> GetRawAsync(string key)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("vogelSessionStorage.get", key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    /// <summary>
    /// Escribe el valor cifrado en sessionStorage, validando que el navegador lo acepte.
    /// </summary>
    private async ValueTask SetRawAsync(string key, string cipherText)
    {
        bool stored;
        try
        {
            stored = await _jsRuntime.InvokeAsync<bool>("vogelSessionStorage.set", key, cipherText);
        }
        catch (JSException ex)
        {
            throw new InvalidOperationException("No fue posible acceder al almacenamiento de sesión del navegador.", ex);
        }

        if (!stored)
        {
            throw new InvalidOperationException("No fue posible escribir en el almacenamiento de sesión del navegador.");
        }
    }
}
