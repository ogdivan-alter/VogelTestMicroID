using System.Net.Http.Json;
using Vogel.Shared.DTOs;

namespace Vogel.Client.Services;

/// <summary>
/// Servicio de logging en el cliente que envía registros y excepciones de forma asíncrona y no bloqueante
/// hacia el endpoint de logging centralizado del servidor Vogel.Server.
/// </summary>
public class ClientLogger : IClientLogger
{
    private const string LogEndpoint = "api/logs";
    private readonly HttpClient _httpClient;

    public ClientLogger(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task LogInformationAsync(string message, string? source = null)
    {
        return SendLogAsync("Information", message, null, source);
    }

    public Task LogWarningAsync(string message, string? source = null)
    {
        return SendLogAsync("Warning", message, null, source);
    }

    public Task LogErrorAsync(string message, Exception? exception = null, string? source = null)
    {
        return SendLogAsync("Error", message, exception?.ToString(), source);
    }

    private Task SendLogAsync(string level, string message, string? stackTrace, string? source)
    {
        var dto = new ClientLogDto
        {
            LogLevel = level,
            Message = message,
            Source = source ?? "Vogel.Client",
            StackTrace = stackTrace,
            Timestamp = DateTime.UtcNow
        };

        // Envío asíncrono no bloqueante hacia el servidor para garantizar óptimo rendimiento y UX
        _ = Task.Run(async () =>
        {
            try
            {
                await _httpClient.PostAsJsonAsync(LogEndpoint, dto);
            }
            catch
            {
                // Falla silenciosa y controlada en caso de desconexión momentánea de red
                // para evitar que un fallo en logging detenga el flujo de la aplicación
            }
        });

        return Task.CompletedTask;
    }
}
