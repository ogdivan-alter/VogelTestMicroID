using Vogel.Shared.DTOs;
using Vogel.Shared.Models;

namespace Vogel.Server.Services;

public sealed record AuthLoginResult(
    bool Success,
    int StatusCode,
    string Message,
    User? User,
    string? Token);

public sealed record AuthPrivilegesResult(
    bool Success,
    int StatusCode,
    BackendPrivilegesResponseDto? Data,
    BackendErrorDto? Error);

public sealed record AuthChangePasswordResult(
    bool Success,
    int StatusCode,
    ChangePasswordResponseDto Response);

/// <summary>
/// Abstracción genérica de proveedor de autenticación, privilegios y gestión de credenciales para el BFF.
/// Permite intercambiar el backend de identidad (MicroID, API externa, Mock, etc.) sin modificar AuthController.
/// </summary>
public interface IAuthProviderService
{
    Task<AuthLoginResult> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default);

    Task<AuthPrivilegesResult> GetPrivilegesAsync(
        string token,
        string? username = null,
        CancellationToken cancellationToken = default);

    Task<AuthChangePasswordResult> ChangePasswordAsync(
        string? token,
        ChangePasswordRequestDto request,
        CancellationToken cancellationToken = default);
}
