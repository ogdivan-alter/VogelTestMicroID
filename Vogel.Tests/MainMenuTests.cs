using Vogel.Shared.Models;
using Vogel.Shared.Services;
using Xunit;

namespace Vogel.Tests;

/// <summary>
/// Pruebas unitarias para la lógica del menú principal (MainMenu) y acordeón exclusivo.
/// Corresponde al Sprint 4 y a las secciones 2.3.2, 2.3.3 y 2.3.4 del Requerimiento Maestro.
/// </summary>
public class MainMenuTests
{
    private readonly IMockDataService _mockDataService;

    public MainMenuTests()
    {
        _mockDataService = new MockDataService();
    }

    [Fact]
    public async Task MainMenu_ShouldExtractRootOptions_WithIdParentZero()
    {
        // Arrange
        var rootPrivileges = await _mockDataService.GetRootPrivilegesAsync();

        // Assert
        Assert.NotNull(rootPrivileges);
        Assert.Single(rootPrivileges);
        Assert.All(rootPrivileges, p => Assert.Equal(0, p.IdParent));

        // Opción raíz: Administración
        Assert.Equal("Administración", rootPrivileges[0].Title);
    }

    [Fact]
    public async Task MainMenu_ShouldLoadSuboptions_ForSpecificRootOption()
    {
        // Arrange
        var suboptions = await _mockDataService.GetSubPrivilegesAsync(1);

        // Assert
        Assert.Equal(3, suboptions.Count);
        Assert.Equal("Menú Secundario", suboptions[0].Title);
        Assert.Equal("menu/sub_2_1", suboptions[0].Action);
        Assert.Equal("Formulario Demo", suboptions[1].Title);
        Assert.Equal("/demo/form", suboptions[1].Action);
        Assert.Equal("Vista de Trabajo", suboptions[2].Title);
        Assert.Equal("/work/blockwork-demo", suboptions[2].Action);
    }

    [Fact]
    public void AccordionExclusivity_ShouldAllowOnlyOneExpandedOptionAtATime()
    {
        // Simulación del estado del acordeón de MainMenu
        int? expandedOptionId = null;

        void ToggleOption(int optionId)
        {
            expandedOptionId = (expandedOptionId == optionId) ? null : optionId;
        }

        // 1. Inicialmente todas las opciones están colapsadas (null)
        Assert.Null(expandedOptionId);

        // 2. Abrir opción 1 (Arranque 1)
        ToggleOption(1);
        Assert.Equal(1, expandedOptionId);

        // 3. Abrir opción 2 (Arranque 2) debe colapsar automáticamente la opción 1
        ToggleOption(2);
        Assert.Equal(2, expandedOptionId);

        // 4. Volver a pulsar la opción 2 debe colapsarla
        ToggleOption(2);
        Assert.Null(expandedOptionId);
    }

    [Fact]
    public void OptionMenu_RootOption_ActionShouldBeEmpty_WhileSuboptionsHaveAction()
    {
        // Arrange
        var mockPrivileges = new List<Privilege>
        {
            new Privilege { Id = 1, IdParent = 0, Title = "Arranque 1", Action = string.Empty },
            new Privilege { Id = 101, IdParent = 1, Title = "Entre Aquí", Action = "demo/entre-aqui" }
        };

        // Assert
        var root = mockPrivileges.First(p => p.IdParent == 0);
        var sub = mockPrivileges.First(p => p.IdParent == 1);

        // Requerimiento 2.3.2: Para las opciones, action no genera acción; para subopciones, invoca acción
        Assert.True(string.IsNullOrEmpty(root.Action));
        Assert.False(string.IsNullOrEmpty(sub.Action));
    }

    private class TestableMainMenu : Vogel.Client.Components.MainMenuBase
    {
        public TestableMainMenu(List<Privilege> privileges)
        {
            Privileges = privileges;
        }
    }

    [Fact]
    public void Submenu_WithChildPrivileges_ShouldReturnMenuCodenameRoute()
    {
        // Arrange: Subopción con privilegios hijos
        var privileges = new List<Privilege>
        {
            new Privilege { Id = 1, IdParent = 0, Codename = "root_1", Title = "Arranque 1" },
            new Privilege { Id = 201, IdParent = 1, Codename = "sub_2_1", Title = "2.1", Action = "secondary/201" },
            new Privilege { Id = 301, IdParent = 201, Codename = "leaf_op", Title = "Operación 1", Action = "work/op1" }
        };

        var menu = new TestableMainMenu(privileges);

        // Act
        var route = menu.Submenu("sub_2_1");

        // Assert: al tener hijo (301), debe resolver hacia la plantilla /menu/{Codename}
        Assert.Equal("/menu/sub_2_1", route);
    }

    [Fact]
    public void Submenu_WithoutChildPrivileges_ShouldReturnConfiguredAction()
    {
        // Arrange: Subopción terminal (hoja) sin privilegios hijos
        var privileges = new List<Privilege>
        {
            new Privilege { Id = 1, IdParent = 0, Codename = "root_1", Title = "Arranque 1" },
            new Privilege { Id = 101, IdParent = 1, Codename = "entre_aqui", Title = "Entre Aquí", Action = "demo/entre-aqui" }
        };

        var menu = new TestableMainMenu(privileges);

        // Act
        var route = menu.Submenu("entre_aqui");

        // Assert: al no tener hijos, retorna su acción directa
        Assert.Equal("demo/entre-aqui", route);
    }

    [Fact]
    public void Submenu_NumericCodename_ShouldFallbackToIdLookup()
    {
        // Arrange: Búsqueda mediante ID numérico
        var privileges = new List<Privilege>
        {
            new Privilege { Id = 201, IdParent = 1, Codename = "sub_2_1", Title = "2.1" },
            new Privilege { Id = 301, IdParent = 201, Codename = "leaf", Title = "Leaf", Action = "work/leaf" }
        };

        var menu = new TestableMainMenu(privileges);

        // Act
        var route = menu.Submenu("201");

        // Assert: localiza por ID 201 y retorna la ruta con su codename
        Assert.Equal("/menu/sub_2_1", route);
    }

    [Fact]
    public void Submenu_NullOrUnknownCodename_ShouldReturnRootSlash()
    {
        // Arrange
        var privileges = new List<Privilege>
        {
            new Privilege { Id = 1, IdParent = 0, Codename = "root" }
        };

        var menu = new TestableMainMenu(privileges);

        // Assert
        Assert.Equal("/", menu.Submenu((string?)null));
        Assert.Equal("/", menu.Submenu("inexistente"));
        Assert.Equal("/", menu.Submenu((Privilege?)null));
    }
}
