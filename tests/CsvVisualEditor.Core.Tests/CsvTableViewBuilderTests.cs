namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using Xunit;

public sealed class CsvTableViewBuilderTests
{
    [Fact]
    public void Build_BlankSearch_PreservesSourceOrder()
    {
        var projection = CreateProjection();

        var result = CsvTableViewBuilder.Build(
            projection,
            new CsvTableViewOptions { SearchText = "   " });

        Assert.False(result.IsFiltered);
        Assert.False(result.IsSorted);
        Assert.Equal([1, 2, 3, 4], result.Rows.Select(static row => row.SourceRecordIndex));
    }

    [Fact]
    public void Build_AllColumnSearch_IsCaseInsensitive()
    {
        var result = CsvTableViewBuilder.Build(
            CreateProjection(),
            new CsvTableViewOptions { SearchText = "EXAMPLE.INVALID" });

        Assert.True(result.IsFiltered);
        Assert.Equal(3, result.VisibleRowCount);
        Assert.Equal([1, 2, 4], result.Rows.Select(static row => row.SourceRecordIndex));
    }

    [Fact]
    public void Build_ColumnSearch_RestrictsMatchingColumn()
    {
        var result = CsvTableViewBuilder.Build(
            CreateProjection(),
            new CsvTableViewOptions
            {
                SearchText = "beta",
                SearchColumnIndex = 1
            });

        Assert.Single(result.Rows);
        Assert.Equal(2, result.Rows[0].SourceRecordIndex);
    }

    [Fact]
    public void Build_AscendingSort_IsStableForEquivalentValues()
    {
        var result = CsvTableViewBuilder.Build(
            CreateProjection(),
            new CsvTableViewOptions
            {
                SortColumnIndex = 2,
                SortDirection = CsvTableSortDirection.Ascending
            });

        Assert.True(result.IsSorted);
        Assert.Equal([3, 1, 2, 4], result.Rows.Select(static row => row.SourceRecordIndex));
    }

    [Fact]
    public void Build_DescendingSort_IsStableForEquivalentValues()
    {
        var result = CsvTableViewBuilder.Build(
            CreateProjection(),
            new CsvTableViewOptions
            {
                SortColumnIndex = 2,
                SortDirection = CsvTableSortDirection.Descending
            });

        Assert.Equal([1, 2, 4, 3], result.Rows.Select(static row => row.SourceRecordIndex));
    }

    [Fact]
    public void Build_FilterThenSort_UsesOnlyMatchingRows()
    {
        var result = CsvTableViewBuilder.Build(
            CreateProjection(),
            new CsvTableViewOptions
            {
                SearchText = "example.invalid",
                SortColumnIndex = 1,
                SortDirection = CsvTableSortDirection.Descending
            });

        Assert.Equal([4, 2, 1], result.Rows.Select(static row => row.SourceRecordIndex));
        Assert.Equal(4, result.TotalRowCount);
        Assert.Equal(3, result.VisibleRowCount);
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(3, null)]
    [InlineData(null, -1)]
    [InlineData(null, 3)]
    public void Build_InvalidColumnIndex_Throws(
        int? searchColumnIndex,
        int? sortColumnIndex)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => CsvTableViewBuilder.Build(
                CreateProjection(),
                new CsvTableViewOptions
                {
                    SearchColumnIndex = searchColumnIndex,
                    SortColumnIndex = sortColumnIndex
                }));

        Assert.Contains("ColumnIndex", exception.ParamName, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_SortDirectionWithoutColumn_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => CsvTableViewBuilder.Build(
                CreateProjection(),
                new CsvTableViewOptions
                {
                    SortDirection = CsvTableSortDirection.Ascending
                }));
    }

    [Fact]
    public void Build_DoesNotMutateProjectionRows()
    {
        var projection = CreateProjection();
        var originalOrder = projection.Rows
            .Select(static row => row.SourceRecordIndex)
            .ToArray();

        _ = CsvTableViewBuilder.Build(
            projection,
            new CsvTableViewOptions
            {
                SearchText = "example.invalid",
                SortColumnIndex = 1,
                SortDirection = CsvTableSortDirection.Descending
            });

        Assert.Equal(
            originalOrder,
            projection.Rows.Select(static row => row.SourceRecordIndex));
    }

    private static CsvTableProjection CreateProjection()
    {
        var columns = new[]
        {
            new CsvTableColumn(0, "Email"),
            new CsvTableColumn(1, "Name"),
            new CsvTableColumn(2, "Group")
        };
        var rows = new[]
        {
            CreateRow(1, "alpha@example.invalid", "Alpha", "B"),
            CreateRow(2, "beta@example.invalid", "Beta", "b"),
            CreateRow(3, "gamma@other.invalid", "Gamma", "A"),
            CreateRow(4, "delta@example.invalid", "Delta", "B")
        };

        return new CsvTableProjection(
            columns,
            rows,
            totalDataRecordCount: rows.Length,
            headerSourceRecordIndex: 0,
            isRowLimited: false);
    }

    private static CsvTableRow CreateRow(
        int sourceRecordIndex,
        params string[] values) =>
        new(
            sourceRecordIndex,
            values,
            new CsvSourceSpan(sourceRecordIndex * 10, values.Sum(static value => value.Length)));
}
