namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using Xunit;

public sealed class CsvGridRenderingPolicyTests
{
    [Theory]
    [InlineData(0, 10, false)]
    [InlineData(999, 10, false)]
    [InlineData(1_000, 1, true)]
    [InlineData(800, 49, false)]
    [InlineData(800, 50, true)]
    [InlineData(10_000, 25, true)]
    public void ShouldUseVirtualReadOnlyRowsUsesBoundedRowAndCellThresholds(
        int rowCount,
        int columnCount,
        bool expected)
    {
        Assert.Equal(
            expected,
            CsvGridRenderingPolicy.ShouldUseVirtualReadOnlyRows(rowCount, columnCount));
    }

    [Fact]
    public void ShouldUseVirtualReadOnlyRowsRejectsNegativeInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CsvGridRenderingPolicy.ShouldUseVirtualReadOnlyRows(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CsvGridRenderingPolicy.ShouldUseVirtualReadOnlyRows(1, -1));
    }
}
