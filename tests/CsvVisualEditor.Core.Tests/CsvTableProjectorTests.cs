namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using Xunit;

public sealed class CsvTableProjectorTests
{
    [Fact]
    public void Create_FirstRecordHeader_UsesHeaderAndExcludesItFromRows()
    {
        var parseResult = CsvParser.Parse(
            "Name,Age\nAnna,42\nBela,38",
            CsvDialect.Create(','));

        var projection = CsvTableProjector.Create(parseResult);

        Assert.Equal(["Name", "Age"], ColumnNames(projection));
        Assert.Equal(2, projection.TotalDataRecordCount);
        Assert.Equal(2, projection.DisplayedRowCount);
        Assert.Equal(0, projection.HeaderSourceRecordIndex);
        Assert.Equal(1, projection.Rows[0].SourceRecordIndex);
        Assert.Equal(["Anna", "42"], projection.Rows[0].Values);
        Assert.False(projection.IsRowLimited);
    }

    [Fact]
    public void Create_NoHeader_UsesFallbackNamesAndKeepsFirstRecord()
    {
        var parseResult = CsvParser.Parse(
            "Anna,42\nBela,38",
            CsvDialect.Create(','));

        var projection = CsvTableProjector.Create(
            parseResult,
            new CsvTableProjectionOptions { HeaderMode = CsvHeaderMode.NoHeader });

        Assert.Equal(["Column 1", "Column 2"], ColumnNames(projection));
        Assert.Null(projection.HeaderSourceRecordIndex);
        Assert.Equal(2, projection.TotalDataRecordCount);
        Assert.Equal(0, projection.Rows[0].SourceRecordIndex);
        Assert.Equal(["Anna", "42"], projection.Rows[0].Values);
    }

    [Fact]
    public void Create_DuplicateEmptyAndMultilineHeaders_ProducesStableUniqueNames()
    {
        const string text = "\" Name \";;Name;\"Multi\nLine\"\nAnna;42;x;y";
        var parseResult = CsvParser.Parse(text, CsvDialect.Create(';'));

        var projection = CsvTableProjector.Create(parseResult);

        Assert.Equal(
            ["Name", "Column 2", "Name (2)", "Multi Line"],
            ColumnNames(projection));
        Assert.Equal(["Anna", "42", "x", "y"], projection.Rows[0].Values);
    }

    [Fact]
    public void Create_InconsistentWidths_PadsOnlyTheViewWithEmptyValues()
    {
        var parseResult = CsvParser.Parse(
            "A,B,C\n1,2\n3,4,5",
            CsvDialect.Create(','));

        var projection = CsvTableProjector.Create(parseResult);

        Assert.Equal(["1", "2", ""], projection.Rows[0].Values);
        Assert.Equal(["3", "4", "5"], projection.Rows[1].Values);
        Assert.Equal(2, parseResult.Records[1].FieldCount);
        Assert.Equal(3, parseResult.Records[2].FieldCount);
    }

    [Fact]
    public void Create_RowLimit_ReportsTruncationAndRetainsTotalCount()
    {
        var parseResult = CsvParser.Parse(
            "1,one\n2,two\n3,three",
            CsvDialect.Create(','));

        var projection = CsvTableProjector.Create(
            parseResult,
            new CsvTableProjectionOptions
            {
                HeaderMode = CsvHeaderMode.NoHeader,
                MaximumRows = 2
            });

        Assert.True(projection.IsRowLimited);
        Assert.Equal(3, projection.TotalDataRecordCount);
        Assert.Equal(2, projection.DisplayedRowCount);
        Assert.Equal(["1", "one"], projection.Rows[0].Values);
        Assert.Equal(["2", "two"], projection.Rows[1].Values);
    }

    [Fact]
    public void Create_CellBudget_LimitsRowsAndRetainsTotalCount()
    {
        var parseResult = CsvParser.Parse(
            "1,a,x\n2,b,y\n3,c,z\n4,d,w",
            CsvDialect.Create(','));

        var projection = CsvTableProjector.Create(
            parseResult,
            new CsvTableProjectionOptions
            {
                HeaderMode = CsvHeaderMode.NoHeader,
                MaximumRows = 10,
                MaximumCells = 6
            });

        Assert.True(projection.IsRowLimited);
        Assert.Equal(4, projection.TotalDataRecordCount);
        Assert.Equal(2, projection.DisplayedRowCount);
        Assert.Equal(["1", "a", "x"], projection.Rows[0].Values);
        Assert.Equal(["2", "b", "y"], projection.Rows[1].Values);
    }

    [Fact]
    public void Create_ColumnLimit_RejectsInsteadOfSilentlyHidingColumns()
    {
        var parseResult = CsvParser.Parse("A,B,C\n1,2,3", CsvDialect.Create(','));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CsvTableProjector.Create(
                parseResult,
                new CsvTableProjectionOptions { MaximumColumns = 2 }));

        Assert.Contains("exceeds", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No columns were hidden", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_CellBudgetSmallerThanOneRow_RejectsInsteadOfHidingColumns()
    {
        var parseResult = CsvParser.Parse("A,B,C\n1,2,3", CsvDialect.Create(','));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CsvTableProjector.Create(
                parseResult,
                new CsvTableProjectionOptions
                {
                    MaximumColumns = 3,
                    MaximumCells = 2
                }));

        Assert.Contains("aggregate cell limit", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No columns were hidden", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_EmptyInput_ReturnsEmptyProjection()
    {
        var parseResult = CsvParser.Parse(string.Empty, CsvDialect.Create(','));

        var projection = CsvTableProjector.Create(parseResult);

        Assert.Empty(projection.Columns);
        Assert.Empty(projection.Rows);
        Assert.Equal(0, projection.TotalDataRecordCount);
        Assert.Null(projection.HeaderSourceRecordIndex);
        Assert.False(projection.IsRowLimited);
    }

    [Fact]
    public void Create_UnknownHeaderMode_IsRejectedBecauseDisplayChoiceMustBeExplicit()
    {
        var parseResult = CsvParser.Parse("A,B", CsvDialect.Create(','));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CsvTableProjector.Create(
                parseResult,
                new CsvTableProjectionOptions { HeaderMode = CsvHeaderMode.Unknown }));
    }

    [Fact]
    public void Create_InvalidDisplayLimits_AreRejected()
    {
        var parseResult = CsvParser.Parse("A,B", CsvDialect.Create(','));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CsvTableProjector.Create(
                parseResult,
                new CsvTableProjectionOptions { MaximumRows = 0 }));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CsvTableProjector.Create(
                parseResult,
                new CsvTableProjectionOptions { MaximumColumns = 0 }));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CsvTableProjector.Create(
                parseResult,
                new CsvTableProjectionOptions { MaximumCells = 0 }));
    }

    private static string[] ColumnNames(CsvTableProjection projection) =>
        projection.Columns.Select(static column => column.Name).ToArray();
}