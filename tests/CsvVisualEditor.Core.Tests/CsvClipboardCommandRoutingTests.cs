namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using Xunit;

public sealed class CsvClipboardCommandRoutingTests
{
    [Fact]
    public void ResolvePasteTarget_GridOwnsFocus_UsesGridForSingleCellText()
    {
        Assert.Equal(
            CsvClipboardPasteRoutingTarget.Grid,
            CsvClipboardCommandRouting.ResolvePasteTarget(false, "single value"));
    }

    [Fact]
    public void ResolvePasteTarget_CellEditorOwnsFocus_PreservesSingleCellTextPaste()
    {
        Assert.Equal(
            CsvClipboardPasteRoutingTarget.InCellEditor,
            CsvClipboardCommandRouting.ResolvePasteTarget(true, "single value"));
    }

    [Fact]
    public void ResolvePasteTarget_CellEditorOwnsFocus_PreservesEmptyTextPaste()
    {
        Assert.Equal(
            CsvClipboardPasteRoutingTarget.InCellEditor,
            CsvClipboardCommandRouting.ResolvePasteTarget(true, string.Empty));
    }

    [Theory]
    [InlineData("A\tB")]
    [InlineData("A\r\nB")]
    [InlineData("A\nB")]
    public void ResolvePasteTarget_CellEditorOwnsFocus_RoutesSpreadsheetTextToGrid(
        string clipboardText)
    {
        Assert.Equal(
            CsvClipboardPasteRoutingTarget.Grid,
            CsvClipboardCommandRouting.ResolvePasteTarget(true, clipboardText));
    }
}
