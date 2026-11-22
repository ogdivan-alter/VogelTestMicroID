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
    private readonly IBackendApiService? _backendApiService;

    public SessionController(ServerSessionInfo serverSessionInfo, IConfiguration configuration, IBackendApiService? backendApiService = null)
    {
        _serverSessionInfo = serverSessionInfo;
        _configuration = configuration;
        _backendApiService = backendApiService;
    }

    /// <summary>
    /// Retorna la información de arranque del servidor actual, la vigencia del token remoto (si aplica) y la política de TTL.
    /// </summary>
    [HttpGet("validate")]
    public async Task<ActionResult<SessionValidationDto>> Validate()
    {
        var ttlSeconds = _configuration.GetValue<int>("SessionValidation:TtlSeconds", 10);

        var authProvider = _configuration.GetValue<string>("AuthProvider", "Mock") ?? "Mock";
        if (string.Equals(authProvider, "Api", StringComparison.OrdinalIgnoreCase) && _backendApiService != null)
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
                var (data, error, statusCode) = await _backendApiService.GetPrivilegesAsync(token, cancellationToken);

                if (statusCode == 401 || statusCode == 403 || data == null)
                {
                    Console.WriteLine($"[SessionController] Token rechazado por backend remoto (Código {statusCode}). Sesión inválida.");
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
