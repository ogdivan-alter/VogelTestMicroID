using Vogel.Shared.DTOs;

namespace Vogel.Server.Services;

/// <summary>
/// Resultado normalizado de la consulta administrativa de privilegios en el BFF.
/// </summary>
public sealed record PrivilegeAdminListResult(
    bool Success,
    int StatusCode,
    PrivilegeAdminListResponseDto? Data,
    BackendErrorDto? Error);

/// <summary>
/// Resultado normalizado de la consulta individual de un privilegio en el BFF.
/// </summary>
public sealed record PrivilegeAdminDetailResult(
    bool Success,
    int StatusCode,
    PrivilegeAdminItemDto? Data,
    BackendErrorDto? Error);

/// <summary>
/// Resultado normalizado de la creación de un privilegio en el BFF.
/// </summary>
public sealed record CreatePrivilegeResult(
    bool Success,
    int StatusCode,
    CreatePrivilegeResponseDto? Data,
    BackendErrorDto? Error);

/// <summary>
/// Resultado normalizado de la actualización de un privilegio en el BFF.
/// </summary>
public sealed record UpdatePrivilegeResult(
    bool Success,
    int StatusCode,
    UpdatePrivilegeResponseDto? Data,
    BackendErrorDto? Error);

/// <summary>
/// Resultado normalizado de la inactivación lógica de un privilegio en el BFF.
/// </summary>
public sealed record DeactivatePrivilegeResult(
    bool Success,
    int StatusCode,
    DeactivatePrivilegeResponseDto? Data,
    BackendErrorDto? Error);

/// <summary>
/// Resultado normalizado de la reactivación lógica de un privilegio en el BFF.
/// </summary>
public sealed record ReactivatePrivilegeResult(
    bool Success,
    int StatusCode,
    ReactivatePrivilegeResponseDto? Data,
    BackendErrorDto? Error);

/// <summary>
/// Abstracción genérica de proveedor para administración de privilegios del sistema en el BFF.
/// Permite intercambiar la fuente de datos (API externa, Mock, etc.) sin acoplar los controladores.
/// </summary>
public interface IPrivilegeAdminProviderService
{
    /// <summary>
    /// Lista los privilegios del sistema objetivo usando el token de sesión del usuario autenticado.
    /// </summary>
    Task<PrivilegeAdminListResult> ListPrivilegesAsync(
        string token,
        string? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta el detalle de un privilegio activo por su identificador.
    /// </summary>
    Task<PrivilegeAdminDetailResult> GetPrivilegeAsync(
        string token,
        int idPrivilege,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un nuevo privilegio en el sistema configurado del BFF.
    /// </summary>
    Task<CreatePrivilegeResult> CreatePrivilegeAsync(
        string token,
        CreatePrivilegeRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza los atributos y jerarquía de un privilegio activo.
    /// </summary>
    Task<UpdatePrivilegeResult> UpdatePrivilegeAsync(
        string token,
        int idPrivilege,
        UpdatePrivilegeRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inactiva lógicamente un privilegio activo con motivo opcional.
    /// </summary>
    Task<DeactivatePrivilegeResult> DeactivatePrivilegeAsync(
        string token,
        int idPrivilege,
        DeactivatePrivilegeRequestDto? request = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactiva lógicamente un privilegio inactivo cambiando su estado a activo.
    /// </summary>
    Task<ReactivatePrivilegeResult> ReactivatePrivilegeAsync(
        string token,
        int idPrivilege,
        CancellationToken cancellationToken = default);
}
