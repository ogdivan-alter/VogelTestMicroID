using Microsoft.AspNetCore.Mvc;
using Vogel.Server.Services;

namespace Vogel.Server.Controllers;

/// <summary>
/// Controlador API BFF que materializa y gestiona las acciones operativas de las entidades maestro-detalle (BlockWork).
/// En modo 'Api' delega la ejecución de la acción de negocio al backend remoto a través de IBackendApiService;
/// en modo 'Mock' emula las acciones de forma local.
/// </summary>
[ApiController]
[Route("api")]
public class BlockWorkController : ControllerBase
{
    private readonly IBackendApiService? _backendApiService;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<BlockWorkController>? _logger;

    public BlockWorkController(
        IBackendApiService? backendApiService = null,
        IConfiguration? configuration = null,
        ILogger<BlockWorkController>? logger = null)
    {
        _backendApiService = backendApiService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Acción de captura de objetos detalle asociados o por asociar al objeto maestro.
    /// </summary>
    [HttpPost("{codename}/capture")]
    public async Task<ActionResult<object>> Capture(string codename)
        => await HandleActionAsync(codename, nameof(Capture));

    /// <summary>
    /// Acción de desasociación o remoción de objetos detalle del objeto maestro.
    /// </summary>
    [HttpPost("{codename}/remove")]
    public async Task<ActionResult<object>> Remove(string codename)
        => await HandleActionAsync(codename, nameof(Remove));

    /// <summary>
    /// Acción de cancelación de cambios sobre el objeto maestro (incluye cancelación de creación).
    /// </summary>
    [HttpPost("{codename}/cancel-master")]
    public async Task<ActionResult<object>> CancelMaster(string codename)
        => await HandleActionAsync(codename, nameof(CancelMaster));

    /// <summary>
    /// Acción de ejecución o aplicación definitiva de cambios sobre el objeto maestro.
    /// </summary>
    [HttpPost("{codename}/apply-master")]
    public async Task<ActionResult<object>> ApplyMaster(string codename)
        => await HandleActionAsync(codename, nameof(ApplyMaster));

    /// <summary>
    /// Acción para reversar cambios aplicados previamente sobre el objeto maestro.
    /// </summary>
    [HttpPost("{codename}/rev-apply-master")]
    public async Task<ActionResult<object>> RevApplyMaster(string codename)
        => await HandleActionAsync(codename, nameof(RevApplyMaster));

    /// <summary>
    /// Acción para aplicar filtros sobre la lista de objetos detalle o generar el listado inicial.
    /// </summary>
    [HttpPost("{codename}/list-detail-master")]
    public async Task<ActionResult<object>> ListDetailMaster(string codename)
        => await HandleActionAsync(codename, nameof(ListDetailMaster));

    /// <summary>
    /// Acción para la descarga de un documento representativo de la entidad (ejemplo: PDF).
    /// </summary>
    [HttpGet("{codename}/doc-master")]
    public async Task<ActionResult<object>> DocMaster(string codename)
        => await HandleActionAsync(codename, nameof(DocMaster));

    /// <summary>
    /// Acción para la descarga de un documento tipo reporte detallado de la entidad (ejemplo: Excel o CSV).
    /// </summary>
    [HttpGet("{codename}/rep-master")]
    public async Task<ActionResult<object>> RepMaster(string codename)
        => await HandleActionAsync(codename, nameof(RepMaster));

    /// <summary>
    /// Procesa la acción del bloque de trabajo gestionando la delegación hacia el backend externo o emulación local.
    /// </summary>
    private async Task<ActionResult<object>> HandleActionAsync(string codename, string action, object? payload = null)
    {
        if (string.IsNullOrWhiteSpace(codename))
        {
            return BadRequest(new { success = false, message = "El codename de la entidad es obligatorio." });
        }

        var authProvider = _configuration?.GetValue<string>("AuthProvider", "Mock") ?? "Mock";
        if (string.Equals(authProvider, "Api", StringComparison.OrdinalIgnoreCase) && _backendApiService != null)
        {
            string? token = null;
            if (Request?.Headers != null && Request.Headers.TryGetValue("Authorization", out var authHeaders))
            {
                var authHeader = authHeaders.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    token = authHeader["Bearer ".Length..].Trim();
                }
            }

            var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;
            var (success, data, error, statusCode) = await _backendApiService.ExecuteBlockWorkActionAsync(
                codename, action, payload, token, cancellationToken);

            if (success && data != null)
            {
                _logger?.LogInformation("Acción {Action} para entidad {Codename} ejecutada exitosamente en backend remoto.", action, codename);
                return Ok(data);
            }
        }

        return Ok(new
        {
            success = true,
            codename,
            action,
            emulated = true,
            timestamp = DateTime.UtcNow
        });
    }
}
