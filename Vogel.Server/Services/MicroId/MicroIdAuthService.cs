using System.Net;
using System.Text;
using System.Text.Json;
using Vogel.Server.Models.MicroId;
using Vogel.Shared.DTOs;
using Vogel.Shared.Models;

namespace Vogel.Server.Services.MicroId;

/// <summary>
/// Implementación del servicio de autenticación y autorización BFF contra MicroID.
/// </summary>
public sealed class MicroIdAuthService : IMicroIdAuthService
{
    private readonly IMicroIdApiClient _apiClient;
    private readonly ILogger<MicroIdAuthService> _logger;

    public MicroIdAuthService(
        IMicroIdApiClient apiClient,
        ILogger<MicroIdAuthService> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<MicroIdLoginResult> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var trimmedUsername = username.Trim();
        var safeUsername = SanitizeForLog(trimmedUsername);

        var loginPayload = new MicroIdLoginRequest(
            Username: trimmedUsername,
            Credential: new LoginCredentialRequest(Secret: password));

        var loginResult = await _apiClient.PostAsync<MicroIdLoginRequest, MicroIdLoginResponse>(
            "login",
            loginPayload,
            cancellationToken: cancellationToken);

        if (!loginResult.IsSuccess ||
            loginResult.Data is null ||
            string.IsNullOrWhiteSpace(loginResult.Data.Token))
        {
            var (statusCode, errorMessage) = MapLoginError(loginResult.Code, loginResult.StatusCode, loginResult.Message);
            _logger.LogWarning(
                "Autenticación MicroID rechazada para usuario '{Username}' (StatusCode: {StatusCode}, Code: {Code}).",
                safeUsername,
                statusCode,
                loginResult.Code);

            return new MicroIdLoginResult(false, statusCode, errorMessage, null, null);
        }

        var token = loginResult.Data.Token;

        if (loginResult.Data.Privileges is null || loginResult.Data.Privileges.Count == 0)
        {
            _logger.LogWarning(
                "Acceso denegado en MicroID para usuario '{Username}': sin privilegios efectivos.",
                safeUsername);

            return new MicroIdLoginResult(
                false,
                StatusCodes.Status403Forbidden,
                "El usuario no cuenta con privilegios en el sistema",
                null,
                null);
        }

        // Enriquecer perfil mediante POST /user/get (con fallback seguro si falla)
        var (name, avatar, typeAuth, allowPasswordChange) =
            await TryGetUserProfileAsync(trimmedUsername, safeUsername, token, cancellationToken);

        var privileges = loginResult.Data.Privileges
            .Select((node, index) => MapToPrivilege(node, index + 1))
            .ToList();

        var user = new User
        {
            Username = trimmedUsername,
            Name = name,
            Avatar = avatar,
            TypeAuthentication = typeAuth,
            AllowPasswordChange = allowPasswordChange,
            BaseRole = "Único",
            Privileges = privileges
        };

        _logger.LogInformation(
            "Autenticación MicroID exitosa para usuario '{Username}' ({PrivilegeCount} privilegios).",
            safeUsername,
            privileges.Count);

        return new MicroIdLoginResult(true, StatusCodes.Status200OK, string.Empty, user, token);
    }

    public async Task<MicroIdPrivilegesResult> GetPrivilegesAsync(
        string bearerToken,
        string? username,
        CancellationToken cancellationToken = default)
    {
        var effectiveUsername = !string.IsNullOrWhiteSpace(username)
            ? username.Trim()
            : ExtractUsernameFromJwt(bearerToken);

        if (string.IsNullOrWhiteSpace(effectiveUsername))
        {
            return new MicroIdPrivilegesResult(
                false,
                StatusCodes.Status401Unauthorized,
                null,
                new BackendErrorDto(StatusCodes.Status401Unauthorized, "No se pudo identificar al usuario desde el token de sesión."));
        }

        var safeUsername = SanitizeForLog(effectiveUsername);

        var authResult = await _apiClient.PostAsync<AuthValidateRequest, AuthValidateResponse>(
            "auth",
            new AuthValidateRequest(effectiveUsername),
            bearerToken: bearerToken,
            cancellationToken: cancellationToken);

        if (!authResult.IsSuccess || authResult.Data is null)
        {
            var (statusCode, errorMessage) = MapError(authResult.Code, authResult.StatusCode, authResult.Message);
            _logger.LogWarning(
                "Validación de privilegios MicroID (/auth) rechazada para usuario '{Username}' (StatusCode: {StatusCode}, Code: {Code}).",
                safeUsername,
                statusCode,
                authResult.Code);

            return new MicroIdPrivilegesResult(
                false,
                statusCode,
                null,
                new BackendErrorDto(statusCode, errorMessage));
        }

        var items = authResult.Data.Privileges
            .Select(node => new BackendPrivilegeItemDto(
                IdParent: node.IdParent,
                Id: node.IdPrivilege,
                Codename: node.Codename,
                Group: node.GroupName ?? string.Empty,
                Title: node.Title,
                Action: node.Action,
                Icon: node.Icon ?? string.Empty,
                Access: new BackendAccessDto(
                    Read: node.Access.Read,
                    Modify: node.Access.Modify,
                    Write: node.Access.Create,
                    Delete: node.Access.Delete,
                    Approve: node.Access.Approve,
                    Validate: node.Access.Validate,
                    Execute: node.Access.Execute)))
            .ToList();

        return new MicroIdPrivilegesResult(
            true,
            StatusCodes.Status200OK,
            new BackendPrivilegesResponseDto(effectiveUsername, items),
            null);
    }

    public async Task<MicroIdChangePasswordResult> ChangePasswordAsync(
        string? bearerToken,
        ChangePasswordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var hasVerificationCode = !string.IsNullOrWhiteSpace(request.VerificationCode);

        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return new MicroIdChangePasswordResult(
                false,
                StatusCodes.Status401Unauthorized,
                new ChangePasswordResponseDto
                {
                    Success = false,
                    Message = "Token de sesión no proporcionado."
                });
        }

        if (!hasVerificationCode && string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            return new MicroIdChangePasswordResult(
                false,
                StatusCodes.Status400BadRequest,
                new ChangePasswordResponseDto
                {
                    Success = false,
                    Message = "La contraseña actual es requerida."
                });
        }

        var username = ExtractUsernameFromJwt(bearerToken);
        if (string.IsNullOrWhiteSpace(username))
        {
            return new MicroIdChangePasswordResult(
                false,
                StatusCodes.Status401Unauthorized,
                new ChangePasswordResponseDto
                {
                    Success = false,
                    Message = "Token de sesión inválido."
                });
        }

        var safeUsername = SanitizeForLog(username);

        // Anti-BOLA/IDOR: resolver id_user exclusivamente desde el perfil del token Bearer autenticado
        var userResult = await _apiClient.PostAsync<GetUserRequest, UserDetailResponse>(
            "user/get",
            new GetUserRequest(Username: username),
            bearerToken: bearerToken,
            cancellationToken: cancellationToken);

        if (!userResult.IsSuccess || userResult.Data is null || userResult.Data.IdUser <= 0)
        {
            var (statusCode, errorMessage) = MapError(userResult.Code, userResult.StatusCode, userResult.Message);
            _logger.LogWarning(
                "No se pudo resolver id_user vía /user/get para cambio de contraseña de '{Username}' (StatusCode: {StatusCode}, Code: {Code}).",
                safeUsername,
                statusCode,
                userResult.Code);

            return new MicroIdChangePasswordResult(
                false,
                statusCode,
                new ChangePasswordResponseDto
                {
                    Success = false,
                    Message = errorMessage
                });
        }

        var changePayload = new MicroIdChangePasswordRequest(
            IdUser: userResult.Data.IdUser,
            CurrentPassword: request.CurrentPassword,
            VerificationCode: request.VerificationCode,
            NewPassword: request.NewPassword);

        var changeResult = await _apiClient.PostAsync<MicroIdChangePasswordRequest, MicroIdChangePasswordResponse>(
            "user/changepw",
            changePayload,
            bearerToken: hasVerificationCode ? null : bearerToken,
            cancellationToken: cancellationToken);

        if (!changeResult.IsSuccess)
        {
            var (statusCode, errorMessage) = MapChangePasswordError(
                changeResult.Code,
                changeResult.StatusCode,
                changeResult.Message);

            _logger.LogWarning(
                "Cambio de contraseña rechazado en MicroID para '{Username}' (StatusCode: {StatusCode}, Code: {Code}).",
                safeUsername,
                statusCode,
                changeResult.Code);

            return new MicroIdChangePasswordResult(
                false,
                statusCode,
                new ChangePasswordResponseDto
                {
                    Success = false,
                    Message = errorMessage
                });
        }

        _logger.LogInformation(
            "Contraseña actualizada exitosamente en MicroID para '{Username}'.",
            safeUsername);

        return new MicroIdChangePasswordResult(
            true,
            StatusCodes.Status200OK,
            new ChangePasswordResponseDto
            {
                Success = true,
                Message = "Contraseña actualizada exitosamente."
            });
    }

    private async Task<(string Name, string Avatar, string TypeAuthentication, bool AllowPasswordChange)>
        TryGetUserProfileAsync(
            string username,
            string safeUsername,
            string bearerToken,
            CancellationToken cancellationToken)
    {
        try
        {
            var userResult = await _apiClient.PostAsync<GetUserRequest, UserDetailResponse>(
                "user/get",
                new GetUserRequest(Username: username),
                bearerToken: bearerToken,
                cancellationToken: cancellationToken);

            if (userResult.IsSuccess && userResult.Data is not null)
            {
                var detail = userResult.Data;
                var fullName = $"{detail.FirstName} {detail.LastName}".Trim();
                var displayName = !string.IsNullOrWhiteSpace(fullName)
                    ? fullName
                    : (!string.IsNullOrWhiteSpace(detail.Alias) ? detail.Alias : username);

                var typeAuth = string.Equals(detail.Sysauth, "self", StringComparison.OrdinalIgnoreCase)
                    ? "INTERNA"
                    : "EXTERNA";

                var allowPasswordChange = string.Equals(typeAuth, "INTERNA", StringComparison.Ordinal);

                return (displayName, detail.Avatar ?? string.Empty, typeAuth, allowPasswordChange);
            }

            _logger.LogWarning(
                "No se pudo enriquecer perfil vía /user/get para '{Username}' (Code: {Code}). Usando valores por defecto.",
                safeUsername,
                userResult.Code);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Excepción al consultar /user/get para '{Username}'. Usando valores de perfil por defecto.",
                safeUsername);
        }

        return (username, string.Empty, "INTERNA", true);
    }

    private static Privilege MapToPrivilege(PrivilegeNodeDto node, int order) => new()
    {
        Id = node.IdPrivilege,
        IdParent = node.IdParent,
        Codename = node.Codename,
        Group = node.GroupName ?? string.Empty,
        Title = node.Title,
        Action = node.Action,
        Icon = node.Icon ?? string.Empty,
        Order = order,
        Access = new PrivilegeAccess
        {
            Create = node.Access.Create,
            Modify = node.Access.Modify,
            Read = node.Access.Read,
            Delete = node.Access.Delete,
            Validate = node.Access.Validate,
            Execute = node.Access.Execute,
            Approve = node.Access.Approve
        }
    };

    private static (int StatusCode, string Message) MapLoginError(
        string code,
        HttpStatusCode httpStatus,
        string rawMessage)
    {
        return code switch
        {
            MicroIdResponseCodes.AuthFailed => (StatusCodes.Status401Unauthorized, "Usuario o Clave incorrectos"),
            MicroIdResponseCodes.InactiveMaster or MicroIdResponseCodes.SystemMismatch =>
                ((int)httpStatus is >= 400 and < 500 ? (int)httpStatus : StatusCodes.Status403Forbidden, rawMessage),
            MicroIdResponseCodes.EmptyEffectiveList =>
                (StatusCodes.Status403Forbidden, "El usuario no cuenta con privilegios en el sistema"),
            MicroIdResponseCodes.IdpCommunication or MicroIdResponseCodes.InternalServer =>
                (StatusCodes.Status502BadGateway, "Error de comunicación con el servicio de identidad."),
            _ => ((int)httpStatus >= 400 ? (int)httpStatus : StatusCodes.Status401Unauthorized,
                string.IsNullOrWhiteSpace(rawMessage) ? "Usuario o Clave incorrectos" : rawMessage)
        };
    }

    private static (int StatusCode, string Message) MapChangePasswordError(
        string code,
        HttpStatusCode httpStatus,
        string rawMessage)
    {
        return code switch
        {
            MicroIdResponseCodes.PasswordPolicy =>
                (StatusCodes.Status400BadRequest, string.IsNullOrWhiteSpace(rawMessage) ? "La nueva contraseña no cumple la política de seguridad." : rawMessage),
            MicroIdResponseCodes.AuthFailed =>
                (StatusCodes.Status401Unauthorized, string.IsNullOrWhiteSpace(rawMessage) ? "Credenciales actuales inválidas o sesión expirada." : rawMessage),
            MicroIdResponseCodes.IdpCommunication or MicroIdResponseCodes.InternalServer =>
                (StatusCodes.Status502BadGateway, "Error de comunicación con el servicio de identidad."),
            _ => ((int)httpStatus >= 400 ? (int)httpStatus : StatusCodes.Status400BadRequest, rawMessage)
        };
    }

    private static (int StatusCode, string Message) MapError(
        string code,
        HttpStatusCode httpStatus,
        string rawMessage)
    {
        return code switch
        {
            MicroIdResponseCodes.AuthFailed =>
                (StatusCodes.Status401Unauthorized, string.IsNullOrWhiteSpace(rawMessage) ? "Sesión inválida o expirada." : rawMessage),
            MicroIdResponseCodes.InactiveMaster or MicroIdResponseCodes.SystemMismatch or MicroIdResponseCodes.EmptyEffectiveList =>
                (StatusCodes.Status403Forbidden, rawMessage),
            MicroIdResponseCodes.IdpCommunication or MicroIdResponseCodes.InternalServer =>
                (StatusCodes.Status502BadGateway, "Error de comunicación con el servicio de identidad."),
            _ => ((int)httpStatus >= 400 ? (int)httpStatus : StatusCodes.Status401Unauthorized, rawMessage)
        };
    }

    private static string? ExtractUsernameFromJwt(string bearerToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return null;
        }

        var parts = bearerToken.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            var payloadBase64 = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');

            switch (payloadBase64.Length % 4)
            {
                case 2:
                    payloadBase64 += "==";
                    break;
                case 3:
                    payloadBase64 += "=";
                    break;
            }

            var bytes = Convert.FromBase64String(payloadBase64);
            using var doc = JsonDocument.Parse(bytes);
            var root = doc.RootElement;

            foreach (var claim in new[] { "preferred_username", "unique_name", "sub" })
            {
                if (root.TryGetProperty(claim, out var prop) &&
                    prop.ValueKind == JsonValueKind.String)
                {
                    var val = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        return val;
                    }
                }
            }
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\r", string.Empty, StringComparison.Ordinal)
                    .Replace("\n", string.Empty, StringComparison.Ordinal);
    }
}
