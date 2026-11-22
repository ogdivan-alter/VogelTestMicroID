using Vogel.Shared.DTOs;
using Vogel.Shared.Services;
using Xunit;

namespace Vogel.Tests;

/// <summary>
/// Pruebas unitarias para modelos de usuario y flujo de autenticación simulada (Mock).
/// </summary>
public class UserAndAuthenticationTests
{
    private readonly IMockDataService _mockDataService;

    public UserAndAuthenticationTests()
    {
        _mockDataService = new MockDataService();
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidInternalCredentials_ShouldSucceedAndAllowPasswordChange()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            Username = "admin",
            Password = "Vogel2026*"
        };

        // Act
        var result = await _mockDataService.AuthenticateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.Equal("admin", result.User.Username);
        Assert.Equal("INTERNA", result.User.TypeAuthentication);
        Assert.True(result.User.AllowPasswordChange);
        Assert.NotEmpty(result.User.Privileges);
        Assert.Equal("Único", result.User.BaseRole);
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidExternalCredentials_ShouldSucceedAndDisallowPasswordChange()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            Username = "external_user",
            Password = "External2026*"
        };

        // Act
        var result = await _mockDataService.AuthenticateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.Equal("external_user", result.User.Username);
        Assert.Equal("EXTERNAL", result.User.TypeAuthentication);
        Assert.False(result.User.AllowPasswordChange);
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidCredentials_ShouldFailWithFriendlyMessage()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            Username = "admin",
            Password = "WrongPassword123"
        };

        // Act
        var result = await _mockDataService.AuthenticateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Null(result.User);
        Assert.Equal("Usuario o Clave incorrectos", result.Message);
    }
}
