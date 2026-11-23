using System.Net;
using Microsoft.Extensions.Options;
using Vogel.Server.Configuration;
using Vogel.Server.Models.MicroId;
using Vogel.Shared.DTOs;

namespace Vogel.Server.Services.MicroId;

/// <summary>
/// Implementación de <see cref="IPrivilegeAdminProviderService"/> contra los endpoints /priv/* de MicroID.
/// </summary>
public sealed class MicroIdPrivilegeAdminService : IPrivilegeAdminProviderService
{
    private const int MaxCodenameLength = 100;
    private const int MaxGroupNameLength = 100;
    private const int MaxTitleLength = 150;
    private const int MaxActionLength = 250;
    private const int MaxIconLength = 100;
    private const int MaxDelDescriptionLength = 500;

    private readonly IMicroIdApiClient _apiClient;
    private readonly MicroIdOptions _options;
    private readonly ILogger<MicroIdPrivilegeAdminService> _logger;

    public MicroIdPrivilegeAdminService(
        IMicroIdApiClient apiClient,
        IOptions<MicroIdOptions> options,
        ILogger<MicroIdPrivilegeAdminService> logger)
    {
        _apiClient = apiClient;
        _options = options.Value;
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

        var normalizedStatus = NormalizeStatusFilter(status);

        var request = new MicroIdListPrivilegeRequest(
            IdSystem: _options.SystemNumericId,
            Filter: new MicroIdListPrivilegeFilter(
                Status: normalizedStatus,
                GroupName: null,
                Search: null));

        var result = await _apiClient.PostAsync<MicroIdListPrivilegeRequest, MicroIdListPrivilegeResponse>(
            "priv/list",
            request,
            bearerToken: token,
            cancellationToken: cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            var (statusCode, errorMessage) = MapListError(result.Code, result.StatusCode, result.Message);
            _logger.LogWarning(
                "Consulta administrativa de privilegios en MicroID (/priv/list) rechazada (StatusCode: {StatusCode}, Code: {Code}).",
                statusCode,
                SanitizeForLog(result.Code));

            return new PrivilegeAdminListResult(
                false,
                statusCode,
                null,
                new BackendErrorDto(statusCode, errorMessage));
        }

        var items = result.Data.Items
            .Select(MapToAdminItemDto)
            .ToList();

        var response = new PrivilegeAdminListResponseDto
        {
            SystemCode = _options.SystemId,
            IdSystem = result.Data.IdSystem > 0 ? result.Data.IdSystem : _options.SystemNumericId,
            Items = items,
            Total = result.Data.Total >= items.Count ? result.Data.Total : items.Count
        };

        _logger.LogInformation(
            "Consulta administrativa de privilegios exitosa para sistema '{SystemCode}' ({Count} registros).",
            SanitizeForLog(response.SystemCode),
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

        var result = await _apiClient.PostAsync<MicroIdGetPrivilegeRequest, MicroIdPrivilegeDetailResponse>(
            "priv/get",
            new MicroIdGetPrivilegeRequest(idPrivilege),
            bearerToken: token,
            cancellationToken: cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            var (statusCode, errorMessage) = MapOperationError(
                result.Code,
                result.StatusCode,
                notFoundOnInactiveMaster: true);

            _logger.LogWarning(
                "Consulta individual de privilegio {PrivilegeId} en MicroID (/priv/get) rechazada (StatusCode: {StatusCode}, Code: {Code}).",
                idPrivilege,
                statusCode,
                SanitizeForLog(result.Code));

            return new PrivilegeAdminDetailResult(
                false,
                statusCode,
                null,
                new BackendErrorDto(statusCode, errorMessage));
        }

        if (result.Data.IdSystem != _options.SystemNumericId)
        {
            _logger.LogWarning(
                "Acceso bloqueado en /priv/get: el privilegio {PrivilegeId} pertenece al sistema {ReturnedSystemId} y no al sistema configurado {ConfiguredSystemId}.",
                idPrivilege,
                result.Data.IdSystem,
                _options.SystemNumericId);

            return new PrivilegeAdminDetailResult(
                false,
                StatusCodes.Status403Forbidden,
                null,
                new BackendErrorDto(StatusCodes.Status403Forbidden, "El privilegio solicitado no pertenece al sistema configurado."));
        }

        return new PrivilegeAdminDetailResult(
            true,
            StatusCodes.Status200OK,
            MapToAdminItemDto(result.Data),
            null);
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

        var payload = new MicroIdCreatePrivilegeRequest(
            IdSystem: _options.SystemNumericId,
            IdParent: request.IdParent.HasValue && request.IdParent.Value > 0 ? request.IdParent.Value : 0,
            Codename: request.Codename.Trim(),
            GroupName: request.GroupName.Trim(),
            Title: request.Title.Trim(),
            Action: request.Action.Trim(),
            Icon: string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim());

        var result = await _apiClient.PostAsync<MicroIdCreatePrivilegeRequest, MicroIdCreatePrivilegeResponse>(
            "priv/create",
            payload,
            bearerToken: token,
            cancellationToken: cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            var (statusCode, errorMessage) = MapOperationError(
                result.Code,
                result.StatusCode,
                notFoundOnInactiveMaster: false);

            _logger.LogWarning(
                "Creación de privilegio en MicroID (/priv/create) rechazada (StatusCode: {StatusCode}, Code: {Code}).",
                statusCode,
                SanitizeForLog(result.Code));

            return new CreatePrivilegeResult(
                false,
                statusCode,
                null,
                new BackendErrorDto(statusCode, errorMessage));
        }

        var response = new CreatePrivilegeResponseDto
        {
            IdPrivilege = result.Data.IdPrivilege,
            IdSystem = result.Data.IdSystem,
            Status = result.Data.Status,
            DateCreation = result.Data.DateCreation
        };

        _logger.LogInformation(
            "Privilegio {PrivilegeId} creado exitosamente en MicroID para sistema '{SystemCode}'.",
            response.IdPrivilege,
            SanitizeForLog(_options.SystemId));

        return new CreatePrivilegeResult(true, StatusCodes.Status201Created, response, null);
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

        var ownershipCheck = await GetPrivilegeAsync(token, idPrivilege, cancellationToken);
        if (!ownershipCheck.Success)
        {
            var ownershipStatus = ownershipCheck.StatusCode == StatusCodes.Status404NotFound
                ? StatusCodes.Status422UnprocessableEntity
                : ownershipCheck.StatusCode;
            var ownershipMessage = ownershipCheck.StatusCode == StatusCodes.Status404NotFound
                ? "El privilegio o la referencia solicitada no existe o se encuentra inactivo."
                : ownershipCheck.Error?.Message ?? "No fue posible validar la pertenencia del privilegio.";

            return new UpdatePrivilegeResult(
                false,
                ownershipStatus,
                null,
                new BackendErrorDto(ownershipStatus, ownershipMessage));
        }

        var payload = new MicroIdUpdatePrivilegeRequest(
            IdPrivilege: idPrivilege,
            IdParent: request.IdParent.HasValue && request.IdParent.Value > 0 ? request.IdParent.Value : 0,
            Codename: request.Codename.Trim(),
            GroupName: request.GroupName.Trim(),
            Title: request.Title.Trim(),
            Action: request.Action.Trim(),
            Icon: string.IsNullOrWhiteSpace(request.Icon) ? string.Empty : request.Icon.Trim());

        var result = await _apiClient.PostAsync<MicroIdUpdatePrivilegeRequest, MicroIdUpdatePrivilegeResponse>(
            "priv/update",
            payload,
            bearerToken: token,
            cancellationToken: cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            var (statusCode, errorMessage) = MapOperationError(
                result.Code,
                result.StatusCode,
                notFoundOnInactiveMaster: false);

            _logger.LogWarning(
                "Actualización de privilegio {PrivilegeId} en MicroID (/priv/update) rechazada (StatusCode: {StatusCode}, Code: {Code}).",
                idPrivilege,
                statusCode,
                SanitizeForLog(result.Code));

            return new UpdatePrivilegeResult(
                false,
                statusCode,
                null,
                new BackendErrorDto(statusCode, errorMessage));
        }

        var response = new UpdatePrivilegeResponseDto
        {
            IdPrivilege = result.Data.IdPrivilege,
            Updated = result.Data.Updated,
            DateModification = result.Data.DateModification
        };

        _logger.LogInformation(
            "Privilegio {PrivilegeId} actualizado exitosamente en MicroID.",
            response.IdPrivilege);

        return new UpdatePrivilegeResult(true, StatusCodes.Status200OK, response, null);
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

        var trimmedDescription = string.IsNullOrWhiteSpace(request?.DelDescription)
            ? null
            : request.DelDescription.Trim();

        if (trimmedDescription is { Length: > MaxDelDescriptionLength })
        {
            return new DeactivatePrivilegeResult(
                false,
                StatusCodes.Status400BadRequest,
                null,
                new BackendErrorDto(StatusCodes.Status400BadRequest, $"El motivo de inactivación no puede exceder los {MaxDelDescriptionLength} caracteres."));
        }

        var ownershipCheck = await GetPrivilegeAsync(token, idPrivilege, cancellationToken);
        if (!ownershipCheck.Success)
        {
            var ownershipStatus = ownershipCheck.StatusCode == StatusCodes.Status404NotFound
                ? StatusCodes.Status422UnprocessableEntity
                : ownershipCheck.StatusCode;
            var ownershipMessage = ownershipCheck.StatusCode == StatusCodes.Status404NotFound
                ? "El privilegio o la referencia solicitada no existe o se encuentra inactivo."
                : ownershipCheck.Error?.Message ?? "No fue posible validar la pertenencia del privilegio.";

            return new DeactivatePrivilegeResult(
                false,
                ownershipStatus,
                null,
                new BackendErrorDto(ownershipStatus, ownershipMessage));
        }

        var payload = new MicroIdDeletePrivilegeRequest(
            IdPrivilege: idPrivilege,
            DelDescription: trimmedDescription);

        var result = await _apiClient.PostAsync<MicroIdDeletePrivilegeRequest, MicroIdDeletePrivilegeResponse>(
            "priv/delete",
            payload,
            bearerToken: token,
            cancellationToken: cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            var (statusCode, errorMessage) = MapOperationError(
                result.Code,
                result.StatusCode,
                notFoundOnInactiveMaster: false);

            _logger.LogWarning(
                "Inactivación de privilegio {PrivilegeId} en MicroID (/priv/delete) rechazada (StatusCode: {StatusCode}, Code: {Code}).",
                idPrivilege,
                statusCode,
                SanitizeForLog(result.Code));

            return new DeactivatePrivilegeResult(
                false,
                statusCode,
                null,
                new BackendErrorDto(statusCode, errorMessage));
        }

        var response = new DeactivatePrivilegeResponseDto
        {
            IdPrivilege = result.Data.IdPrivilege,
            Status = result.Data.Status,
            CascadedRolePrivileges = result.Data.CascadedRolePrivileges
        };

        _logger.LogInformation(
            "Privilegio {PrivilegeId} inactivado exitosamente en MicroID (asignaciones trasladadas: {Cascaded}).",
            response.IdPrivilege,
            response.CascadedRolePrivileges);

        return new DeactivatePrivilegeResult(true, StatusCodes.Status200OK, response, null);
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

        var ownershipList = await ListPrivilegesAsync(token, status: "I", cancellationToken);
        if (!ownershipList.Success || ownershipList.Data is null)
        {
            return new ReactivatePrivilegeResult(
                false,
                ownershipList.StatusCode,
                null,
                ownershipList.Error ?? new BackendErrorDto(
                    ownershipList.StatusCode,
                    "No fue posible validar la pertenencia del privilegio."));
        }

        var belongsToConfiguredSystem = ownershipList.Data.Items.Any(item =>
            item.IdPrivilege == idPrivilege &&
            item.IdSystem == _options.SystemNumericId &&
            string.Equals(item.Status, "I", StringComparison.OrdinalIgnoreCase));

        if (!belongsToConfiguredSystem)
        {
            _logger.LogWarning(
                "Reactivación bloqueada en /priv/reactivate: el privilegio {PrivilegeId} no existe como inactivo en el sistema configurado {ConfiguredSystemId}.",
                idPrivilege,
                _options.SystemNumericId);

            return new ReactivatePrivilegeResult(
                false,
                StatusCodes.Status422UnprocessableEntity,
                null,
                new BackendErrorDto(
                    StatusCodes.Status422UnprocessableEntity,
                    "El privilegio solicitado no existe como inactivo en el sistema configurado."));
        }

        var payload = new MicroIdReactivatePrivilegeRequest(IdPrivilege: idPrivilege);

        var result = await _apiClient.PostAsync<MicroIdReactivatePrivilegeRequest, MicroIdReactivatePrivilegeResponse>(
            "priv/reactivate",
            payload,
            bearerToken: token,
            cancellationToken: cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            var (statusCode, errorMessage) = MapOperationError(
                result.Code,
                result.StatusCode,
                notFoundOnInactiveMaster: false);

            _logger.LogWarning(
                "Reactivación de privilegio {PrivilegeId} en MicroID (/priv/reactivate) rechazada (StatusCode: {StatusCode}, Code: {Code}).",
                idPrivilege,
                statusCode,
                SanitizeForLog(result.Code));

            return new ReactivatePrivilegeResult(
                false,
                statusCode,
                null,
                new BackendErrorDto(statusCode, errorMessage));
        }

        var response = new ReactivatePrivilegeResponseDto
        {
            IdPrivilege = result.Data.IdPrivilege,
            Status = result.Data.Status,
            DateModification = result.Data.DateModification
        };

        _logger.LogInformation(
            "Privilegio {PrivilegeId} reactivado exitosamente en MicroID.",
            response.IdPrivilege);

        return new ReactivatePrivilegeResult(true, StatusCodes.Status200OK, response, null);
    }

    private static PrivilegeAdminItemDto MapToAdminItemDto(MicroIdPrivilegeDetailResponse item)
    {
        return new PrivilegeAdminItemDto
        {
            IdPrivilege = item.IdPrivilege,
            IdSystem = item.IdSystem,
            IdParent = item.IdParent,
            Codename = item.Codename,
            Title = item.Title,
            GroupName = item.GroupName ?? string.Empty,
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

    private static string? NormalizeStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var trimmed = status.Trim().ToUpperInvariant();
        return trimmed is "A" or "I" ? trimmed : null;
    }

    private static (int StatusCode, string Message) MapListError(
        string code,
        HttpStatusCode httpStatus,
        string rawMessage)
    {
        _ = rawMessage;
        return code switch
        {
            MicroIdResponseCodes.AuthFailed =>
                (StatusCodes.Status401Unauthorized, "Sesión inválida o expirada."),
            MicroIdResponseCodes.InactiveMaster or MicroIdResponseCodes.SystemMismatch or MicroIdResponseCodes.EmptyEffectiveList =>
                (StatusCodes.Status403Forbidden, "Acceso denegado a los privilegios del sistema."),
            MicroIdResponseCodes.IdpCommunication or MicroIdResponseCodes.InternalServer =>
                (StatusCodes.Status502BadGateway, "Error de comunicación con el servicio de identidad."),
            _ => ((int)httpStatus >= 400 && (int)httpStatus < 500
                ? (int)httpStatus
                : StatusCodes.Status502BadGateway,
                "Error al consultar los privilegios del sistema.")
        };
    }

    private static (int StatusCode, string Message) MapOperationError(
        string code,
        HttpStatusCode httpStatus,
        bool notFoundOnInactiveMaster)
    {
        return code switch
        {
            MicroIdResponseCodes.AuthFailed =>
                (StatusCodes.Status401Unauthorized, "Sesión inválida o expirada."),
            MicroIdResponseCodes.InvalidPayload =>
                (StatusCodes.Status400BadRequest, "Los datos enviados para el privilegio no son válidos."),
            MicroIdResponseCodes.InactiveMaster =>
                (notFoundOnInactiveMaster ? StatusCodes.Status404NotFound : StatusCodes.Status422UnprocessableEntity,
                    "El privilegio o la referencia solicitada no existe o se encuentra inactivo."),
            MicroIdResponseCodes.SystemMismatch =>
                (StatusCodes.Status422UnprocessableEntity,
                    "La jerarquía o pertenencia al sistema del privilegio no es válida."),
            MicroIdResponseCodes.IdpCommunication or MicroIdResponseCodes.InternalServer =>
                (StatusCodes.Status502BadGateway, "Error de comunicación con el servicio de identidad."),
            _ => ((int)httpStatus >= 400 && (int)httpStatus < 500
                ? (int)httpStatus
                : StatusCodes.Status502BadGateway,
                "No fue posible completar la operación sobre el privilegio.")
        };
    }

    private static string SanitizeForLog(string? value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("\r", string.Empty).Replace("\n", string.Empty);
    }
}
