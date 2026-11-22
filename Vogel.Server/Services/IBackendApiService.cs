using Vogel.Shared.DTOs;

namespace Vogel.Server.Services;

/// <summary>
/// Contrato del servicio de comunicación entre Vogel.Server (BFF) y el Backend de negocio / autenticación externo.
/// Centraliza todas las llamadas HTTP remotas para que el cliente WebAssembly nunca interactúe directamente con APIs externas.
/// </summary>
public interface IBackendApiService
{
    /// <summary>
    /// Intenta autenticar un usuario contra el Backend externo.
    /// </summary>
    Task<(BackendLoginResponseDto? Profile, string? Token, BackendErrorDto? Error, int StatusCode)> LoginAsync(
        string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los privilegios asociados a un token de sesión desde el Backend externo.
    /// </summary>
    Task<(BackendPrivilegesResponseDto? Data, BackendErrorDto? Error, int StatusCode)> GetPrivilegesAsync(
        string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta una acción operativa de BlockWork contra el Backend externo.
    /// </summary>
    Task<(bool Success, object? Data, string? Error, int StatusCode)> ExecuteBlockWorkActionAsync(
        string codename, string action, object? payload = null, string? token = null, CancellationToken cancellationToken = default);
}

