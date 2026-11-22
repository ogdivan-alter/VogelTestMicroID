using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Vogel.Server.Controllers;
using Vogel.Server.Services;
using Vogel.Shared.DTOs;
using Vogel.Shared.Services;
using Xunit;

namespace Vogel.Tests;

/// <summary>
/// Pruebas unitarias para la arquitectura BFF (Backend for Frontend) gestionada por Vogel.Server.
/// Valida el aislamiento del frontend, la intermediación de autenticación y la orquestación de lógica de negocio.
/// </summary>
public class BffArchitectureTests
{
    private class FakeBackendApiService : IBackendApiService
    {
        public BackendLoginResponseDto? LoginResponse { get; set; }
        public string? TokenResponse { get; set; }
        public BackendErrorDto? LoginError { get; set; }
        public int LoginStatusCode { get; set; } = 200;

        public BackendPrivilegesResponseDto? PrivilegesResponse { get; set; }
        public BackendErrorDto? PrivilegesError { get; set; }
        public int PrivilegesStatusCode { get; set; } = 200;

        public bool ActionSuccess { get; set; } = true;
        public object? ActionData { get; set; }
        public string? ActionError { get; set; }
        public int ActionStatusCode { get; set; } = 200;
        public string? LastActionExecuted { get; private set; }

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

        public Task<(bool Success, object? Data, string? Error, int StatusCode)> ExecuteBlockWorkActionAsync(
            string codename, string action, object? payload = null, string? token = null, CancellationToken cancellationToken = default)
        {
            LastActionExecuted = action;
            object? data = ActionData ?? new { executed = true, action, codename };
            return Task.FromResult<(bool Success, object? Data, string? Error, int StatusCode)>((ActionSuccess, data, ActionError, ActionStatusCode));
        }
    }

    [Fact]
    public async Task AuthController_Login_MockMode_ValidCredentials_ShouldReturnOkWithTokenHeader()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?> { ["AuthProvider"] = "Mock" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var mockData = new MockDataService();
        var fakeBackend = new FakeBackendApiService();
        var controller = new AuthController(fakeBackend, mockData, config, NullLogger<AuthController>.Instance);

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new LoginRequestDto { Username = "admin", Password = "Vogel2026*" };

        // Act
        var result = await controller.Login(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseDto = Assert.IsType<LoginResponseDto>(okResult.Value);
        Assert.Equal("admin", responseDto.User?.Username);

        Assert.True(httpContext.Response.Headers.ContainsKey("X-Session-Token"));
        Assert.StartsWith("vogel.mock_session_token.", httpContext.Response.Headers["X-Session-Token"].ToString());
    }

    [Fact]
    public async Task AuthController_Login_MockMode_InvalidCredentials_ShouldReturnUnauthorized()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?> { ["AuthProvider"] = "Mock" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var mockData = new MockDataService();
        var fakeBackend = new FakeBackendApiService();
        var controller = new AuthController(fakeBackend, mockData, config, NullLogger<AuthController>.Instance);

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var request = new LoginRequestDto { Username = "admin", Password = "ClaveInvalida" };

        // Act
        var result = await controller.Login(request);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task AuthController_Login_ApiMode_DelegatesToBackendApiService()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?> { ["AuthProvider"] = "Api" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var mockData = new MockDataService();
        var fakeBackend = new FakeBackendApiService
        {
            LoginResponse = new BackendLoginResponseDto("jdoe", "John Doe", "/avatars/jdoe.png", "INTERNA", true),
            TokenResponse = "remote.session.token.jwt",
            LoginStatusCode = 200,
            PrivilegesResponse = new BackendPrivilegesResponseDto("jdoe", new List<BackendPrivilegeItemDto>
            {
                new(0, 1, "menu_1", "General", "Inicio", "/home", "/icons/menu1.svg", new BackendAccessDto(true, false, false, false, false))
            })
        };
        var controller = new AuthController(fakeBackend, mockData, config, NullLogger<AuthController>.Instance);

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new LoginRequestDto { Username = "jdoe", Password = "SecurePassword123!" };

        // Act
        var result = await controller.Login(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseDto = Assert.IsType<LoginResponseDto>(okResult.Value);
        Assert.Equal("jdoe", responseDto.User?.Username);
        Assert.Equal("remote.session.token.jwt", httpContext.Response.Headers["X-Session-Token"].ToString());
    }

    [Fact]
    public async Task AuthController_Privileges_WithoutToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?> { ["AuthProvider"] = "Api" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var controller = new AuthController(new FakeBackendApiService(), new MockDataService(), config, NullLogger<AuthController>.Instance);

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        // Act
        var result = await controller.Privileges();

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task AuthController_Privileges_ApiMode_WithToken_ShouldReturnBackendPrivileges()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?> { ["AuthProvider"] = "Api" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var fakeBackend = new FakeBackendApiService
        {
            PrivilegesResponse = new BackendPrivilegesResponseDto("jdoe", new List<BackendPrivilegeItemDto>
            {
                new(0, 1, "menu_1", "General", "Inicio", "/home", "/icon.svg", new BackendAccessDto(true, false, false, false, false))
            })
        };
        var controller = new AuthController(fakeBackend, new MockDataService(), config, NullLogger<AuthController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer test.valid.token";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var result = await controller.Privileges();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var privilegesData = Assert.IsType<BackendPrivilegesResponseDto>(okResult.Value);
        Assert.Equal("jdoe", privilegesData.Username);
        Assert.Single(privilegesData.Privileges);
    }

    [Fact]
    public void AuthController_Logout_ShouldReturnOk()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?> { ["AuthProvider"] = "Mock" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var controller = new AuthController(new FakeBackendApiService(), new MockDataService(), config, NullLogger<AuthController>.Instance);

        // Act
        var result = controller.Logout();

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task BlockWorkController_ApiMode_DelegatesToBackendApiService()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?> { ["AuthProvider"] = "Api" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var fakeBackend = new FakeBackendApiService();
        var controller = new BlockWorkController(fakeBackend, config, NullLogger<BlockWorkController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer token123";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var result = await controller.ApplyMaster("blockwork-demo");

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("ApplyMaster", fakeBackend.LastActionExecuted);
    }

    [Fact]
    public async Task BlockWorkController_MockMode_ReturnsEmulatedResponse()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?> { ["AuthProvider"] = "Mock" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var controller = new BlockWorkController(new FakeBackendApiService(), config, NullLogger<BlockWorkController>.Instance);

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        // Act
        var result = await controller.Capture("blockwork-demo");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }
}
