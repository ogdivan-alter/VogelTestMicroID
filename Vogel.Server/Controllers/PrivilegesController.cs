using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using Vogel.Server.Configuration;
using Vogel.Server.Services;
using Vogel.Shared.DTOs;
using Vogel.Shared.Enums;

namespace Vogel.Server.Controllers;

/// <summary>
/// Controlador BFF (Backend for Frontend) agnóstico al proveedor para la administración de privilegios.
/// Delega exclusivamente en <see cref="IPrivilegeAdminProviderService"/> y autoriza cada operación
/// mediante <see cref="IPrivilegeAuthorizationService"/> usando el codename configurado en <see cref="ModulePrivilegesOptions"/>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
public class PrivilegesController : ControllerBase
{
    private const int MaxCodenameLength = 100;
    private const int MaxGroupNameLength = 100;
    private const int MaxTitleLength = 150;
    private const int MaxActionLength = 250;
    private const int MaxIconLength = 100;
    private const int MaxDelDescriptionLength = 500;

    private readonly IPrivilegeAdminProviderService _privilegeAdminProvider;
    private readonly IPrivilegeAuthorizationService _privilegeAuthorization;
    private readonly string _modulePrivilegeCodename;
    private readonly ILogger<PrivilegesController> _logger;

    public PrivilegesController(
        IPrivilegeAdminProviderService privilegeAdminProvider,
        IPrivilegeAuthorizationService privilegeAuthorization,
        IOptions<ModulePrivilegesOptions> modulePrivilegesOptions,
        ILogger<PrivilegesController> logger)
    {
        _privilegeAdminProvider = privilegeAdminProvider;
        _privilegeAuthorization = privilegeAuthorization;
        _modulePrivilegeCodename = modulePrivilegesOptions?.Value?.Privileges?.Trim() ?? string.Empty;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene el listado administrativo de privilegios del sistema configurado.
    /// Exige token de sesión en cabecera Authorization: Bearer o X-Session-Token y permiso Read en adm_priv.
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

        var authFailure = await AuthorizeModuleActionAsync(token, PrivilegeAction.Read);
        if (authFailure is not null)
        {
            return authFailure;
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

    /// <summary>
    /// Obtiene el detalle de un privilegio activo por su identificador.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var token = ExtractSessionToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Solicitud a GET /api/privileges/{PrivilegeId} rechazada: token de sesión no proporcionado.", id);
            return Unauthorized(new BackendErrorDto(StatusCodes.Status401Unauthorized, "Token de sesión no proporcionado."));
        }

        var authFailure = await AuthorizeModuleActionAsync(token, PrivilegeAction.Read);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (id <= 0)
        {
            return BadRequest(new BackendErrorDto(StatusCodes.Status400BadRequest, "El identificador del privilegio debe ser mayor a 0."));
        }

        var result = await _privilegeAdminProvider.GetPrivilegeAsync(token, id, HttpContext.RequestAborted);

        if (!result.Success || result.Data is null)
        {
            return StatusCode(
                result.StatusCode,
                result.Error ?? new BackendErrorDto(result.StatusCode, "Error al consultar el detalle del privilegio."));
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Crea un nuevo privilegio en el sistema configurado del BFF.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePrivilegeRequestDto? request)
    {
        var token = ExtractSessionToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Solicitud a POST /api/privileges rechazada: token de sesión no proporcionado.");
            return Unauthorized(new BackendErrorDto(StatusCodes.Status401Unauthorized, "Token de sesión no proporcionado."));
        }

        var authFailure = await AuthorizeModuleActionAsync(token, PrivilegeAction.Create);
        if (authFailure is not null)
        {
            return authFailure;
        }

        var validationError = ValidatePrivilegePayload(
            request?.IdParent,
            request?.Codename,
            request?.GroupName,
            request?.Title,
            request?.Action,
            request?.Icon);

        if (validationError is not null || request is null)
        {
            return BadRequest(new BackendErrorDto(
                StatusCodes.Status400BadRequest,
                validationError ?? "Los datos del privilegio son obligatorios."));
        }

        var result = await _privilegeAdminProvider.CreatePrivilegeAsync(token, request, HttpContext.RequestAborted);

        if (!result.Success || result.Data is null)
        {
            return StatusCode(
                result.StatusCode,
                result.Error ?? new BackendErrorDto(result.StatusCode, "Error al crear el privilegio."));
        }

        return StatusCode(StatusCodes.Status201Created, result.Data);
    }

    /// <summary>
    /// Actualiza los atributos y jerarquía de un privilegio activo.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePrivilegeRequestDto? request)
    {
        var token = ExtractSessionToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Solicitud a PUT /api/privileges/{PrivilegeId} rechazada: token de sesión no proporcionado.", id);
            return Unauthorized(new BackendErrorDto(StatusCodes.Status401Unauthorized, "Token de sesión no proporcionado."));
        }

        var authFailure = await AuthorizeModuleActionAsync(token, PrivilegeAction.Modify);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (id <= 0)
        {
            return BadRequest(new BackendErrorDto(StatusCodes.Status400BadRequest, "El identificador del privilegio debe ser mayor a 0."));
        }

        var validationError = ValidatePrivilegePayload(
            request?.IdParent,
            request?.Codename,
            request?.GroupName,
            request?.Title,
            request?.Action,
            request?.Icon);

        if (validationError is not null || request is null)
        {
            return BadRequest(new BackendErrorDto(
                StatusCodes.Status400BadRequest,
                validationError ?? "Los datos del privilegio son obligatorios."));
        }

        if (request.IdParent.HasValue && request.IdParent.Value == id)
        {
            return UnprocessableEntity(new BackendErrorDto(
                StatusCodes.Status422UnprocessableEntity,
                "Un privilegio no puede ser su propio padre jerárquico."));
        }

        var result = await _privilegeAdminProvider.UpdatePrivilegeAsync(token, id, request, HttpContext.RequestAborted);

        if (!result.Success || result.Data is null)
        {
            return StatusCode(
                result.StatusCode,
                result.Error ?? new BackendErrorDto(result.StatusCode, "Error al actualizar el privilegio."));
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Inactiva lógicamente un privilegio activo con motivo opcional.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(
        int id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] DeactivatePrivilegeRequestDto? request = null)
    {
        var token = ExtractSessionToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Solicitud a DELETE /api/privileges/{PrivilegeId} rechazada: token de sesión no proporcionado.", id);
            return Unauthorized(new BackendErrorDto(StatusCodes.Status401Unauthorized, "Token de sesión no proporcionado."));
        }

        var authFailure = await AuthorizeModuleActionAsync(token, PrivilegeAction.Delete);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (id <= 0)
        {
            return BadRequest(new BackendErrorDto(StatusCodes.Status400BadRequest, "El identificador del privilegio debe ser mayor a 0."));
        }

        if (request?.DelDescription is { Length: > MaxDelDescriptionLength })
        {
            return BadRequest(new BackendErrorDto(
                StatusCodes.Status400BadRequest,
                $"El motivo de inactivación no puede exceder los {MaxDelDescriptionLength} caracteres."));
        }

        var result = await _privilegeAdminProvider.DeactivatePrivilegeAsync(token, id, request, HttpContext.RequestAborted);

        if (!result.Success || result.Data is null)
        {
            return StatusCode(
                result.StatusCode,
                result.Error ?? new BackendErrorDto(result.StatusCode, "Error al inactivar el privilegio."));
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Reactiva lógicamente un privilegio inactivo cambiando su estado a activo.
    /// </summary>
    [HttpPost("{id:int}/reactivate")]
    public async Task<IActionResult> Reactivate(int id)
    {
        var token = ExtractSessionToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Solicitud a POST /api/privileges/{PrivilegeId}/reactivate rechazada: token de sesión no proporcionado.", id);
            return Unauthorized(new BackendErrorDto(StatusCodes.Status401Unauthorized, "Token de sesión no proporcionado."));
        }

        var authFailure = await AuthorizeModuleActionAsync(token, PrivilegeAction.Create);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (id <= 0)
        {
            return BadRequest(new BackendErrorDto(StatusCodes.Status400BadRequest, "El identificador del privilegio debe ser mayor a 0."));
        }

        var result = await _privilegeAdminProvider.ReactivatePrivilegeAsync(token, id, HttpContext.RequestAborted);

        if (!result.Success || result.Data is null)
        {
            return StatusCode(
                result.StatusCode,
                result.Error ?? new BackendErrorDto(result.StatusCode, "Error al reactivar el privilegio."));
        }

        return Ok(result.Data);
    }

    private async Task<IActionResult?> AuthorizeModuleActionAsync(string token, PrivilegeAction requiredAction)
    {
        var authResult = await _privilegeAuthorization.AuthorizeAsync(
            token,
            _modulePrivilegeCodename,
            requiredAction,
            HttpContext.RequestAborted);

        if (!authResult.IsAuthorized)
        {
            return StatusCode(
                authResult.StatusCode,
                authResult.Error ?? new BackendErrorDto(
                    authResult.StatusCode,
                    "No cuenta con permisos suficientes para realizar esta operación."));
        }

        return null;
    }

    private static string? ValidatePrivilegePayload(
        int? idParent,
        string? codename,
        string? groupName,
        string? title,
        string? action,
        string? icon)
    {
        if (idParent.HasValue && idParent.Value < 0)
        {
            return "El privilegio padre debe ser mayor o igual a 0.";
        }

        if (string.IsNullOrWhiteSpace(codename))
        {
            return "El campo codename es obligatorio.";
        }

        if (codename.Trim().Length > MaxCodenameLength)
        {
            return $"El campo codename no puede exceder los {MaxCodenameLength} caracteres.";
        }

        if (string.IsNullOrWhiteSpace(groupName))
        {
            return "El campo group_name es obligatorio.";
        }

        if (groupName.Trim().Length > MaxGroupNameLength)
        {
            return $"El campo group_name no puede exceder los {MaxGroupNameLength} caracteres.";
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return "El campo title es obligatorio.";
        }

        if (title.Trim().Length > MaxTitleLength)
        {
            return $"El campo title no puede exceder los {MaxTitleLength} caracteres.";
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            return "El campo action es obligatorio.";
        }

        if (action.Trim().Length > MaxActionLength)
        {
            return $"El campo action no puede exceder los {MaxActionLength} caracteres.";
        }

        if (!string.IsNullOrWhiteSpace(icon) && icon.Trim().Length > MaxIconLength)
        {
            return $"El campo icon no puede exceder los {MaxIconLength} caracteres.";
        }

        return null;
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
