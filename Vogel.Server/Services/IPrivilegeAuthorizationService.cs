using Vogel.Shared.DTOs;
using Vogel.Shared.Enums;

namespace Vogel.Server.Services;

/// <summary>
/// Resultado de una verificación granular de permisos RBAC sobre un privilegio en el BFF.
/// </summary>
public sealed record PrivilegeAuthorizationResult(
    bool IsAuthorized,
    int StatusCode,
    BackendErrorDto? Error);

/// <summary>
/// Abstracción agnóstica al proveedor para autorizar operaciones granulares (RBAC)
/// según los privilegios efectivos del usuario autenticado.
/// </summary>
public interface IPrivilegeAuthorizationService
{
    /// <summary>
    /// Evalúa si el usuario propietario del <paramref name="token"/> posee permiso
    /// para ejecutar <paramref name="requiredAction"/> sobre <paramref name="privilegeCodename"/>.
    /// Aplica evaluación estricta fail-closed.
    /// </summary>
    Task<PrivilegeAuthorizationResult> AuthorizeAsync(
        string token,
        string privilegeCodename,
        PrivilegeAction requiredAction,
        CancellationToken cancellationToken = default);
}
