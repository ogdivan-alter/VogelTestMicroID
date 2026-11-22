using System.Text.Json.Serialization;

namespace Vogel.Server.Models.MicroId;

/// <summary>
/// Credenciales enviadas dentro de POST /login hacia MicroID.
/// </summary>
public sealed record LoginCredentialRequest(
    [property: JsonPropertyName("secret")] string? Secret = null,
    [property: JsonPropertyName("otp_code")] string? OtpCode = null,
    [property: JsonPropertyName("provider_token")] string? ProviderToken = null);

/// <summary>
/// Payload de solicitud para POST /login en MicroID.
/// </summary>
public sealed record MicroIdLoginRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("credential")] LoginCredentialRequest? Credential = null);

/// <summary>
/// Matriz booleana de los 7 permisos verbales sobre un privilegio en MicroID.
/// </summary>
public sealed record MicroIdPrivilegeAccess(
    [property: JsonPropertyName("create")] bool Create = false,
    [property: JsonPropertyName("modify")] bool Modify = false,
    [property: JsonPropertyName("read")] bool Read = false,
    [property: JsonPropertyName("delete")] bool Delete = false,
    [property: JsonPropertyName("validate")] bool Validate = false,
    [property: JsonPropertyName("execute")] bool Execute = false,
    [property: JsonPropertyName("approve")] bool Approve = false);

/// <summary>
/// Nodo consolidado de privilegio devuelto por POST /login y POST /auth en MicroID.
/// </summary>
public sealed record PrivilegeNodeDto(
    [property: JsonPropertyName("id_privilege")] int IdPrivilege,
    [property: JsonPropertyName("id_parent")] int IdParent,
    [property: JsonPropertyName("codename")] string Codename,
    [property: JsonPropertyName("group_name")] string GroupName,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("icon")] string? Icon,
    [property: JsonPropertyName("access")] MicroIdPrivilegeAccess Access);

/// <summary>
/// Respuesta de POST /login en MicroID.
/// </summary>
public sealed record MicroIdLoginResponse(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("privileges")] IReadOnlyList<PrivilegeNodeDto> Privileges);

/// <summary>
/// Payload de solicitud para POST /auth en MicroID.
/// </summary>
public sealed record AuthValidateRequest(
    [property: JsonPropertyName("username")] string Username);

/// <summary>
/// Respuesta de POST /auth en MicroID.
/// </summary>
public sealed record AuthValidateResponse(
    [property: JsonPropertyName("privileges")] IReadOnlyList<PrivilegeNodeDto> Privileges);
