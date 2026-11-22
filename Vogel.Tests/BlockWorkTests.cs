using Vogel.Shared.Services;
using Xunit;

namespace Vogel.Tests;

/// <summary>
/// Pruebas unitarias para la entidad compuesta maestro-detalle BlockWork y paginación de datos.
/// </summary>
public class BlockWorkTests
{
    private readonly IMockDataService _mockDataService;

    public BlockWorkTests()
    {
        _mockDataService = new MockDataService();
    }

    [Fact]
    public async Task GetBlockWorkAsync_ShouldReturnValidDemoBlockWork()
    {
        // Act
        var blockWork = await _mockDataService.GetBlockWorkAsync("blockwork-demo");

        // Assert
        Assert.NotNull(blockWork);
        Assert.Equal("blockwork-demo", blockWork.Codename);
        Assert.True(blockWork.ShowInfo);
        Assert.Equal("Id", blockWork.KeyMasterAttribute);
        Assert.Equal("ItemCode", blockWork.KeyDetailAttribute);

        // Validar ActionSet
        Assert.NotNull(blockWork.ActionSet);
        Assert.False(string.IsNullOrEmpty(blockWork.ActionSet.Start));
        Assert.False(string.IsNullOrEmpty(blockWork.ActionSet.Capture));
        Assert.False(string.IsNullOrEmpty(blockWork.ActionSet.Remove));
        Assert.False(string.IsNullOrEmpty(blockWork.ActionSet.CancelMaster));
        Assert.False(string.IsNullOrEmpty(blockWork.ActionSet.ApplyMaster));
        Assert.False(string.IsNullOrEmpty(blockWork.ActionSet.Exit));

        // Validar ViewSet
        Assert.NotNull(blockWork.ViewSet);
        Assert.False(string.IsNullOrEmpty(blockWork.ViewSet.Info));
        Assert.False(string.IsNullOrEmpty(blockWork.ViewSet.Capture));
        Assert.False(string.IsNullOrEmpty(blockWork.ViewSet.Filter));
        Assert.False(string.IsNullOrEmpty(blockWork.ViewSet.List));
    }

    [Fact]
    public async Task GetDetailDataPagedAsync_ShouldPaginateProperly()
    {
        // Act - Página 1 con tamaño 10
        var (page1Items, totalCount) = await _mockDataService.GetDetailDataPagedAsync("blockwork-demo", 1, 10);

        // Assert
        Assert.Equal(35, totalCount);
        Assert.Equal(10, page1Items.Count);
        Assert.Equal("ITM-001", page1Items[0]["ItemCode"]);
        Assert.Equal("ITM-010", page1Items[9]["ItemCode"]);

        // Act - Página 4 (última página, solo 5 items de 35)
        var (page4Items, _) = await _mockDataService.GetDetailDataPagedAsync("blockwork-demo", 4, 10);
        Assert.Equal(5, page4Items.Count);
        Assert.Equal("ITM-031", page4Items[0]["ItemCode"]);
        Assert.Equal("ITM-035", page4Items[4]["ItemCode"]);
    }
}
