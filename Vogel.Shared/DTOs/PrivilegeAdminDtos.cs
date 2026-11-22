namespace Vogel.Shared.DTOs;

/// <summary>
/// Representa un privilegio del sistema para la vista de administración de privilegios (AdmPriv).
/// </summary>
public sealed class PrivilegeAdminItemDto
{
    public int IdPrivilege { get; set; }

    public int IdSystem { get; set; }

    public int IdParent { get; set; }

    public string Codename { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string GroupName { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string? Icon { get; set; }

    public string Status { get; set; } = "A";
}

/// <summary>
/// Respuesta del BFF con el catálogo administrativo de privilegios del sistema configurado.
/// </summary>
public sealed class PrivilegeAdminListResponseDto
{
    public string SystemCode { get; set; } = string.Empty;

    public int IdSystem { get; set; }

    public List<PrivilegeAdminItemDto> Items { get; set; } = new();

    public int Total { get; set; }
}
