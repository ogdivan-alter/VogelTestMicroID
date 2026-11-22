using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Vogel.Server.Controllers;
using Vogel.Shared.Services;
using Xunit;

namespace Vogel.Tests;

/// <summary>
/// Pruebas de integridad de acciones del front (Ajuste Fase 2 - Pendiente 2).
/// Verifica que toda acción declarada en los privilegios y en ActionBlockWork
/// corresponda a una Page de Blazor o a un endpoint real del servidor.
/// </summary>
public class ActionIntegrityTests
{
    private static readonly HashSet<string> KnownPageRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "",
        "login",
        "change-password",
        "secondary/{IdParent:int}",
        "menu/{Codename}",
        "demo/form",
        "work/{Codename}",
        "work/auto/{Codename}",
        "demo/entre-aqui"
    };

    private static readonly HashSet<string> KnownServerRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "api/session/validate",
        "api/logs",
        "api/auth/login",
        "api/auth/privileges",
        "api/auth/logout",
        "api/reports/daily",
        "api/reports/monthly",
        "api/{codename}/capture",
        "api/{codename}/remove",
        "api/{codename}/cancel-master",
        "api/{codename}/apply-master",
        "api/{codename}/rev-apply-master",
        "api/{codename}/list-detail-master",
        "api/{codename}/doc-master",
        "api/{codename}/rep-master"
    };

    /// <summary>
    /// Normaliza una acción a su patrón de ruta (elimina la barra inicial y reemplaza
    /// los segmentos variables por sus comodines correspondientes).
    /// </summary>
    private static string NormalizeAction(string action)
    {
        var value = action.TrimStart('/');

        if (value.StartsWith("secondary/", StringComparison.OrdinalIgnoreCase))
        {
            return "secondary/{IdParent:int}";
        }

        if (value.StartsWith("menu/", StringComparison.OrdinalIgnoreCase))
        {
            return "menu/{Codename}";
        }

        if (value.StartsWith("work/auto/", StringComparison.OrdinalIgnoreCase))
        {
            return "work/auto/{Codename}";
        }

        if (value.StartsWith("work/", StringComparison.OrdinalIgnoreCase))
        {
            return "work/{Codename}";
        }

        // Acciones de descarga de reportes (front -> endpoint api/reports/{tipo})
        if (value.StartsWith("reports/", StringComparison.OrdinalIgnoreCase))
        {
            return $"api/{value}";
        }

        return value;
    }

    [Theory]
    [InlineData("menu/sub_2_1")]
    [InlineData("demo/form")]
    [InlineData("work/auto/op-secundaria")]
    [InlineData("work/blockwork-demo")]
    [InlineData("reports/daily")]
    [InlineData("reports/monthly")]
    public void PrivilegeActions_ShouldBePageOrServerEndpoint(string action)
    {
        Assert.True(ActionExists(action), $"La acción '{action}' no corresponde a una Page ni a un endpoint conocido.");
    }

    [Fact]
    public async Task ActionBlockWorkActions_ShouldAllExist()
    {
        var mock = new MockDataService();

        foreach (var codename in new[] { "blockwork-demo", "op-secundaria" })
        {
            var blockWork = await mock.GetBlockWorkAsync(codename);
            Assert.NotNull(blockWork);

            var actions = new Dictionary<string, string>
            {
                ["Start"] = blockWork!.ActionSet.Start,
                ["Capture"] = blockWork.ActionSet.Capture,
                ["Remove"] = blockWork.ActionSet.Remove,
                ["CancelMaster"] = blockWork.ActionSet.CancelMaster,
                ["ApplyMaster"] = blockWork.ActionSet.ApplyMaster,
                ["RevApplyMaster"] = blockWork.ActionSet.RevApplyMaster,
                ["ListDetailMaster"] = blockWork.ActionSet.ListDetailMaster,
                ["DocMaster"] = blockWork.ActionSet.DocMaster,
                ["RepMaster"] = blockWork.ActionSet.RepMaster,
                ["Exit"] = blockWork.ActionSet.Exit
            };

            foreach (var (field, value) in actions)
            {
                Assert.False(string.IsNullOrWhiteSpace(value), $"{codename}.{field} está vacío.");
                Assert.True(ActionExists(value), $"{codename}.{field} ('{value}') no corresponde a una Page ni a un endpoint conocido.");
            }
        }
    }

    [Fact]
    public async Task OpSecundariaStart_ShouldBeFrontRoute_NotApiEndpoint()
    {
        var mock = new MockDataService();
        var blockWork = await mock.GetBlockWorkAsync("op-secundaria");

        Assert.NotNull(blockWork);
        Assert.Equal("work/auto/op-secundaria", blockWork!.ActionSet.Start);
        Assert.False(blockWork.ActionSet.Start.StartsWith("api/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BlockWorkView_ShouldNotDeclareDuplicateWorkRoutes()
    {
        // La dualidad work-auto / work/auto debe haberse eliminado.
        // Se inspecciona el código fuente de la página para validar las rutas declaradas.
        var repoRoot = FindRepositoryRoot();
        var razorPath = Path.Combine(repoRoot, "Vogel.Client", "Pages", "Work", "BlockWorkView.razor");
        Assert.True(File.Exists(razorPath), $"No se encontró el archivo: {razorPath}");

        var content = File.ReadAllText(razorPath);

        Assert.Contains("/work/auto/{Codename}", content);
        Assert.DoesNotContain("/work-auto/", content);
    }

    /// <summary>
    /// Localiza la raíz del repositorio subiendo desde el directorio de ejecución
    /// hasta encontrar el archivo de solución.
    /// </summary>
    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.GetFiles("*.slnx").Length > 0 || dir.GetFiles("*.sln").Length > 0)
            {
                return dir.FullName;
            }
            dir = dir.Parent!;
        }

        throw new InvalidOperationException("No se pudo localizar la raíz del repositorio.");
    }

    private static bool ActionExists(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        var normalized = NormalizeAction(action);

        if (KnownPageRoutes.Contains(normalized))
        {
            return true;
        }

        // Endpoint exacto
        if (KnownServerRoutes.Contains(normalized))
        {
            return true;
        }

        // Endpoint con comodín de entidad (api/{codename}/...)
        if (normalized.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
        {
            var segments = normalized.Split('/');
            if (segments.Length == 3)
            {
                var pattern = $"api/{{codename}}/{segments[2]}";
                return KnownServerRoutes.Contains(pattern);
            }
        }

        return false;
    }
}
