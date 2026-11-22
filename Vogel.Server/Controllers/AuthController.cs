using Microsoft.AspNetCore.Mvc;
using Vogel.Server.Services;
using Vogel.Shared.DTOs;
using Vogel.Shared.Mappers;
using Vogel.Shared.Models;
using Vogel.Shared.Services;

namespace Vogel.Server.Controllers;

/// <summary>
/// Controlador BFF (Backend for Frontend) que gestiona la autenticación, privilegios y sesiones.
/// Centraliza toda la comunicación con el Backend externo de negocio y autenticación,
/// de modo que Vogel.Client solo interactúa con Vogel.Server y recibe modelos de dominio listos para usar.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
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

    /// <summary>
    /// Procesa el inicio de sesión. En modo 'Api' autentica contra el backend externo,
    /// consulta sus privilegios granulares y los mapea a la entidad de dominio User.
    /// En modo 'Mock' utiliza el servicio de datos simulados local.
    /// Retorna un LoginResponseDto con la entidad User completa hacia Vogel.Client.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new LoginResponseDto { Success = false, Message = "El usuario y la clave son requeridos." });
        }

        var authProvider = _configuration.GetValue<string>("AuthProvider", "Mock") ?? "Mock";

        if (string.Equals(authProvider, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            var mockResult = await _mockDataService.AuthenticateAsync(request);

            if (!mockResult.Success || mockResult.User == null)
            {
                _logger.LogWarning("Autenticación fallida (Mock BFF) para usuario '{Username}'.", request.Username);
                return Unauthorized(new LoginResponseDto
                {
                    Success = false,
                    Message = mockResult.Message ?? "Usuario o Clave incorrectos"
                });
            }

            var mockToken = $"vogel.mock_session_token.{Guid.NewGuid():N}";
            Response.Headers["X-Session-Token"] = mockToken;
            Response.Headers["Authorization"] = $"Bearer {mockToken}";

            _logger.LogInformation("Autenticación exitosa (Mock BFF) para usuario '{Username}'.", request.Username);
            return Ok(mockResult);
        }

        // Modo API: El BFF gestiona la autenticación completa con el Backend remoto
        var (profile, token, error, statusCode) = await _backendApiService.LoginAsync(
            request.Username.Trim(), request.Password, HttpContext.RequestAborted);

        if (profile == null || string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Autenticación rechazada por el backend remoto para usuario '{Username}' (Código {StatusCode}): {Error}",
                request.Username, statusCode, error?.Message);

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
                request.Username, errorMsg, privStatus);

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

        _logger.LogInformation("Autenticación exitosa y privilegios mapeados vía BFF para usuario '{Username}'.", request.Username);
        return Ok(new LoginResponseDto
        {
            Success = true,
            User = user,
            Message = string.Empty
        });
    }

    /// <summary>
    /// Consulta los privilegios del usuario a partir del token de sesión proporcionado en cabecera.
    /// </summary>
    [HttpGet("privileges")]
    public async Task<IActionResult> Privileges()
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

        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new BackendErrorDto(StatusCodes.Status401Unauthorized, "Token de sesión no proporcionado"));
        }

        var authProvider = _configuration.GetValue<string>("AuthProvider", "Mock") ?? "Mock";

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
                    p.Access?.Approve ?? false)
            )).ToList();

            var responseDto = new BackendPrivilegesResponseDto("mock_user", privilegeItems);
            return Ok(responseDto);
        }

        // Modo API: Consulta hacia el backend externo a través del BFF
        var (data, error, statusCode) = await _backendApiService.GetPrivilegesAsync(token, HttpContext.RequestAborted);

        if (data == null)
        {
            _logger.LogWarning("Fallo al obtener privilegios desde el backend externo (Código {StatusCode}).", statusCode);
            return StatusCode(statusCode, error ?? new BackendErrorDto(statusCode, "Error al consultar privilegios"));
        }

        return Ok(data);
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
}
