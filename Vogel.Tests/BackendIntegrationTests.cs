using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Vogel.Client.Services;
using Vogel.Client.Services.Api;
using Vogel.Shared.DTOs;
using Vogel.Shared.Models;
using Vogel.Shared.Services;
using Xunit;

namespace Vogel.Tests;

public class BackendIntegrationTests
{
    private class TestHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Sender { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Sender(request));
        }
    }

    private class MockSecureSessionStorage : ISecureSessionStorageService
    {
        public readonly Dictionary<string, string> Storage = new();

        public ValueTask SetItemEncryptedAsync(string key, string plainValue)
        {
            Storage[key] = plainValue;
            return ValueTask.CompletedTask;
        }

        public ValueTask<string?> GetItemDecryptedAsync(string key)
        {
            Storage.TryGetValue(key, out var val);
            return ValueTask.FromResult<string?>(val);
        }

        public ValueTask SetItemEncryptedObjectAsync<T>(string key, T value)
        {
            Storage[key] = System.Text.Json.JsonSerializer.Serialize(value);
            return ValueTask.CompletedTask;
        }

        public ValueTask<T?> GetItemDecryptedObjectAsync<T>(string key)
        {
            if (Storage.TryGetValue(key, out var json))
            {
                return ValueTask.FromResult(System.Text.Json.JsonSerializer.Deserialize<T>(json));
            }
            return ValueTask.FromResult<T?>(default);
        }

        public ValueTask RemoveItemAsync(string key)
        {
            Storage.Remove(key);
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearAsync()
        {
            Storage.Clear();
            return ValueTask.CompletedTask;
        }

        public ValueTask SetActiveIdParentAsync(int idParent) => ValueTask.CompletedTask;

        public ValueTask<int> GetActiveIdParentAsync() => ValueTask.FromResult(0);
    }

    private class MockSessionValidation : ISessionValidationService
    {
        public bool EstablishSuccess { get; set; } = true;
        public bool ValidateSuccess { get; set; } = true;

        public Task<bool> EnsureValidAsync() => Task.FromResult(ValidateSuccess);
        public Task<bool> EstablishSessionAsync() => Task.FromResult(EstablishSuccess);
        public Task<bool> ValidateServerInstanceAsync() => Task.FromResult(ValidateSuccess);
    }

    private class MockClientLogger : IClientLogger
    {
        public Task LogInformationAsync(string message, string? source = null) => Task.CompletedTask;
        public Task LogWarningAsync(string message, string? source = null) => Task.CompletedTask;
        public Task LogErrorAsync(string message, Exception? ex = null, string? source = null) => Task.CompletedTask;
    }

    private class StubApiBackendClient : IApiBackendClient
    {
        public BackendLoginResponseDto? LoginResponse { get; set; }
        public string? TokenResponse { get; set; }
        public BackendErrorDto? LoginError { get; set; }
        public int LoginStatusCode { get; set; } = 200;

        public BackendPrivilegesResponseDto? PrivilegesResponse { get; set; }
        public BackendErrorDto? PrivilegesError { get; set; }
        public int PrivilegesStatusCode { get; set; } = 200;

        public Task<(BackendLoginResponseDto? Profile, string? Token, BackendErrorDto? Error, int StatusCode)> LoginAsync(
            string username, string password, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((LoginResponse, TokenResponse, LoginError, LoginStatusCode));
        }

        public Task<(BackendPrivilegesResponseDto? Data, BackendErrorDto? Error, int StatusCode)> GetPrivilegesAsync(
            string token, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((PrivilegesResponse, PrivilegesError, PrivilegesStatusCode));
        }
    }

    [Fact]
    public void MapToUser_ShouldMapWriteToCreate_AndNormalizeIconUrls()
    {
        // Arrange
        var profile = new BackendLoginResponseDto("jdoe", "John Doe", "/images/avatars/jdoe.jpg", "INTERNA", true);
        var backendPrivileges = new List<BackendPrivilegeItemDto>
        {
            new(
                IdParent: 0,
                Id: 1,
                Codename: "main_option_1",
                Group: "General",
                Title: "Arranque 1",
                Action: "",
                Icon: "/icons/option1.svg",
                Access: new BackendAccessDto(Read: true, Modify: false, Write: false, Delete: false, Approve: false)
            ),
            new(
                IdParent: 1,
                Id: 2,
                Codename: "suboption_1_1",
                Group: "Operaciones",
                Title: "Entre Aquí",
                Action: "/work/start",
                Icon: "/icons/suboption1_1.svg",
                Access: new BackendAccessDto(Read: true, Modify: true, Write: true, Delete: false, Approve: true)
            )
        };

        const string baseUrl = "https://localhost:7113";

        // Act
        var user = AuthenticationService.MapToUser(profile, backendPrivileges, baseUrl);

        // Assert
        Assert.NotNull(user);
        Assert.Equal("jdoe", user.Username);
        Assert.Equal("John Doe", user.Name);
        Assert.Equal("INTERNA", user.TypeAuthentication);
        Assert.True(user.AllowPasswordChange);
        Assert.Equal("Único", user.BaseRole);
        Assert.Equal(2, user.Privileges.Count);

        // Validar normalización de íconos
        Assert.Equal("https://localhost:7113/icons/option1.svg", user.Privileges[0].Icon);
        Assert.Equal("https://localhost:7113/icons/suboption1_1.svg", user.Privileges[1].Icon);

        // Validar mapeo de Write -> Create
        Assert.False(user.Privileges[0].Access!.Create);
        Assert.True(user.Privileges[1].Access!.Create);
        Assert.True(user.Privileges[1].Access!.Read);
        Assert.True(user.Privileges[1].Access!.Modify);
        Assert.False(user.Privileges[1].Access!.Delete);
        Assert.True(user.Privileges[1].Access!.Approve);
    }

    [Fact]
    public async Task AuthenticationService_Bff_SuccessfulLogin_ShouldSetSessionAndToken()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["RememberMe:Enabled"] = "true"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var handler = new TestHttpMessageHandler
        {
            Sender = req =>
            {
                var loginResponse = new LoginResponseDto
                {
                    Success = true,
                    User = new User
                    {
                        Username = "jdoe",
                        Name = "John Doe",
                        Privileges = new List<Privilege>
                        {
                            new() { Id = 1, IdParent = 0, Codename = "opt1", Title = "Arranque 1" }
                        }
                    }
                };
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(loginResponse)
                };
                response.Headers.Add("X-Session-Token", "vogel.test_token_payload.signature");
                return response;
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://vogel.bff.test/") };
        var sessionStorage = new MockSecureSessionStorage();
        var sessionValidation = new MockSessionValidation();
        var sessionState = new SessionState();
        var logger = new MockClientLogger();

        var authService = new AuthenticationService(
            httpClient, sessionState, sessionStorage, sessionValidation, logger, config);

        // Act
        var (success, errorMsg) = await authService.LoginAsync("jdoe", "SecurePassword123!");

        // Assert
        Assert.True(success);
        Assert.Empty(errorMsg);
        Assert.True(sessionState.IsAuthenticated);
        Assert.Equal("jdoe", sessionState.CurrentUser?.Username);
        Assert.Single(sessionState.CurrentUser!.Privileges);

        // Validar almacenamiento cifrado del token y sesión
        var savedToken = await sessionStorage.GetItemDecryptedAsync(AuthenticationService.BackendTokenKey);
        Assert.Equal("vogel.test_token_payload.signature", savedToken);

        var savedUser = await sessionStorage.GetItemDecryptedObjectAsync<User>(AuthenticationService.SessionUserKey);
        Assert.NotNull(savedUser);
        Assert.Equal("jdoe", savedUser!.Username);
    }

    [Fact]
    public async Task AuthenticationService_Bff_InvalidCredentials_ShouldReturnError()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var handler = new TestHttpMessageHandler
        {
            Sender = req => new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent.Create(new LoginResponseDto
                {
                    Success = false,
                    Message = "Usuario o Clave incorrectos"
                })
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://vogel.bff.test/") };
        var sessionStorage = new MockSecureSessionStorage();
        var sessionState = new SessionState();

        var authService = new AuthenticationService(
            httpClient, sessionState, sessionStorage,
            new MockSessionValidation(), new MockClientLogger(), config);

        // Act
        var (success, errorMsg) = await authService.LoginAsync("jdoe", "WrongPass");

        // Assert
        Assert.False(success);
        Assert.Equal("Usuario o Clave incorrectos", errorMsg);
        Assert.False(sessionState.IsAuthenticated);
    }

    [Fact]
    public async Task AuthenticationService_Bff_DisabledUser_ShouldReturn403Message()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var handler = new TestHttpMessageHandler
        {
            Sender = req => new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = JsonContent.Create(new LoginResponseDto
                {
                    Success = false,
                    Message = "El usuario se encuentra deshabilitado en el sistema"
                })
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://vogel.bff.test/") };
        var sessionStorage = new MockSecureSessionStorage();
        var sessionState = new SessionState();

        var authService = new AuthenticationService(
            httpClient, sessionState, sessionStorage,
            new MockSessionValidation(), new MockClientLogger(), config);

        // Act
        var (success, errorMsg) = await authService.LoginAsync("disabled_user", "SecurePassword123!");

        // Assert
        Assert.False(success);
        Assert.Equal("El usuario se encuentra deshabilitado en el sistema", errorMsg);
        Assert.False(sessionState.IsAuthenticated);
    }

    [Fact]
    public async Task AuthenticationService_Bff_NoPrivilegesUser_ShouldRejectLogin()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var handler = new TestHttpMessageHandler
        {
            Sender = req => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = JsonContent.Create(new LoginResponseDto
                {
                    Success = false,
                    Message = "El usuario no cuenta con privilegios en el sistema"
                })
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://vogel.bff.test/") };
        var sessionStorage = new MockSecureSessionStorage();
        var sessionState = new SessionState();

        var authService = new AuthenticationService(
            httpClient, sessionState, sessionStorage,
            new MockSessionValidation(), new MockClientLogger(), config);

        // Act
        var (success, errorMsg) = await authService.LoginAsync("nopriv_user", "SecurePassword123!");

        // Assert
        Assert.False(success);
        Assert.Equal("El usuario no cuenta con privilegios en el sistema", errorMsg);
        Assert.False(sessionState.IsAuthenticated);
    }

    [Fact]
    public async Task AuthenticationService_Bff_MockResponse_ShouldPreserveOriginalFlow()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var handler = new TestHttpMessageHandler
        {
            Sender = req =>
            {
                var loginResponse = new LoginResponseDto
                {
                    Success = true,
                    User = new User
                    {
                        Username = "admin",
                        Name = "Administrador",
                        Privileges = new List<Privilege>
                        {
                            new() { Id = 1, IdParent = 0, Codename = "admin", Title = "Administración" }
                        }
                    }
                };
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(loginResponse)
                };
                response.Headers.Add("X-Session-Token", "vogel.mock_session_token.123");
                return response;
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://vogel.bff.test/") };
        var sessionStorage = new MockSecureSessionStorage();
        var sessionState = new SessionState();

        var authService = new AuthenticationService(
            httpClient, sessionState, sessionStorage,
            new MockSessionValidation(), new MockClientLogger(), config);

        // Act
        var (success, errorMsg) = await authService.LoginAsync("admin", "Vogel2026*");

        // Assert
        Assert.True(success);
        Assert.Empty(errorMsg);
        Assert.True(sessionState.IsAuthenticated);
        Assert.Equal("admin", sessionState.CurrentUser?.Username);
    }
}
