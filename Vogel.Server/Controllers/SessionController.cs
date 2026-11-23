using Microsoft.AspNetCore.Mvc;
using Vogel.Server.Services;
using Vogel.Shared.DTOs;

namespace Vogel.Server.Controllers;

/// <summary>
/// Controlador BFF para validación de sesión entre el cliente y el servidor.
/// Comprueba la instancia activa del servidor y, en modo API, valida la vigencia del token contra el Backend remoto.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SessionController : ControllerBase
{
    private readonly ServerSessionInfo _serverSessionInfo;
    private readonly IConfiguration _configuration;
    private readonly IAuthProviderService? _authProviderService;
    private readonly ILogger<SessionController>? _logger;

    public SessionController(
        ServerSessionInfo serverSessionInfo,
        IConfiguration configuration,
        IAuthProviderService? authProviderService = null,
        ILogger<SessionController>? logger = null)
    {
        _serverSessionInfo = serverSessionInfo;
        _configuration = configuration;
        _authProviderService = authProviderService;
        _logger = logger;
    }

    /// <summary>
    /// Retorna la información de arranque del servidor actual, la vigencia del token remoto (si aplica) y la política de TTL.
    /// </summary>
    [HttpGet("validate")]
    public async Task<ActionResult<SessionValidationDto>> Validate()
    {
        var ttlSeconds = _configuration.GetValue<int>("SessionValidation:TtlSeconds", 10);

        var authProvider = _configuration.GetValue<string>("AuthProvider", "Api") ?? "Api";
        if (string.Equals(authProvider, "Api", StringComparison.OrdinalIgnoreCase) && _authProviderService != null)
        {
            string? token = null;

            if (Request.Headers.TryGetValue("Authorization", out var authHeaders))
            {
                var authHeader = authHeaders.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    token = authHeader["Bearer ".Length..].Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(token) && Request.Headers.TryGetValue("X-Session-Token", out var sessionTokens))
            {
                token = sessionTokens.FirstOrDefault()?.Trim();
            }

            if (!string.IsNullOrWhiteSpace(token))
            {
                var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;
                var result = await _authProviderService.GetPrivilegesAsync(token, cancellationToken: cancellationToken);

                if (!result.Success || result.StatusCode == StatusCodes.Status401Unauthorized || result.StatusCode == StatusCodes.Status403Forbidden || result.Data == null)
                {
                    _logger?.LogWarning("Token rechazado por el proveedor de identidad durante /api/session/validate (Código {StatusCode}). Sesión inválida.", result.StatusCode);
                    return Ok(new SessionValidationDto
                    {
                        ServerStartUtc = _serverSessionInfo.StartTimeUtc,
                        InstanceId = _serverSessionInfo.InstanceId,
                        Valid = false,
                        TtlSeconds = ttlSeconds < 0 ? 0 : ttlSeconds
                    });
                }
            }
        }

        var response = new SessionValidationDto
        {
            ServerStartUtc = _serverSessionInfo.StartTimeUtc,
            InstanceId = _serverSessionInfo.InstanceId,
            Valid = true,
            TtlSeconds = ttlSeconds < 0 ? 0 : ttlSeconds
        };

        return Ok(response);
    }
}
