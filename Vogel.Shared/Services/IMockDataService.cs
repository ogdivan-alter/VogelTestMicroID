using Vogel.Shared.DTOs;
using Vogel.Shared.Models;

namespace Vogel.Shared.Services;

/// <summary>
/// Contrato del servicio de datos simulados (Mock) para usuarios, privilegios y bloques de trabajo.
/// Requerido para la Fase 2 y Sprint 1 según el Requerimiento Maestro.
/// </summary>
public interface IMockDataService
{
    /// <summary>
    /// Simula el proceso de autenticación de un usuario con validación de credenciales.
    /// </summary>
    /// <param name="request">Credenciales e información de login.</param>
    /// <returns>Resultado con éxito o fallo y usuario con sus privilegios.</returns>
    Task<LoginResponseDto> AuthenticateAsync(LoginRequestDto request);

    /// <summary>
    /// Obtiene un usuario simulado por su nombre de usuario.
    /// </summary>
    Task<User?> GetUserByUsernameAsync(string username);

    /// <summary>
    /// Obtiene las opciones de menú raíz (cuyo IdParent es 0).
    /// </summary>
    Task<List<Privilege>> GetRootPrivilegesAsync();

    /// <summary>
    /// Obtiene las subopciones subordinadas a un privilegio padre.
    /// </summary>
    Task<List<Privilege>> GetSubPrivilegesAsync(int parentId);

    /// <summary>
    /// Obtiene un privilegio específico a partir de su identificador numérico.
    /// </summary>
    Task<Privilege?> GetPrivilegeByIdAsync(int id);

    /// <summary>
    /// Obtiene un privilegio específico a partir de su nombre clave.
    /// </summary>
    Task<Privilege?> GetPrivilegeByCodenameAsync(string codename);

    /// <summary>
    /// Obtiene la configuración de una entidad de trabajo maestro-detalle (BlockWork).
    /// </summary>
    Task<BlockWork?> GetBlockWorkAsync(string codename);

    /// <summary>
    /// Obtiene datos de detalle simulados para la tabla paginada de ListViewFrame.
    /// </summary>
    Task<(List<Dictionary<string, object>> Items, int TotalCount)> GetDetailDataPagedAsync(string blockWorkCodename, int pageNumber, int pageSize);
}
