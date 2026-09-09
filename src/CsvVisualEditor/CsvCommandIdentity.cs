namespace CsvVisualEditor;

/// <summary>Stable icon/menu identities; never dispatch on localized captions.</summary>
internal static class CsvCommandIdentity
{
    internal static string Of(ToolStripButton button) => button.Name switch
    {
        "CsvRefreshButton" => "Refresh",
        "CsvEditButton" => "Edit",
        "CsvAddRowButton" => "Add Row",
        "CsvDeleteRowButton" => "Delete Row",
        "CsvApplyButton" => "Apply",
        "CsvRevertAllButton" => "Revert",
        "CsvResetViewButton" => "Reset view",
        "CsvDiagnosticsButton" => "Diagnostics",
        "CsvClipboardCopyButton" => "Copy",
        "CsvClipboardCutButton" => "Cut",
        "CsvClipboardPasteButton" => "Paste",
        "CsvGoToSourceButton" => "Source",
        "CsvTransformButton" => "Transform",
        "CsvDataViewButton" => "Filter and sort",
        "CsvColumnSummaryButton" => "Column summary",
        "CsvShowSpacesButton" => "Show spaces",
        // Compatibility for caller-owned unnamed items. Production commands have names.
        _ => button.Text ?? string.Empty
    };
}
