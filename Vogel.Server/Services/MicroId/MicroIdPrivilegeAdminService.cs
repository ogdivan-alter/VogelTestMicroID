using System.Net;
using Microsoft.Extensions.Options;
using Vogel.Server.Configuration;
using Vogel.Server.Models.MicroId;
using Vogel.Shared.DTOs;

namespace Vogel.Server.Services.MicroId;

/// <summary>
/// Implementación de <see cref="IPrivilegeAdminProviderService"/> contra POST /priv/list de MicroID.
/// </summary>
public sealed class MicroIdPrivilegeAdminService : IPrivilegeAdminProviderService
{
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
            var (statusCode, errorMessage) = MapError(result.Code, result.StatusCode, result.Message);
            _logger.LogWarning(
                "Consulta administrativa de privilegios en MicroID (/priv/list) rechazada (StatusCode: {StatusCode}, Code: {Code}).",
                statusCode,
                result.Code);

            return new PrivilegeAdminListResult(
                false,
                statusCode,
                null,
                new BackendErrorDto(statusCode, errorMessage));
        }

        var items = result.Data.Items
            .Select(item => new PrivilegeAdminItemDto
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
            })
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
            response.SystemCode,
            response.Items.Count);

        return new PrivilegeAdminListResult(true, StatusCodes.Status200OK, response, null);
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

    private static (int StatusCode, string Message) MapError(
        string code,
        HttpStatusCode httpStatus,
        string rawMessage)
    {
        return code switch
        {
            MicroIdResponseCodes.AuthFailed =>
                (StatusCodes.Status401Unauthorized, string.IsNullOrWhiteSpace(rawMessage) ? "Sesión inválida o expirada." : rawMessage),
            MicroIdResponseCodes.InactiveMaster or MicroIdResponseCodes.SystemMismatch or MicroIdResponseCodes.EmptyEffectiveList =>
                (StatusCodes.Status403Forbidden, string.IsNullOrWhiteSpace(rawMessage) ? "Acceso denegado a los privilegios del sistema." : rawMessage),
            MicroIdResponseCodes.IdpCommunication or MicroIdResponseCodes.InternalServer =>
                (StatusCodes.Status502BadGateway, "Error de comunicación con el servicio de identidad."),
            _ => ((int)httpStatus >= 400 ? (int)httpStatus : StatusCodes.Status502BadGateway,
                string.IsNullOrWhiteSpace(rawMessage) ? "Error al consultar los privilegios del sistema." : rawMessage)
        };
    }
}
