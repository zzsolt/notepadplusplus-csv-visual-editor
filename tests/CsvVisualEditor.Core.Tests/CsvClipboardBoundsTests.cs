namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using Xunit;

public sealed class CsvClipboardBoundsTests
{
    [Fact]
    public void OversizedTextIsRejectedBeforeSplitting() => Assert.Throws<FormatException>(() =>
        CsvClipboardMatrix.Parse(new string('x', CsvClipboardMatrix.MaximumTextCharacters + 1)));

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void MaximumRowsAndOneTerminalLineBreakAreAccepted(string ending)
    {
        var text = string.Join(ending, Enumerable.Repeat("x", 10_000));
        Assert.Equal(10_000, CsvClipboardMatrix.Parse(text).RowCount);
        Assert.Equal(10_000, CsvClipboardMatrix.Parse(text + ending).RowCount);
        Assert.Throws<FormatException>(() => CsvClipboardMatrix.Parse(text + ending + "x"));
        Assert.Throws<FormatException>(() => CsvClipboardMatrix.Parse(text + ending + ending));
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("\n", 1)]
    [InlineData("\n\n", 2)]
    [InlineData("\r\n\r\n", 2)]
    public void EmptyRowsRetainTheirExistingMeaning(string text, int expectedRows) =>
        Assert.Equal(expectedRows, CsvClipboardMatrix.Parse(text).RowCount);

    [Fact]
    public void ColumnSplittingIsBoundedAndKeepsTrailingEmptyFields()
    {
        var text = new string('\t', 511);
        Assert.Equal(512, CsvClipboardMatrix.Parse(text).ColumnCount);
        Assert.Equal("", CsvClipboardMatrix.Parse(text)[0, 511]);
        Assert.Throws<FormatException>(() => CsvClipboardMatrix.Parse(new string('\t', 100_000)));
    }

    [Fact]
    public void CellLimitIsStillEnforced()
    {
        var row = string.Join('\t', Enumerable.Repeat("x", 500));
        var text = string.Join('\n', Enumerable.Repeat(row, 500));
        Assert.Equal(500, CsvClipboardMatrix.Parse(text).RowCount);
        Assert.Throws<FormatException>(() => CsvClipboardMatrix.Parse(text + "\n" + row));
    }
}
