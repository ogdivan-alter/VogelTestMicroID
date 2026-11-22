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
}
