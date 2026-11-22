using System.Text.Json.Serialization;

namespace Vogel.Server.Models.MicroId;

/// <summary>
/// Payload de solicitud para POST /user/get en MicroID.
/// </summary>
public sealed record GetUserRequest(
    [property: JsonPropertyName("id_user")] int? IdUser = null,
    [property: JsonPropertyName("username")] string? Username = null);

/// <summary>
/// Detalle de usuario devuelto por POST /user/get en MicroID.
/// </summary>
public sealed record UserDetailResponse
{
    [JsonPropertyName("id_user")]
    public int IdUser { get; init; }

    [JsonPropertyName("external_id")]
    public string ExternalId { get; init; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("sysauth")]
    public string Sysauth { get; init; } = string.Empty;

    [JsonPropertyName("mfa")]
    public string Mfa { get; init; } = "NO";

    [JsonPropertyName("alias")]
    public string Alias { get; init; } = string.Empty;

    [JsonPropertyName("first_name")]
    public string FirstName { get; init; } = string.Empty;

    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }

    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("avatar")]
    public string? Avatar { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "A";

    [JsonPropertyName("date_creation")]
    public DateTimeOffset DateCreation { get; init; }

    [JsonPropertyName("user_creation")]
    public string? UserCreation { get; init; }

    [JsonPropertyName("date_modification")]
    public DateTimeOffset? DateModification { get; init; }

    [JsonPropertyName("user_modification")]
    public string? UserModification { get; init; }
}

/// <summary>
/// Payload de solicitud para POST /user/changepw en MicroID.
/// </summary>
public sealed record MicroIdChangePasswordRequest(
    [property: JsonPropertyName("id_user")] int IdUser,
    [property: JsonPropertyName("current_password")] string? CurrentPassword,
    [property: JsonPropertyName("verification_code")] string? VerificationCode,
    [property: JsonPropertyName("new_password")] string NewPassword);

/// <summary>
/// Respuesta de POST /user/changepw en MicroID.
/// </summary>
public sealed record MicroIdChangePasswordResponse(
    [property: JsonPropertyName("id_user")] int IdUser,
    [property: JsonPropertyName("password_updated_in_idp")] bool PasswordUpdatedInIdp,
    [property: JsonPropertyName("date_modification")] DateTimeOffset DateModification);
