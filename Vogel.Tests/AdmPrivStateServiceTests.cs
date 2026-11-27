using System.Net;
using System.Net.Http.Json;
using Vogel.Client.Services;
using Vogel.Shared.DTOs;
using Vogel.Shared.Models;
using Xunit;

namespace Vogel.Tests;

/// <summary>
/// Pruebas unitarias para AdmPrivStateService (T3.1 / T3.4).
/// Verifica carga inicial con token Bearer, filtro por estado (A por defecto, I, ALL),
/// filtro ignorecase por title y filtro exacto por id_parent.
/// </summary>
public class AdmPrivStateServiceTests
{
    private static PrivilegeAdminListResponseDto CreateSampleResponse() => new()
    {
        SystemCode = "MICROID",
        IdSystem = 1,
        Total = 4,
        Items = new List<PrivilegeAdminItemDto>
        {
            new() { IdPrivilege = 1, IdSystem = 1, IdParent = 0, Codename = "adm_sec", Title = "Administración de Seguridad", GroupName = "SEGURIDAD", Status = "A" },
            new() { IdPrivilege = 2, IdSystem = 1, IdParent = 1, Codename = "adm_priv", Title = "Administrar Privilegios", GroupName = "SEGURIDAD", Status = "A" },
            new() { IdPrivilege = 3, IdSystem = 1, IdParent = 1, Codename = "adm_users", Title = "Gestión de Usuarios", GroupName = "SEGURIDAD", Status = "I" },
            new() { IdPrivilege = 4, IdSystem = 1, IdParent = 2, Codename = "adm_roles", Title = "Roles y Permisos", GroupName = "SEGURIDAD", Status = "I" }
        }
    };

    private static SessionState CreateSessionState(bool canRead = true, bool canCreate = true, bool canModify = true, bool canDelete = true)
    {
        var sessionState = new SessionState();
        sessionState.SetUser(new User
        {
            Username = "admin",
            Privileges = new List<Privilege>
            {
                new()
                {
                    Id = 204,
                    Codename = "adm_priv",
                    Title = "Administración de Privilegios",
                    Access = new PrivilegeAccess
                    {
                        Read = canRead,
                        Create = canCreate,
                        Modify = canModify,
                        Delete = canDelete
                    }
                }
            }
        });
        return sessionState;
    }

    private static (AdmPrivStateService Service, StubHttpMessageHandler Handler) CreateService(
        HttpResponseMessage response,
        string? token = "jwt-session-token",
        SessionState? sessionState = null)
    {
        var handler = new StubHttpMessageHandler(response);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5031/") };
        var storage = new InMemorySessionStorage();
        if (token != null)
        {
            storage.SetRaw(AuthenticationService.BackendTokenKey, token);
        }

        var logger = new NoOpClientLogger();
        var service = new AdmPrivStateService(httpClient, storage, logger, sessionState ?? CreateSessionState());
        return (service, handler);
    }

    [Fact]
    public async Task LoadAsync_ShouldDefaultToActiveStatusAndSendBearerToken()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(CreateSampleResponse())
        };
        var (service, handler) = CreateService(response, "test-jwt-123");

        // Act
        await service.LoadAsync();

        // Assert
        Assert.Equal("MICROID", service.SystemCode);
        Assert.Equal(1, service.IdSystem);
        Assert.Equal("A", service.StatusFilter);
        Assert.Equal(4, service.AllItems.Count);
        Assert.Equal(2, service.FilteredItems.Count);
        Assert.All(service.FilteredItems, item => Assert.Equal("A", item.Status));
        Assert.Null(service.ErrorMessage);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("test-jwt-123", handler.LastRequest.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task SetStatusFilter_ShouldSupportActiveInactiveAndAll()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(CreateSampleResponse())
        };
        var (service, _) = CreateService(response);
        await service.LoadAsync();

        // Act & Assert: Inactivos ("I")
        service.SetStatusFilter("I");
        Assert.Equal("I", service.StatusFilter);
        Assert.Equal(2, service.FilteredItems.Count);
        Assert.All(service.FilteredItems, item => Assert.Equal("I", item.Status));

        // Act & Assert: Todos ("ALL")
        service.SetStatusFilter("ALL");
        Assert.Equal("ALL", service.StatusFilter);
        Assert.Equal(4, service.FilteredItems.Count);

        // Act & Assert: Activos ("A")
        service.SetStatusFilter("A");
        Assert.Equal("A", service.StatusFilter);
        Assert.Equal(2, service.FilteredItems.Count);
        Assert.All(service.FilteredItems, item => Assert.Equal("A", item.Status));
    }

    [Fact]
    public async Task SetTitleFilter_ShouldFilterCaseInsensitive()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(CreateSampleResponse())
        };
        var (service, _) = CreateService(response);
        await service.LoadAsync();
        service.SetStatusFilter("ALL");

        // Act
        service.SetTitleFilter("PRIVILEGIOS");

        // Assert
        Assert.Single(service.FilteredItems);
        Assert.Equal("adm_priv", service.FilteredItems[0].Codename);

        // Act: minúsculas
        service.SetTitleFilter("usuarios");

        // Assert
        Assert.Single(service.FilteredItems);
        Assert.Equal("adm_users", service.FilteredItems[0].Codename);
    }

    [Fact]
    public async Task SetIdParentFilter_ShouldFilterByExactIdParent()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(CreateSampleResponse())
        };
        var (service, _) = CreateService(response);
        await service.LoadAsync();
        service.SetStatusFilter("ALL");

        // Act
        service.SetIdParentFilter("1");

        // Assert
        Assert.Equal(2, service.FilteredItems.Count);
        Assert.All(service.FilteredItems, item => Assert.Equal(1, item.IdParent));

        // Act: combinado con estado activo ("A")
        service.SetStatusFilter("A");

        // Assert
        Assert.Single(service.FilteredItems);
        Assert.Equal("adm_priv", service.FilteredItems[0].Codename);
    }

    [Fact]
    public async Task LoadAsync_WhenUnauthorized_ShouldSetErrorMessageAndClearItems()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        var (service, _) = CreateService(response, token: null);

        // Act
        await service.LoadAsync();

        // Assert
        Assert.Empty(service.AllItems);
        Assert.Empty(service.FilteredItems);
        Assert.False(string.IsNullOrWhiteSpace(service.ErrorMessage));
    }

    // ==================== Fase 3 (T3.3): CRUD y SelectedEditPrivilegeId ====================

    [Fact]
    public void SelectedEditPrivilegeId_SetAndClear_ShouldUpdateStateAndNotify()
    {
        var (service, _) = CreateServiceWithHandler(new QueueHttpMessageHandler());
        var notifications = 0;
        service.OnStateChanged += () => notifications++;

        Assert.Null(service.SelectedEditPrivilegeId);

        service.SetSelectedEditPrivilegeId(42);
        Assert.Equal(42, service.SelectedEditPrivilegeId);
        Assert.Equal(1, notifications);

        // Repetir mismo ID no debe notificar
        service.SetSelectedEditPrivilegeId(42);
        Assert.Equal(1, notifications);

        // Clear debe limpiar y notificar
        service.ClearSelectedEditPrivilegeId();
        Assert.Null(service.SelectedEditPrivilegeId);
        Assert.Equal(2, notifications);

        // Clear cuando ya es null no debe notificar
        service.ClearSelectedEditPrivilegeId();
        Assert.Equal(2, notifications);

        // ID <= 0 normaliza a null
        service.SetSelectedEditPrivilegeId(10);
        service.SetSelectedEditPrivilegeId(0);
        Assert.Null(service.SelectedEditPrivilegeId);
    }

    [Fact]
    public async Task GetByIdAsync_WhenIdInvalid_ShouldReturn400WithoutHttpCall()
    {
        var handler = new QueueHttpMessageHandler();
        var (service, _) = CreateServiceWithHandler(handler);

        var result = await service.GetByIdAsync(0);

        Assert.False(result.Success);
        Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetByIdAsync_WhenSuccess_ShouldSendBearerTokenAndReturnItem()
    {
        var item = new PrivilegeAdminItemDto
        {
            IdPrivilege = 25,
            IdSystem = 1,
            IdParent = 1,
            Codename = "adm_priv",
            GroupName = "SEGURIDAD",
            Title = "Administrar Privilegios",
            Action = "adm/priv",
            Status = "A"
        };
        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(item) });
        var (service, _) = CreateServiceWithHandler(handler, "jwt-get-25");

        var result = await service.GetByIdAsync(25);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(25, result.Data!.IdPrivilege);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.EndsWith("api/privileges/25", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal("jwt-get-25", handler.Requests[0].Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GetByIdAsync_WhenError_ShouldExtractBackendErrorMessage()
    {
        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = JsonContent.Create(new BackendErrorDto(403, "El privilegio no pertenece al sistema."))
            });
        var (service, _) = CreateServiceWithHandler(handler);

        var result = await service.GetByIdAsync(99);

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("El privilegio no pertenece al sistema.", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateAsync_WhenSuccess_ShouldPostAndReloadCatalog()
    {
        var createdDto = new CreatePrivilegeResponseDto
        {
            IdPrivilege = 50,
            IdSystem = 1,
            Status = "A",
            DateCreation = DateTimeOffset.UtcNow
        };
        var reloadedCatalog = CreateSampleResponse();
        reloadedCatalog.Items.Add(new PrivilegeAdminItemDto
        {
            IdPrivilege = 50,
            IdSystem = 1,
            IdParent = 1,
            Codename = "adm_new",
            GroupName = "SEGURIDAD",
            Title = "Nuevo Privilegio",
            Action = "adm/new",
            Status = "A"
        });
        reloadedCatalog.Total = reloadedCatalog.Items.Count;

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(createdDto) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(reloadedCatalog) });

        var (service, _) = CreateServiceWithHandler(handler, "jwt-create");

        var result = await service.CreateAsync(new CreatePrivilegeRequestDto
        {
            IdParent = 1,
            Codename = "adm_new",
            GroupName = "SEGURIDAD",
            Title = "Nuevo Privilegio",
            Action = "adm/new"
        });

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(50, result.Data!.IdPrivilege);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        Assert.Contains(service.AllItems, x => x.IdPrivilege == 50);
    }

    [Fact]
    public async Task CreateAsync_WhenFailed_ShouldNotReloadCatalog()
    {
        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = JsonContent.Create(new BackendErrorDto(422, "Padre inválido."))
            });
        var (service, _) = CreateServiceWithHandler(handler);

        var result = await service.CreateAsync(new CreatePrivilegeRequestDto
        {
            IdParent = 999,
            Codename = "adm_fail",
            GroupName = "SEGURIDAD",
            Title = "Fallido",
            Action = "adm/fail"
        });

        Assert.False(result.Success);
        Assert.Equal(422, result.StatusCode);
        Assert.Equal("Padre inválido.", result.ErrorMessage);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task UpdateAsync_WhenSuccess_ShouldPutAndReloadCatalog()
    {
        var updatedDto = new UpdatePrivilegeResponseDto
        {
            IdPrivilege = 2,
            Updated = true,
            DateModification = DateTimeOffset.UtcNow
        };
        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(updatedDto) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(CreateSampleResponse()) });

        var (service, _) = CreateServiceWithHandler(handler, "jwt-update");

        var result = await service.UpdateAsync(2, new UpdatePrivilegeRequestDto
        {
            IdParent = 1,
            Codename = "adm_priv",
            GroupName = "SEGURIDAD",
            Title = "Administrar Privilegios (Editado)",
            Action = "adm/priv"
        });

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.True(result.Data!.Updated);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
        Assert.EndsWith("api/privileges/2", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        Assert.Equal(4, service.AllItems.Count);
    }

    [Fact]
    public async Task UpdateAsync_WhenIdInvalid_ShouldReturn400WithoutHttpCall()
    {
        var handler = new QueueHttpMessageHandler();
        var (service, _) = CreateServiceWithHandler(handler);

        var result = await service.UpdateAsync(0, new UpdatePrivilegeRequestDto());

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task DeactivateAsync_WhenSuccess_ShouldDeleteAndReloadCatalog()
    {
        var deactivatedDto = new DeactivatePrivilegeResponseDto
        {
            IdPrivilege = 2,
            Status = "I",
            CascadedRolePrivileges = 1
        };
        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(deactivatedDto) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(CreateSampleResponse()) });

        var (service, _) = CreateServiceWithHandler(handler, "jwt-delete");

        var result = await service.DeactivateAsync(2, new DeactivatePrivilegeRequestDto { DelDescription = "Obsoleto" });

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("I", result.Data!.Status);
        Assert.Equal(1, result.Data.CascadedRolePrivileges);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.EndsWith("api/privileges/2", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
    }

    [Fact]
    public async Task DeactivateAsync_WhenIdInvalid_ShouldReturn400WithoutHttpCall()
    {
        var handler = new QueueHttpMessageHandler();
        var (service, _) = CreateServiceWithHandler(handler);

        var result = await service.DeactivateAsync(-1);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ReactivateAsync_WhenSuccess_ShouldPostReloadCatalogAndSetSuccessMessage()
    {
        var reactivatedDto = new ReactivatePrivilegeResponseDto
        {
            IdPrivilege = 3,
            Status = "A",
            DateModification = DateTimeOffset.UtcNow
        };
        var reloadedCatalog = CreateSampleResponse();
        reloadedCatalog.Items[2].Status = "A";

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(reactivatedDto) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(reloadedCatalog) });

        var (service, _) = CreateServiceWithHandler(handler, "jwt-reactivate");

        var result = await service.ReactivateAsync(3);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("A", result.Data!.Status);
        Assert.False(string.IsNullOrWhiteSpace(service.SuccessMessage));
        Assert.Null(service.ErrorMessage);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.EndsWith("api/privileges/3/reactivate", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal("jwt-reactivate", handler.Requests[0].Headers.Authorization?.Parameter);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
    }

    [Fact]
    public async Task ReactivateAsync_When422Error_ShouldSetErrorMessageWithoutReloadingCatalog()
    {
        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = JsonContent.Create(new BackendErrorDto(422, "El privilegio padre se encuentra inactivo."))
            });

        var (service, _) = CreateServiceWithHandler(handler);

        var result = await service.ReactivateAsync(4);

        Assert.False(result.Success);
        Assert.Equal(422, result.StatusCode);
        Assert.Equal("El privilegio padre se encuentra inactivo.", result.ErrorMessage);
        Assert.Equal("El privilegio padre se encuentra inactivo.", service.ErrorMessage);
        Assert.Null(service.SuccessMessage);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ReactivateAsync_WhenWithoutToken_ShouldReturn401AndSetErrorMessage()
    {
        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var (service, _) = CreateServiceWithHandler(handler, token: null);

        var result = await service.ReactivateAsync(3);

        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(service.ErrorMessage));
        Assert.Null(service.SuccessMessage);
        Assert.Single(handler.Requests);
        Assert.Null(handler.Requests[0].Headers.Authorization);
    }

    [Fact]
    public async Task ReactivateAsync_WhenIdInvalid_ShouldReturn400WithoutHttpCall()
    {
        var handler = new QueueHttpMessageHandler();
        var (service, _) = CreateServiceWithHandler(handler);

        var result = await service.ReactivateAsync(0);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreateAsync_WhenWithoutReadPermission_ShouldNotReloadCatalog()
    {
        var createdDto = new CreatePrivilegeResponseDto
        {
            IdPrivilege = 77,
            IdSystem = 1,
            Status = "A",
            DateCreation = DateTimeOffset.UtcNow
        };
        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(createdDto) });
        var sessionState = CreateSessionState(canRead: false, canCreate: true, canModify: false, canDelete: false);
        var (service, _) = CreateServiceWithHandler(handler, "jwt-create-only", sessionState);

        var result = await service.CreateAsync(new CreatePrivilegeRequestDto
        {
            IdParent = 0,
            Codename = "adm_root_only",
            GroupName = "SEGURIDAD",
            Title = "Raíz Solo Creación",
            Action = "adm/root-only"
        });

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
    }

    [Fact]
    public async Task CreateAsync_WithCustomModuleCodename_ShouldEvaluateReadAccessDynamically()
    {
        var createdDto = new CreatePrivilegeResponseDto
        {
            IdPrivilege = 88,
            IdSystem = 1,
            Status = "A",
            DateCreation = DateTimeOffset.UtcNow
        };
        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(createdDto) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(CreateSampleResponse()) });

        var sessionState = new SessionState();
        sessionState.SetUser(new User
        {
            Username = "custom_admin",
            Privileges = new List<Privilege>
            {
                new()
                {
                    Id = 301,
                    Codename = "custom_priv_codename",
                    Title = "Privilegios Custom",
                    Access = new PrivilegeAccess { Read = true, Create = true }
                }
            }
        });

        var (service, _) = CreateServiceWithHandler(handler, "jwt-custom", sessionState, moduleCodename: "custom_priv_codename");

        var result = await service.CreateAsync(new CreatePrivilegeRequestDto
        {
            IdParent = 0,
            Codename = "custom_item",
            GroupName = "SEGURIDAD",
            Title = "Item Custom",
            Action = "adm/custom"
        });

        Assert.True(result.Success);
        Assert.Equal("custom_priv_codename", service.ModuleCodename);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
    }

    private static (AdmPrivStateService Service, QueueHttpMessageHandler Handler) CreateServiceWithHandler(
        QueueHttpMessageHandler handler,
        string? token = "jwt-session-token",
        SessionState? sessionState = null,
        string moduleCodename = "adm_priv")
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5031/") };
        var storage = new InMemorySessionStorage();
        if (token != null)
        {
            storage.SetRaw(AuthenticationService.BackendTokenKey, token);
        }

        var logger = new NoOpClientLogger();
        var service = new AdmPrivStateService(httpClient, storage, logger, sessionState ?? CreateSessionState());
        service.SetModuleCodename(moduleCodename);
        return (service, handler);
    }

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public List<HttpRequestMessage> Requests { get; } = new();

        public QueueHttpMessageHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No hay respuestas configuradas en QueueHttpMessageHandler.");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_response);
        }
    }

    private sealed class InMemorySessionStorage : ISecureSessionStorageService
    {
        private readonly Dictionary<string, string> _items = new();
        private int _activeIdParent;

        public void SetRaw(string key, string value) => _items[key] = value;

        public ValueTask SetItemEncryptedAsync(string key, string value)
        {
            _items[key] = value;
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

    private sealed class NoOpClientLogger : IClientLogger
    {
        public Task LogInformationAsync(string message, string? source = null) => Task.CompletedTask;
        public Task LogWarningAsync(string message, string? source = null) => Task.CompletedTask;
        public Task LogErrorAsync(string message, Exception? exception = null, string? source = null) => Task.CompletedTask;
    }
}
