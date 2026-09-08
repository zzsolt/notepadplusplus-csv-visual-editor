namespace CsvVisualEditor.Core.Tests;

using Xunit;

public sealed class CsvCellSearchIndexTests
{
    [Fact]
    public void CountsCellsNotRepeatedOccurrencesAndPreservesRawValues()
    {
        var view = View("test", null, ["  TEST test  ", "other", "test"]);
        var index = CsvCellSearchIndex.Create(view);
        Assert.Equal([new CsvSearchCell(0, 0), new CsvSearchCell(0, 2)], index.Cells);
        Assert.Equal("  TEST test  ", view.Rows[0].Values[0]);
    }

    [Fact]
    public void ColumnScopeExcludesOtherDataColumns()
    {
        var index = CsvCellSearchIndex.Create(View("test", 1, ["test", "TEST"], ["test", "no"]));
        Assert.Equal([new CsvSearchCell(0, 1)], index.Cells);
    }

    [Fact]
    public void EmptyQueryDoesNotHighlightEveryCell()
    {
        Assert.Empty(CsvCellSearchIndex.Create(View("", null, ["", "value"])).Cells);
    }

    [Fact]
    public void FullValueBeyondPaintLimitStillMatches()
    {
        var index = CsvCellSearchIndex.Create(View("needle", null, [new string('x', 2048) + "needle"]));
        Assert.Equal(0, index.FindIndex(0, 0));
    }

    [Theory]
    [InlineData(-1, -1, false, 0)]
    [InlineData(-1, -1, true, 2)]
    [InlineData(0, 0, false, 1)]
    [InlineData(0, 0, true, 2)]
    [InlineData(1, 0, false, 2)]
    [InlineData(1, 0, true, 1)]
    [InlineData(2, 0, false, 0)]
    [InlineData(2, 0, true, 1)]
    [InlineData(10, 0, false, 0)]
    [InlineData(10, 0, true, 2)]
    public void NavigationUsesVisibleOrderAndWraps(int row, int column, bool backwards, int expected)
    {
        var index = CsvCellSearchIndex.Create(View("x", null, ["x", "x"], ["no", "no"], ["x", "no"]));
        Assert.Equal(expected, index.MoveFrom(row, column, backwards));
    }

    [Fact]
    public void EmptyResultNavigationIsSafe()
    {
        var index = CsvCellSearchIndex.Create(View("absent", null, ["value"]));
        Assert.Equal(-1, index.MoveFrom(0, 0, false));
        Assert.Equal(-1, index.MoveFrom(-1, -1, true));
        Assert.Equal(-1, index.FindIndex(0, 0));
    }

    [Fact]
    public void PresentationAndNegativeAddressesAreNotMatches()
    {
        var index = CsvCellSearchIndex.Create(View("x", null, ["x", "x"]));
        Assert.Equal(-1, index.FindIndex(0, 2));
        Assert.Equal(-1, index.FindIndex(-1, 0));
        Assert.Equal(-1, index.FindIndex(0, -1));
        Assert.Equal(-1, index.FindIndex(1, 0));
    }

    [Fact]
    public void RealFilterSortAndIndexUseTheSameQueryAndVisibleRows()
    {
        var rows = new[] { Row(1, "test", "B"), Row(2, "TEST", "A"), Row(3, "other", "C") };
        var projection = new CsvTableProjection([new CsvTableColumn(0, "Value"), new CsvTableColumn(1, "Order")],
            rows, 3, 0, false);
        var view = CsvTableViewBuilder.Build(projection, new CsvTableViewOptions
        {
            SearchText = "  TeSt  ", SortColumnIndex = 1, SortDirection = CsvTableSortDirection.Ascending
        });
        var index = CsvCellSearchIndex.Create(view);
        Assert.Equal("TeSt", view.EffectiveSearchText);
        Assert.Equal(2, view.Rows[0].SourceRecordIndex);
        Assert.Equal([new CsvSearchCell(0, 0), new CsvSearchCell(1, 0)], index.Cells);
    }

    [Fact]
    public void LargeIndexKeepsAllMatchesWithoutA4096EntryCacheReset()
    {
        var rows = Enumerable.Range(0, 10000).Select(i => Row(i, "match", "match")).ToArray();
        var view = new CsvTableViewResult(rows, rows.Length, "match", null, null, CsvTableSortDirection.None);
        var index = CsvCellSearchIndex.Create(view);
        Assert.Equal(20000, index.Count);
        Assert.Equal(19999, index.FindIndex(9999, 1));
        Assert.Equal(0, index.MoveFrom(9999, 1, false));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(99)]
    public void InvalidSortDirectionIsRejected(int direction)
    {
        var projection = new CsvTableProjection([new CsvTableColumn(0, "Value")], [Row(0, "x")], 1, null, false);
        Assert.Throws<ArgumentOutOfRangeException>(() => CsvTableViewBuilder.Build(projection,
            new CsvTableViewOptions { SortColumnIndex = 0, SortDirection = (CsvTableSortDirection)direction }));
    }

    [Fact]
    public void NullViewIsRejected() => Assert.Throws<ArgumentNullException>(() => CsvCellSearchIndex.Create(null!));

    private static CsvTableViewResult View(string query, int? column, params string[][] values) =>
        new(values.Select((row, index) => Row(index, row)), values.Length, query, column, null, CsvTableSortDirection.None);

    private static CsvTableRow Row(int index, params string[] values) => new(index, values, new CsvSourceSpan(index * 10, 1));
}
