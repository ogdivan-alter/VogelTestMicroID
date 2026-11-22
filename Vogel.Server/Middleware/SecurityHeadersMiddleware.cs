namespace Vogel.Server.Middleware;

/// <summary>
/// Middleware de cabeceras HTTP de seguridad para cumplimiento de estándares OWASP y CWE.
/// Carga dinámicamente las directivas de seguridad (CSP, X-Frame-Options, Referrer-Policy, X-XSS-Protection)
/// configuradas en appsettings.json.
/// Corresponde a la sección 4.4 del Requerimiento Maestro.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public SecurityHeadersMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headersSection = _configuration.GetSection("SecurityHeaders");
            if (headersSection.Exists())
            {
                foreach (var child in headersSection.GetChildren())
                {
                    if (!string.IsNullOrEmpty(child.Key) && !string.IsNullOrEmpty(child.Value))
                    {
                        if (!context.Response.Headers.ContainsKey(child.Key))
                        {
                            context.Response.Headers.Append(child.Key, child.Value);
                        }
                    }
                }
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}

/// <summary>
/// Métodos de extensión para registrar SecurityHeadersMiddleware en el pipeline de ASP.NET Core.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
