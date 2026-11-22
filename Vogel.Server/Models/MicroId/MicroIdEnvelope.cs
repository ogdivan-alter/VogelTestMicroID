using System.Text.Json.Serialization;

namespace Vogel.Server.Models.MicroId;

/// <summary>
/// Envoltorio estándar de solicitud hacia MicroID: { "data": { ... } }.
/// </summary>
public sealed class MicroIdRequestMessage<T>
{
    [JsonPropertyName("data")]
    public T Data { get; set; } = default!;

    public MicroIdRequestMessage()
    {
    }

    public MicroIdRequestMessage(T data)
    {
        Data = data;
    }
}

/// <summary>
/// Envoltorio estándar de respuesta devuelto por MicroID: { "code": "...", "message": "...", "data": { ... } }.
/// </summary>
public sealed class MicroIdResponseMessage<T>
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public T? Data { get; set; }
}

/// <summary>
/// Catálogo oficial de códigos de respuesta y error de MicroID.
/// </summary>
public static class MicroIdResponseCodes
{
    public const string Success = "SUCCESS";
    public const string MissingHeaders = "ERR_MISSING_HEADERS";
    public const string InvalidPayload = "ERR_INVALID_PAYLOAD";
    public const string InvalidSystemKey = "ERR_INVALID_SYSTEM_KEY";
    public const string TimestampOutOfRange = "ERR_TIMESTAMP_OUT_OF_RANGE";
    public const string AuthFailed = "ERR_AUTH_FAILED";
    public const string IpNotAllowed = "ERR_IP_NOT_ALLOWED";
    public const string SelfRegDisabled = "ERR_SELF_REG_DISABLED";
    public const string InactiveMaster = "ERR_INACTIVE_MASTER";
    public const string SystemMismatch = "ERR_SYSTEM_MISMATCH";
    public const string EmptyEffectiveList = "ERR_EMPTY_EFFECTIVE_LIST";
    public const string IncompatibleSysauth = "ERR_INCOMPATIBLE_SYSAUTH";
    public const string PasswordPolicy = "ERR_PASSWORD_POLICY";
    public const string IdpCommunication = "ERR_IDP_COMMUNICATION";
    public const string InternalServer = "ERR_INTERNAL_SERVER";
}
