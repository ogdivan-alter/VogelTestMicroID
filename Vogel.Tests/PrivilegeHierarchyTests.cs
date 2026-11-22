using Vogel.Shared.Services;
using Xunit;

namespace Vogel.Tests;

/// <summary>
/// Pruebas unitarias para la jerarquía del árbol de privilegios y su recuperación.
/// Corresponde a los criterios de verificación del Sprint 1.
/// </summary>
public class PrivilegeHierarchyTests
{
    private readonly IMockDataService _mockDataService;

    public PrivilegeHierarchyTests()
    {
        _mockDataService = new MockDataService();
    }

    [Fact]
    public async Task GetRootPrivilegesAsync_ShouldReturnOnlyPrivilegesWithIdParentZero()
    {
        // Act
        var rootPrivileges = await _mockDataService.GetRootPrivilegesAsync();

        // Assert
        Assert.NotNull(rootPrivileges);
        Assert.Single(rootPrivileges);
        Assert.All(rootPrivileges, p => Assert.True(p.IsRoot));
        Assert.All(rootPrivileges, p => Assert.Equal(0, p.IdParent));

        // Debe contener Administración según especificación
        Assert.Contains(rootPrivileges, p => p.Codename == "administracion" && p.Title == "Administración");
    }

    [Fact]
    public async Task GetSubPrivilegesAsync_ForAdministracion_ShouldReturnConfiguredSuboptions()
    {
        // Act (Id = 1 es Administración)
        var subOptions = await _mockDataService.GetSubPrivilegesAsync(1);

        // Assert
        Assert.Equal(3, subOptions.Count);
        Assert.Contains(subOptions, p => p.Codename == "sub_2_1" && p.Title == "Menú Secundario");
        Assert.Contains(subOptions, p => p.Codename == "demo_form" && p.Title == "Formulario Demo");
        Assert.Contains(subOptions, p => p.Codename == "blockwork_demo" && p.Title == "Vista de Trabajo");
    }

    [Fact]
    public async Task GetSubPrivilegesAsync_ForSecondaryMenu21_ShouldGroupAndOrderCorrectly()
    {
        // Act (Id = 201 es subopción 2.1)
        var secondaryItems = await _mockDataService.GetSubPrivilegesAsync(201);

        // Assert
        Assert.NotEmpty(secondaryItems);
        Assert.All(secondaryItems, p => Assert.Equal(201, p.IdParent));

        // Verificar que contiene los elementos del grupo Operaciones y Reportes
        var groups = secondaryItems.Select(p => p.Group).Distinct().ToList();
        Assert.Contains("Operaciones", groups);
        Assert.Contains("Reportes", groups);

        // Comprobar que existe la opción para la vista de trabajo BlockWork
        var blockWorkItem = secondaryItems.FirstOrDefault(p => p.Codename == "blockwork_demo");
        Assert.NotNull(blockWorkItem);
        Assert.Equal("Gestión Principal", blockWorkItem.Title);
        Assert.NotNull(blockWorkItem.Access);
        Assert.True(blockWorkItem.Access.Read);
        Assert.True(blockWorkItem.Access.Execute);
    }
}
