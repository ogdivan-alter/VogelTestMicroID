using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Vogel.Server.Services;
using Vogel.Shared.DTOs;
using Vogel.Shared.Enums;
using Xunit;

namespace Vogel.Tests;

/// <summary>
/// Pruebas unitarias para PrivilegeAuthorizationService (Fase 4 - T4.1).
/// Verifica autorización granular RBAC, normalización de codename, comportamiento fail-closed
/// y propagación de errores del proveedor de autenticación.
/// </summary>
public class PrivilegeAuthorizationServiceTests
{
    private static PrivilegeAuthorizationService CreateService(IAuthProviderService authProvider) =>
        new(authProvider, NullLogger<PrivilegeAuthorizationService>.Instance);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AuthorizeAsync_WhenTokenEmptyOrWhitespace_ShouldReturn401WithoutCallingProvider(string token)
    {
        var authProvider = new StubAuthProviderService();
        var service = CreateService(authProvider);

        var result = await service.AuthorizeAsync(token, "adm_priv", PrivilegeAction.Read);

        Assert.False(result.IsAuthorized);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.NotNull(result.Error);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.Error!.Status);
        Assert.Equal(0, authProvider.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AuthorizeAsync_WhenCodenameEmpty_ShouldReturn403FailClosedWithoutCallingProvider(string codename)
    {
        var authProvider = new StubAuthProviderService();
        var service = CreateService(authProvider);

        var result = await service.AuthorizeAsync("valid-token", codename, PrivilegeAction.Read);

        Assert.False(result.IsAuthorized);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.NotNull(result.Error);
        Assert.Equal(StatusCodes.Status403Forbidden, result.Error!.Status);
        Assert.Equal(0, authProvider.Calls);
    }

    [Theory]
    [InlineData(PrivilegeAction.Read)]
    [InlineData(PrivilegeAction.Create)]
    [InlineData(PrivilegeAction.Modify)]
    [InlineData(PrivilegeAction.Delete)]
    [InlineData(PrivilegeAction.Validate)]
    [InlineData(PrivilegeAction.Execute)]
    [InlineData(PrivilegeAction.Approve)]
    public async Task AuthorizeAsync_WhenRequiredActionGranted_ShouldReturnAuthorized(PrivilegeAction action)
    {
        var access = new BackendAccessDto(
            Read: action == PrivilegeAction.Read,
            Modify: action == PrivilegeAction.Modify,
            Write: action == PrivilegeAction.Create,
            Delete: action == PrivilegeAction.Delete,
            Approve: action == PrivilegeAction.Approve,
            Validate: action == PrivilegeAction.Validate,
            Execute: action == PrivilegeAction.Execute);

        var authProvider = new StubAuthProviderService
        {
            Result = CreateSuccessResult("adm_priv", access)
        };
        var service = CreateService(authProvider);

        var result = await service.AuthorizeAsync("jwt-token", "adm_priv", action);

        Assert.True(result.IsAuthorized);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Null(result.Error);
        Assert.Equal(1, authProvider.Calls);
        Assert.Equal("jwt-token", authProvider.LastToken);
    }

    [Theory]
    [InlineData("ADM_PRIV")]
    [InlineData("adm-priv")]
    [InlineData("Adm-Priv")]
    [InlineData(" adm_priv ")]
    public async Task AuthorizeAsync_WhenCodenameDiffersInCaseOrSeparator_ShouldNormalizeAndAuthorize(string requestedCodename)
    {
        var authProvider = new StubAuthProviderService
        {
            Result = CreateSuccessResult(
                "adm_priv",
                new BackendAccessDto(Read: true, Modify: false, Write: false, Delete: false, Approve: false))
        };
        var service = CreateService(authProvider);

        var result = await service.AuthorizeAsync("jwt-token", requestedCodename, PrivilegeAction.Read);

        Assert.True(result.IsAuthorized);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
    }

    [Theory]
    [InlineData(PrivilegeAction.Read)]
    [InlineData(PrivilegeAction.Create)]
    [InlineData(PrivilegeAction.Modify)]
    [InlineData(PrivilegeAction.Delete)]
    [InlineData(PrivilegeAction.Validate)]
    [InlineData(PrivilegeAction.Execute)]
    [InlineData(PrivilegeAction.Approve)]
    public async Task AuthorizeAsync_WhenRequiredActionDenied_ShouldReturn403Forbidden(PrivilegeAction action)
    {
        var access = new BackendAccessDto(
            Read: action != PrivilegeAction.Read,
            Modify: action != PrivilegeAction.Modify,
            Write: action != PrivilegeAction.Create,
            Delete: action != PrivilegeAction.Delete,
            Approve: action != PrivilegeAction.Approve,
            Validate: action != PrivilegeAction.Validate,
            Execute: action != PrivilegeAction.Execute);

        var authProvider = new StubAuthProviderService
        {
            Result = CreateSuccessResult("adm_priv", access)
        };
        var service = CreateService(authProvider);

        var result = await service.AuthorizeAsync("jwt-token", "adm_priv", action);

        Assert.False(result.IsAuthorized);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.NotNull(result.Error);
        Assert.Equal(StatusCodes.Status403Forbidden, result.Error!.Status);
        Assert.Contains("permisos suficientes", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenPrivilegeAbsent_ShouldReturn403FailClosed()
    {
        var authProvider = new StubAuthProviderService
        {
            Result = CreateSuccessResult(
                "other_module",
                new BackendAccessDto(Read: true, Modify: true, Write: true, Delete: true, Approve: true, Validate: true, Execute: true))
        };
        var service = CreateService(authProvider);

        var result = await service.AuthorizeAsync("jwt-token", "adm_priv", PrivilegeAction.Read);

        Assert.False(result.IsAuthorized);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.NotNull(result.Error);
        Assert.Equal(StatusCodes.Status403Forbidden, result.Error!.Status);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenAccessIsNull_ShouldReturn403FailClosed()
    {
        var authProvider = new StubAuthProviderService
        {
            Result = new AuthPrivilegesResult(
                true,
                StatusCodes.Status200OK,
                new BackendPrivilegesResponseDto(
                    "user1",
                    new List<BackendPrivilegeItemDto>
                    {
                        new(1, 204, "adm_priv", "Seguridad", "Admin Privilegios", "/admsec/priv", "icons/roles.svg", null!)
                    }),
                null)
        };
        var service = CreateService(authProvider);

        var result = await service.AuthorizeAsync("jwt-token", "adm_priv", PrivilegeAction.Read);

        Assert.False(result.IsAuthorized);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Theory]
    [InlineData(StatusCodes.Status401Unauthorized, "Token expirado.")]
    [InlineData(StatusCodes.Status403Forbidden, "Acceso denegado por IdP.")]
    [InlineData(StatusCodes.Status502BadGateway, "Error de comunicación con IdP.")]
    public async Task AuthorizeAsync_WhenProviderFails_ShouldPropagateStatusAndFailClosed(int statusCode, string message)
    {
        var authProvider = new StubAuthProviderService
        {
            Result = new AuthPrivilegesResult(false, statusCode, null, new BackendErrorDto(statusCode, message))
        };
        var service = CreateService(authProvider);

        var result = await service.AuthorizeAsync("jwt-token", "adm_priv", PrivilegeAction.Read);

        Assert.False(result.IsAuthorized);
        Assert.Equal(statusCode, result.StatusCode);
        Assert.NotNull(result.Error);
        Assert.Equal(statusCode, result.Error!.Status);
        Assert.Equal(message, result.Error.Message);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenProviderReturns200WithNullData_ShouldReturn502FailClosed()
    {
        var authProvider = new StubAuthProviderService
        {
            Result = new AuthPrivilegesResult(true, StatusCodes.Status200OK, null, null)
        };
        var service = CreateService(authProvider);

        var result = await service.AuthorizeAsync("jwt-token", "adm_priv", PrivilegeAction.Read);

        Assert.False(result.IsAuthorized);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenProviderThrowsException_ShouldReturn502FailClosed()
    {
        var authProvider = new StubAuthProviderService
        {
            ExceptionToThrow = new InvalidOperationException("Fallo inesperado de red")
        };
        var service = CreateService(authProvider);

        var result = await service.AuthorizeAsync("jwt-token", "adm_priv", PrivilegeAction.Read);

        Assert.False(result.IsAuthorized);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.NotNull(result.Error);
        Assert.Equal(StatusCodes.Status502BadGateway, result.Error!.Status);
    }

    [Theory]
    [InlineData("/admsec/priv")]
    [InlineData("admsec/priv")]
    [InlineData("/ADMSEC/PRIV/")]
    public async Task AuthorizeByActionAsync_WithAnyCustomCodename_ShouldAuthorizeByRouteAction(string requestedAction)
    {
        var authProvider = new StubAuthProviderService
        {
            Result = CreateSuccessResult("codename_personalizado_en_microid", new BackendAccessDto(Read: true, Write: true, Modify: false, Delete: false, Validate: false, Execute: false, Approve: false))
        };
        var service = CreateService(authProvider);

        var readResult = await service.AuthorizeByActionAsync("jwt-token", requestedAction, PrivilegeAction.Read);
        var createResult = await service.AuthorizeByActionAsync("jwt-token", requestedAction, PrivilegeAction.Create);
        var modifyResult = await service.AuthorizeByActionAsync("jwt-token", requestedAction, PrivilegeAction.Modify);

        Assert.True(readResult.IsAuthorized);
        Assert.True(createResult.IsAuthorized);
        Assert.False(modifyResult.IsAuthorized);
        Assert.Equal(StatusCodes.Status403Forbidden, modifyResult.StatusCode);
    }

    [Fact]
    public async Task AuthorizeByActionAsync_WhenRouteActionNotFound_ShouldReturn403FailClosed()
    {
        var authProvider = new StubAuthProviderService
        {
            Result = CreateSuccessResult("codename_cualquiera", new BackendAccessDto(Read: true, Write: true, Modify: true, Delete: true, Validate: false, Execute: false, Approve: false))
        };
        var service = CreateService(authProvider);

        var result = await service.AuthorizeByActionAsync("jwt-token", "/otra/ruta", PrivilegeAction.Read);

        Assert.False(result.IsAuthorized);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    private static AuthPrivilegesResult CreateSuccessResult(string codename, BackendAccessDto access) =>
        new(
            true,
            StatusCodes.Status200OK,
            new BackendPrivilegesResponseDto(
                "test_user",
                new List<BackendPrivilegeItemDto>
                {
                    new(1, 204, codename, "Seguridad", "Administración de Privilegios", "/admsec/priv", "icons/roles.svg", access)
                }),
            null);

    private sealed class StubAuthProviderService : IAuthProviderService
    {
        public int Calls { get; private set; }
        public string? LastToken { get; private set; }
        public AuthPrivilegesResult Result { get; set; } =
            new(false, StatusCodes.Status401Unauthorized, null, new BackendErrorDto(StatusCodes.Status401Unauthorized, "Unauthorized"));
        public Exception? ExceptionToThrow { get; set; }

        public Task<AuthLoginResult> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AuthPrivilegesResult> GetPrivilegesAsync(
            string token,
            string? username = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastToken = token;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Result);
        }

        public Task<AuthChangePasswordResult> ChangePasswordAsync(
            string? token,
            ChangePasswordRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
