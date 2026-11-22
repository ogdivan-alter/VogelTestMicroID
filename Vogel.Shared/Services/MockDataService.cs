using Vogel.Shared.DTOs;
using Vogel.Shared.Models;

namespace Vogel.Shared.Services;

/// <summary>
/// Proveedor de datos simulados para la Fase 2 del proyecto Vogel.
/// Proporciona usuarios, estructura de privilegios y bloques de trabajo para demostración y pruebas.
/// </summary>
public class MockDataService : IMockDataService
{
    private readonly List<(User User, string Password)> _mockUsers;
    private readonly List<Privilege> _mockPrivileges;
    private readonly List<BlockWork> _mockBlockWorks;
    private readonly List<Dictionary<string, object>> _mockDetailRecords;

    public MockDataService()
    {
        // 1. Definición de privilegios del escenario DEMO
        _mockPrivileges = new List<Privilege>
        {
            // Opción Raíz: Administración (IdParent = 0)
            new Privilege
            {
                Id = 1,
                IdParent = 0,
                Codename = "administracion",
                Title = "Administración",
                Action = string.Empty,
                Icon = "icons/roles.svg",
                Order = 1,
                Access = new PrivilegeAccess { Read = true, Execute = true }
            },

            // Subopciones de Administración (IdParent = 1)
            new Privilege
            {
                Id = 201,
                IdParent = 1,
                Codename = "sub_2_1",
                Title = "Menú Secundario",
                Action = "menu/sub_2_1",
                Icon = "icons/roles.svg",
                Order = 1,
                Access = new PrivilegeAccess { Read = true, Execute = true }
            },
            new Privilege
            {
                Id = 202,
                IdParent = 1,
                Codename = "demo_form",
                Title = "Formulario Demo",
                Action = "/demo/form",
                Icon = "icons/users.svg",
                Order = 2,
                Access = new PrivilegeAccess { Read = true, Execute = true }
            },
            new Privilege
            {
                Id = 203,
                IdParent = 1,
                Codename = "blockwork_demo",
                Title = "Vista de Trabajo",
                Action = "/work/blockwork-demo",
                Icon = "icons/work.svg",
                Order = 3,
                Access = new PrivilegeAccess { Read = true, Execute = true }
            },

            // Privilegios para el menú secundario de "sub_2_1" (IdParent = 201)
            // Grupo: Operaciones
            new Privilege
            {
                Id = 2011,
                IdParent = 201,
                Codename = "blockwork_demo",
                Group = "Operaciones",
                Title = "Gestión Principal",
                Action = "work/blockwork-demo",
                Icon = "icons/work.svg",
                Order = 1,
                Access = new PrivilegeAccess
                {
                    Read = true,
                    Create = true,
                    Modify = true,
                    Delete = true,
                    Validate = true,
                    Execute = true,
                    Approve = true
                }
            },
            new Privilege
            {
                Id = 2012,
                IdParent = 201,
                Codename = "op_secundaria",
                Group = "Operaciones",
                Title = "Operación Secundaria",
                Action = "work/auto/op-secundaria",
                Icon = "icons/op2.svg",
                Order = 2,
                Access = new PrivilegeAccess { Read = true, Execute = true }
            },
            new Privilege
            {
                Id = 2013,
                IdParent = 201,
                Codename = "rep_diario",
                Group = "Reportes",
                Title = "Reporte Diario",
                Action = "reports/daily",
                Icon = "icons/rep_daily.svg",
                Order = 1,
                Access = new PrivilegeAccess { Read = true, Execute = true }
            },
            new Privilege
            {
                Id = 2014,
                IdParent = 201,
                Codename = "rep_mensual",
                Group = "Reportes",
                Title = "Reporte Mensual",
                Action = "reports/monthly",
                Icon = "icons/rep_monthly.svg",
                Order = 2,
                Access = new PrivilegeAccess { Read = true, Execute = true }
            }
        };

        // 2. Definición de Usuarios simulados
        _mockUsers = new List<(User User, string Password)>
        {
            // Usuario jdoe (referencia visual en capturas de pantalla)
            (
                new User
                {
                    Username = "jdoe",
                    Name = "John Doe",
                    Avatar = null,
                    TypeAuthentication = "INTERNA",
                    AllowPasswordChange = true,
                    BaseRole = "Único",
                    Privileges = new List<Privilege>(_mockPrivileges)
                },
                "SecurePassword123!"
            ),
            // Usuario con autenticación INTERNA y cambio de clave habilitado
            (
                new User
                {
                    Username = "admin",
                    Name = "Administrador Vogel",
                    Avatar = "images/avatars/admin.jpg",
                    TypeAuthentication = "INTERNA",
                    AllowPasswordChange = true,
                    BaseRole = "Único",
                    Privileges = new List<Privilege>(_mockPrivileges)
                },
                "Vogel2026*"
            ),
            // Usuario con autenticación externa y cambio de clave deshabilitado
            (
                new User
                {
                    Username = "external_user",
                    Name = "Usuario Externo SSO",
                    Avatar = null,
                    TypeAuthentication = "EXTERNAL",
                    AllowPasswordChange = false,
                    BaseRole = "Único",
                    Privileges = _mockPrivileges.Where(p => p.Id != 2014).ToList()
                },
                "External2026*"
            )
        };

        // 3. Definición de Bloques de Trabajo (BlockWork)
        _mockBlockWorks = new List<BlockWork>
        {
            new BlockWork
            {
                Codename = "blockwork-demo",
                Title = "Gestión de Entidad Demo (Maestro-Detalle)",
                ShowInfo = true,
                KeyMasterAttribute = "Id",
                KeyDetailAttribute = "ItemCode",
                ActionSet = new ActionBlockWork
                {
                    Start = "work/blockwork-demo",
                    Capture = "api/blockwork-demo/capture",
                    Remove = "api/blockwork-demo/remove",
                    CancelMaster = "api/blockwork-demo/cancel-master",
                    ApplyMaster = "api/blockwork-demo/apply-master",
                    RevApplyMaster = "api/blockwork-demo/rev-apply-master",
                    ListDetailMaster = "api/blockwork-demo/list-detail-master",
                    DocMaster = "api/blockwork-demo/doc-master",
                    RepMaster = "api/blockwork-demo/rep-master",
                    Exit = "menu/sub_2_1"
                },
                ViewSet = new ViewBlockWork
                {
                    Info = "DemoInfoFrame",
                    Capture = "DemoCaptureFrame",
                    Filter = "DemoFilterFrame",
                    List = "DemoListFrame"
                }
            },
            new BlockWork
            {
                Codename = "op-secundaria",
                Title = "Operación Secundaria (BlockWork Autónomo)",
                ShowInfo = true,
                KeyMasterAttribute = "Id",
                KeyDetailAttribute = "ItemCode",
                ActionSet = new ActionBlockWork
                {
                    Start = "work/auto/op-secundaria",
                    Capture = "api/op-secundaria/capture",
                    Remove = "api/op-secundaria/remove",
                    CancelMaster = "api/op-secundaria/cancel-master",
                    ApplyMaster = "api/op-secundaria/apply-master",
                    RevApplyMaster = "api/op-secundaria/rev-apply-master",
                    ListDetailMaster = "api/op-secundaria/list-detail-master",
                    DocMaster = "api/op-secundaria/doc-master",
                    RepMaster = "api/op-secundaria/rep-master",
                    Exit = "menu/sub_2_1"
                },
                ViewSet = new ViewBlockWork
                {
                    Info = "OpSecundariaInfoFrame",
                    Capture = "OpSecundariaCaptureFrame",
                    Filter = "OpSecundariaFilterFrame",
                    List = "OpSecundariaListFrame"
                }
            }
        };

        // 4. Generación de registros detalle de prueba para la tabla paginada
        _mockDetailRecords = new List<Dictionary<string, object>>();
        for (int i = 1; i <= 35; i++)
        {
            _mockDetailRecords.Add(new Dictionary<string, object>
            {
                { "ItemCode", $"ITM-{i:D3}" },
                { "Description", $"Elemento de prueba número {i}" },
                { "Category", i % 2 == 0 ? "Categoría A" : "Categoría B" },
                { "Quantity", i * 5 },
                { "UnitPrice", 15.50m + (i * 1.25m) },
                { "Status", i % 3 == 0 ? "Inactivo" : "Activo" }
            });
        }
    }

    public Task<LoginResponseDto> AuthenticateAsync(LoginRequestDto request)
    {
        var found = _mockUsers.FirstOrDefault(u =>
            string.Equals(u.User.Username, request.Username, StringComparison.OrdinalIgnoreCase));

        if (found.User == null || found.Password != request.Password)
        {
            return Task.FromResult(new LoginResponseDto
            {
                Success = false,
                Message = "Usuario o Clave incorrectos",
                User = null
            });
        }

        return Task.FromResult(new LoginResponseDto
        {
            Success = true,
            Message = "Autenticación exitosa",
            User = found.User
        });
    }

    public Task<User?> GetUserByUsernameAsync(string username)
    {
        var user = _mockUsers.FirstOrDefault(u =>
            string.Equals(u.User.Username, username, StringComparison.OrdinalIgnoreCase)).User;

        return Task.FromResult<User?>(user);
    }


    public Task<List<Privilege>> GetRootPrivilegesAsync()
    {
        var roots = _mockPrivileges.Where(p => p.IdParent == 0)
                                   .OrderBy(p => p.Order)
                                   .ToList();

        return Task.FromResult(roots);
    }

    public Task<List<Privilege>> GetSubPrivilegesAsync(int parentId)
    {
        var subs = _mockPrivileges.Where(p => p.IdParent == parentId)
                                  .OrderBy(p => p.Group)
                                  .ThenBy(p => p.Order)
                                  .ToList();

        return Task.FromResult(subs);
    }

    public Task<Privilege?> GetPrivilegeByIdAsync(int id)
    {
        var privilege = _mockPrivileges.FirstOrDefault(p => p.Id == id);
        return Task.FromResult(privilege);
    }

    public Task<Privilege?> GetPrivilegeByCodenameAsync(string codename)
    {
        var privilege = _mockPrivileges.FirstOrDefault(p =>
            string.Equals(p.Codename, codename, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(privilege);
    }

    public Task<BlockWork?> GetBlockWorkAsync(string codename)
    {
        var bw = _mockBlockWorks.FirstOrDefault(b =>
            string.Equals(b.Codename, codename, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(bw);
    }

    public Task<(List<Dictionary<string, object>> Items, int TotalCount)> GetDetailDataPagedAsync(
        string blockWorkCodename, int pageNumber, int pageSize)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var total = _mockDetailRecords.Count;
        var pagedItems = _mockDetailRecords
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult((pagedItems, total));
    }
}
