using System.Net;
using System.Net.Http.Json;
using Vogel.Client.Services;
using Vogel.Shared.DTOs;
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

    private static (AdmPrivStateService Service, StubHttpMessageHandler Handler) CreateService(
        HttpResponseMessage response,
        string? token = "jwt-session-token")
    {
        var handler = new StubHttpMessageHandler(response);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5031/") };
        var storage = new InMemorySessionStorage();
        if (token != null)
        {
            storage.SetRaw(AuthenticationService.BackendTokenKey, token);
        }

        var logger = new NoOpClientLogger();
        var service = new AdmPrivStateService(httpClient, storage, logger);
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
