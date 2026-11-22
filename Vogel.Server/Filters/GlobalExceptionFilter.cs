using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Vogel.Server.Filters;

/// <summary>
/// Filtro global de excepciones para controladores API.
/// Captura, registra y sanitiza cualquier error no controlado para evitar fuga de información
/// sensible en conformidad con OWASP Top 10 y CWE-200.
/// Corresponde a la sección 4.4 del Requerimiento Maestro.
/// </summary>
public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public void OnException(ExceptionContext context)
    {
        _logger.LogError(
            context.Exception,
            "Excepción no controlada capturada en [{Method}] {Path}: {Message}",
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path,
            context.Exception.Message);

        var responsePayload = new
        {
            success = false,
            message = "Ha ocurrido un error interno en el servidor. Por favor, intente nuevamente o contacte al administrador.",
            errorId = context.HttpContext.TraceIdentifier,
            details = _environment.IsDevelopment() ? context.Exception.Message : null
        };

        context.Result = new ObjectResult(responsePayload)
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };

        context.ExceptionHandled = true;
    }
}
