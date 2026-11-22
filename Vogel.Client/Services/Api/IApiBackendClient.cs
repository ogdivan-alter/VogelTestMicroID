using Vogel.Shared.DTOs;

namespace Vogel.Client.Services.Api;

/// <summary>
/// Contrato del cliente HTTP para comunicación con MockBackend API.
/// </summary>
public interface IApiBackendClient
{
    /// <summary>
    /// Intenta autenticar un usuario en el backend remoto.
    /// Retorna el perfil si fue exitoso, el DTO de error y el código HTTP.
    /// </summary>
    Task<(BackendLoginResponseDto? Profile, string? Token, BackendErrorDto? Error, int StatusCode)> LoginAsync(
        string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los privilegios asociados a una sesión a partir de su token.
    /// </summary>
    Task<(BackendPrivilegesResponseDto? Data, BackendErrorDto? Error, int StatusCode)> GetPrivilegesAsync(
        string token, CancellationToken cancellationToken = default);
}

