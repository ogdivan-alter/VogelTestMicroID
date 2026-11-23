using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vogel.Server.Configuration;
using Vogel.Server.Models.MicroId;
using Vogel.Server.Services;
using Vogel.Server.Services.MicroId;
using Vogel.Shared.DTOs;
using Vogel.Shared.Services;

namespace Vogel.Tests;

/// <summary>
/// Pruebas unitarias para MicroIdPrivilegeAdminService y MockPrivilegeAdminService (Fase 3 - T3.2).
/// Cubre creación, lectura individual (incluyendo validación de id_system -> 403), actualización,
/// inactivación, prevención de autorreferencia/ciclos y mapeo de errores.
/// </summary>
public class PrivilegeAdminServicesTests
{
    private static MicroIdOptions CreateOptions(int systemNumericId = 1, string systemId = "MICROID") => new()
    {
        BaseUrl = "http://localhost:8021",
        SystemId = systemId,
        SystemNumericId = systemNumericId,
        TimeoutSeconds = 30,
        CredRenovation = "MONTHLY"
    };

    private static MicroIdPrivilegeAdminService CreateMicroIdService(
        StubMicroIdApiClient apiClient,
        int systemNumericId = 1) =>
        new(apiClient, Options.Create(CreateOptions(systemNumericId)), NullLogger<MicroIdPrivilegeAdminService>.Instance);

    private static MockPrivilegeAdminService CreateMockService() =>
        new(new MockDataService(), NullLogger<MockPrivilegeAdminService>.Instance);

    // =========================================================================
    // MicroIdPrivilegeAdminService
    // =========================================================================

    [Fact]
    public async Task MicroId_GetPrivilegeAsync_WhenSystemMatches_ShouldReturn200AndMappedDto()
    {
        var apiClient = new StubMicroIdApiClient();
        apiClient.SetNextResponse(new MicroIdApiResult<MicroIdPrivilegeDetailResponse>(
            HttpStatusCode.OK,
            MicroIdResponseCodes.Success,
            "OK",
            new MicroIdPrivilegeDetailResponse(
                IdPrivilege: 20,
                IdSystem: 1,
                IdParent: 1,
                Codename: "adm_priv",
                GroupName: "SEGURIDAD",
                Title: "Administrar Privilegios",
                Action: "adm/priv",
                Icon: "icons/priv.svg",
                Status: "A",
                DateCreation: DateTimeOffset.UtcNow,
                UserCreation: "admin")));

        var service = CreateMicroIdService(apiClient, systemNumericId: 1);

        var result = await service.GetPrivilegeAsync("jwt-token", 20);

        Assert.True(result.Success);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(20, result.Data!.IdPrivilege);
        Assert.Equal(1, result.Data.IdSystem);
        Assert.Equal("adm_priv", result.Data.Codename);
        Assert.Equal("priv/get", apiClient.LastEndpoint);
        Assert.Equal("jwt-token", apiClient.LastBearerToken);
        var sentPayload = Assert.IsType<MicroIdGetPrivilegeRequest>(apiClient.LastPayload);
        Assert.Equal(20, sentPayload.IdPrivilege);
    }

    [Fact]
    public async Task MicroId_GetPrivilegeAsync_WhenSystemDiffers_ShouldReturn403Forbidden()
    {
        var apiClient = new StubMicroIdApiClient();
        apiClient.SetNextResponse(new MicroIdApiResult<MicroIdPrivilegeDetailResponse>(
            HttpStatusCode.OK,
            MicroIdResponseCodes.Success,
            "OK",
            new MicroIdPrivilegeDetailResponse(
                IdPrivilege: 99,
                IdSystem: 999,
                IdParent: 0,
                Codename: "other_sys_priv",
                GroupName: "OTRO",
                Title: "Privilegio de otro sistema",
                Action: "other/action",
                Icon: null,
                Status: "A",
                DateCreation: DateTimeOffset.UtcNow,
                UserCreation: "admin")));

        var service = CreateMicroIdService(apiClient, systemNumericId: 1);

        var result = await service.GetPrivilegeAsync("jwt-token", 99);

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Null(result.Data);
        Assert.NotNull(result.Error);
        Assert.Equal(StatusCodes.Status403Forbidden, result.Error!.Status);
    }

    [Theory]
    [InlineData(MicroIdResponseCodes.InactiveMaster, HttpStatusCode.UnprocessableEntity, StatusCodes.Status404NotFound)]
    [InlineData(MicroIdResponseCodes.AuthFailed, HttpStatusCode.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(MicroIdResponseCodes.IdpCommunication, HttpStatusCode.BadGateway, StatusCodes.Status502BadGateway)]
    [InlineData(MicroIdResponseCodes.InternalServer, HttpStatusCode.InternalServerError, StatusCodes.Status502BadGateway)]
    public async Task MicroId_GetPrivilegeAsync_ErrorMapping_ShouldReturnExpectedStatusCode(
        string microIdCode,
        HttpStatusCode httpStatus,
        int expectedStatusCode)
    {
        var apiClient = new StubMicroIdApiClient();
        apiClient.SetNextResponse(new MicroIdApiResult<MicroIdPrivilegeDetailResponse>(
            httpStatus,
            microIdCode,
            "raw error detail",
            null));

        var service = CreateMicroIdService(apiClient);

        var result = await service.GetPrivilegeAsync("jwt-token", 20);

        Assert.False(result.Success);
        Assert.Equal(expectedStatusCode, result.StatusCode);
        Assert.NotNull(result.Error);
        Assert.Equal(expectedStatusCode, result.Error!.Status);
    }

    [Fact]
    public async Task MicroId_CreatePrivilegeAsync_ShouldInjectSystemNumericIdTrimFieldsAndReturn201()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var apiClient = new StubMicroIdApiClient();
        apiClient.SetNextResponse(new MicroIdApiResult<MicroIdCreatePrivilegeResponse>(
            HttpStatusCode.OK,
            MicroIdResponseCodes.Success,
            "Created",
            new MicroIdCreatePrivilegeResponse(55, 7, "A", createdAt)));

        var service = CreateMicroIdService(apiClient, systemNumericId: 7);

        var result = await service.CreatePrivilegeAsync("jwt-create", new CreatePrivilegeRequestDto
        {
            IdParent = null,
            Codename = "  adm_new  ",
            GroupName = "  SEGURIDAD ",
            Title = " Nuevo Privilegio ",
            Action = " adm/new ",
            Icon = "   "
        });

        Assert.True(result.Success);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(55, result.Data!.IdPrivilege);
        Assert.Equal(7, result.Data.IdSystem);
        Assert.Equal("priv/create", apiClient.LastEndpoint);

        var sentPayload = Assert.IsType<MicroIdCreatePrivilegeRequest>(apiClient.LastPayload);
        Assert.Equal(7, sentPayload.IdSystem);
        Assert.Equal(0, sentPayload.IdParent);
        Assert.Equal("adm_new", sentPayload.Codename);
        Assert.Equal("SEGURIDAD", sentPayload.GroupName);
        Assert.Equal("Nuevo Privilegio", sentPayload.Title);
        Assert.Equal("adm/new", sentPayload.Action);
        Assert.Null(sentPayload.Icon);
    }

    [Theory]
    [InlineData(MicroIdResponseCodes.InvalidPayload, HttpStatusCode.BadRequest, StatusCodes.Status400BadRequest)]
    [InlineData(MicroIdResponseCodes.InactiveMaster, HttpStatusCode.UnprocessableEntity, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(MicroIdResponseCodes.SystemMismatch, HttpStatusCode.UnprocessableEntity, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(MicroIdResponseCodes.InternalServer, HttpStatusCode.InternalServerError, StatusCodes.Status502BadGateway)]
    public async Task MicroId_CreatePrivilegeAsync_ErrorMapping_ShouldReturnExpectedStatusCode(
        string microIdCode,
        HttpStatusCode httpStatus,
        int expectedStatusCode)
    {
        var apiClient = new StubMicroIdApiClient();
        apiClient.SetNextResponse(new MicroIdApiResult<MicroIdCreatePrivilegeResponse>(
            httpStatus,
            microIdCode,
            "error",
            null));

        var service = CreateMicroIdService(apiClient);

        var result = await service.CreatePrivilegeAsync("jwt-token", new CreatePrivilegeRequestDto
        {
            IdParent = 1,
            Codename = "adm_x",
            GroupName = "SEGURIDAD",
            Title = "Titulo",
            Action = "adm/x"
        });

        Assert.False(result.Success);
        Assert.Equal(expectedStatusCode, result.StatusCode);
    }

    [Fact]
    public async Task MicroId_UpdatePrivilegeAsync_SelfReference_ShouldReturn422WithoutCallingApi()
    {
        var apiClient = new StubMicroIdApiClient();
        var service = CreateMicroIdService(apiClient);

        var result = await service.UpdatePrivilegeAsync("jwt-token", 12, new UpdatePrivilegeRequestDto
        {
            IdParent = 12,
            Codename = "adm_self",
            GroupName = "SEGURIDAD",
            Title = "Self",
            Action = "adm/self"
        });

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        Assert.Equal(0, apiClient.CallCount);
    }

    [Fact]
    public async Task MicroId_UpdatePrivilegeAsync_WhenValid_ShouldCallPrivUpdateAndReturn200()
    {
        var modifiedAt = DateTimeOffset.UtcNow;
        var apiClient = new StubMicroIdApiClient();
        apiClient.EnqueueResponse(new MicroIdApiResult<MicroIdPrivilegeDetailResponse>(
            HttpStatusCode.OK,
            MicroIdResponseCodes.Success,
            "OK",
            new MicroIdPrivilegeDetailResponse(
                IdPrivilege: 12,
                IdSystem: 1,
                IdParent: 0,
                Codename: "adm_old",
                GroupName: "SEGURIDAD",
                Title: "Old",
                Action: "adm/old",
                Icon: null,
                Status: "A",
                DateCreation: DateTimeOffset.UtcNow,
                UserCreation: "admin")));
        apiClient.EnqueueResponse(new MicroIdApiResult<MicroIdUpdatePrivilegeResponse>(
            HttpStatusCode.OK,
            MicroIdResponseCodes.Success,
            "Updated",
            new MicroIdUpdatePrivilegeResponse(12, true, modifiedAt)));

        var service = CreateMicroIdService(apiClient, systemNumericId: 1);

        var result = await service.UpdatePrivilegeAsync("jwt-upd", 12, new UpdatePrivilegeRequestDto
        {
            IdParent = 1,
            Codename = " adm_upd ",
            GroupName = " SEGURIDAD ",
            Title = " Titulo Upd ",
            Action = " adm/upd ",
            Icon = "   "
        });

        Assert.True(result.Success);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.Updated);
        Assert.Equal(2, apiClient.CallCount);
        Assert.Equal("priv/update", apiClient.LastEndpoint);

        var sentPayload = Assert.IsType<MicroIdUpdatePrivilegeRequest>(apiClient.LastPayload);
        Assert.Equal(12, sentPayload.IdPrivilege);
        Assert.Equal(1, sentPayload.IdParent);
        Assert.Equal("adm_upd", sentPayload.Codename);
        Assert.Equal(string.Empty, sentPayload.Icon);
    }

    [Fact]
    public async Task MicroId_UpdatePrivilegeAsync_WhenPrivilegeBelongsToAnotherSystem_ShouldReturn403WithoutCallingUpdate()
    {
        var apiClient = new StubMicroIdApiClient();
        apiClient.EnqueueResponse(new MicroIdApiResult<MicroIdPrivilegeDetailResponse>(
            HttpStatusCode.OK,
            MicroIdResponseCodes.Success,
            "OK",
            new MicroIdPrivilegeDetailResponse(
                IdPrivilege: 99,
                IdSystem: 999,
                IdParent: 0,
                Codename: "foreign_priv",
                GroupName: "OTRO",
                Title: "Privilegio externo",
                Action: "other/action",
                Icon: null,
                Status: "A",
                DateCreation: DateTimeOffset.UtcNow,
                UserCreation: "admin")));

        var service = CreateMicroIdService(apiClient, systemNumericId: 1);

        var result = await service.UpdatePrivilegeAsync("jwt-upd", 99, new UpdatePrivilegeRequestDto
        {
            IdParent = 0,
            Codename = "hack_upd",
            GroupName = "OTRO",
            Title = "Intento cross-system",
            Action = "other/hack"
        });

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal(1, apiClient.CallCount);
        Assert.Equal("priv/get", apiClient.LastEndpoint);
    }

    [Fact]
    public async Task MicroId_DeactivatePrivilegeAsync_ShouldValidateReasonAndCallPrivDelete()
    {
        var apiClient = new StubMicroIdApiClient();
        var service = CreateMicroIdService(apiClient, systemNumericId: 1);

        // Motivo > 500 chars -> 400 sin llamar a MicroID
        var invalidResult = await service.DeactivatePrivilegeAsync(
            "jwt-del",
            12,
            new DeactivatePrivilegeRequestDto { DelDescription = new string('M', 501) });
        Assert.False(invalidResult.Success);
        Assert.Equal(StatusCodes.Status400BadRequest, invalidResult.StatusCode);
        Assert.Equal(0, apiClient.CallCount);

        // Caso exitoso (priv/get verifica pertenencia al sistema + priv/delete)
        apiClient.EnqueueResponse(new MicroIdApiResult<MicroIdPrivilegeDetailResponse>(
            HttpStatusCode.OK,
            MicroIdResponseCodes.Success,
            "OK",
            new MicroIdPrivilegeDetailResponse(
                IdPrivilege: 12,
                IdSystem: 1,
                IdParent: 0,
                Codename: "adm_del",
                GroupName: "SEGURIDAD",
                Title: "Eliminar",
                Action: "adm/del",
                Icon: null,
                Status: "A",
                DateCreation: DateTimeOffset.UtcNow,
                UserCreation: "admin")));
        apiClient.EnqueueResponse(new MicroIdApiResult<MicroIdDeletePrivilegeResponse>(
            HttpStatusCode.OK,
            MicroIdResponseCodes.Success,
            "Deleted",
            new MicroIdDeletePrivilegeResponse(12, "I", 2)));

        var okResult = await service.DeactivatePrivilegeAsync(
            "jwt-del",
            12,
            new DeactivatePrivilegeRequestDto { DelDescription = "  Deprecado  " });

        Assert.True(okResult.Success);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.NotNull(okResult.Data);
        Assert.Equal("I", okResult.Data!.Status);
        Assert.Equal(2, okResult.Data.CascadedRolePrivileges);
        Assert.Equal(2, apiClient.CallCount);
        Assert.Equal("priv/delete", apiClient.LastEndpoint);

        var sentPayload = Assert.IsType<MicroIdDeletePrivilegeRequest>(apiClient.LastPayload);
        Assert.Equal(12, sentPayload.IdPrivilege);
        Assert.Equal("Deprecado", sentPayload.DelDescription);
    }

    [Fact]
    public async Task MicroId_DeactivatePrivilegeAsync_WhenPrivilegeBelongsToAnotherSystem_ShouldReturn403WithoutCallingDelete()
    {
        var apiClient = new StubMicroIdApiClient();
        apiClient.EnqueueResponse(new MicroIdApiResult<MicroIdPrivilegeDetailResponse>(
            HttpStatusCode.OK,
            MicroIdResponseCodes.Success,
            "OK",
            new MicroIdPrivilegeDetailResponse(
                IdPrivilege: 99,
                IdSystem: 999,
                IdParent: 0,
                Codename: "foreign_priv",
                GroupName: "OTRO",
                Title: "Privilegio externo",
                Action: "other/action",
                Icon: null,
                Status: "A",
                DateCreation: DateTimeOffset.UtcNow,
                UserCreation: "admin")));

        var service = CreateMicroIdService(apiClient, systemNumericId: 1);

        var result = await service.DeactivatePrivilegeAsync(
            "jwt-del",
            99,
            new DeactivatePrivilegeRequestDto { DelDescription = "Intento cross-system" });

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal(1, apiClient.CallCount);
        Assert.Equal("priv/get", apiClient.LastEndpoint);
    }

    [Fact]
    public async Task MicroId_Operations_WithoutToken_ShouldReturn401()
    {
        var apiClient = new StubMicroIdApiClient();
        var service = CreateMicroIdService(apiClient);

        Assert.Equal(StatusCodes.Status401Unauthorized, (await service.GetPrivilegeAsync("", 1)).StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await service.CreatePrivilegeAsync("", new CreatePrivilegeRequestDto())).StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await service.UpdatePrivilegeAsync("", 1, new UpdatePrivilegeRequestDto())).StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await service.DeactivatePrivilegeAsync("", 1)).StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await service.ReactivatePrivilegeAsync("", 1)).StatusCode);
        Assert.Equal(0, apiClient.CallCount);
    }

    [Fact]
    public async Task MicroId_ReactivatePrivilegeAsync_WhenValid_ShouldVerifyOwnershipAndCallPrivReactivate()
    {
        var modifiedAt = DateTimeOffset.UtcNow;
        var apiClient = new StubMicroIdApiClient();
        apiClient.EnqueueResponse(new MicroIdApiResult<MicroIdListPrivilegeResponse>(
            HttpStatusCode.OK,
            MicroIdResponseCodes.Success,
            "OK",
            new MicroIdListPrivilegeResponse(
                IdSystem: 1,
                Items: new[]
                {
                    new MicroIdPrivilegeDetailResponse(
                        IdPrivilege: 12,
                        IdSystem: 1,
                        IdParent: 0,
                        Codename: "adm_inactive",
                        GroupName: "SEGURIDAD",
                        Title: "Inactivo",
                        Action: "adm/inactive",
                        Icon: null,
                        Status: "I",
                        DateCreation: DateTimeOffset.UtcNow,
                        UserCreation: "admin")
                },
                Total: 1)));
        apiClient.EnqueueResponse(new MicroIdApiResult<MicroIdReactivatePrivilegeResponse>(
            HttpStatusCode.OK,
            MicroIdResponseCodes.Success,
            "Reactivated",
            new MicroIdReactivatePrivilegeResponse(12, "A", modifiedAt)));

        var service = CreateMicroIdService(apiClient, systemNumericId: 1);

        var result = await service.ReactivatePrivilegeAsync("jwt-react", 12);

        Assert.True(result.Success);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(12, result.Data!.IdPrivilege);
        Assert.Equal("A", result.Data.Status);
        Assert.Equal(2, apiClient.CallCount);
        Assert.Equal("priv/reactivate", apiClient.LastEndpoint);

        var sentPayload = Assert.IsType<MicroIdReactivatePrivilegeRequest>(apiClient.LastPayload);
        Assert.Equal(12, sentPayload.IdPrivilege);
    }

    [Fact]
    public async Task MicroId_ReactivatePrivilegeAsync_WhenNotInConfiguredSystemInactiveList_ShouldReturn422WithoutCallingReactivate()
    {
        var apiClient = new StubMicroIdApiClient();
        apiClient.EnqueueResponse(new MicroIdApiResult<MicroIdListPrivilegeResponse>(
            HttpStatusCode.OK,
            MicroIdResponseCodes.Success,
            "OK",
            new MicroIdListPrivilegeResponse(
                IdSystem: 1,
                Items: Array.Empty<MicroIdPrivilegeDetailResponse>(),
                Total: 0)));

        var service = CreateMicroIdService(apiClient, systemNumericId: 1);

        var result = await service.ReactivatePrivilegeAsync("jwt-react", 999);

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        Assert.Equal(1, apiClient.CallCount);
        Assert.Equal("priv/list", apiClient.LastEndpoint);
    }

    [Theory]
    [InlineData(MicroIdResponseCodes.InactiveMaster, HttpStatusCode.UnprocessableEntity, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(MicroIdResponseCodes.InvalidPayload, HttpStatusCode.BadRequest, StatusCodes.Status400BadRequest)]
    public async Task MicroId_ReactivatePrivilegeAsync_ErrorMapping_ShouldReturnExpectedStatusCode(
        string microIdCode,
        HttpStatusCode httpStatus,
        int expectedStatusCode)
    {
        var apiClient = new StubMicroIdApiClient();
        apiClient.EnqueueResponse(new MicroIdApiResult<MicroIdListPrivilegeResponse>(
            HttpStatusCode.OK,
            MicroIdResponseCodes.Success,
            "OK",
            new MicroIdListPrivilegeResponse(
                IdSystem: 1,
                Items: new[]
                {
                    new MicroIdPrivilegeDetailResponse(
                        IdPrivilege: 15,
                        IdSystem: 1,
                        IdParent: 5,
                        Codename: "adm_child",
                        GroupName: "SEGURIDAD",
                        Title: "Hijo",
                        Action: "adm/child",
                        Icon: null,
                        Status: "I",
                        DateCreation: DateTimeOffset.UtcNow,
                        UserCreation: "admin")
                },
                Total: 1)));
        apiClient.EnqueueResponse(new MicroIdApiResult<MicroIdReactivatePrivilegeResponse>(
            httpStatus,
            microIdCode,
            "error",
            null));

        var service = CreateMicroIdService(apiClient, systemNumericId: 1);

        var result = await service.ReactivatePrivilegeAsync("jwt-react", 15);

        Assert.False(result.Success);
        Assert.Equal(expectedStatusCode, result.StatusCode);
        Assert.Equal(2, apiClient.CallCount);
        Assert.Equal("priv/reactivate", apiClient.LastEndpoint);
    }

    // =========================================================================
    // MockPrivilegeAdminService
    // =========================================================================

    [Fact]
    public async Task Mock_CreateGetUpdateDeactivate_FullLifecycle_ShouldPersistInMemory()
    {
        var service = CreateMockService();
        const string token = "mock-session-token";

        // 1. Create root privilege
        var createRoot = await service.CreatePrivilegeAsync(token, new CreatePrivilegeRequestDto
        {
            IdParent = 0,
            Codename = "mock_root_qa",
            GroupName = "QA",
            Title = "Raíz QA",
            Action = "qa/root",
            Icon = "icons/qa.svg"
        });
        Assert.True(createRoot.Success);
        Assert.Equal(StatusCodes.Status201Created, createRoot.StatusCode);
        var rootId = createRoot.Data!.IdPrivilege;

        // 2. Get by Id
        var fetched = await service.GetPrivilegeAsync(token, rootId);
        Assert.True(fetched.Success);
        Assert.Equal("mock_root_qa", fetched.Data!.Codename);
        Assert.Equal("A", fetched.Data.Status);

        // 3. Update
        var updated = await service.UpdatePrivilegeAsync(token, rootId, new UpdatePrivilegeRequestDto
        {
            IdParent = 0,
            Codename = "mock_root_qa_v2",
            GroupName = "QA",
            Title = "Raíz QA v2",
            Action = "qa/root-v2",
            Icon = null
        });
        Assert.True(updated.Success);
        Assert.Equal(StatusCodes.Status200OK, updated.StatusCode);
        Assert.True(updated.Data!.Updated);

        var fetchedAfterUpdate = await service.GetPrivilegeAsync(token, rootId);
        Assert.Equal("mock_root_qa_v2", fetchedAfterUpdate.Data!.Codename);
        Assert.Equal("Raíz QA v2", fetchedAfterUpdate.Data.Title);

        // 4. Deactivate
        var deactivated = await service.DeactivatePrivilegeAsync(token, rootId, new DeactivatePrivilegeRequestDto
        {
            DelDescription = "Limpieza QA"
        });
        Assert.True(deactivated.Success);
        Assert.Equal(StatusCodes.Status200OK, deactivated.StatusCode);
        Assert.Equal("I", deactivated.Data!.Status);

        // 5. Get after deactivation -> 404
        var fetchedInactive = await service.GetPrivilegeAsync(token, rootId);
        Assert.False(fetchedInactive.Success);
        Assert.Equal(StatusCodes.Status404NotFound, fetchedInactive.StatusCode);

        // 6. Deactivate again -> 422
        var secondDeactivate = await service.DeactivatePrivilegeAsync(token, rootId);
        Assert.False(secondDeactivate.Success);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, secondDeactivate.StatusCode);

        // 7. Appears in inactive list
        var inactiveList = await service.ListPrivilegesAsync(token, "I");
        Assert.True(inactiveList.Success);
        Assert.Contains(inactiveList.Data!.Items, x => x.IdPrivilege == rootId && x.Status == "I");
    }

    [Fact]
    public async Task Mock_UpdatePrivilegeAsync_ShouldPreventSelfReferenceAndCircularHierarchy()
    {
        var service = CreateMockService();
        const string token = "mock-session-token";

        // Crear jerarquía A -> B -> C
        var nodeA = (await service.CreatePrivilegeAsync(token, new CreatePrivilegeRequestDto
        {
            IdParent = 0,
            Codename = "node_a",
            GroupName = "QA",
            Title = "Nodo A",
            Action = "qa/a"
        })).Data!.IdPrivilege;

        var nodeB = (await service.CreatePrivilegeAsync(token, new CreatePrivilegeRequestDto
        {
            IdParent = nodeA,
            Codename = "node_b",
            GroupName = "QA",
            Title = "Nodo B",
            Action = "qa/b"
        })).Data!.IdPrivilege;

        var nodeC = (await service.CreatePrivilegeAsync(token, new CreatePrivilegeRequestDto
        {
            IdParent = nodeB,
            Codename = "node_c",
            GroupName = "QA",
            Title = "Nodo C",
            Action = "qa/c"
        })).Data!.IdPrivilege;

        // Autorreferencia: A como padre de A -> 422
        var selfRef = await service.UpdatePrivilegeAsync(token, nodeA, new UpdatePrivilegeRequestDto
        {
            IdParent = nodeA,
            Codename = "node_a",
            GroupName = "QA",
            Title = "Nodo A",
            Action = "qa/a"
        });
        Assert.False(selfRef.Success);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, selfRef.StatusCode);

        // Ciclo indirecto: C como padre de A (A -> C -> B -> A) -> 422
        var cycle = await service.UpdatePrivilegeAsync(token, nodeA, new UpdatePrivilegeRequestDto
        {
            IdParent = nodeC,
            Codename = "node_a",
            GroupName = "QA",
            Title = "Nodo A",
            Action = "qa/a"
        });
        Assert.False(cycle.Success);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, cycle.StatusCode);
        Assert.Contains("circular", cycle.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Mock_CreateAndUpdate_WithNonExistentOrInactiveParent_ShouldReturn422()
    {
        var service = CreateMockService();
        const string token = "mock-session-token";

        // Padre inexistente al crear
        var createMissingParent = await service.CreatePrivilegeAsync(token, new CreatePrivilegeRequestDto
        {
            IdParent = 999999,
            Codename = "orphan",
            GroupName = "QA",
            Title = "Huérfano",
            Action = "qa/orphan"
        });
        Assert.False(createMissingParent.Success);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, createMissingParent.StatusCode);

        // Crear y luego inactivar padre
        var parentId = (await service.CreatePrivilegeAsync(token, new CreatePrivilegeRequestDto
        {
            IdParent = 0,
            Codename = "temp_parent",
            GroupName = "QA",
            Title = "Padre Temporal",
            Action = "qa/temp-parent"
        })).Data!.IdPrivilege;

        await service.DeactivatePrivilegeAsync(token, parentId);

        var createWithInactiveParent = await service.CreatePrivilegeAsync(token, new CreatePrivilegeRequestDto
        {
            IdParent = parentId,
            Codename = "child_of_inactive",
            GroupName = "QA",
            Title = "Hijo",
            Action = "qa/child"
        });
        Assert.False(createWithInactiveParent.Success);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, createWithInactiveParent.StatusCode);

        // Actualizar privilegio inactivo -> 422
        var updateInactive = await service.UpdatePrivilegeAsync(token, parentId, new UpdatePrivilegeRequestDto
        {
            IdParent = 0,
            Codename = "temp_parent",
            GroupName = "QA",
            Title = "Padre Temporal",
            Action = "qa/temp-parent"
        });
        Assert.False(updateInactive.Success);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, updateInactive.StatusCode);
    }

    [Fact]
    public async Task Mock_ReactivatePrivilegeAsync_ShouldValidateBusinessRulesAndRestoreActiveStatus()
    {
        var service = CreateMockService();
        const string token = "mock-session-token";

        // 1. Inexistente -> 422
        var missing = await service.ReactivatePrivilegeAsync(token, 999999);
        Assert.False(missing.Success);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, missing.StatusCode);

        // 2. Crear padre e hijo
        var parentId = (await service.CreatePrivilegeAsync(token, new CreatePrivilegeRequestDto
        {
            IdParent = 0,
            Codename = "react_parent",
            GroupName = "QA",
            Title = "Padre Reactivar",
            Action = "qa/react-parent"
        })).Data!.IdPrivilege;

        var childId = (await service.CreatePrivilegeAsync(token, new CreatePrivilegeRequestDto
        {
            IdParent = parentId,
            Codename = "react_child",
            GroupName = "QA",
            Title = "Hijo Reactivar",
            Action = "qa/react-child"
        })).Data!.IdPrivilege;

        // 3. Ya activo -> 400
        var alreadyActive = await service.ReactivatePrivilegeAsync(token, parentId);
        Assert.False(alreadyActive.Success);
        Assert.Equal(StatusCodes.Status400BadRequest, alreadyActive.StatusCode);

        // 4. Inactivar hijo y padre
        await service.DeactivatePrivilegeAsync(token, childId);
        await service.DeactivatePrivilegeAsync(token, parentId);

        // 5. Intentar reactivar hijo mientras padre está inactivo -> 422
        var childWithInactiveParent = await service.ReactivatePrivilegeAsync(token, childId);
        Assert.False(childWithInactiveParent.Success);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, childWithInactiveParent.StatusCode);

        // 6. Reactivar padre -> 200 OK y Status == "A"
        var reactivatedParent = await service.ReactivatePrivilegeAsync(token, parentId);
        Assert.True(reactivatedParent.Success);
        Assert.Equal(StatusCodes.Status200OK, reactivatedParent.StatusCode);
        Assert.Equal("A", reactivatedParent.Data!.Status);

        // 7. Ahora reactivar hijo -> 200 OK y accesible por GetPrivilegeAsync
        var reactivatedChild = await service.ReactivatePrivilegeAsync(token, childId);
        Assert.True(reactivatedChild.Success);
        Assert.Equal(StatusCodes.Status200OK, reactivatedChild.StatusCode);
        Assert.Equal("A", reactivatedChild.Data!.Status);

        var fetchedChild = await service.GetPrivilegeAsync(token, childId);
        Assert.True(fetchedChild.Success);
        Assert.Equal("A", fetchedChild.Data!.Status);
    }

    private sealed class StubMicroIdApiClient : IMicroIdApiClient
    {
        private readonly Queue<object> _responses = new();
        private object? _nextResponse;

        public int CallCount { get; private set; }
        public string? LastEndpoint { get; private set; }
        public object? LastPayload { get; private set; }
        public string? LastBearerToken { get; private set; }

        public void SetNextResponse<TResponse>(MicroIdApiResult<TResponse> response)
        {
            _responses.Clear();
            _nextResponse = response;
        }

        public void EnqueueResponse<TResponse>(MicroIdApiResult<TResponse> response)
        {
            _responses.Enqueue(response);
        }

        public Task<MicroIdApiResult<TResponse>> PostAsync<TRequest, TResponse>(
            string endpoint,
            TRequest payload,
            string? bearerToken = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastEndpoint = endpoint;
            LastPayload = payload;
            LastBearerToken = bearerToken;

            var candidate = _responses.Count > 0 ? _responses.Dequeue() : _nextResponse;

            if (candidate is MicroIdApiResult<TResponse> typed)
            {
                return Task.FromResult(typed);
            }

            throw new InvalidOperationException($"No se configuró una respuesta stub de tipo {typeof(TResponse).Name}.");
        }
    }
}
