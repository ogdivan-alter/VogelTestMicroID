using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Vogel.Client.Services;
using Vogel.Server.Services;
using Vogel.Shared.DTOs;
using Vogel.Shared.Enums;
using Vogel.Shared.Models;
using Vogel.Shared.Services;
using Xunit;

namespace Vogel.Tests;

/// <summary>
/// Pruebas unitarias para SessionState (HasPrivilegeAccess / GetPrivilege) y
/// MockAuthProviderService + MockSessionStore con perfiles RBAC diferenciados (Fase 4 - T4.3).
/// </summary>
public class SessionStateAndMockRbacTests
{
    // ==================== SessionState ====================

    [Fact]
    public void SessionState_WhenNotAuthenticated_ShouldFailClosedForAllActions()
    {
        var sessionState = new SessionState();

        Assert.Null(sessionState.GetPrivilege("adm_priv"));
        Assert.False(sessionState.HasPrivilegeAccess("adm_priv", PrivilegeAction.Read));
        Assert.False(sessionState.HasPrivilegeAccess("adm_priv", PrivilegeAction.Create));
        Assert.False(sessionState.HasPrivilegeAccess("adm_priv", PrivilegeAction.Modify));
        Assert.False(sessionState.HasPrivilegeAccess("adm_priv", PrivilegeAction.Delete));
    }

    [Fact]
    public void SessionState_WhenPrivilegeAbsentOrAccessNull_ShouldFailClosed()
    {
        var sessionState = new SessionState();
        sessionState.SetUser(new User
        {
            Username = "user_no_access",
            Privileges = new List<Privilege>
            {
                new()
                {
                    Id = 204,
                    Codename = "adm_priv",
                    Title = "Administración de Privilegios",
                    Access = null
                }
            }
        });

        Assert.NotNull(sessionState.GetPrivilege("adm_priv"));
        Assert.Null(sessionState.GetPrivilege("missing_priv"));
        Assert.False(sessionState.HasPrivilegeAccess("adm_priv", PrivilegeAction.Read));
        Assert.False(sessionState.HasPrivilegeAccess("missing_priv", PrivilegeAction.Read));
    }

    [Theory]
    [InlineData("adm_priv")]
    [InlineData("ADM_PRIV")]
    [InlineData("adm-priv")]
    [InlineData("Adm-Priv")]
    public void SessionState_GetPrivilegeAndHasPrivilegeAccess_ShouldNormalizeCodename(string codename)
    {
        var sessionState = new SessionState();
        sessionState.SetUser(new User
        {
            Username = "rbac_user",
            Privileges = new List<Privilege>
            {
                new()
                {
                    Id = 204,
                    Codename = "adm_priv",
                    Title = "Administración de Privilegios",
                    Access = new PrivilegeAccess
                    {
                        Read = true,
                        Create = false,
                        Modify = false,
                        Delete = true,
                        Validate = true,
                        Execute = false,
                        Approve = true
                    }
                }
            }
        });

        Assert.NotNull(sessionState.GetPrivilege(codename));
        Assert.True(sessionState.HasPrivilegeAccess(codename, PrivilegeAction.Read));
        Assert.False(sessionState.HasPrivilegeAccess(codename, PrivilegeAction.Create));
        Assert.False(sessionState.HasPrivilegeAccess(codename, PrivilegeAction.Modify));
        Assert.True(sessionState.HasPrivilegeAccess(codename, PrivilegeAction.Delete));
        Assert.True(sessionState.HasPrivilegeAccess(codename, PrivilegeAction.Validate));
        Assert.False(sessionState.HasPrivilegeAccess(codename, PrivilegeAction.Execute));
        Assert.True(sessionState.HasPrivilegeAccess(codename, PrivilegeAction.Approve));
    }

    // ==================== MockAuthProviderService + MockSessionStore ====================

    [Theory]
    [InlineData("admin", "Vogel2026*", true, true, true, true)]
    [InlineData("readonly_user", "ReadOnly2026*", true, false, false, false)]
    [InlineData("read_delete_user", "ReadDelete2026*", true, false, false, true)]
    [InlineData("create_only_user", "CreateOnly2026*", false, true, false, false)]
    public async Task MockAuthProviderService_LoginAndGetPrivileges_ShouldReturnExpectedRbacMatrix(
        string username,
        string password,
        bool expectedRead,
        bool expectedCreate,
        bool expectedModify,
        bool expectedDelete)
    {
        var mockData = new MockDataService();
        var sessionStore = new MockSessionStore();
        var authProvider = new MockAuthProviderService(mockData, sessionStore, NullLogger<MockAuthProviderService>.Instance);

        var loginResult = await authProvider.LoginAsync(new LoginRequestDto
        {
            Username = username,
            Password = password
        });

        Assert.True(loginResult.Success);
        Assert.Equal(StatusCodes.Status200OK, loginResult.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(loginResult.Token));

        var privResult = await authProvider.GetPrivilegesAsync(loginResult.Token!);

        Assert.True(privResult.Success);
        Assert.NotNull(privResult.Data);
        Assert.Equal(username, privResult.Data!.Username);

        var admPriv = Assert.Single(privResult.Data.Privileges, p => p.Codename == "adm_priv");
        Assert.Equal(expectedRead, admPriv.Access.Read);
        Assert.Equal(expectedCreate, admPriv.Access.Write);
        Assert.Equal(expectedModify, admPriv.Access.Modify);
        Assert.Equal(expectedDelete, admPriv.Access.Delete);

        // Integración con PrivilegeAuthorizationService
        var authzService = new PrivilegeAuthorizationService(authProvider, NullLogger<PrivilegeAuthorizationService>.Instance);
        Assert.Equal(expectedRead, (await authzService.AuthorizeAsync(loginResult.Token!, "adm_priv", PrivilegeAction.Read)).IsAuthorized);
        Assert.Equal(expectedCreate, (await authzService.AuthorizeAsync(loginResult.Token!, "adm_priv", PrivilegeAction.Create)).IsAuthorized);
        Assert.Equal(expectedModify, (await authzService.AuthorizeAsync(loginResult.Token!, "adm_priv", PrivilegeAction.Modify)).IsAuthorized);
        Assert.Equal(expectedDelete, (await authzService.AuthorizeAsync(loginResult.Token!, "adm_priv", PrivilegeAction.Delete)).IsAuthorized);
    }

    [Fact]
    public async Task MockAuthProviderService_GetPrivileges_WithUnknownToken_ShouldReturn401FailClosed()
    {
        var mockData = new MockDataService();
        var sessionStore = new MockSessionStore();
        var authProvider = new MockAuthProviderService(mockData, sessionStore, NullLogger<MockAuthProviderService>.Instance);

        var result = await authProvider.GetPrivilegesAsync("vogel.mock_session_token.desconocido");

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Null(result.Data);
        Assert.NotNull(result.Error);
    }
}
