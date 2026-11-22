using Vogel.Shared.DTOs;
using Vogel.Shared.Mappers;

namespace Vogel.Server.Services;

/// <summary>
/// Implementación de <see cref="IAuthProviderService"/> que delega al backend HTTP genérico (<see cref="IBackendApiService"/>).
/// </summary>
public sealed class ApiAuthProviderService : IAuthProviderService
{
    private readonly IBackendApiService _backendApiService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiAuthProviderService> _logger;

    public ApiAuthProviderService(
        IBackendApiService backendApiService,
        IConfiguration configuration,
        ILogger<ApiAuthProviderService> logger)
    {
        _backendApiService = backendApiService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthLoginResult> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var safeUsername = SanitizeForLog(request.Username);
        var (profile, token, error, statusCode) = await _backendApiService.LoginAsync(
            request.Username.Trim(), request.Password, cancellationToken);

        if (profile == null || string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning(
                "Autenticación rechazada por el backend remoto para usuario '{Username}' (Código {StatusCode}): {Error}",
                safeUsername, statusCode, error?.Message);

            return new AuthLoginResult(
                false,
                statusCode,
                error?.Message ?? "Usuario o Clave incorrectos",
                null,
                null);
        }

        var (privilegesResponse, privError, privStatus) = await _backendApiService.GetPrivilegesAsync(token, cancellationToken);

        if (privilegesResponse == null || privilegesResponse.Privileges == null || !privilegesResponse.Privileges.Any())
        {
            var errorMsg = privilegesResponse?.Privileges == null || !privilegesResponse.Privileges.Any()
                ? "El usuario no cuenta con privilegios en el sistema"
                : (privError?.Message ?? "No fue posible obtener los privilegios del usuario");

            _logger.LogWarning(
                "Acceso denegado en BFF tras login para usuario '{Username}': {Reason} (Código {StatusCode}).",
                safeUsername, errorMsg, privStatus);

            return new AuthLoginResult(
                false,
                StatusCodes.Status400BadRequest,
                errorMsg,
                null,
                null);
        }

        var apiBackendBaseUrl = _configuration.GetValue<string>("ApiBackend:BaseUrl", "https://localhost:7113") ?? "https://localhost:7113";
        var user = BackendUserMapper.MapToUser(profile, privilegesResponse.Privileges, apiBackendBaseUrl);

        _logger.LogInformation("Autenticación exitosa y privilegios mapeados vía BFF para usuario '{Username}'.", safeUsername);
        return new AuthLoginResult(true, StatusCodes.Status200OK, string.Empty, user, token);
    }

    public async Task<AuthPrivilegesResult> GetPrivilegesAsync(
        string token,
        string? username = null,
        CancellationToken cancellationToken = default)
    {
        var (data, error, statusCode) = await _backendApiService.GetPrivilegesAsync(token, cancellationToken);

        if (data == null)
        {
            _logger.LogWarning("Fallo al obtener privilegios desde el backend externo (Código {StatusCode}).", statusCode);
            return new AuthPrivilegesResult(
                false,
                statusCode,
                null,
                error ?? new BackendErrorDto(statusCode, "Error al consultar privilegios"));
        }

        return new AuthPrivilegesResult(true, StatusCodes.Status200OK, data, null);
    }

    public Task<AuthChangePasswordResult> ChangePasswordAsync(
        string? token,
        ChangePasswordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AuthChangePasswordResult(
            false,
            StatusCodes.Status501NotImplemented,
            new ChangePasswordResponseDto
            {
                Success = false,
                Message = "El cambio de contraseña no está habilitado para el proveedor Api."
            }));
    }

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
