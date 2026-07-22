namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using Xunit;

public sealed class CsvTableBuilderTests
{
    [Fact]
    public void Build_EmptyText_ReturnsEmptyWithoutInventingDialect()
    {
        var result = CsvTableBuilder.Build(string.Empty);

        Assert.Equal(CsvTableBuildStatus.Empty, result.Status);
        Assert.Null(result.DetectionResult);
        Assert.Null(result.ParseResult);
        Assert.Null(result.Projection);
        Assert.False(result.DelimiterWasAutomatic);
    }

    [Fact]
    public void Build_ReliableAutomaticDetection_ReturnsReadyTable()
    {
        const string text = "Name,Age\nAnna,42\nBela,38";

        var result = CsvTableBuilder.Build(text);

        Assert.Equal(CsvTableBuildStatus.Ready, result.Status);
        Assert.True(result.DelimiterWasAutomatic);
        Assert.NotNull(result.DetectionResult);
        Assert.True(result.DetectionResult.IsReliable);
        Assert.NotNull(result.ParseResult);
        Assert.Equal(',', result.ParseResult.Dialect.Delimiter);
        Assert.NotNull(result.Projection);
        Assert.Equal(["Name", "Age"], result.Projection.Columns.Select(static column => column.Name));
        Assert.Equal(2, result.Projection.DisplayedRowCount);
    }

    [Fact]
    public void Build_LowConfidenceAutomaticDetection_RequiresExplicitChoice()
    {
        var result = CsvTableBuilder.Build("a,b,c");

        Assert.Equal(CsvTableBuildStatus.DelimiterSelectionRequired, result.Status);
        Assert.True(result.DelimiterWasAutomatic);
        Assert.NotNull(result.DetectionResult);
        Assert.Equal(CsvDelimiterConfidence.Low, result.DetectionResult.Confidence);
        Assert.Null(result.ParseResult);
        Assert.Null(result.Projection);
    }

    [Fact]
    public void Build_ManualDelimiter_BypassesLowConfidenceGate()
    {
        var result = CsvTableBuilder.Build(
            "a,b,c",
            new CsvTableBuildOptions
            {
                DelimiterOverride = ',',
                HeaderMode = CsvHeaderMode.NoHeader
            });

        Assert.Equal(CsvTableBuildStatus.Ready, result.Status);
        Assert.False(result.DelimiterWasAutomatic);
        Assert.Null(result.DetectionResult);
        Assert.NotNull(result.ParseResult);
        Assert.Equal(',', result.ParseResult.Dialect.Delimiter);
        Assert.NotNull(result.Projection);
        Assert.Equal(1, result.Projection.DisplayedRowCount);
        Assert.Equal(["Column 1", "Column 2", "Column 3"], result.Projection.Columns.Select(static column => column.Name));
    }

    [Fact]
    public void Build_ManualSemicolon_PreservesDecimalCommasAsCellContent()
    {
        const string text = "Item;Value\nA;12,5\nB;3,3";

        var result = CsvTableBuilder.Build(
            text,
            new CsvTableBuildOptions { DelimiterOverride = ';' });

        Assert.Equal(CsvTableBuildStatus.Ready, result.Status);
        Assert.NotNull(result.Projection);
        Assert.Equal("12,5", result.Projection.Rows[0].Values[1]);
        Assert.Equal("3,3", result.Projection.Rows[1].Values[1]);
    }

    [Fact]
    public void Build_NoHeaderMode_KeepsFirstRecordAsData()
    {
        const string text = "Name,Age\nAnna,42\nBela,38";

        var result = CsvTableBuilder.Build(
            text,
            new CsvTableBuildOptions
            {
                DelimiterOverride = ',',
                HeaderMode = CsvHeaderMode.NoHeader
            });

        Assert.NotNull(result.Projection);
        Assert.Null(result.Projection.HeaderSourceRecordIndex);
        Assert.Equal(3, result.Projection.TotalDataRecordCount);
        Assert.Equal(["Name", "Age"], result.Projection.Rows[0].Values);
    }

    [Fact]
    public void Build_InvalidDelimiterOverride_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CsvTableBuilder.Build(
                "a|b",
                new CsvTableBuildOptions { DelimiterOverride = '|' }));
    }
}