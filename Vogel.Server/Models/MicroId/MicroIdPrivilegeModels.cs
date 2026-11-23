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

/// <summary>
/// Payload de solicitud para POST /priv/get en MicroID.
/// </summary>
public sealed record MicroIdGetPrivilegeRequest(
    [property: JsonPropertyName("id_privilege")] int IdPrivilege);

/// <summary>
/// Payload de solicitud para POST /priv/create en MicroID.
/// </summary>
public sealed record MicroIdCreatePrivilegeRequest(
    [property: JsonPropertyName("id_system")] int IdSystem,
    [property: JsonPropertyName("id_parent")] int? IdParent = 0,
    [property: JsonPropertyName("codename")] string Codename = "",
    [property: JsonPropertyName("group_name")] string GroupName = "",
    [property: JsonPropertyName("title")] string Title = "",
    [property: JsonPropertyName("action")] string Action = "",
    [property: JsonPropertyName("icon")] string? Icon = null);

/// <summary>
/// Respuesta de POST /priv/create en MicroID.
/// </summary>
public sealed record MicroIdCreatePrivilegeResponse(
    [property: JsonPropertyName("id_privilege")] int IdPrivilege,
    [property: JsonPropertyName("id_system")] int IdSystem,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("date_creation")] DateTimeOffset DateCreation);

/// <summary>
/// Payload de solicitud para POST /priv/update en MicroID.
/// </summary>
public sealed record MicroIdUpdatePrivilegeRequest(
    [property: JsonPropertyName("id_privilege")] int IdPrivilege,
    [property: JsonPropertyName("id_parent")] int? IdParent = null,
    [property: JsonPropertyName("codename")] string? Codename = null,
    [property: JsonPropertyName("group_name")] string? GroupName = null,
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("action")] string? Action = null,
    [property: JsonPropertyName("icon")] string? Icon = null);

/// <summary>
/// Respuesta de POST /priv/update en MicroID.
/// </summary>
public sealed record MicroIdUpdatePrivilegeResponse(
    [property: JsonPropertyName("id_privilege")] int IdPrivilege,
    [property: JsonPropertyName("updated")] bool Updated,
    [property: JsonPropertyName("date_modification")] DateTimeOffset DateModification);

/// <summary>
/// Payload de solicitud para POST /priv/delete en MicroID.
/// </summary>
public sealed record MicroIdDeletePrivilegeRequest(
    [property: JsonPropertyName("id_privilege")] int IdPrivilege,
    [property: JsonPropertyName("del_description")] string? DelDescription = null);

/// <summary>
/// Respuesta de POST /priv/delete en MicroID.
/// </summary>
public sealed record MicroIdDeletePrivilegeResponse(
    [property: JsonPropertyName("id_privilege")] int IdPrivilege,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("cascaded_role_privileges")] int CascadedRolePrivileges);

/// <summary>
/// Payload de solicitud para POST /priv/reactivate en MicroID.
/// </summary>
public sealed record MicroIdReactivatePrivilegeRequest(
    [property: JsonPropertyName("id_privilege")] int IdPrivilege);

/// <summary>
/// Respuesta de POST /priv/reactivate en MicroID.
/// </summary>
public sealed record MicroIdReactivatePrivilegeResponse(
    [property: JsonPropertyName("id_privilege")] int IdPrivilege,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("date_modification")] DateTimeOffset DateModification);
