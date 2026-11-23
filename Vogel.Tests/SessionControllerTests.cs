using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Vogel.Server.Controllers;
using Vogel.Server.Services;
using Vogel.Shared.DTOs;

namespace Vogel.Tests;

/// <summary>
/// Pruebas unitarias para SessionController (/api/session/validate).
/// Verifica que en modo Api delegue en IAuthProviderService (MicroID) en lugar del cliente legado.
/// </summary>
public class SessionControllerTests
{
    private static SessionController CreateController(
        string authProvider,
        IAuthProviderService? authProviderService,
        string? bearerToken = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthProvider"] = authProvider,
                ["SessionValidation:TtlSeconds"] = "10"
            })
            .Build();

        var controller = new SessionController(
            new ServerSessionInfo(),
            config,
            authProviderService,
            NullLogger<SessionController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            controller.ControllerContext.HttpContext.Request.Headers.Authorization = $"Bearer {bearerToken}";
        }

        return controller;
    }

    [Fact]
    public async Task Validate_ApiMode_WithValidToken_ShouldDelegateToAuthProviderAndReturnValidTrue()
    {
        var stubProvider = new StubAuthProviderService(new AuthPrivilegesResult(
            true,
            StatusCodes.Status200OK,
            new BackendPrivilegesResponseDto("carlos.gomez", new List<BackendPrivilegeItemDto>()),
            null));

        var controller = CreateController("Api", stubProvider, bearerToken: "valid-microid-jwt");

        var actionResult = await controller.Validate();

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<SessionValidationDto>(ok.Value);
        Assert.True(dto.Valid);
        Assert.Equal(1, stubProvider.CallCount);
        Assert.Equal("valid-microid-jwt", stubProvider.LastToken);
    }

    [Fact]
    public async Task Validate_ApiMode_WithRejectedToken_ShouldReturnValidFalse()
    {
        var stubProvider = new StubAuthProviderService(new AuthPrivilegesResult(
            false,
            StatusCodes.Status401Unauthorized,
            null,
            new BackendErrorDto(StatusCodes.Status401Unauthorized, "Sesión expirada.")));

        var controller = CreateController("Api", stubProvider, bearerToken: "expired-jwt");

        var actionResult = await controller.Validate();

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<SessionValidationDto>(ok.Value);
        Assert.False(dto.Valid);
        Assert.Equal(1, stubProvider.CallCount);
    }

    [Fact]
    public async Task Validate_MockMode_ShouldReturnValidTrueWithoutCallingAuthProvider()
    {
        var stubProvider = new StubAuthProviderService(new AuthPrivilegesResult(
            false,
            StatusCodes.Status502BadGateway,
            null,
            null));

        var controller = CreateController("Mock", stubProvider, bearerToken: "mock-token");

        var actionResult = await controller.Validate();

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<SessionValidationDto>(ok.Value);
        Assert.True(dto.Valid);
        Assert.Equal(0, stubProvider.CallCount);
    }

    private sealed class StubAuthProviderService : IAuthProviderService
    {
        private readonly AuthPrivilegesResult _privilegesResult;

        public StubAuthProviderService(AuthPrivilegesResult privilegesResult)
        {
            _privilegesResult = privilegesResult;
        }

        public int CallCount { get; private set; }
        public string? LastToken { get; private set; }

        public Task<AuthLoginResult> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AuthPrivilegesResult> GetPrivilegesAsync(string token, string? username = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastToken = token;
            return Task.FromResult(_privilegesResult);
        }

        public Task<AuthChangePasswordResult> ChangePasswordAsync(string? token, ChangePasswordRequestDto request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
