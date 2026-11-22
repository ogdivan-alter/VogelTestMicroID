using System.Security.Claims;
using Vogel.Client.Services;
using Vogel.Shared.DTOs;
using Vogel.Shared.Models;
using Vogel.Shared.Services;
using Xunit;

namespace Vogel.Tests;

/// <summary>
/// Pruebas de integración E2E para el flujo completo de la Fase 2 y seguridad declarativa.
/// Corresponde al Sprint 7 y a las secciones 3.2, 4.1 y 4.2 del Requerimiento Maestro.
/// </summary>
public class Sprint7IntegrationTests
{
    private readonly IMockDataService _mockDataService;
    private readonly SessionState _sessionState;

    public Sprint7IntegrationTests()
    {
        _mockDataService = new MockDataService();
        _sessionState = new SessionState();
    }

    [Fact]
    public async Task Phase2_FullFunctionalFlow_AllSixSteps_SucceedSeamlessly()
    {
        // -------------------------------------------------------------
        // PASO 1: Autenticación exitosa (credenciales admin / Vogel2026*)
        // -------------------------------------------------------------
        var loginRequest = new LoginRequestDto
        {
            Username = "admin",
            Password = "Vogel2026*"
        };

        var loginResult = await _mockDataService.AuthenticateAsync(loginRequest);
        Assert.True(loginResult.Success);
        Assert.NotNull(loginResult.User);
        Assert.Equal("Único", loginResult.User.BaseRole);

        // Establecer sesión activa
        _sessionState.SetUser(loginResult.User);
        Assert.True(_sessionState.IsAuthenticated);

        // -------------------------------------------------------------
        // PASO 2: Carga de Vista Principal (MainView)
        // -------------------------------------------------------------
        // PASO 2: Consulta de Opciones Raíz (Administración)
        // -------------------------------------------------------------
        var rootPrivileges = await _mockDataService.GetRootPrivilegesAsync();
        Assert.Single(rootPrivileges);
        Assert.Contains(rootPrivileges, p => p.Title == "Administración");

        // -------------------------------------------------------------
        // PASO 3: Entrada a Subopciones de Administración (IdParent = 1)
        // -------------------------------------------------------------
        var subAdmin = await _mockDataService.GetSubPrivilegesAsync(1);
        Assert.Equal(3, subAdmin.Count);
        var subMenuSec = subAdmin.FirstOrDefault(p => p.Title == "Menú Secundario");
        Assert.NotNull(subMenuSec);
        Assert.Equal("menu/sub_2_1", subMenuSec.Action);
        Assert.NotNull(subMenuSec.Access);
        Assert.True(subMenuSec.Access.Read);
        Assert.True(subMenuSec.Access.Execute);

        // -------------------------------------------------------------
        // PASO 4: Salida / Retorno a MainView
        // -------------------------------------------------------------
        // Al regresar a '/', el menú principal vuelve a estar disponible
        Assert.True(_sessionState.IsAuthenticated);

        // -------------------------------------------------------------
        // PASO 5: Entrada a Menú Secundario ("sub_2_1" - IdParent = 201) y WorkViewFrame
        // -------------------------------------------------------------
        // En SecondaryMenu (IdParent = 201), se despliegan las tarjetas por grupo
        var secondaryPrivileges = await _mockDataService.GetSubPrivilegesAsync(201);
        Assert.Equal(4, secondaryPrivileges.Count);

        var tarjetaGestion = secondaryPrivileges.FirstOrDefault(p => p.Title == "Gestión Principal");
        Assert.NotNull(tarjetaGestion);
        Assert.Equal("work/blockwork-demo", tarjetaGestion.Action);

        // Carga de WorkViewFrame con BlockWork "blockwork-demo"
        var blockWork = await _mockDataService.GetBlockWorkAsync("blockwork-demo");
        Assert.NotNull(blockWork);
        Assert.True(blockWork.ShowInfo);
        Assert.Equal("menu/sub_2_1", blockWork.ActionSet.Exit);

        // Consulta de datos de detalle paginados para ListViewFrame (Página 1: 10 de 35)
        var (items, totalCount) = await _mockDataService.GetDetailDataPagedAsync("blockwork-demo", 1, 10);
        Assert.Equal(35, totalCount);
        Assert.Equal(10, items.Count);

        // -------------------------------------------------------------
        // PASO 6: Salida de WorkViewFrame mediante ActionSet.Exit
        // -------------------------------------------------------------
        var exitRoute = blockWork.ActionSet.Exit;
        Assert.Equal("menu/sub_2_1", exitRoute);
    }

    [Fact]
    public void DeclarativeSecurity_PrivilegeFunction_ReturnsUnicoOrNegado()
    {
        // 1. Sin sesión: retorna "Negado"
        Assert.Equal("Negado", _sessionState.PrivilegeFunction("blockwork_demo"));

        // 2. Con sesión de usuario admin que posee el privilegio
        _sessionState.SetUser(new User
        {
            Username = "admin",
            BaseRole = "Único",
            Privileges = new List<Privilege>
            {
                new Privilege { Id = 2011, Codename = "blockwork_demo", Title = "Gestión Principal" },
                new Privilege { Id = 101, Codename = "entre_aqui", Title = "Entre Aquí" }
            }
        });

        // Privilegios otorgados retornan "Único" (tanto exacto como normalizado)
        Assert.Equal("Único", _sessionState.PrivilegeFunction("blockwork_demo"));
        Assert.Equal("Único", _sessionState.PrivilegeFunction("blockwork-demo"));
        Assert.Equal("Único", _sessionState.PrivilegeFunction("entre_aqui"));

        // Privilegios no otorgados retornan "Negado"
        Assert.Equal("Negado", _sessionState.PrivilegeFunction("privilegio_no_existente"));
        Assert.Equal("Negado", _sessionState.PrivilegeFunction("super_admin_secret"));
    }

    [Fact]
    public async Task CustomAuthenticationStateProvider_ClaimsPrincipal_ReflectsRoleUnico()
    {
        // Arrange
        using var authProvider = new CustomAuthenticationStateProvider(_sessionState);

        // Estado inicial anónimo
        var initialAuthState = await authProvider.GetAuthenticationStateAsync();
        Assert.False(initialAuthState.User.Identity?.IsAuthenticated);

        // Act: Autenticación
        _sessionState.SetUser(new User
        {
            Username = "admin",
            BaseRole = "Único"
        });

        var authState = await authProvider.GetAuthenticationStateAsync();

        // Assert
        Assert.NotNull(authState.User.Identity);
        Assert.True(authState.User.Identity.IsAuthenticated);
        Assert.Equal("admin", authState.User.Identity.Name);
        Assert.True(authState.User.IsInRole("Único"));

        // Act: Cierre de sesión
        _sessionState.ClearUser();
        var loggedOutState = await authProvider.GetAuthenticationStateAsync();
        Assert.False(loggedOutState.User.Identity?.IsAuthenticated);
    }
}
