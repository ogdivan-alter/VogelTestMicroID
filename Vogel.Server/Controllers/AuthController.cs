using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Vogel.Server.Services;
using Vogel.Shared.DTOs;
using Vogel.Shared.Services;

namespace Vogel.Server.Controllers;

/// <summary>
/// Controlador BFF (Backend for Frontend) agnóstico al proveedor que gestiona la autenticación,
/// privilegios y sesiones delegando en <see cref="IAuthProviderService"/>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthProviderService _authProvider;
    private readonly ILogger<AuthController> _logger;

    [ActivatorUtilitiesConstructor]
    public AuthController(
        IAuthProviderService authProvider,
        ILogger<AuthController> logger)
    {
        _authProvider = authProvider;
        _logger = logger;
    }

    /// <summary>
    /// Constructor de compatibilidad para pruebas unitarias heredadas.
    /// </summary>
    public AuthController(
        IBackendApiService backendApiService,
        IMockDataService mockDataService,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _logger = logger;
        var provider = configuration.GetValue<string>("AuthProvider", "Api") ?? "Api";

        _authProvider = string.Equals(provider, "Mock", StringComparison.OrdinalIgnoreCase)
            ? new MockAuthProviderService(mockDataService, NullLogger<MockAuthProviderService>.Instance)
            : new ApiAuthProviderService(backendApiService, configuration, NullLogger<ApiAuthProviderService>.Instance);
    }

    /// <summary>
    /// Procesa el inicio de sesión delegando al proveedor configurado.
    /// Retorna un <see cref="LoginResponseDto"/> con la entidad User completa hacia Vogel.Client.
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("AuthStrict")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new LoginResponseDto { Success = false, Message = "El usuario y la clave son requeridos." });
        }

        var result = await _authProvider.LoginAsync(request, HttpContext.RequestAborted);

        if (!result.Success || result.User is null || string.IsNullOrWhiteSpace(result.Token))
        {
            var errorBody = new LoginResponseDto
            {
                Success = false,
                Message = result.Message
            };

            return result.StatusCode == StatusCodes.Status401Unauthorized
                ? Unauthorized(errorBody)
                : StatusCode(result.StatusCode, errorBody);
        }

        Response.Headers["X-Session-Token"] = result.Token;
        Response.Headers["Authorization"] = $"Bearer {result.Token}";

        return Ok(new LoginResponseDto
        {
            Success = true,
            User = result.User,
            Message = string.Empty
        });
    }

    /// <summary>
    /// Consulta los privilegios del usuario a partir del token de sesión proporcionado en cabecera.
    /// </summary>
    [HttpGet("privileges")]
    public async Task<IActionResult> Privileges([FromQuery] string? username = null)
    {
        var token = ExtractSessionToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new BackendErrorDto(StatusCodes.Status401Unauthorized, "Token de sesión no proporcionado"));
        }

        var result = await _authProvider.GetPrivilegesAsync(token, username, HttpContext.RequestAborted);

        if (!result.Success || result.Data is null)
        {
            return StatusCode(
                result.StatusCode,
                result.Error ?? new BackendErrorDto(result.StatusCode, "Error al consultar privilegios"));
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Cambia la contraseña del usuario autenticado (o con código de verificación).
    /// </summary>
    [HttpPost("change-password")]
    [EnableRateLimiting("AuthStrict")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        if (request == null)
        {
            return BadRequest(new ChangePasswordResponseDto
            {
                Success = false,
                Message = "La solicitud es requerida."
            });
        }

        var token = ExtractSessionToken();
        if (string.IsNullOrWhiteSpace(token) && string.IsNullOrWhiteSpace(request.VerificationCode))
        {
            return Unauthorized(new ChangePasswordResponseDto
            {
                Success = false,
                Message = "Token de sesión no proporcionado."
            });
        }

        var result = await _authProvider.ChangePasswordAsync(token, request, HttpContext.RequestAborted);
        return StatusCode(result.StatusCode, result.Response);
    }

    /// <summary>
    /// Cierra la sesión activa en el BFF.
    /// </summary>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        _logger.LogInformation("Cierre de sesión registrado en el BFF.");
        return Ok(new { success = true, message = "Sesión cerrada exitosamente en el BFF." });
    }

    private string? ExtractSessionToken()
    {
        if (Request.Headers.TryGetValue("Authorization", out var authHeaders))
        {
            var authHeader = authHeaders.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(authHeader) &&
                authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authHeader["Bearer ".Length..].Trim();
            }
        }

        if (Request.Headers.TryGetValue("X-Session-Token", out var sessionTokens))
        {
            return sessionTokens.FirstOrDefault()?.Trim();
        }

        return null;
    }
}
