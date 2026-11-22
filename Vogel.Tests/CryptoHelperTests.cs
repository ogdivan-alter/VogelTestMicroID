using Vogel.Shared.Models;
using Vogel.Shared.Security;
using Xunit;

namespace Vogel.Tests;

/// <summary>
/// Pruebas unitarias para CryptoHelper (cifrado y descifrado AES-256).
/// </summary>
public class CryptoHelperTests
{
    [Fact]
    public void Encrypt_And_Decrypt_ShouldPreserveOriginalString()
    {
        // Arrange
        const string originalText = "InformacionSensibleDeNavegacion_IdParent_201";

        // Act
        var encrypted = CryptoHelper.Encrypt(originalText);
        var decrypted = CryptoHelper.Decrypt(encrypted);

        // Assert
        Assert.NotNull(encrypted);
        Assert.NotEqual(originalText, encrypted);
        Assert.Equal(originalText, decrypted);
    }

    [Fact]
    public void Encrypt_WithCustomPassphrase_ShouldDecryptOnlyWithSamePassphrase()
    {
        // Arrange
        const string text = "ClaveSecreta123";
        const string correctPassphrase = "MiClavePersonalizada2026!";
        const string wrongPassphrase = "OtraClaveDiferente999!";

        // Act
        var encrypted = CryptoHelper.Encrypt(text, correctPassphrase);
        var decrypted = CryptoHelper.Decrypt(encrypted, correctPassphrase);

        // Assert
        Assert.Equal(text, decrypted);
        Assert.ThrowsAny<Exception>(() => CryptoHelper.Decrypt(encrypted, wrongPassphrase));
    }

    [Fact]
    public void EncryptObject_And_DecryptObject_ShouldPreserveComplexObject()
    {
        // Arrange
        var originalPrivilege = new Privilege
        {
            Id = 201,
            IdParent = 2,
            Codename = "sub_2_1",
            Title = "2.1",
            Action = "secondary/201",
            Access = new PrivilegeAccess { Read = true, Execute = true }
        };

        // Act
        var encrypted = CryptoHelper.EncryptObject(originalPrivilege);
        var decrypted = CryptoHelper.DecryptObject<Privilege>(encrypted);

        // Assert
        Assert.NotNull(decrypted);
        Assert.Equal(originalPrivilege.Id, decrypted.Id);
        Assert.Equal(originalPrivilege.Codename, decrypted.Codename);
        Assert.Equal(originalPrivilege.Title, decrypted.Title);
        Assert.NotNull(decrypted.Access);
        Assert.True(decrypted.Access.Read);
        Assert.True(decrypted.Access.Execute);
    }

    [Fact]
    public void Encrypt_ShouldProduceVersion3Payload()
    {
        // Arrange
        const string text = "PruebaVersion3";

        // Act
        var encrypted = CryptoHelper.Encrypt(text);
        var bytes = Convert.FromBase64String(encrypted);

        // Assert: primer byte debe ser VersionTagV3 (0x03)
        Assert.NotEmpty(bytes);
        Assert.Equal(0x03, bytes[0]);
    }

    [Fact]
    public void Encrypt_And_Decrypt_ShouldBeHighPerformance_Under50msFor50Operations()
    {
        // Arrange
        const string text = "RendimientoCriptograficoInstantaneo2026";
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Act: 50 ciclos completos de cifrado + descifrado
        for (int i = 0; i < 50; i++)
        {
            var enc = CryptoHelper.Encrypt(text);
            var dec = CryptoHelper.Decrypt(enc);
            Assert.Equal(text, dec);
        }
        sw.Stop();

        // Assert: 50 operaciones completas deben ejecutarse en menos de 50ms (rendimiento de sub-milisegundo)
        Assert.True(sw.ElapsedMilliseconds < 50, $"Tardó {sw.ElapsedMilliseconds}ms, esperado < 50ms");
    }
}
