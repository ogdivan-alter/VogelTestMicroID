using Microsoft.AspNetCore.Mvc;
using Vogel.Server.Services;
using Vogel.Shared.DTOs;

namespace Vogel.Server.Controllers;

/// <summary>
/// Controlador BFF (Backend for Frontend) agnóstico al proveedor para la administración de privilegios.
/// Delega exclusivamente en <see cref="IPrivilegeAdminProviderService"/>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
public class PrivilegesController : ControllerBase
{
    private readonly IPrivilegeAdminProviderService _privilegeAdminProvider;
    private readonly ILogger<PrivilegesController> _logger;

    public PrivilegesController(
        IPrivilegeAdminProviderService privilegeAdminProvider,
        ILogger<PrivilegesController> logger)
    {
        _privilegeAdminProvider = privilegeAdminProvider;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene el listado administrativo de privilegios del sistema configurado.
    /// Exige token de sesión en cabecera Authorization: Bearer o X-Session-Token.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status = null)
    {
        var token = ExtractSessionToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Solicitud a GET /api/privileges rechazada: token de sesión no proporcionado.");
            return Unauthorized(new BackendErrorDto(StatusCodes.Status401Unauthorized, "Token de sesión no proporcionado."));
        }

        var result = await _privilegeAdminProvider.ListPrivilegesAsync(token, status, HttpContext.RequestAborted);

        if (!result.Success || result.Data is null)
        {
            return StatusCode(
                result.StatusCode,
                result.Error ?? new BackendErrorDto(result.StatusCode, "Error al consultar los privilegios del sistema."));
        }

        return Ok(result.Data);
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
