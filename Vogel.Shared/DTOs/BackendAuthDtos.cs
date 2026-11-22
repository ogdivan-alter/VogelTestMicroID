using System.Text.Json.Serialization;

namespace Vogel.Shared.DTOs;

/// <summary>
/// Solicitud de autenticación enviada hacia el MockBackend API.
/// </summary>
public record BackendLoginRequestDto(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);

/// <summary>
/// Perfil del usuario devuelto por el MockBackend API tras un login exitoso.
/// </summary>
public record BackendLoginResponseDto(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("avatar")] string Avatar,
    [property: JsonPropertyName("type_authentication")] string TypeAuthentication,
    [property: JsonPropertyName("allow_password_change")] bool AllowPasswordChange);

/// <summary>
/// Estructura de respuesta de privilegios retornada por el MockBackend API.
/// </summary>
public record BackendPrivilegesResponseDto(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("privileges")] List<BackendPrivilegeItemDto> Privileges);

/// <summary>
/// Elemento de privilegio retornado por el MockBackend API.
/// </summary>
public record BackendPrivilegeItemDto(
    [property: JsonPropertyName("id_parent")] int IdParent,
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("codename")] string Codename,
    [property: JsonPropertyName("group")] string Group,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("icon")] string Icon,
    [property: JsonPropertyName("access")] BackendAccessDto Access);

/// <summary>
/// Permisos de acceso granulares retornados por el MockBackend API.
/// </summary>
public record BackendAccessDto(
    [property: JsonPropertyName("read")] bool Read,
    [property: JsonPropertyName("modify")] bool Modify,
    [property: JsonPropertyName("write")] bool Write,
    [property: JsonPropertyName("delete")] bool Delete,
    [property: JsonPropertyName("approve")] bool Approve);

/// <summary>
/// Estructura de error devuelta por el MockBackend API (401, 403, etc.).
/// </summary>
public record BackendErrorDto(
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("message")] string Message);

