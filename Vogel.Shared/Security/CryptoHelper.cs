using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vogel.Shared.Security;

/// <summary>
/// Provee utilidades criptográficas simétricas para el cifrado y descifrado seguro de datos sensibles
/// en almacenamiento de sesión y estado del cliente.
/// Diseñado para compatibilidad universal tanto en servidor como en Blazor WebAssembly (WASM).
/// Corresponde a la sección 5.2 del Requerimiento Maestro.
/// </summary>
public static class CryptoHelper
{
    private const string DefaultAppKey = "Vogel_2026_Secure_Key_Master_Antigravity!";
    private const int KeySize = 256;
    private const int DerivationIterations = 10_000;
    private const int SaltSize = 16;
    private const int IvSize = 16;
    private const int MacSize = 32;
    private const byte VersionTagV2 = 0x02;
    private const byte VersionTagV3 = 0x03;
    private const byte CurrentVersion = VersionTagV3;
    private static readonly byte[] HkdfInfo = Encoding.UTF8.GetBytes("Vogel_2026_HKDF_Session");

    /// <summary>
    /// Cifra una cadena de texto en claro utilizando cifrado autenticado de flujo (HMAC-SHA256 CTR)
    /// derivado instantáneamente por HKDF (RFC 5869) y un IV único, de altísimo rendimiento en WebAssembly y servidor.
    /// </summary>
    /// <param name="plainText">Texto en claro a cifrar.</param>
    /// <param name="passphrase">Frase o clave secreta opcional (utiliza clave por defecto si no se suministra).</param>
    /// <returns>Cadena en base64 que incluye Version + Salt + IV + Tag + Texto Cifrado.</returns>
    public static string Encrypt(string plainText, string? passphrase = null)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        var keyPhrase = string.IsNullOrEmpty(passphrase) ? DefaultAppKey : passphrase;
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var iv = RandomNumberGenerator.GetBytes(IvSize);

        // Derivación de clave instantánea mediante HKDF-SHA256: 32 bytes para encKey y 32 bytes para macKey
        var derived = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Encoding.UTF8.GetBytes(keyPhrase),
            64,
            salt,
            HkdfInfo);

        var encKey = new byte[32];
        var macKey = new byte[32];
        Buffer.BlockCopy(derived, 0, encKey, 0, 32);
        Buffer.BlockCopy(derived, 32, macKey, 0, 32);

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = ApplyKeystream(plainBytes, encKey, iv);

        // Calcular HMAC sobre [Version (1) + Salt (16) + IV (16) + CipherBytes]
        var dataToMac = new byte[1 + SaltSize + IvSize + cipherBytes.Length];
        dataToMac[0] = CurrentVersion;
        Buffer.BlockCopy(salt, 0, dataToMac, 1, SaltSize);
        Buffer.BlockCopy(iv, 0, dataToMac, 1 + SaltSize, IvSize);
        Buffer.BlockCopy(cipherBytes, 0, dataToMac, 1 + SaltSize + IvSize, cipherBytes.Length);

        var mac = HMACSHA256.HashData(macKey, dataToMac);

        // Empaque final: [Version (1)] + [Salt (16)] + [IV (16)] + [MAC (32)] + [CipherBytes]
        var combined = new byte[1 + SaltSize + IvSize + MacSize + cipherBytes.Length];
        combined[0] = CurrentVersion;
        Buffer.BlockCopy(salt, 0, combined, 1, SaltSize);
        Buffer.BlockCopy(iv, 0, combined, 1 + SaltSize, IvSize);
        Buffer.BlockCopy(mac, 0, combined, 1 + SaltSize + IvSize, MacSize);
        Buffer.BlockCopy(cipherBytes, 0, combined, 1 + SaltSize + IvSize + MacSize, cipherBytes.Length);

        return Convert.ToBase64String(combined);
    }

    /// <summary>
    /// Descifra una cadena cifrada en base64 generada por Encrypt.
    /// Valida la integridad del mensaje y la clave secreta antes de descifrar.
    /// Soporta formato v3 (HKDF), v2 (PBKDF2) y legado AES-CBC.
    /// </summary>
    /// <param name="cipherTextBase64">Cadena en base64 conteniendo el payload empaquetado.</param>
    /// <param name="passphrase">Frase o clave secreta empleada durante el cifrado.</param>
    /// <returns>Cadena en texto claro original.</returns>
    public static string Decrypt(string cipherTextBase64, string? passphrase = null)
    {
        if (string.IsNullOrEmpty(cipherTextBase64))
        {
            return string.Empty;
        }

        var combined = Convert.FromBase64String(cipherTextBase64);

        if (combined.Length > 0 && combined[0] == VersionTagV3)
        {
            return DecryptVersion3(combined, passphrase);
        }

        // Si empieza con VersionTagV2 (0x02), procesar formato autenticado v2
        if (combined.Length > 0 && combined[0] == VersionTagV2)
        {
            return DecryptVersion2(combined, passphrase);
        }

        // Formato legado o estándar AES CBC (para compatibilidad hacia atrás)
        return DecryptLegacyAes(combined, passphrase);
    }

    private static string DecryptVersion3(byte[] combined, string? passphrase)
    {
        var minLength = 1 + SaltSize + IvSize + MacSize;
        if (combined.Length < minLength)
        {
            throw new CryptographicException("El payload cifrado no tiene la longitud mínima requerida.");
        }

        var salt = new byte[SaltSize];
        var iv = new byte[IvSize];
        var expectedMac = new byte[MacSize];
        var cipherBytesLength = combined.Length - minLength;
        var cipherBytes = new byte[cipherBytesLength];

        Buffer.BlockCopy(combined, 1, salt, 0, SaltSize);
        Buffer.BlockCopy(combined, 1 + SaltSize, iv, 0, IvSize);
        Buffer.BlockCopy(combined, 1 + SaltSize + IvSize, expectedMac, 0, MacSize);
        Buffer.BlockCopy(combined, 1 + SaltSize + IvSize + MacSize, cipherBytes, 0, cipherBytesLength);

        var keyPhrase = string.IsNullOrEmpty(passphrase) ? DefaultAppKey : passphrase;
        var derived = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Encoding.UTF8.GetBytes(keyPhrase),
            64,
            salt,
            HkdfInfo);

        var encKey = new byte[32];
        var macKey = new byte[32];
        Buffer.BlockCopy(derived, 0, encKey, 0, 32);
        Buffer.BlockCopy(derived, 32, macKey, 0, 32);

        // Verificar HMAC
        var dataToMac = new byte[1 + SaltSize + IvSize + cipherBytes.Length];
        dataToMac[0] = VersionTagV3;
        Buffer.BlockCopy(salt, 0, dataToMac, 1, SaltSize);
        Buffer.BlockCopy(iv, 0, dataToMac, 1 + SaltSize, IvSize);
        Buffer.BlockCopy(cipherBytes, 0, dataToMac, 1 + SaltSize + IvSize, cipherBytes.Length);

        var computedMac = HMACSHA256.HashData(macKey, dataToMac);
        if (!CryptographicOperations.FixedTimeEquals(computedMac, expectedMac))
        {
            throw new CryptographicException("La clave secreta es incorrecta o los datos fueron alterados.");
        }

        var plainBytes = ApplyKeystream(cipherBytes, encKey, iv);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private static string DecryptVersion2(byte[] combined, string? passphrase)
    {
        var minLength = 1 + SaltSize + IvSize + MacSize;
        if (combined.Length < minLength)
        {
            throw new CryptographicException("El payload cifrado no tiene la longitud mínima requerida.");
        }

        var salt = new byte[SaltSize];
        var iv = new byte[IvSize];
        var expectedMac = new byte[MacSize];
        var cipherBytesLength = combined.Length - minLength;
        var cipherBytes = new byte[cipherBytesLength];

        Buffer.BlockCopy(combined, 1, salt, 0, SaltSize);
        Buffer.BlockCopy(combined, 1 + SaltSize, iv, 0, IvSize);
        Buffer.BlockCopy(combined, 1 + SaltSize + IvSize, expectedMac, 0, MacSize);
        Buffer.BlockCopy(combined, 1 + SaltSize + IvSize + MacSize, cipherBytes, 0, cipherBytesLength);

        var keyPhrase = string.IsNullOrEmpty(passphrase) ? DefaultAppKey : passphrase;
        var derived = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(keyPhrase),
            salt,
            DerivationIterations,
            HashAlgorithmName.SHA256,
            64);

        var encKey = new byte[32];
        var macKey = new byte[32];
        Buffer.BlockCopy(derived, 0, encKey, 0, 32);
        Buffer.BlockCopy(derived, 32, macKey, 0, 32);

        // Verificar HMAC
        var dataToMac = new byte[1 + SaltSize + IvSize + cipherBytes.Length];
        dataToMac[0] = VersionTagV2;
        Buffer.BlockCopy(salt, 0, dataToMac, 1, SaltSize);
        Buffer.BlockCopy(iv, 0, dataToMac, 1 + SaltSize, IvSize);
        Buffer.BlockCopy(cipherBytes, 0, dataToMac, 1 + SaltSize + IvSize, cipherBytes.Length);

        var computedMac = HMACSHA256.HashData(macKey, dataToMac);
        if (!CryptographicOperations.FixedTimeEquals(computedMac, expectedMac))
        {
            throw new CryptographicException("La clave secreta es incorrecta o los datos fueron alterados.");
        }

        var plainBytes = ApplyKeystream(cipherBytes, encKey, iv);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private static string DecryptLegacyAes(byte[] combined, string? passphrase)
    {
        if (combined.Length < SaltSize + IvSize)
        {
            throw new CryptographicException("El payload cifrado no tiene la longitud mínima requerida.");
        }

        var salt = new byte[SaltSize];
        var iv = new byte[IvSize];
        var cipherBytesLength = combined.Length - SaltSize - IvSize;
        var cipherBytes = new byte[cipherBytesLength];

        Buffer.BlockCopy(combined, 0, salt, 0, SaltSize);
        Buffer.BlockCopy(combined, SaltSize, iv, 0, IvSize);
        Buffer.BlockCopy(combined, SaltSize + IvSize, cipherBytes, 0, cipherBytesLength);

        var keyPhrase = string.IsNullOrEmpty(passphrase) ? DefaultAppKey : passphrase;
        var key = Rfc2898DeriveBytes.Pbkdf2(keyPhrase, salt, 100_000, HashAlgorithmName.SHA256, KeySize / 8);

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(decryptedBytes);
    }

    private static byte[] ApplyKeystream(byte[] input, byte[] key, byte[] iv)
    {
        var output = new byte[input.Length];
        var counterBuffer = new byte[iv.Length + 4];
        Buffer.BlockCopy(iv, 0, counterBuffer, 0, iv.Length);

        var outputOffset = 0;
        uint counter = 0;

        while (outputOffset < input.Length)
        {
            counterBuffer[^4] = (byte)(counter >> 24);
            counterBuffer[^3] = (byte)(counter >> 16);
            counterBuffer[^2] = (byte)(counter >> 8);
            counterBuffer[^1] = (byte)counter;

            var keyBlock = HMACSHA256.HashData(key, counterBuffer);
            var bytesToXor = Math.Min(keyBlock.Length, input.Length - outputOffset);

            for (var i = 0; i < bytesToXor; i++)
            {
                output[outputOffset + i] = (byte)(input[outputOffset + i] ^ keyBlock[i]);
            }

            outputOffset += bytesToXor;
            counter++;
        }

        return output;
    }

    /// <summary>
    /// Serializa y cifra un objeto de tipo T en formato seguro para almacenamiento.
    /// </summary>
    public static string EncryptObject<T>(T value, string? passphrase = null)
    {
        if (value == null) return string.Empty;
        var json = JsonSerializer.Serialize(value);
        return Encrypt(json, passphrase);
    }

    /// <summary>
    /// Descifra y deserializa un objeto previamente cifrado con EncryptObject.
    /// </summary>
    public static T? DecryptObject<T>(string cipherTextBase64, string? passphrase = null)
    {
        if (string.IsNullOrEmpty(cipherTextBase64)) return default;
        var json = Decrypt(cipherTextBase64, passphrase);
        return JsonSerializer.Deserialize<T>(json);
    }
}
