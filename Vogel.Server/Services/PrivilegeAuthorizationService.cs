using Vogel.Shared.DTOs;
using Vogel.Shared.Enums;

namespace Vogel.Server.Services;

/// <summary>
/// Implementación agnóstica al proveedor de <see cref="IPrivilegeAuthorizationService"/>.
/// Consulta los privilegios efectivos del usuario mediante <see cref="IAuthProviderService"/>
/// y aplica autorización granular RBAC con comportamiento estricto fail-closed.
/// </summary>
public sealed class PrivilegeAuthorizationService : IPrivilegeAuthorizationService
{
    private const string ForbiddenMessage = "No cuenta con permisos suficientes para realizar esta operación.";

    private readonly IAuthProviderService _authProvider;
    private readonly ILogger<PrivilegeAuthorizationService> _logger;

    public PrivilegeAuthorizationService(
        IAuthProviderService authProvider,
        ILogger<PrivilegeAuthorizationService> logger)
    {
        _authProvider = authProvider;
        _logger = logger;
    }

    public async Task<PrivilegeAuthorizationResult> AuthorizeAsync(
        string token,
        string privilegeCodename,
        PrivilegeAction requiredAction,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new PrivilegeAuthorizationResult(
                false,
                StatusCodes.Status401Unauthorized,
                new BackendErrorDto(StatusCodes.Status401Unauthorized, "Token de sesión no proporcionado."));
        }

        var safeCodename = SanitizeForLog(privilegeCodename);
        var normalizedTarget = NormalizeCodename(privilegeCodename);
        if (string.IsNullOrEmpty(normalizedTarget))
        {
            _logger.LogWarning(
                "Autorización RBAC denegada (403): codename de privilegio inválido para acción '{RequiredAction}'.",
                requiredAction);
            return Forbidden();
        }

        AuthPrivilegesResult privilegesResult;
        try
        {
            privilegesResult = await _authProvider.GetPrivilegesAsync(token, username: null, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error inesperado al validar permisos RBAC para privilegio '{PrivilegeCodename}' y acción '{RequiredAction}'. Acceso denegado (fail-closed).",
                safeCodename,
                requiredAction);

            return new PrivilegeAuthorizationResult(
                false,
                StatusCodes.Status502BadGateway,
                new BackendErrorDto(StatusCodes.Status502BadGateway, "No fue posible validar los permisos del usuario."));
        }

        if (!privilegesResult.Success || privilegesResult.Data is null)
        {
            var failureStatus = privilegesResult.StatusCode is >= 400 and <= 599
                ? privilegesResult.StatusCode
                : StatusCodes.Status502BadGateway;

            _logger.LogWarning(
                "Validación RBAC fallida (HTTP {StatusCode}) al consultar privilegios para '{PrivilegeCodename}' acción '{RequiredAction}'.",
                failureStatus,
                safeCodename,
                requiredAction);

            return new PrivilegeAuthorizationResult(
                false,
                failureStatus,
                privilegesResult.Error ?? new BackendErrorDto(failureStatus, "No fue posible validar los permisos del usuario."));
        }

        var privilege = privilegesResult.Data.Privileges?
            .FirstOrDefault(p => string.Equals(NormalizeCodename(p.Codename), normalizedTarget, StringComparison.Ordinal));

        if (privilege?.Access is null || !HasActionPermission(privilege.Access, requiredAction))
        {
            _logger.LogWarning(
                "Acceso denegado (403) para privilegio '{PrivilegeCodename}' acción '{RequiredAction}'.",
                safeCodename,
                requiredAction);

            return Forbidden();
        }

        return new PrivilegeAuthorizationResult(true, StatusCodes.Status200OK, null);
    }

    private static PrivilegeAuthorizationResult Forbidden() =>
        new(
            false,
            StatusCodes.Status403Forbidden,
            new BackendErrorDto(StatusCodes.Status403Forbidden, ForbiddenMessage));

    private static bool HasActionPermission(BackendAccessDto access, PrivilegeAction requiredAction) =>
        requiredAction switch
        {
            PrivilegeAction.Read => access.Read,
            PrivilegeAction.Create => access.Write,
            PrivilegeAction.Modify => access.Modify,
            PrivilegeAction.Delete => access.Delete,
            PrivilegeAction.Validate => access.Validate,
            PrivilegeAction.Execute => access.Execute,
            PrivilegeAction.Approve => access.Approve,
            _ => false
        };

    private static string NormalizeCodename(string? codename) =>
        string.IsNullOrWhiteSpace(codename)
            ? string.Empty
            : codename.Trim().Replace('-', '_').ToLowerInvariant();

    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\r", string.Empty, StringComparison.Ordinal)
                    .Replace("\n", string.Empty, StringComparison.Ordinal);
    }
}
