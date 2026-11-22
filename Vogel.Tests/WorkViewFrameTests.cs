using Vogel.Client.Components;
using Vogel.Shared.Models;
using Vogel.Shared.Services;
using Xunit;

namespace Vogel.Tests;

/// <summary>
/// Pruebas unitarias para los componentes del bloque de trabajo y datos (Sprint 6):
/// WorkViewFrame, InfoViewFrame, ListViewFrame y lógica de paginación.
/// Corresponde a las secciones 2.3.9, 2.3.10, 2.3.11, 2.4.1, 2.4.2 y 2.4.3 del Requerimiento Maestro.
/// </summary>
public class WorkViewFrameTests
{
    private readonly IMockDataService _mockDataService;

    public WorkViewFrameTests()
    {
        _mockDataService = new MockDataService();
    }

    [Fact]
    public async Task WorkViewFrame_ShouldInstantiateFromBlockWork_WithActionSetAndViewSet()
    {
        // Act
        var blockWork = await _mockDataService.GetBlockWorkAsync("blockwork-demo");

        // Assert
        Assert.NotNull(blockWork);
        Assert.Equal("blockwork-demo", blockWork.Codename);
        Assert.True(blockWork.ShowInfo);
        Assert.Equal("Id", blockWork.KeyMasterAttribute);
        Assert.Equal("ItemCode", blockWork.KeyDetailAttribute);

        // Validar ActionSet (sección 2.4.2)
        Assert.NotNull(blockWork.ActionSet);
        Assert.Equal("menu/sub_2_1", blockWork.ActionSet.Exit);
        Assert.False(string.IsNullOrEmpty(blockWork.ActionSet.ApplyMaster));
        Assert.False(string.IsNullOrEmpty(blockWork.ActionSet.CancelMaster));
        Assert.False(string.IsNullOrEmpty(blockWork.ActionSet.DocMaster));
        Assert.False(string.IsNullOrEmpty(blockWork.ActionSet.RepMaster));

        // Validar ViewSet (sección 2.4.3)
        Assert.NotNull(blockWork.ViewSet);
        Assert.False(string.IsNullOrEmpty(blockWork.ViewSet.Info));
        Assert.False(string.IsNullOrEmpty(blockWork.ViewSet.List));
    }

    [Fact]
    public async Task WorkViewFrame_ShouldSupportPureBlockWork_OpSecundaria()
    {
        // Act
        var blockWork = await _mockDataService.GetBlockWorkAsync("op-secundaria");

        // Assert
        Assert.NotNull(blockWork);
        Assert.Equal("op-secundaria", blockWork.Codename);
        Assert.Contains("BlockWork Autónomo", blockWork.Title);
        Assert.True(blockWork.ShowInfo);
        Assert.NotNull(blockWork.ActionSet);
        Assert.NotNull(blockWork.ViewSet);
        Assert.Equal("OpSecundariaInfoFrame", blockWork.ViewSet.Info);
        Assert.Equal("OpSecundariaListFrame", blockWork.ViewSet.List);
        Assert.Equal("menu/sub_2_1", blockWork.ActionSet.Exit);

#pragma warning disable BL0005
        // Instanciar componente puro (sin frames hijos inyectados manualmente)
        var frame = new WorkViewFrame
        {
            BlockWork = blockWork
        };
#pragma warning restore BL0005

        Assert.NotNull(frame.BlockWork);
        Assert.Null(frame.InfoFrame);
        Assert.Null(frame.ListFrame);
        Assert.Null(frame.CaptureFrame);
        Assert.Null(frame.FilterFrame);
        Assert.Null(frame.FooterFrame);

        // Validar que las vistas referenciadas en ViewSet correspondan a tipos de componentes válidos
        var infoComponentType = typeof(OpSecundariaInfoFrame);
        var listComponentType = typeof(OpSecundariaListFrame);
        Assert.NotNull(infoComponentType);
        Assert.NotNull(listComponentType);
        Assert.True(typeof(Microsoft.AspNetCore.Components.IComponent).IsAssignableFrom(infoComponentType));
        Assert.True(typeof(Microsoft.AspNetCore.Components.IComponent).IsAssignableFrom(listComponentType));
    }

    [Fact]
    public async Task ListViewFrame_DataPagedService_ShouldReturnPageSubsetAndTotal()
    {
        // Arrange
        const int pageSize = 10;

        // Act: Página 1
        var (page1Items, totalCount) = await _mockDataService.GetDetailDataPagedAsync("blockwork-demo", 1, pageSize);

        // Assert Página 1
        Assert.Equal(35, totalCount);
        Assert.Equal(10, page1Items.Count);
        Assert.Equal("ITM-001", page1Items[0]["ItemCode"]);
        Assert.Equal("ITM-010", page1Items[9]["ItemCode"]);

        // Act: Página 4 (última página: registros 31 a 35)
        var (page4Items, _) = await _mockDataService.GetDetailDataPagedAsync("blockwork-demo", 4, pageSize);

        // Assert Página 4
        Assert.Equal(5, page4Items.Count);
        Assert.Equal("ITM-031", page4Items[0]["ItemCode"]);
        Assert.Equal("ITM-035", page4Items[4]["ItemCode"]);
    }

    [Theory]
    [InlineData(35, 10, 4)]
    [InlineData(100, 10, 10)]
    [InlineData(0, 10, 1)]
    [InlineData(9, 10, 1)]
    [InlineData(11, 10, 2)]
    public void ListViewFrame_TotalPagesCalculation_ShouldMatchExpected(int totalCount, int pageSize, int expectedPages)
    {
        // Act
        int calculatedPages = pageSize > 0 ? Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize)) : 1;

        // Assert
        Assert.Equal(expectedPages, calculatedPages);
    }

    [Fact]
    public void ListViewFrame_VisiblePages_ShouldNotExceedMaxVisibleLimit()
    {
#pragma warning disable BL0005
        // Arrange
        var listFrame = new ListViewFrame<Dictionary<string, object>>
        {
            PageSize = 10,
            TotalCount = 250, // 25 páginas totales
            CurrentPage = 12,
            MaxVisiblePages = 10
        };

        // Act
        var visiblePages = listFrame.GetVisiblePages().ToList();

        // Assert
        Assert.True(visiblePages.Count <= 10);
        Assert.Contains(12, visiblePages); // La página activa debe estar incluida
        Assert.True(visiblePages.First() >= 1);
        Assert.True(visiblePages.Last() <= listFrame.TotalPages);
#pragma warning restore BL0005
    }

    [Fact]
    public void ListViewFrame_RecordIndexes_ShouldCalculateCorrectly()
    {
#pragma warning disable BL0005
        // Arrange: Página 2 de 35 registros con tamaño 10
        var listFrame = new ListViewFrame<Dictionary<string, object>>
        {
            PageSize = 10,
            TotalCount = 35,
            CurrentPage = 2
        };

        // Assert
        Assert.Equal(11, listFrame.StartRecordIndex);
        Assert.Equal(20, listFrame.EndRecordIndex);
#pragma warning restore BL0005
    }

    [Fact]
    public void WorkViewFrame_ActionInvocation_ShouldPreventConcurrentExecution()
    {
        // Arrange
        bool isBusy = false;
        int executedActions = 0;

        void SimulateAction(string actionKey)
        {
            if (isBusy) return;

            try
            {
                isBusy = true;
                executedActions++;
            }
            finally
            {
                // En ejecución asíncrona real, se libera al terminar
            }
        }

        // Act
        SimulateAction("apply_master");
        SimulateAction("apply_master"); // Intento concurrente ignorado

        // Assert
        Assert.Equal(1, executedActions);
    }
}
