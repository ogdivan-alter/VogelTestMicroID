using System.ComponentModel.DataAnnotations;

namespace Vogel.Server.Configuration;

/// <summary>
/// Mapeo configurable entre los módulos CRUD del BFF y el codename del privilegio
/// que gobierna sus permisos RBAC en el proveedor de identidad (MicroID / Mock).
/// </summary>
public sealed class ModulePrivilegesOptions
{
    public const string SectionName = "ModulePrivileges";

    /// <summary>
    /// Codename del privilegio que gobierna el módulo de Administración de Privilegios.
    /// </summary>
    [Required(ErrorMessage = "ModulePrivileges:Privileges es obligatorio.")]
    [MaxLength(100)]
    public string Privileges { get; set; } = string.Empty;
}
