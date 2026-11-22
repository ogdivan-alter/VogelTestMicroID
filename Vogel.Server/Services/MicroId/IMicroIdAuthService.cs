using Vogel.Shared.DTOs;
using Vogel.Shared.Models;

namespace Vogel.Server.Services.MicroId;

public sealed record MicroIdLoginResult(
    bool Success,
    int StatusCode,
    string Message,
    User? User,
    string? Token);

public sealed record MicroIdPrivilegesResult(
    bool Success,
    int StatusCode,
    BackendPrivilegesResponseDto? Data,
    BackendErrorDto? Error);

public sealed record MicroIdChangePasswordResult(
    bool Success,
    int StatusCode,
    ChangePasswordResponseDto Response);

/// <summary>
/// Servicio BFF de alto nivel que orquesta la autenticación, enriquecimiento de perfil,
/// validación de privilegios en tiempo real y cambio de contraseña contra MicroID.
/// </summary>
public interface IMicroIdAuthService
{
    Task<MicroIdLoginResult> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<MicroIdPrivilegesResult> GetPrivilegesAsync(
        string bearerToken,
        string? username,
        CancellationToken cancellationToken = default);

    Task<MicroIdChangePasswordResult> ChangePasswordAsync(
        string? bearerToken,
        ChangePasswordRequestDto request,
        CancellationToken cancellationToken = default);
}
