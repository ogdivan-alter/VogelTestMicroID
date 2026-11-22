using Vogel.Shared.DTOs;
using Vogel.Shared.Models;

namespace Vogel.Shared.Mappers;

/// <summary>
/// Mapeador de DTOs del backend externo hacia modelos de dominio de Vogel (User, Privilege, PrivilegeAccess).
/// Centralizado en Vogel.Shared para su uso exclusivo por Vogel.Server (BFF) y pruebas de integración.
/// </summary>
public static class BackendUserMapper
{
    /// <summary>
    /// Convierte el perfil y los privilegios retornados por el Backend remoto en una entidad User de Vogel.
    /// </summary>
    public static User MapToUser(BackendLoginResponseDto profile, IEnumerable<BackendPrivilegeItemDto> backendPrivileges, string backendBaseUrl)
    {
        var normalizedBaseUrl = (backendBaseUrl ?? string.Empty).TrimEnd('/');

        var privileges = backendPrivileges.Select((item, index) =>
        {
            var iconUrl = item.Icon;
            if (!string.IsNullOrWhiteSpace(iconUrl) && iconUrl.StartsWith("/") && !string.IsNullOrWhiteSpace(normalizedBaseUrl))
            {
                iconUrl = $"{normalizedBaseUrl}{iconUrl}";
            }

            return new Privilege
            {
                Id = item.Id,
                IdParent = item.IdParent,
                Codename = item.Codename,
                Group = item.Group ?? string.Empty,
                Title = item.Title,
                Action = item.Action,
                Icon = iconUrl ?? string.Empty,
                Order = index + 1,
                Access = item.Access == null ? null : new PrivilegeAccess
                {
                    Read = item.Access.Read,
                    Modify = item.Access.Modify,
                    Create = item.Access.Write, // write mapeado a Create
                    Delete = item.Access.Delete,
                    Approve = item.Access.Approve,
                    Validate = item.Access.Validate,
                    Execute = item.Access.Execute
                }
            };
        }).ToList();

        return new User
        {
            Username = profile.Username,
            Name = profile.Name ?? profile.Username,
            Avatar = profile.Avatar ?? string.Empty,
            TypeAuthentication = profile.TypeAuthentication ?? "INTERNA",
            AllowPasswordChange = profile.AllowPasswordChange,
            BaseRole = "Único",
            Privileges = privileges
        };
    }
}

