using System.Text.Json.Serialization;

namespace Vogel.Server.Models.MicroId;

/// <summary>
/// Payload de solicitud para POST /creds/create en MicroID.
/// </summary>
public sealed record CreateCredRequest(
    [property: JsonPropertyName("id_system")] int IdSystem,
    [property: JsonPropertyName("renovation")] string? Renovation = null,
    [property: JsonPropertyName("date_expiration")] DateTimeOffset? DateExpiration = null);

/// <summary>
/// Payload de respuesta de POST /creds/create con la llave de sistema en texto plano.
/// </summary>
public sealed record CreateCredResponse(
    [property: JsonPropertyName("id_cred")] int IdCred,
    [property: JsonPropertyName("id_system")] int IdSystem,
    [property: JsonPropertyName("plain_api_key")] string PlainApiKey,
    [property: JsonPropertyName("date_expiration")] DateTimeOffset DateExpiration,
    [property: JsonPropertyName("renovation")] string Renovation);
