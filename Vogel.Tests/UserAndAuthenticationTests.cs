using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Vogel.Client.Services;
using Vogel.Shared.DTOs;
using Vogel.Shared.Models;
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

    [Fact]
    public async Task ChangePasswordAsync_WithValidToken_SendsBearerHeaderAndReturnsSuccessWithoutLoggingSecrets()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async req =>
        {
            capturedRequest = req;
            capturedBody = req.Content is not null ? await req.Content.ReadAsStringAsync() : null;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ChangePasswordResponseDto
                {
                    Success = true,
                    Message = "Contraseña actualizada"
                })
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        var storage = new InMemorySecureSessionStorage();
        await storage.SetItemEncryptedAsync(AuthenticationService.BackendTokenKey, "jwt-token-secreto");

        var sessionState = new SessionState();
        sessionState.SetUser(new User { Username = "carlos.gomez", AllowPasswordChange = true });

        var logger = new RecordingClientLogger();
        var authService = CreateAuthService(httpClient, sessionState, storage, logger);

        // Act
        var (success, errorMessage) = await authService.ChangePasswordAsync("OldPass2026*", "NewPass2026*");

        // Assert
        Assert.True(success);
        Assert.Equal(string.Empty, errorMessage);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("api/auth/change-password", capturedRequest.RequestUri?.PathAndQuery.TrimStart('/'));
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("jwt-token-secreto", capturedRequest.Headers.Authorization?.Parameter);
        Assert.Contains("NewPass2026*", capturedBody);
        Assert.DoesNotContain(logger.Messages, m => m.Contains("OldPass2026*") || m.Contains("NewPass2026*") || m.Contains("jwt-token-secreto"));
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenPolicyFails400_ReturnsPolicyMessageWithoutLoggingSecrets()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new ChangePasswordResponseDto
            {
                Success = false,
                Message = "ERR_PASSWORD_POLICY: La contraseña debe incluir símbolos."
            })
        }));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        var storage = new InMemorySecureSessionStorage();
        await storage.SetItemEncryptedAsync(AuthenticationService.BackendTokenKey, "jwt-token-123");

        var logger = new RecordingClientLogger();
        var authService = CreateAuthService(httpClient, new SessionState(), storage, logger);

        // Act
        var (success, errorMessage) = await authService.ChangePasswordAsync("OldPass1!", "weak");

        // Assert
        Assert.False(success);
        Assert.Contains("ERR_PASSWORD_POLICY", errorMessage);
        Assert.DoesNotContain(logger.Messages, m => m.Contains("OldPass1!") || m.Contains("weak") || m.Contains("jwt-token-123"));
    }

    [Fact]
    public async Task LogoutAsync_CallsBffLogoutAndClearsSessionStateAndStorage()
    {
        // Arrange
        var logoutCalled = false;
        var handler = new StubHttpMessageHandler(req =>
        {
            if (req.RequestUri?.PathAndQuery.EndsWith("api/auth/logout", StringComparison.OrdinalIgnoreCase) == true)
            {
                logoutCalled = true;
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        var storage = new InMemorySecureSessionStorage();
        await storage.SetItemEncryptedAsync(AuthenticationService.BackendTokenKey, "jwt-token-123");

        var sessionState = new SessionState();
        sessionState.SetUser(new User { Username = "carlos.gomez" });

        var authService = CreateAuthService(httpClient, sessionState, storage, new RecordingClientLogger());

        // Act
        await authService.LogoutAsync();

        // Assert
        Assert.True(logoutCalled);
        Assert.False(sessionState.IsAuthenticated);
        Assert.Null(sessionState.CurrentUser);
        Assert.Null(await storage.GetItemDecryptedAsync(AuthenticationService.BackendTokenKey));
    }

    private static AuthenticationService CreateAuthService(
        HttpClient httpClient,
        SessionState sessionState,
        ISecureSessionStorageService storage,
        IClientLogger logger)
    {
        var config = new ConfigurationBuilder().Build();
        return new AuthenticationService(
            httpClient,
            sessionState,
            storage,
            new StubSessionValidationService(),
            logger,
            config);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request);
    }

    private sealed class InMemorySecureSessionStorage : ISecureSessionStorageService
    {
        private readonly Dictionary<string, string> _items = new();
        private int _activeIdParent;

        public ValueTask SetItemEncryptedAsync(string key, string plainValue)
        {
            _items[key] = plainValue;
            return ValueTask.CompletedTask;
        }

        public ValueTask SetItemEncryptedObjectAsync<T>(string key, T value)
        {
            _items[key] = System.Text.Json.JsonSerializer.Serialize(value);
            return ValueTask.CompletedTask;
        }

        public ValueTask<string?> GetItemDecryptedAsync(string key)
            => ValueTask.FromResult(_items.TryGetValue(key, out var val) ? val : null);

        public ValueTask<T?> GetItemDecryptedObjectAsync<T>(string key)
            => ValueTask.FromResult(_items.TryGetValue(key, out var val)
                ? System.Text.Json.JsonSerializer.Deserialize<T>(val)
                : default);

        public ValueTask RemoveItemAsync(string key)
        {
            _items.Remove(key);
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearAsync()
        {
            _items.Clear();
            return ValueTask.CompletedTask;
        }

        public ValueTask SetActiveIdParentAsync(int idParent)
        {
            _activeIdParent = idParent;
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> GetActiveIdParentAsync() => ValueTask.FromResult(_activeIdParent);
    }

    private sealed class StubSessionValidationService : ISessionValidationService
    {
        public Task<bool> ValidateServerInstanceAsync() => Task.FromResult(true);
        public Task<bool> EnsureValidAsync() => Task.FromResult(true);
        public Task<bool> EstablishSessionAsync() => Task.FromResult(true);
    }

    private sealed class RecordingClientLogger : IClientLogger
    {
        public List<string> Messages { get; } = new();

        public Task LogInformationAsync(string message, string? source = null)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task LogWarningAsync(string message, string? source = null)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task LogErrorAsync(string message, Exception? exception = null, string? source = null)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
