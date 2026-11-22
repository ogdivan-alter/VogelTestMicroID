using Microsoft.AspNetCore.Mvc;

namespace Vogel.Server.Controllers;

/// <summary>
/// Controlador API que materializa las acciones de descarga de reportes.
/// En la Fase 2 las descargas se emulan; en la Fase 3 se generarán los documentos reales.
/// </summary>
[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    /// <summary>
    /// Acción para la descarga del reporte diario de la entidad o del sistema.
    /// </summary>
    [HttpGet("daily")]
    public ActionResult<object> Daily()
        => Report("daily");

    /// <summary>
    /// Acción para la descarga del reporte mensual de la entidad o del sistema.
    /// </summary>
    [HttpGet("monthly")]
    public ActionResult<object> Monthly()
        => Report("monthly");

    /// <summary>
    /// Genera una respuesta uniforme para los reportes emulados.
    /// </summary>
    private ActionResult<object> Report(string reportType)
        => Ok(new
        {
            success = true,
            reportType,
            format = "emulated",
            emulated = true,
            timestamp = DateTime.UtcNow
        });
}
