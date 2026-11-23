using System.ComponentModel.DataAnnotations;

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

/// <summary>
/// Solicitud para crear un nuevo privilegio en el sistema configurado del BFF.
/// No incluye IdSystem porque siempre es inyectado por el BFF desde configuración.
/// </summary>
public sealed class CreatePrivilegeRequestDto
{
    [Range(0, int.MaxValue, ErrorMessage = "El privilegio padre debe ser mayor o igual a 0.")]
    public int? IdParent { get; set; }

    [Required(ErrorMessage = "El campo codename es obligatorio.")]
    [MaxLength(100, ErrorMessage = "El campo codename no puede exceder los 100 caracteres.")]
    public string Codename { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo group_name es obligatorio.")]
    [MaxLength(100, ErrorMessage = "El campo group_name no puede exceder los 100 caracteres.")]
    public string GroupName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo title es obligatorio.")]
    [MaxLength(150, ErrorMessage = "El campo title no puede exceder los 150 caracteres.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo action es obligatorio.")]
    [MaxLength(250, ErrorMessage = "El campo action no puede exceder los 250 caracteres.")]
    public string Action { get; set; } = string.Empty;

    [MaxLength(100, ErrorMessage = "El campo icon no puede exceder los 100 caracteres.")]
    public string? Icon { get; set; }
}

/// <summary>
/// Respuesta del BFF tras crear un privilegio exitosamente.
/// </summary>
public sealed class CreatePrivilegeResponseDto
{
    public int IdPrivilege { get; set; }

    public int IdSystem { get; set; }

    public string Status { get; set; } = "A";

    public DateTimeOffset DateCreation { get; set; }
}

/// <summary>
/// Solicitud para actualizar atributos y jerarquía de un privilegio activo.
/// </summary>
public sealed class UpdatePrivilegeRequestDto
{
    [Range(0, int.MaxValue, ErrorMessage = "El privilegio padre debe ser mayor o igual a 0.")]
    public int? IdParent { get; set; }

    [Required(ErrorMessage = "El campo codename es obligatorio.")]
    [MaxLength(100, ErrorMessage = "El campo codename no puede exceder los 100 caracteres.")]
    public string Codename { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo group_name es obligatorio.")]
    [MaxLength(100, ErrorMessage = "El campo group_name no puede exceder los 100 caracteres.")]
    public string GroupName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo title es obligatorio.")]
    [MaxLength(150, ErrorMessage = "El campo title no puede exceder los 150 caracteres.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo action es obligatorio.")]
    [MaxLength(250, ErrorMessage = "El campo action no puede exceder los 250 caracteres.")]
    public string Action { get; set; } = string.Empty;

    [MaxLength(100, ErrorMessage = "El campo icon no puede exceder los 100 caracteres.")]
    public string? Icon { get; set; }
}

/// <summary>
/// Respuesta del BFF tras actualizar un privilegio exitosamente.
/// </summary>
public sealed class UpdatePrivilegeResponseDto
{
    public int IdPrivilege { get; set; }

    public bool Updated { get; set; }

    public DateTimeOffset DateModification { get; set; }
}

/// <summary>
/// Solicitud opcional para inactivar lógicamente un privilegio activo.
/// </summary>
public sealed class DeactivatePrivilegeRequestDto
{
    [MaxLength(500, ErrorMessage = "El motivo de inactivación no puede exceder los 500 caracteres.")]
    public string? DelDescription { get; set; }
}

/// <summary>
/// Respuesta del BFF tras inactivar lógicamente un privilegio.
/// </summary>
public sealed class DeactivatePrivilegeResponseDto
{
    public int IdPrivilege { get; set; }

    public string Status { get; set; } = "I";

    public int CascadedRolePrivileges { get; set; }
}
