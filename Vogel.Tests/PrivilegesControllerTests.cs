using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Vogel.Server.Controllers;
using Vogel.Server.Services;
using Vogel.Shared.DTOs;
using Vogel.Shared.Enums;

namespace Vogel.Tests;

/// <summary>
/// Pruebas unitarias para PrivilegesController (Fase 3 - T3.1 / Fase 4 - T4.2).
/// Cubre los 6 endpoints (List, GetById, Create, Update, Deactivate, Reactivate):
/// casos exitosos, autorización RBAC granular por acción de módulo (403 Forbidden / 401 propagado),
/// 401 sin token, 400 payload/ID inválido, 422 autorreferencia y propagación de errores del proveedor.
/// </summary>
public class PrivilegesControllerTests
{
    private static PrivilegesController CreateController(
        StubPrivilegeAdminProviderService provider,
        string? bearerToken = "valid-session-token",
        string? xSessionToken = null,
        StubPrivilegeAuthorizationService? authService = null)
    {
        var controller = new PrivilegesController(
            provider,
            authService ?? new StubPrivilegeAuthorizationService(),
            NullLogger<PrivilegesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        if (!string.IsNullOrEmpty(bearerToken))
        {
            controller.HttpContext.Request.Headers.Authorization = $"Bearer {bearerToken}";
        }

        if (!string.IsNullOrEmpty(xSessionToken))
        {
            controller.HttpContext.Request.Headers["X-Session-Token"] = xSessionToken;
        }

        return controller;
    }

    private static CreatePrivilegeRequestDto CreateValidCreateRequest() => new()
    {
        IdParent = 1,
        Codename = "adm_test",
        GroupName = "SEGURIDAD",
        Title = "Privilegio de Prueba",
        Action = "adm/test",
        Icon = "icons/test.svg"
    };

    private static UpdatePrivilegeRequestDto CreateValidUpdateRequest(int? idParent = 1) => new()
    {
        IdParent = idParent,
        Codename = "adm_test_upd",
        GroupName = "SEGURIDAD",
        Title = "Privilegio Actualizado",
        Action = "adm/test-upd",
        Icon = "icons/test-upd.svg"
    };

    // ==================== GET /api/privileges/{id} ====================

    [Fact]
    public async Task GetById_WithoutToken_ShouldReturn401Unauthorized()
    {
        var provider = new StubPrivilegeAdminProviderService();
        var controller = CreateController(provider, bearerToken: null);

        var actionResult = await controller.GetById(10);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        var error = Assert.IsType<BackendErrorDto>(unauthorized.Value);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.Status);
        Assert.Equal(0, provider.GetCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetById_WithInvalidId_ShouldReturn400BadRequest(int invalidId)
    {
        var provider = new StubPrivilegeAdminProviderService();
        var controller = CreateController(provider);

        var actionResult = await controller.GetById(invalidId);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        var error = Assert.IsType<BackendErrorDto>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, error.Status);
        Assert.Equal(0, provider.GetCalls);
    }

    [Fact]
    public async Task GetById_WhenProviderSucceeds_ShouldReturn200OkAndForwardToken()
    {
        var expected = new PrivilegeAdminItemDto
        {
            IdPrivilege = 15,
            IdSystem = 1,
            IdParent = 1,
            Codename = "adm_priv",
            GroupName = "SEGURIDAD",
            Title = "Administrar Privilegios",
            Action = "adm/priv",
            Icon = "icons/priv.svg",
            Status = "A"
        };

        var provider = new StubPrivilegeAdminProviderService
        {
            GetResult = new PrivilegeAdminDetailResult(true, StatusCodes.Status200OK, expected, null)
        };
        var controller = CreateController(provider, bearerToken: null, xSessionToken: "x-token-999");

        var actionResult = await controller.GetById(15);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var dto = Assert.IsType<PrivilegeAdminItemDto>(ok.Value);
        Assert.Equal(15, dto.IdPrivilege);
        Assert.Equal("adm_priv", dto.Codename);
        Assert.Equal("x-token-999", provider.LastToken);
        Assert.Equal(15, provider.LastIdPrivilege);
    }

    [Theory]
    [InlineData(StatusCodes.Status403Forbidden, "No pertenece al sistema.")]
    [InlineData(StatusCodes.Status404NotFound, "No encontrado.")]
    [InlineData(StatusCodes.Status502BadGateway, "Error de comunicación.")]
    public async Task GetById_WhenProviderFails_ShouldPropagateStatusAndError(int statusCode, string message)
    {
        var provider = new StubPrivilegeAdminProviderService
        {
            GetResult = new PrivilegeAdminDetailResult(false, statusCode, null, new BackendErrorDto(statusCode, message))
        };
        var controller = CreateController(provider);

        var actionResult = await controller.GetById(15);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(statusCode, objectResult.StatusCode);
        var error = Assert.IsType<BackendErrorDto>(objectResult.Value);
        Assert.Equal(statusCode, error.Status);
        Assert.Equal(message, error.Message);
    }

    // ==================== POST /api/privileges ====================

    [Fact]
    public async Task Create_WithoutToken_ShouldReturn401Unauthorized()
    {
        var provider = new StubPrivilegeAdminProviderService();
        var controller = CreateController(provider, bearerToken: null);

        var actionResult = await controller.Create(CreateValidCreateRequest());

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        Assert.Equal(0, provider.CreateCalls);
    }

    [Fact]
    public async Task Create_WithNullPayload_ShouldReturn400BadRequest()
    {
        var provider = new StubPrivilegeAdminProviderService();
        var controller = CreateController(provider);

        var actionResult = await controller.Create(null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        var error = Assert.IsType<BackendErrorDto>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, error.Status);
        Assert.Equal(0, provider.CreateCalls);
    }

    [Fact]
    public async Task Create_WithInvalidFields_ShouldReturn400BadRequest()
    {
        var provider = new StubPrivilegeAdminProviderService();
        var controller = CreateController(provider);

        var negativeParent = CreateValidCreateRequest();
        negativeParent.IdParent = -2;
        Assert.IsType<BadRequestObjectResult>(await controller.Create(negativeParent));

        var missingCodename = CreateValidCreateRequest();
        missingCodename.Codename = "   ";
        Assert.IsType<BadRequestObjectResult>(await controller.Create(missingCodename));

        var longTitle = CreateValidCreateRequest();
        longTitle.Title = new string('A', 151);
        Assert.IsType<BadRequestObjectResult>(await controller.Create(longTitle));

        var longIcon = CreateValidCreateRequest();
        longIcon.Icon = new string('I', 101);
        Assert.IsType<BadRequestObjectResult>(await controller.Create(longIcon));

        Assert.Equal(0, provider.CreateCalls);
    }

    [Fact]
    public async Task Create_WhenProviderSucceeds_ShouldReturn201Created()
    {
        var created = new CreatePrivilegeResponseDto
        {
            IdPrivilege = 101,
            IdSystem = 1,
            Status = "A",
            DateCreation = DateTimeOffset.UtcNow
        };
        var provider = new StubPrivilegeAdminProviderService
        {
            CreateResult = new CreatePrivilegeResult(true, StatusCodes.Status201Created, created, null)
        };
        var controller = CreateController(provider, bearerToken: "jwt-create");

        var actionResult = await controller.Create(CreateValidCreateRequest());

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
        var dto = Assert.IsType<CreatePrivilegeResponseDto>(objectResult.Value);
        Assert.Equal(101, dto.IdPrivilege);
        Assert.Equal("jwt-create", provider.LastToken);
        Assert.Equal(1, provider.CreateCalls);
    }

    [Fact]
    public async Task Create_WhenProviderFails_ShouldPropagateProviderError()
    {
        var provider = new StubPrivilegeAdminProviderService
        {
            CreateResult = new CreatePrivilegeResult(
                false,
                StatusCodes.Status422UnprocessableEntity,
                null,
                new BackendErrorDto(StatusCodes.Status422UnprocessableEntity, "Padre inactivo."))
        };
        var controller = CreateController(provider);

        var actionResult = await controller.Create(CreateValidCreateRequest());

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, objectResult.StatusCode);
        var error = Assert.IsType<BackendErrorDto>(objectResult.Value);
        Assert.Equal("Padre inactivo.", error.Message);
    }

    // ==================== PUT /api/privileges/{id} ====================

    [Fact]
    public async Task Update_WithoutToken_ShouldReturn401Unauthorized()
    {
        var provider = new StubPrivilegeAdminProviderService();
        var controller = CreateController(provider, bearerToken: null);

        var actionResult = await controller.Update(5, CreateValidUpdateRequest());

        Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal(0, provider.UpdateCalls);
    }

    [Fact]
    public async Task Update_WithInvalidIdOrPayload_ShouldReturn400BadRequest()
    {
        var provider = new StubPrivilegeAdminProviderService();
        var controller = CreateController(provider);

        Assert.IsType<BadRequestObjectResult>(await controller.Update(0, CreateValidUpdateRequest()));
        Assert.IsType<BadRequestObjectResult>(await controller.Update(5, null));

        var missingAction = CreateValidUpdateRequest();
        missingAction.Action = "";
        Assert.IsType<BadRequestObjectResult>(await controller.Update(5, missingAction));

        Assert.Equal(0, provider.UpdateCalls);
    }

    [Fact]
    public async Task Update_WithSelfReferenceParent_ShouldReturn422UnprocessableEntity()
    {
        var provider = new StubPrivilegeAdminProviderService();
        var controller = CreateController(provider);

        var actionResult = await controller.Update(42, CreateValidUpdateRequest(idParent: 42));

        var unprocessable = Assert.IsType<UnprocessableEntityObjectResult>(actionResult);
        var error = Assert.IsType<BackendErrorDto>(unprocessable.Value);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, error.Status);
        Assert.Contains("propio padre", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, provider.UpdateCalls);
    }

    [Fact]
    public async Task Update_WhenProviderSucceeds_ShouldReturn200Ok()
    {
        var updated = new UpdatePrivilegeResponseDto
        {
            IdPrivilege = 42,
            Updated = true,
            DateModification = DateTimeOffset.UtcNow
        };
        var provider = new StubPrivilegeAdminProviderService
        {
            UpdateResult = new UpdatePrivilegeResult(true, StatusCodes.Status200OK, updated, null)
        };
        var controller = CreateController(provider);

        var actionResult = await controller.Update(42, CreateValidUpdateRequest(idParent: 1));

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var dto = Assert.IsType<UpdatePrivilegeResponseDto>(ok.Value);
        Assert.Equal(42, dto.IdPrivilege);
        Assert.True(dto.Updated);
        Assert.Equal(1, provider.UpdateCalls);
    }

    [Fact]
    public async Task Update_WhenProviderFails_ShouldPropagateError()
    {
        var provider = new StubPrivilegeAdminProviderService
        {
            UpdateResult = new UpdatePrivilegeResult(
                false,
                StatusCodes.Status422UnprocessableEntity,
                null,
                new BackendErrorDto(StatusCodes.Status422UnprocessableEntity, "Ciclo detectado."))
        };
        var controller = CreateController(provider);

        var actionResult = await controller.Update(42, CreateValidUpdateRequest(idParent: 1));

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, objectResult.StatusCode);
        var error = Assert.IsType<BackendErrorDto>(objectResult.Value);
        Assert.Equal("Ciclo detectado.", error.Message);
    }

    // ==================== DELETE /api/privileges/{id} ====================

    [Fact]
    public async Task Deactivate_WithoutToken_ShouldReturn401Unauthorized()
    {
        var provider = new StubPrivilegeAdminProviderService();
        var controller = CreateController(provider, bearerToken: null);

        var actionResult = await controller.Deactivate(7);

        Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal(0, provider.DeactivateCalls);
    }

    [Fact]
    public async Task Deactivate_WithInvalidIdOrTooLongReason_ShouldReturn400BadRequest()
    {
        var provider = new StubPrivilegeAdminProviderService();
        var controller = CreateController(provider);

        Assert.IsType<BadRequestObjectResult>(await controller.Deactivate(0));
        Assert.IsType<BadRequestObjectResult>(await controller.Deactivate(
            7,
            new DeactivatePrivilegeRequestDto { DelDescription = new string('X', 501) }));

        Assert.Equal(0, provider.DeactivateCalls);
    }

    [Fact]
    public async Task Deactivate_WhenProviderSucceeds_ShouldReturn200OkWithOptionalBody()
    {
        var deactivated = new DeactivatePrivilegeResponseDto
        {
            IdPrivilege = 7,
            Status = "I",
            CascadedRolePrivileges = 3
        };
        var provider = new StubPrivilegeAdminProviderService
        {
            DeactivateResult = new DeactivatePrivilegeResult(true, StatusCodes.Status200OK, deactivated, null)
        };
        var controller = CreateController(provider);

        var actionResult = await controller.Deactivate(7, new DeactivatePrivilegeRequestDto { DelDescription = "Obsoleto" });

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var dto = Assert.IsType<DeactivatePrivilegeResponseDto>(ok.Value);
        Assert.Equal(7, dto.IdPrivilege);
        Assert.Equal("I", dto.Status);
        Assert.Equal(3, dto.CascadedRolePrivileges);
        Assert.Equal("Obsoleto", provider.LastDeactivateRequest?.DelDescription);

        // También sin cuerpo (null)
        var actionResultNoBody = await controller.Deactivate(7, null);
        Assert.IsType<OkObjectResult>(actionResultNoBody);
        Assert.Equal(2, provider.DeactivateCalls);
    }

    [Fact]
    public async Task Deactivate_WhenProviderFails_ShouldPropagateError()
    {
        var provider = new StubPrivilegeAdminProviderService
        {
            DeactivateResult = new DeactivatePrivilegeResult(
                false,
                StatusCodes.Status422UnprocessableEntity,
                null,
                new BackendErrorDto(StatusCodes.Status422UnprocessableEntity, "Ya se encuentra inactivo."))
        };
        var controller = CreateController(provider);

        var actionResult = await controller.Deactivate(7);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, objectResult.StatusCode);
        var error = Assert.IsType<BackendErrorDto>(objectResult.Value);
        Assert.Equal("Ya se encuentra inactivo.", error.Message);
    }

    // ==================== POST /api/privileges/{id}/reactivate ====================

    [Fact]
    public async Task Reactivate_WithoutToken_ShouldReturn401Unauthorized()
    {
        var provider = new StubPrivilegeAdminProviderService();
        var controller = CreateController(provider, bearerToken: null);

        var actionResult = await controller.Reactivate(7);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        var error = Assert.IsType<BackendErrorDto>(unauthorized.Value);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.Status);
        Assert.Equal(0, provider.ReactivateCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Reactivate_WithInvalidId_ShouldReturn400BadRequest(int invalidId)
    {
        var provider = new StubPrivilegeAdminProviderService();
        var controller = CreateController(provider);

        var actionResult = await controller.Reactivate(invalidId);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        var error = Assert.IsType<BackendErrorDto>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, error.Status);
        Assert.Equal(0, provider.ReactivateCalls);
    }

    [Fact]
    public async Task Reactivate_WhenProviderSucceeds_ShouldReturn200OkAndForwardToken()
    {
        var reactivated = new ReactivatePrivilegeResponseDto
        {
            IdPrivilege = 12,
            Status = "A",
            DateModification = DateTimeOffset.UtcNow
        };
        var provider = new StubPrivilegeAdminProviderService
        {
            ReactivateResult = new ReactivatePrivilegeResult(true, StatusCodes.Status200OK, reactivated, null)
        };
        var controller = CreateController(provider, bearerToken: "jwt-reactivate");

        var actionResult = await controller.Reactivate(12);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var dto = Assert.IsType<ReactivatePrivilegeResponseDto>(ok.Value);
        Assert.Equal(12, dto.IdPrivilege);
        Assert.Equal("A", dto.Status);
        Assert.Equal("jwt-reactivate", provider.LastToken);
        Assert.Equal(12, provider.LastIdPrivilege);
        Assert.Equal(1, provider.ReactivateCalls);
    }

    [Fact]
    public async Task Reactivate_WhenProviderFails_ShouldPropagateBusinessError()
    {
        var provider = new StubPrivilegeAdminProviderService
        {
            ReactivateResult = new ReactivatePrivilegeResult(
                false,
                StatusCodes.Status422UnprocessableEntity,
                null,
                new BackendErrorDto(StatusCodes.Status422UnprocessableEntity, "El privilegio padre está inactivo."))
        };
        var controller = CreateController(provider);

        var actionResult = await controller.Reactivate(12);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, objectResult.StatusCode);
        var error = Assert.IsType<BackendErrorDto>(objectResult.Value);
        Assert.Equal("El privilegio padre está inactivo.", error.Message);
    }

    // ==================== RBAC Authorization (Fase 4 - T4.2) ====================

    [Fact]
    public async Task List_WhenAuthorized_ShouldRequireReadActionAndReturn200()
    {
        var provider = new StubPrivilegeAdminProviderService();
        var auth = new StubPrivilegeAuthorizationService();
        var controller = CreateController(provider, authService: auth);

        var actionResult = await controller.List("A");

        Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal("/admsec/priv", auth.LastModuleAction);
        Assert.Equal(PrivilegeAction.Read, auth.LastAction);
        Assert.Equal(1, provider.ListCalls);
    }

    [Fact]
    public async Task List_WhenUnauthorized403_ShouldReturn403WithoutCallingProvider()
    {
        var provider = new StubPrivilegeAdminProviderService();
        var auth = StubPrivilegeAuthorizationService.DenyForbidden();
        var controller = CreateController(provider, authService: auth);

        var actionResult = await controller.List();

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var error = Assert.IsType<BackendErrorDto>(objectResult.Value);
        Assert.Equal(StatusCodes.Status403Forbidden, error.Status);
        Assert.Equal(PrivilegeAction.Read, auth.LastAction);
        Assert.Equal(0, provider.ListCalls);
    }

    [Fact]
    public async Task GetById_WhenUnauthorized403_ShouldReturn403WithoutCallingProvider()
    {
        var provider = new StubPrivilegeAdminProviderService();
        var auth = StubPrivilegeAuthorizationService.DenyForbidden();
        var controller = CreateController(provider, authService: auth);

        var actionResult = await controller.GetById(15);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Equal("/admsec/priv", auth.LastModuleAction);
        Assert.Equal(PrivilegeAction.Read, auth.LastAction);
        Assert.Equal(0, provider.GetCalls);
    }

    [Fact]
    public async Task Create_WhenUnauthorized403_ShouldReturn403WithoutCallingProvider()
    {
        var provider = new StubPrivilegeAdminProviderService();
        var auth = StubPrivilegeAuthorizationService.DenyForbidden();
        var controller = CreateController(provider, authService: auth);

        var actionResult = await controller.Create(CreateValidCreateRequest());

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Equal("/admsec/priv", auth.LastModuleAction);
        Assert.Equal(PrivilegeAction.Create, auth.LastAction);
        Assert.Equal(0, provider.CreateCalls);
    }

    [Fact]
    public async Task Update_WhenUnauthorized403_ShouldReturn403WithoutCallingProvider()
    {
        var provider = new StubPrivilegeAdminProviderService();
        var auth = StubPrivilegeAuthorizationService.DenyForbidden();
        var controller = CreateController(provider, authService: auth);

        var actionResult = await controller.Update(42, CreateValidUpdateRequest());

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Equal("/admsec/priv", auth.LastModuleAction);
        Assert.Equal(PrivilegeAction.Modify, auth.LastAction);
        Assert.Equal(0, provider.UpdateCalls);
    }

    [Fact]
    public async Task Deactivate_WhenUnauthorized403_ShouldReturn403WithoutCallingProvider()
    {
        var provider = new StubPrivilegeAdminProviderService();
        var auth = StubPrivilegeAuthorizationService.DenyForbidden();
        var controller = CreateController(provider, authService: auth);

        var actionResult = await controller.Deactivate(7);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Equal("/admsec/priv", auth.LastModuleAction);
        Assert.Equal(PrivilegeAction.Delete, auth.LastAction);
        Assert.Equal(0, provider.DeactivateCalls);
    }

    [Fact]
    public async Task Reactivate_WhenUnauthorized403_ShouldRequireCreateActionAndReturn403WithoutCallingProvider()
    {
        var provider = new StubPrivilegeAdminProviderService();
        var auth = StubPrivilegeAuthorizationService.DenyForbidden();
        var controller = CreateController(provider, authService: auth);

        var actionResult = await controller.Reactivate(12);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Equal("/admsec/priv", auth.LastModuleAction);
        Assert.Equal(PrivilegeAction.Create, auth.LastAction);
        Assert.Equal(0, provider.ReactivateCalls);
    }

    [Fact]
    public async Task Endpoints_WhenAuthorizationReturns401_ShouldPropagate401WithoutCallingProvider()
    {
        var provider = new StubPrivilegeAdminProviderService();
        var auth = new StubPrivilegeAuthorizationService
        {
            Result = new PrivilegeAuthorizationResult(
                false,
                StatusCodes.Status401Unauthorized,
                new BackendErrorDto(StatusCodes.Status401Unauthorized, "Token expirado."))
        };
        var controller = CreateController(provider, authService: auth);

        var listResult = Assert.IsType<ObjectResult>(await controller.List());
        var createResult = Assert.IsType<ObjectResult>(await controller.Create(CreateValidCreateRequest()));
        var deactivateResult = Assert.IsType<ObjectResult>(await controller.Deactivate(7));

        Assert.Equal(StatusCodes.Status401Unauthorized, listResult.StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, createResult.StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, deactivateResult.StatusCode);
        Assert.Equal(0, provider.ListCalls);
        Assert.Equal(0, provider.CreateCalls);
        Assert.Equal(0, provider.DeactivateCalls);
    }

    private sealed class StubPrivilegeAuthorizationService : IPrivilegeAuthorizationService
    {
        public string? LastToken { get; private set; }
        public string? LastCodename { get; private set; }
        public string? LastModuleAction { get; private set; }
        public PrivilegeAction? LastAction { get; private set; }

        public PrivilegeAuthorizationResult Result { get; set; } =
            new(true, StatusCodes.Status200OK, null);

        public static StubPrivilegeAuthorizationService DenyForbidden() => new()
        {
            Result = new PrivilegeAuthorizationResult(
                false,
                StatusCodes.Status403Forbidden,
                new BackendErrorDto(StatusCodes.Status403Forbidden, "No cuenta con permisos suficientes para realizar esta operación."))
        };

        public Task<PrivilegeAuthorizationResult> AuthorizeAsync(
            string token,
            string privilegeCodename,
            PrivilegeAction requiredAction,
            CancellationToken cancellationToken = default)
        {
            LastToken = token;
            LastCodename = privilegeCodename;
            LastAction = requiredAction;
            return Task.FromResult(Result);
        }

        public Task<PrivilegeAuthorizationResult> AuthorizeByActionAsync(
            string token,
            string moduleAction,
            PrivilegeAction requiredAction,
            CancellationToken cancellationToken = default)
        {
            LastToken = token;
            LastModuleAction = moduleAction;
            LastAction = requiredAction;
            return Task.FromResult(Result);
        }
    }

    private sealed class StubPrivilegeAdminProviderService : IPrivilegeAdminProviderService
    {
        public string? LastToken { get; private set; }
        public int LastIdPrivilege { get; private set; }
        public DeactivatePrivilegeRequestDto? LastDeactivateRequest { get; private set; }

        public int ListCalls { get; private set; }
        public int GetCalls { get; private set; }
        public int CreateCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public int DeactivateCalls { get; private set; }
        public int ReactivateCalls { get; private set; }

        public PrivilegeAdminListResult ListResult { get; set; } =
            new(true, StatusCodes.Status200OK, new PrivilegeAdminListResponseDto(), null);

        public PrivilegeAdminDetailResult GetResult { get; set; } =
            new(true, StatusCodes.Status200OK, new PrivilegeAdminItemDto(), null);

        public CreatePrivilegeResult CreateResult { get; set; } =
            new(true, StatusCodes.Status201Created, new CreatePrivilegeResponseDto(), null);

        public UpdatePrivilegeResult UpdateResult { get; set; } =
            new(true, StatusCodes.Status200OK, new UpdatePrivilegeResponseDto(), null);

        public DeactivatePrivilegeResult DeactivateResult { get; set; } =
            new(true, StatusCodes.Status200OK, new DeactivatePrivilegeResponseDto(), null);

        public ReactivatePrivilegeResult ReactivateResult { get; set; } =
            new(true, StatusCodes.Status200OK, new ReactivatePrivilegeResponseDto(), null);

        public Task<PrivilegeAdminListResult> ListPrivilegesAsync(
            string token,
            string? status = null,
            CancellationToken cancellationToken = default)
        {
            ListCalls++;
            LastToken = token;
            return Task.FromResult(ListResult);
        }

        public Task<PrivilegeAdminDetailResult> GetPrivilegeAsync(
            string token,
            int idPrivilege,
            CancellationToken cancellationToken = default)
        {
            GetCalls++;
            LastToken = token;
            LastIdPrivilege = idPrivilege;
            return Task.FromResult(GetResult);
        }

        public Task<CreatePrivilegeResult> CreatePrivilegeAsync(
            string token,
            CreatePrivilegeRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            LastToken = token;
            return Task.FromResult(CreateResult);
        }

        public Task<UpdatePrivilegeResult> UpdatePrivilegeAsync(
            string token,
            int idPrivilege,
            UpdatePrivilegeRequestDto request,
            CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            LastToken = token;
            LastIdPrivilege = idPrivilege;
            return Task.FromResult(UpdateResult);
        }

        public Task<DeactivatePrivilegeResult> DeactivatePrivilegeAsync(
            string token,
            int idPrivilege,
            DeactivatePrivilegeRequestDto? request = null,
            CancellationToken cancellationToken = default)
        {
            DeactivateCalls++;
            LastToken = token;
            LastIdPrivilege = idPrivilege;
            LastDeactivateRequest = request;
            return Task.FromResult(DeactivateResult);
        }

        public Task<ReactivatePrivilegeResult> ReactivatePrivilegeAsync(
            string token,
            int idPrivilege,
            CancellationToken cancellationToken = default)
        {
            ReactivateCalls++;
            LastToken = token;
            LastIdPrivilege = idPrivilege;
            return Task.FromResult(ReactivateResult);
        }
    }
}
