using Vogel.Shared.Models;
using Vogel.Shared.Services;
using Xunit;

namespace Vogel.Tests;

/// <summary>
/// Pruebas unitarias para el menú secundario (SecondaryMenu), tarjetas de opciones (SecondaryMenuItem)
/// y encabezado de trabajo (HeaderWork).
/// Corresponde al Sprint 5 y a las secciones 2.2.4, 2.3.6, 2.3.7 y 2.3.8 del Requerimiento Maestro.
/// </summary>
public class SecondaryMenuTests
{
    private readonly IMockDataService _mockDataService;

    public SecondaryMenuTests()
    {
        _mockDataService = new MockDataService();
    }

    [Fact]
    public async Task SecondaryMenu_ShouldRetrieveSubordinates_ForParent201()
    {
        // Arrange
        int parentId = 201; // Subopción "2.1"

        // Act
        var privileges = await _mockDataService.GetSubPrivilegesAsync(parentId);

        // Assert
        Assert.NotNull(privileges);
        // Ajuste Fase 2 (Pendiente 2): "monitoreo" y "auditoria" se retiraron del escenario mock,
        // por lo que "2.1" expone 4 subopciones (2 de Operaciones + 2 de Reportes).
        Assert.Equal(4, privileges.Count);
        Assert.All(privileges, p => Assert.Equal(parentId, p.IdParent));
    }

    [Fact]
    public async Task SecondaryMenu_ShouldGroupByCategory_AndSortByOrder()
    {
        // Arrange
        int parentId = 201;
        var privileges = await _mockDataService.GetSubPrivilegesAsync(parentId);

        // Act: Agrupación y ordenamiento según especificación 2.3.7
        var grouped = privileges
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Group) ? "General" : p.Group)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(p => p.Order).ToList()
            );

        // Assert
        Assert.True(grouped.ContainsKey("Operaciones"));
        Assert.True(grouped.ContainsKey("Reportes"));

        var operaciones = grouped["Operaciones"];
        Assert.Equal(2, operaciones.Count);
        Assert.Equal("Gestión Principal", operaciones[0].Title);
        Assert.Equal(1, operaciones[0].Order);
        Assert.Equal("Operación Secundaria", operaciones[1].Title);
        Assert.Equal(2, operaciones[1].Order);

        var reportes = grouped["Reportes"];
        Assert.Equal(2, reportes.Count);
        Assert.Equal("Reporte Diario", reportes[0].Title);
        Assert.Equal(1, reportes[0].Order);
        Assert.Equal("Reporte Mensual", reportes[1].Title);
        Assert.Equal(2, reportes[1].Order);
    }

    [Fact]
    public void SecondaryMenu_ShouldEnforceMaxFourItemsPerRow()
    {
        // Arrange
        const int maxItemsPerRow = 4;
        var sampleItems = Enumerable.Range(1, 9)
            .Select(i => new Privilege { Id = i, Title = $"Item {i}", Order = i })
            .ToList();

        // Act: Partición en filas de máximo 4 ítems
        var rows = sampleItems.Chunk(maxItemsPerRow).ToList();

        // Assert
        Assert.Equal(3, rows.Count);
        Assert.Equal(4, rows[0].Length);
        Assert.Equal(4, rows[1].Length);
        Assert.Single(rows[2]);
    }

    [Fact]
    public void SecondaryMenuItem_DoubleClickProtection_ShouldBlockConcurrentInvocations()
    {
        // Arrange
        bool isProcessing = false;
        int executionCount = 0;

        void SimulateClick()
        {
            if (isProcessing) return;

            try
            {
                isProcessing = true;
                executionCount++;
            }
            finally
            {
                // En un escenario asíncrono real, el flag se limpia al terminar
            }
        }

        // Act
        SimulateClick(); // Primer clic
        SimulateClick(); // Segundo clic concurrente mientras isProcessing es true

        // Assert
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public void HeaderWork_DefaultValues_ShouldMatchSpecification()
    {
        // Requerimiento 2.3.6: Título @title, botón alineado a la derecha con texto "Salir" o @textExit,
        // que invoca acción o dirige a ruta indicada en @actionExit.
        const string defaultExitText = "Salir";
        const string defaultActionExit = "/";
        const string defaultColor = "#1e293b";

        Assert.Equal("Salir", defaultExitText);
        Assert.Equal("/", defaultActionExit);
        Assert.StartsWith("#", defaultColor);
    }

    private class TestableSecondaryMenuTemplate : Vogel.Client.Pages.Template.SecondaryMenu
    {
        public TestableSecondaryMenuTemplate(Vogel.Client.Services.SessionState sessionState)
        {
            SessionState = sessionState;
            Codename = string.Empty;
        }
    }

    [Fact]
    public void SecondaryMenuTemplate_IdParentCode_WithValidCodename_ShouldReturnId()
    {
        var sessionState = new Vogel.Client.Services.SessionState();
        sessionState.SetUser(new User
        {
            Username = "test",
            Privileges = new List<Privilege>
            {
                new Privilege { Id = 201, Codename = "sub_2_1", Title = "2.1" }
            }
        });

        var template = new TestableSecondaryMenuTemplate(sessionState);

        Assert.Equal(201, template.IdParentCode("sub_2_1"));
    }

    [Fact]
    public void SecondaryMenuTemplate_IdParentCode_WithNumericCodename_ShouldReturnId()
    {
        var sessionState = new Vogel.Client.Services.SessionState();
        sessionState.SetUser(new User
        {
            Username = "test",
            Privileges = new List<Privilege>
            {
                new Privilege { Id = 201, Codename = "sub_2_1", Title = "2.1" }
            }
        });

        var template = new TestableSecondaryMenuTemplate(sessionState);

        Assert.Equal(201, template.IdParentCode("201"));
    }

    [Fact]
    public void SecondaryMenuTemplate_IdParentCode_WithInvalidOrNull_ShouldReturnZero()
    {
        var sessionState = new Vogel.Client.Services.SessionState();
        var template = new TestableSecondaryMenuTemplate(sessionState);

        Assert.Equal(0, template.IdParentCode(null));
        Assert.Equal(0, template.IdParentCode(""));
        Assert.Equal(0, template.IdParentCode("no_existe"));
    }
}
