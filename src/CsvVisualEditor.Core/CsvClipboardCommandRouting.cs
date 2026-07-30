namespace CsvVisualEditor.Core;

/// <summary>
/// Selects whether Ctrl+V belongs to the spreadsheet-style grid paste path or to
/// the native in-cell text editor. Multi-cell clipboard text must never be inserted
/// as one literal cell value merely because the editing control currently owns focus.
/// </summary>
public static class CsvClipboardCommandRouting
{
    public static CsvClipboardPasteRoutingTarget ResolvePasteTarget(
        bool isCellEditorActive,
        string clipboardText)
    {
        ArgumentNullException.ThrowIfNull(clipboardText);

        if (!isCellEditorActive)
        {
            return CsvClipboardPasteRoutingTarget.Grid;
        }

        return ContainsSpreadsheetSeparator(clipboardText)
            ? CsvClipboardPasteRoutingTarget.Grid
            : CsvClipboardPasteRoutingTarget.InCellEditor;
    }

    private static bool ContainsSpreadsheetSeparator(string text) =>
        text.Contains('\t') || text.Contains('\r') || text.Contains('\n');
}

public enum CsvClipboardPasteRoutingTarget
{
    Grid,
    InCellEditor
}
