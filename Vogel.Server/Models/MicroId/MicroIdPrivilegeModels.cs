using System.Text.Json.Serialization;

namespace Vogel.Server.Models.MicroId;

/// <summary>
/// Filtro de consulta para POST /priv/list en MicroID.
/// </summary>
public sealed record MicroIdListPrivilegeFilter(
    [property: JsonPropertyName("status")] string? Status = null,
    [property: JsonPropertyName("group_name")] string? GroupName = null,
    [property: JsonPropertyName("search")] string? Search = null);

/// <summary>
/// Payload de solicitud para POST /priv/list en MicroID.
/// </summary>
public sealed record MicroIdListPrivilegeRequest(
    [property: JsonPropertyName("id_system")] int IdSystem,
    [property: JsonPropertyName("filter")] MicroIdListPrivilegeFilter? Filter = null);

/// <summary>
/// Detalle de un privilegio retornado por POST /priv/list en MicroID.
/// </summary>
public sealed record MicroIdPrivilegeDetailResponse(
    [property: JsonPropertyName("id_privilege")] int IdPrivilege,
    [property: JsonPropertyName("id_system")] int IdSystem,
    [property: JsonPropertyName("id_parent")] int IdParent,
    [property: JsonPropertyName("codename")] string Codename,
    [property: JsonPropertyName("group_name")] string GroupName,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("icon")] string? Icon,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("date_creation")] DateTimeOffset DateCreation,
    [property: JsonPropertyName("user_creation")] string UserCreation,
    [property: JsonPropertyName("date_modification")] DateTimeOffset? DateModification = null,
    [property: JsonPropertyName("user_modification")] string? UserModification = null);

/// <summary>
/// Respuesta de POST /priv/list en MicroID.
/// </summary>
public sealed record MicroIdListPrivilegeResponse(
    [property: JsonPropertyName("id_system")] int IdSystem,
    [property: JsonPropertyName("items")] IReadOnlyList<MicroIdPrivilegeDetailResponse> Items,
    [property: JsonPropertyName("total")] int Total);
