using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Vogel.Server.Services;
using Vogel.Server.Services.MicroId;
using Vogel.Shared.DTOs;
using Vogel.Shared.Mappers;
using Vogel.Shared.Models;
using Vogel.Shared.Services;

namespace Vogel.Server.Controllers;

/// <summary>
/// Controlador BFF (Backend for Frontend) que gestiona la autenticación, privilegios y sesiones.
/// Centraliza toda la comunicación con MicroID o el Backend externo de negocio,
/// de modo que Vogel.Client solo interactúa con Vogel.Server y recibe modelos de dominio listos para usar.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMicroIdAuthService? _microIdAuthService;
    private readonly IBackendApiService _backendApiService;
    private readonly IMockDataService _mockDataService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IBackendApiService backendApiService,
        IMockDataService mockDataService,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _backendApiService = backendApiService;
        _mockDataService = mockDataService;
        _configuration = configuration;
        _logger = logger;
    }

    [ActivatorUtilitiesConstructor]
    public AuthController(
        IMicroIdAuthService microIdAuthService,
        IBackendApiService backendApiService,
        IMockDataService mockDataService,
        IConfiguration configuration,
        ILogger<AuthController> logger)
        : this(backendApiService, mockDataService, configuration, logger)
    {
        _microIdAuthService = microIdAuthService;
    }

    /// <summary>
    /// Procesa el inicio de sesión según AuthProvider ('MicroID' por defecto, 'Mock' o 'Api').
    /// Retorna un LoginResponseDto con la entidad User completa hacia Vogel.Client.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new LoginResponseDto { Success = false, Message = "El usuario y la clave son requeridos." });
        }

        var safeUsername = SanitizeForLog(request.Username);
        var authProvider = _configuration.GetValue<string>("AuthProvider", "MicroID") ?? "MicroID";

        if (string.Equals(authProvider, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            var mockResult = await _mockDataService.AuthenticateAsync(request);

            if (!mockResult.Success || mockResult.User == null)
            {
                _logger.LogWarning("Autenticación fallida (Mock BFF) para usuario '{Username}'.", safeUsername);
                return Unauthorized(new LoginResponseDto
                {
                    Success = false,
                    Message = mockResult.Message ?? "Usuario o Clave incorrectos"
                });
            }

            var mockToken = $"vogel.mock_session_token.{Guid.NewGuid():N}";
            Response.Headers["X-Session-Token"] = mockToken;
            Response.Headers["Authorization"] = $"Bearer {mockToken}";

            _logger.LogInformation("Autenticación exitosa (Mock BFF) para usuario '{Username}'.", safeUsername);
            return Ok(mockResult);
        }

        if (string.Equals(authProvider, "Api", StringComparison.OrdinalIgnoreCase))
        {
            // Modo API: El BFF gestiona la autenticación completa con el Backend remoto
            var (profile, token, error, statusCode) = await _backendApiService.LoginAsync(
                request.Username.Trim(), request.Password, HttpContext.RequestAborted);

            if (profile == null || string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Autenticación rechazada por el backend remoto para usuario '{Username}' (Código {StatusCode}): {Error}",
                    safeUsername, statusCode, error?.Message);

                return StatusCode(statusCode, new LoginResponseDto
                {
                    Success = false,
                    Message = error?.Message ?? "Usuario o Clave incorrectos"
                });
            }

            // Consulta de privilegios asociados en el backend externo
            var (privilegesResponse, privError, privStatus) = await _backendApiService.GetPrivilegesAsync(
                token, HttpContext.RequestAborted);

            if (privilegesResponse == null || privilegesResponse.Privileges == null || !privilegesResponse.Privileges.Any())
            {
                var errorMsg = privilegesResponse?.Privileges == null || !privilegesResponse.Privileges.Any()
                    ? "El usuario no cuenta con privilegios en el sistema"
                    : (privError?.Message ?? "No fue posible obtener los privilegios del usuario");

                _logger.LogWarning("Acceso denegado en BFF tras login para usuario '{Username}': {Reason} (Código {StatusCode}).",
                    safeUsername, errorMsg, privStatus);

                return BadRequest(new LoginResponseDto
                {
                    Success = false,
                    Message = errorMsg
                });
            }

            var apiBackendBaseUrl = _configuration.GetValue<string>("ApiBackend:BaseUrl", "https://localhost:7113") ?? "https://localhost:7113";
            var user = BackendUserMapper.MapToUser(profile, privilegesResponse.Privileges, apiBackendBaseUrl);

            Response.Headers["X-Session-Token"] = token;
            Response.Headers["Authorization"] = $"Bearer {token}";

            _logger.LogInformation("Autenticación exitosa y privilegios mapeados vía BFF para usuario '{Username}'.", safeUsername);
            return Ok(new LoginResponseDto
            {
                Success = true,
                User = user,
                Message = string.Empty
            });
        }

        // Modo MicroID (por defecto)
        if (_microIdAuthService is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new LoginResponseDto
            {
                Success = false,
                Message = "Servicio MicroID no configurado."
            });
        }

        var microIdResult = await _microIdAuthService.LoginAsync(
            request.Username,
            request.Password,
            HttpContext.RequestAborted);

        if (!microIdResult.Success || microIdResult.User is null || string.IsNullOrWhiteSpace(microIdResult.Token))
        {
            return StatusCode(microIdResult.StatusCode, new LoginResponseDto
            {
                Success = false,
                Message = microIdResult.Message
            });
        }

        Response.Headers["X-Session-Token"] = microIdResult.Token;
        Response.Headers["Authorization"] = $"Bearer {microIdResult.Token}";

        return Ok(new LoginResponseDto
        {
            Success = true,
            User = microIdResult.User,
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

        var authProvider = _configuration.GetValue<string>("AuthProvider", "MicroID") ?? "MicroID";

        if (string.Equals(authProvider, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            var rootPrivileges = await _mockDataService.GetRootPrivilegesAsync();
            var allPrivileges = new List<Privilege>(rootPrivileges);
            foreach (var root in rootPrivileges)
            {
                var subs = await _mockDataService.GetSubPrivilegesAsync(root.Id);
                allPrivileges.AddRange(subs);
            }

            var privilegeItems = allPrivileges.Select(p => new BackendPrivilegeItemDto(
                p.IdParent,
                p.Id,
                p.Codename,
                p.Group ?? string.Empty,
                p.Title,
                p.Action,
                p.Icon ?? string.Empty,
                new BackendAccessDto(
                    p.Access?.Read ?? true,
                    p.Access?.Modify ?? false,
                    p.Access?.Create ?? false,
                    p.Access?.Delete ?? false,
                    p.Access?.Approve ?? false,
                    p.Access?.Validate ?? false,
                    p.Access?.Execute ?? false)
            )).ToList();

            var responseDto = new BackendPrivilegesResponseDto("mock_user", privilegeItems);
            return Ok(responseDto);
        }

        if (string.Equals(authProvider, "Api", StringComparison.OrdinalIgnoreCase))
        {
            // Modo API: Consulta hacia el backend externo a través del BFF
            var (data, error, statusCode) = await _backendApiService.GetPrivilegesAsync(token, HttpContext.RequestAborted);

            if (data == null)
            {
                _logger.LogWarning("Fallo al obtener privilegios desde el backend externo (Código {StatusCode}).", statusCode);
                return StatusCode(statusCode, error ?? new BackendErrorDto(statusCode, "Error al consultar privilegios"));
            }

            return Ok(data);
        }

        // Modo MicroID (por defecto)
        if (_microIdAuthService is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new BackendErrorDto(StatusCodes.Status500InternalServerError, "Servicio MicroID no configurado."));
        }

        var microIdPrivileges = await _microIdAuthService.GetPrivilegesAsync(
            token,
            username,
            HttpContext.RequestAborted);

        if (!microIdPrivileges.Success || microIdPrivileges.Data is null)
        {
            return StatusCode(
                microIdPrivileges.StatusCode,
                microIdPrivileges.Error ?? new BackendErrorDto(microIdPrivileges.StatusCode, "Error al consultar privilegios"));
        }

        return Ok(microIdPrivileges.Data);
    }

    /// <summary>
    /// Cambia la contraseña del usuario autenticado (o con código de verificación).
    /// </summary>
    [HttpPost("change-password")]
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

        var authProvider = _configuration.GetValue<string>("AuthProvider", "MicroID") ?? "MicroID";

        if (string.Equals(authProvider, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new ChangePasswordResponseDto
            {
                Success = true,
                Message = "Contraseña actualizada exitosamente (Modo Mock)."
            });
        }

        if (string.Equals(authProvider, "Api", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status501NotImplemented, new ChangePasswordResponseDto
            {
                Success = false,
                Message = "El cambio de contraseña no está habilitado para el proveedor Api."
            });
        }

        if (_microIdAuthService is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ChangePasswordResponseDto
            {
                Success = false,
                Message = "Servicio MicroID no configurado."
            });
        }

        var result = await _microIdAuthService.ChangePasswordAsync(
            token,
            request,
            HttpContext.RequestAborted);

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
