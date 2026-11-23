using Vogel.Shared.DTOs;
using Vogel.Shared.Models;
using Vogel.Shared.Services;

namespace Vogel.Server.Services;

/// <summary>
/// Implementación local de <see cref="IPrivilegeAdminProviderService"/> basada en <see cref="IMockDataService"/>
/// con estado en memoria sincronizado (thread-safe) para soportar CRUD e inactivación lógica en modo Mock.
/// </summary>
public sealed class MockPrivilegeAdminService : IPrivilegeAdminProviderService
{
    private const string MockSystemCode = "VOGEL-MOCK";
    private const int MockSystemId = 1;

    private const int MaxCodenameLength = 100;
    private const int MaxGroupNameLength = 100;
    private const int MaxTitleLength = 150;
    private const int MaxActionLength = 250;
    private const int MaxIconLength = 100;
    private const int MaxDelDescriptionLength = 500;

    private readonly IMockDataService _mockDataService;
    private readonly ILogger<MockPrivilegeAdminService> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private readonly List<PrivilegeAdminItemDto> _items = new();
    private bool _initialized;
    private int _nextId = 1;

    public MockPrivilegeAdminService(
        IMockDataService mockDataService,
        ILogger<MockPrivilegeAdminService> logger)
    {
        _mockDataService = mockDataService;
        _logger = logger;
    }

    public async Task<PrivilegeAdminListResult> ListPrivilegesAsync(
        string token,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new PrivilegeAdminListResult(
                false,
                StatusCodes.Status401Unauthorized,
                null,
                new BackendErrorDto(StatusCodes.Status401Unauthorized, "Token de sesión no proporcionado."));
        }

        await EnsureInitializedAsync(cancellationToken);

        var normalizedStatus = string.IsNullOrWhiteSpace(status)
            ? null
            : status.Trim().ToUpperInvariant();

        List<PrivilegeAdminItemDto> snapshot;
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            snapshot = _items
                .Where(item => normalizedStatus is null or "ALL" ||
                               string.Equals(item.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase))
                .Select(CloneItem)
                .ToList();
        }
        finally
        {
            _syncLock.Release();
        }

        var response = new PrivilegeAdminListResponseDto
        {
            SystemCode = MockSystemCode,
            IdSystem = MockSystemId,
            Items = snapshot,
            Total = snapshot.Count
        };

        _logger.LogInformation(
            "Consulta administrativa de privilegios (Mock BFF) exitosa ({Count} registros).",
            response.Items.Count);

        return new PrivilegeAdminListResult(true, StatusCodes.Status200OK, response, null);
    }

    public async Task<PrivilegeAdminDetailResult> GetPrivilegeAsync(
        string token,
        int idPrivilege,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new PrivilegeAdminDetailResult(
                false,
                StatusCodes.Status401Unauthorized,
                null,
                new BackendErrorDto(StatusCodes.Status401Unauthorized, "Token de sesión no proporcionado."));
        }

        if (idPrivilege <= 0)
        {
            return new PrivilegeAdminDetailResult(
                false,
                StatusCodes.Status400BadRequest,
                null,
                new BackendErrorDto(StatusCodes.Status400BadRequest, "El identificador del privilegio debe ser mayor a 0."));
        }

        await EnsureInitializedAsync(cancellationToken);

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var item = _items.FirstOrDefault(p => p.IdPrivilege == idPrivilege);
            if (item is null || !string.Equals(item.Status, "A", StringComparison.OrdinalIgnoreCase))
            {
                return new PrivilegeAdminDetailResult(
                    false,
                    StatusCodes.Status404NotFound,
                    null,
                    new BackendErrorDto(StatusCodes.Status404NotFound, "El privilegio solicitado no existe o se encuentra inactivo."));
            }

            return new PrivilegeAdminDetailResult(true, StatusCodes.Status200OK, CloneItem(item), null);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<CreatePrivilegeResult> CreatePrivilegeAsync(
        string token,
        CreatePrivilegeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new CreatePrivilegeResult(
                false,
                StatusCodes.Status401Unauthorized,
                null,
                new BackendErrorDto(StatusCodes.Status401Unauthorized, "Token de sesión no proporcionado."));
        }

        var validationError = ValidatePrivilegePayload(
            request?.IdParent,
            request?.Codename,
            request?.GroupName,
            request?.Title,
            request?.Action,
            request?.Icon);

        if (validationError is not null || request is null)
        {
            return new CreatePrivilegeResult(
                false,
                StatusCodes.Status400BadRequest,
                null,
                new BackendErrorDto(StatusCodes.Status400BadRequest, validationError ?? "Solicitud de creación de privilegio inválida."));
        }

        await EnsureInitializedAsync(cancellationToken);

        var parentId = request.IdParent.HasValue && request.IdParent.Value > 0 ? request.IdParent.Value : 0;

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            if (parentId > 0)
            {
                var parent = _items.FirstOrDefault(p => p.IdPrivilege == parentId);
                if (parent is null || !string.Equals(parent.Status, "A", StringComparison.OrdinalIgnoreCase))
                {
                    return new CreatePrivilegeResult(
                        false,
                        StatusCodes.Status422UnprocessableEntity,
                        null,
                        new BackendErrorDto(StatusCodes.Status422UnprocessableEntity, "El privilegio padre especificado no existe o se encuentra inactivo."));
                }
            }

            var newId = _nextId++;
            var now = DateTimeOffset.UtcNow;

            var newItem = new PrivilegeAdminItemDto
            {
                IdPrivilege = newId,
                IdSystem = MockSystemId,
                IdParent = parentId,
                Codename = request.Codename.Trim(),
                GroupName = request.GroupName.Trim(),
                Title = request.Title.Trim(),
                Action = request.Action.Trim(),
                Icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim(),
                Status = "A"
            };

            _items.Add(newItem);

            var response = new CreatePrivilegeResponseDto
            {
                IdPrivilege = newId,
                IdSystem = MockSystemId,
                Status = "A",
                DateCreation = now
            };

            _logger.LogInformation(
                "Privilegio {PrivilegeId} creado exitosamente en proveedor Mock BFF.",
                response.IdPrivilege);

            return new CreatePrivilegeResult(true, StatusCodes.Status201Created, response, null);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<UpdatePrivilegeResult> UpdatePrivilegeAsync(
        string token,
        int idPrivilege,
        UpdatePrivilegeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new UpdatePrivilegeResult(
                false,
                StatusCodes.Status401Unauthorized,
                null,
                new BackendErrorDto(StatusCodes.Status401Unauthorized, "Token de sesión no proporcionado."));
        }

        if (idPrivilege <= 0)
        {
            return new UpdatePrivilegeResult(
                false,
                StatusCodes.Status400BadRequest,
                null,
                new BackendErrorDto(StatusCodes.Status400BadRequest, "El identificador del privilegio debe ser mayor a 0."));
        }

        var validationError = ValidatePrivilegePayload(
            request?.IdParent,
            request?.Codename,
            request?.GroupName,
            request?.Title,
            request?.Action,
            request?.Icon);

        if (validationError is not null || request is null)
        {
            return new UpdatePrivilegeResult(
                false,
                StatusCodes.Status400BadRequest,
                null,
                new BackendErrorDto(StatusCodes.Status400BadRequest, validationError ?? "Solicitud de actualización de privilegio inválida."));
        }

        if (request.IdParent.HasValue && request.IdParent.Value == idPrivilege)
        {
            return new UpdatePrivilegeResult(
                false,
                StatusCodes.Status422UnprocessableEntity,
                null,
                new BackendErrorDto(StatusCodes.Status422UnprocessableEntity, "Un privilegio no puede ser su propio padre jerárquico."));
        }

        await EnsureInitializedAsync(cancellationToken);

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var item = _items.FirstOrDefault(p => p.IdPrivilege == idPrivilege);
            if (item is null || !string.Equals(item.Status, "A", StringComparison.OrdinalIgnoreCase))
            {
                return new UpdatePrivilegeResult(
                    false,
                    StatusCodes.Status422UnprocessableEntity,
                    null,
                    new BackendErrorDto(StatusCodes.Status422UnprocessableEntity, "El privilegio no existe o se encuentra inactivo."));
            }

            if (request.IdParent.HasValue)
            {
                if (request.IdParent.Value > 0)
                {
                    var parent = _items.FirstOrDefault(p => p.IdPrivilege == request.IdParent.Value);
                    if (parent is null || !string.Equals(parent.Status, "A", StringComparison.OrdinalIgnoreCase))
                    {
                        return new UpdatePrivilegeResult(
                            false,
                            StatusCodes.Status422UnprocessableEntity,
                            null,
                            new BackendErrorDto(StatusCodes.Status422UnprocessableEntity, "El privilegio padre especificado no existe o se encuentra inactivo."));
                    }

                    if (HasCircularHierarchy(idPrivilege, parent.IdPrivilege))
                    {
                        return new UpdatePrivilegeResult(
                            false,
                            StatusCodes.Status422UnprocessableEntity,
                            null,
                            new BackendErrorDto(StatusCodes.Status422UnprocessableEntity, "Dependencia jerárquica circular detectada en la relación de privilegios."));
                    }

                    item.IdParent = parent.IdPrivilege;
                }
                else
                {
                    item.IdParent = 0;
                }
            }

            item.Codename = request.Codename.Trim();
            item.GroupName = request.GroupName.Trim();
            item.Title = request.Title.Trim();
            item.Action = request.Action.Trim();
            item.Icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim();

            var response = new UpdatePrivilegeResponseDto
            {
                IdPrivilege = item.IdPrivilege,
                Updated = true,
                DateModification = DateTimeOffset.UtcNow
            };

            _logger.LogInformation(
                "Privilegio {PrivilegeId} actualizado exitosamente en proveedor Mock BFF.",
                response.IdPrivilege);

            return new UpdatePrivilegeResult(true, StatusCodes.Status200OK, response, null);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<DeactivatePrivilegeResult> DeactivatePrivilegeAsync(
        string token,
        int idPrivilege,
        DeactivatePrivilegeRequestDto? request = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new DeactivatePrivilegeResult(
                false,
                StatusCodes.Status401Unauthorized,
                null,
                new BackendErrorDto(StatusCodes.Status401Unauthorized, "Token de sesión no proporcionado."));
        }

        if (idPrivilege <= 0)
        {
            return new DeactivatePrivilegeResult(
                false,
                StatusCodes.Status400BadRequest,
                null,
                new BackendErrorDto(StatusCodes.Status400BadRequest, "El identificador del privilegio debe ser mayor a 0."));
        }

        if (request?.DelDescription is { Length: > MaxDelDescriptionLength })
        {
            return new DeactivatePrivilegeResult(
                false,
                StatusCodes.Status400BadRequest,
                null,
                new BackendErrorDto(StatusCodes.Status400BadRequest, $"El motivo de inactivación no puede exceder los {MaxDelDescriptionLength} caracteres."));
        }

        await EnsureInitializedAsync(cancellationToken);

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var item = _items.FirstOrDefault(p => p.IdPrivilege == idPrivilege);
            if (item is null || !string.Equals(item.Status, "A", StringComparison.OrdinalIgnoreCase))
            {
                return new DeactivatePrivilegeResult(
                    false,
                    StatusCodes.Status422UnprocessableEntity,
                    null,
                    new BackendErrorDto(StatusCodes.Status422UnprocessableEntity, "El privilegio no existe o ya se encuentra inactivo."));
            }

            item.Status = "I";

            var response = new DeactivatePrivilegeResponseDto
            {
                IdPrivilege = item.IdPrivilege,
                Status = "I",
                CascadedRolePrivileges = 0
            };

            _logger.LogInformation(
                "Privilegio {PrivilegeId} inactivado exitosamente en proveedor Mock BFF.",
                response.IdPrivilege);

            return new DeactivatePrivilegeResult(true, StatusCodes.Status200OK, response, null);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<ReactivatePrivilegeResult> ReactivatePrivilegeAsync(
        string token,
        int idPrivilege,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new ReactivatePrivilegeResult(
                false,
                StatusCodes.Status401Unauthorized,
                null,
                new BackendErrorDto(StatusCodes.Status401Unauthorized, "Token de sesión no proporcionado."));
        }

        if (idPrivilege <= 0)
        {
            return new ReactivatePrivilegeResult(
                false,
                StatusCodes.Status400BadRequest,
                null,
                new BackendErrorDto(StatusCodes.Status400BadRequest, "El identificador del privilegio debe ser mayor a 0."));
        }

        await EnsureInitializedAsync(cancellationToken);

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var item = _items.FirstOrDefault(p => p.IdPrivilege == idPrivilege);
            if (item is null)
            {
                return new ReactivatePrivilegeResult(
                    false,
                    StatusCodes.Status422UnprocessableEntity,
                    null,
                    new BackendErrorDto(StatusCodes.Status422UnprocessableEntity, "El privilegio solicitado no existe."));
            }

            if (string.Equals(item.Status, "A", StringComparison.OrdinalIgnoreCase))
            {
                return new ReactivatePrivilegeResult(
                    false,
                    StatusCodes.Status400BadRequest,
                    null,
                    new BackendErrorDto(StatusCodes.Status400BadRequest, "El privilegio ya se encuentra activo."));
            }

            if (item.IdParent > 0)
            {
                var parent = _items.FirstOrDefault(p => p.IdPrivilege == item.IdParent);
                if (parent is null || !string.Equals(parent.Status, "A", StringComparison.OrdinalIgnoreCase))
                {
                    return new ReactivatePrivilegeResult(
                        false,
                        StatusCodes.Status422UnprocessableEntity,
                        null,
                        new BackendErrorDto(StatusCodes.Status422UnprocessableEntity, "El privilegio padre especificado no existe o se encuentra inactivo."));
                }
            }

            item.Status = "A";

            var response = new ReactivatePrivilegeResponseDto
            {
                IdPrivilege = item.IdPrivilege,
                Status = "A",
                DateModification = DateTimeOffset.UtcNow
            };

            _logger.LogInformation(
                "Privilegio {PrivilegeId} reactivado exitosamente en proveedor Mock BFF.",
                response.IdPrivilege);

            return new ReactivatePrivilegeResult(true, StatusCodes.Status200OK, response, null);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            var rootPrivileges = await _mockDataService.GetRootPrivilegesAsync();
            var visited = new HashSet<int>();
            var queue = new Queue<Privilege>();
            var allPrivileges = new List<Privilege>();

            foreach (var root in rootPrivileges)
            {
                if (visited.Add(root.Id))
                {
                    queue.Enqueue(root);
                }
            }

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var current = queue.Dequeue();
                allPrivileges.Add(current);

                var children = await _mockDataService.GetSubPrivilegesAsync(current.Id);
                foreach (var child in children)
                {
                    if (visited.Add(child.Id))
                    {
                        queue.Enqueue(child);
                    }
                }
            }

            _items.Clear();
            foreach (var p in allPrivileges)
            {
                _items.Add(new PrivilegeAdminItemDto
                {
                    IdPrivilege = p.Id,
                    IdSystem = MockSystemId,
                    IdParent = p.IdParent,
                    Codename = p.Codename,
                    Title = p.Title,
                    GroupName = p.Group ?? string.Empty,
                    Action = p.Action,
                    Icon = p.Icon,
                    Status = "A"
                });
            }

            _nextId = _items.Count > 0 ? _items.Max(x => x.IdPrivilege) + 1 : 1;
            _initialized = true;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private bool HasCircularHierarchy(int targetPrivilegeId, int candidateParentId)
    {
        var currentId = candidateParentId;
        var visited = new HashSet<int>();

        while (currentId > 0)
        {
            if (currentId == targetPrivilegeId)
            {
                return true;
            }

            if (!visited.Add(currentId))
            {
                break;
            }

            var current = _items.FirstOrDefault(p => p.IdPrivilege == currentId);
            if (current is null)
            {
                break;
            }

            currentId = current.IdParent;
        }

        return false;
    }

    private static PrivilegeAdminItemDto CloneItem(PrivilegeAdminItemDto item)
    {
        return new PrivilegeAdminItemDto
        {
            IdPrivilege = item.IdPrivilege,
            IdSystem = item.IdSystem,
            IdParent = item.IdParent,
            Codename = item.Codename,
            Title = item.Title,
            GroupName = item.GroupName,
            Action = item.Action,
            Icon = item.Icon,
            Status = item.Status
        };
    }

    private static string? ValidatePrivilegePayload(
        int? idParent,
        string? codename,
        string? groupName,
        string? title,
        string? action,
        string? icon)
    {
        if (idParent.HasValue && idParent.Value < 0)
        {
            return "El privilegio padre debe ser mayor o igual a 0.";
        }

        if (string.IsNullOrWhiteSpace(codename))
        {
            return "El campo codename es obligatorio.";
        }

        if (codename.Trim().Length > MaxCodenameLength)
        {
            return $"El campo codename no puede exceder los {MaxCodenameLength} caracteres.";
        }

        if (string.IsNullOrWhiteSpace(groupName))
        {
            return "El campo group_name es obligatorio.";
        }

        if (groupName.Trim().Length > MaxGroupNameLength)
        {
            return $"El campo group_name no puede exceder los {MaxGroupNameLength} caracteres.";
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return "El campo title es obligatorio.";
        }

        if (title.Trim().Length > MaxTitleLength)
        {
            return $"El campo title no puede exceder los {MaxTitleLength} caracteres.";
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            return "El campo action es obligatorio.";
        }

        if (action.Trim().Length > MaxActionLength)
        {
            return $"El campo action no puede exceder los {MaxActionLength} caracteres.";
        }

        if (!string.IsNullOrWhiteSpace(icon) && icon.Trim().Length > MaxIconLength)
        {
            return $"El campo icon no puede exceder los {MaxIconLength} caracteres.";
        }

        return null;
    }
}
