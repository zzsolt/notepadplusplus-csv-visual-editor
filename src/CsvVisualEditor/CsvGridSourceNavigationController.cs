namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using Npp.DotNet.Plugin;
using System.Globalization;

/// <summary>
/// Resolves the current grid row/cell to stable parser source identity and executes
/// one read-only Scintilla selection. It never changes document text, modified state,
/// the edit model, or disk content.
/// </summary>
internal static class CsvGridSourceNavigationController
{
    internal static bool CanNavigate(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return TryResolveAddress(grid, out _, out _);
    }

    internal static bool TryNavigate(DataGridView grid, CsvGridForm form)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(form);

        if (!TryResolveAddress(grid, out var address, out var unavailableReason))
        {
            CsvGridClipboardController.ShowStatus(form, unavailableReason);
            return true;
        }

        ActiveDocumentSnapshot currentSnapshot;
        try
        {
            currentSnapshot = new NotepadActiveDocumentReader().ReadActiveDocument();
        }
        catch (Exception)
        {
            CsvGridClipboardController.ShowStatus(
                form,
                "Go to source failed: the active Notepad++ buffer could not be read. No document content was changed.");
            return true;
        }

        if (form.IsEditMode &&
            form.EditSession is CsvEditSession editSession &&
            !MatchesEditBaseline(editSession.Baseline, currentSnapshot, out var conflictMessage))
        {
            CsvGridClipboardController.ShowStatus(form, conflictMessage);
            return true;
        }

        CsvTableBuildResult buildResult;
        try
        {
            buildResult = CsvTableBuilder.Build(
                currentSnapshot.Text,
                new CsvTableBuildOptions
                {
                    DelimiterOverride = form.SelectedDelimiterOverride,
                    HeaderMode = form.SelectedHeaderMode
                });
        }
        catch (Exception)
        {
            CsvGridClipboardController.ShowStatus(
                form,
                "Go to source failed: the current buffer could not be parsed with the active table options.");
            return true;
        }

        if (buildResult.Status != CsvTableBuildStatus.Ready ||
            buildResult.ParseResult is null)
        {
            CsvGridClipboardController.ShowStatus(
                form,
                "Go to source is unavailable until the current buffer has a complete parse result. Refresh or choose the delimiter explicitly.");
            return true;
        }

        if (!form.IsEditMode &&
            !CurrentGridRowMatchesFreshRecord(
                grid,
                address.SourceRecordIndex,
                buildResult.ParseResult))
        {
            CsvGridClipboardController.ShowStatus(
                form,
                "Go to source blocked: the visual row no longer matches the active buffer. Refresh the table first.");
            return true;
        }

        var plan = CsvSourceNavigationPlanner.Create(
            currentSnapshot,
            currentSnapshot,
            buildResult.ParseResult,
            address);

        if (plan.Status == CsvSourceNavigationStatus.SourceColumnUnavailable &&
            address.ColumnIndex is not null)
        {
            // A projected padded cell has no raw field span. Fall back to the complete
            // source record rather than inventing a non-existent cell position.
            plan = CsvSourceNavigationPlanner.Create(
                currentSnapshot,
                currentSnapshot,
                buildResult.ParseResult,
                new CsvSourceNavigationAddress(address.SourceRecordIndex, columnIndex: null));
        }

        if (!plan.IsReady)
        {
            CsvGridClipboardController.ShowStatus(
                form,
                DescribeBlockedPlan(plan.Status));
            return true;
        }

        try
        {
            // SCI_SETSEL is byte-oriented and scrolls the caret into view. The planner
            // has already proven exact code-page byte positions and whole-buffer length.
            PluginData.Editor.SetSel(
                plan.AnchorBytePosition,
                plan.CaretBytePosition);
        }
        catch (Exception)
        {
            CsvGridClipboardController.ShowStatus(
                form,
                "Go to source failed while moving the Scintilla selection. No document content was changed.");
            return true;
        }

        CsvGridClipboardController.ShowStatus(
            form,
            plan.Address.ColumnIndex is int columnIndex
                ? $"Selected source record {(plan.Address.SourceRecordIndex + 1).ToString(CultureInfo.CurrentCulture)}, column {(columnIndex + 1).ToString(CultureInfo.CurrentCulture)} in Notepad++."
                : $"Selected source record {(plan.Address.SourceRecordIndex + 1).ToString(CultureInfo.CurrentCulture)} in Notepad++.");
        return true;
    }

    internal static bool TryResolveAddress(
        DataGridView grid,
        out CsvSourceNavigationAddress address,
        out string unavailableReason)
    {
        ArgumentNullException.ThrowIfNull(grid);

        address = default;
        unavailableReason =
            "Go to source requires a current CSV row or cell.";

        var currentCell = grid.CurrentCell;
        if (currentCell is null ||
            currentCell.RowIndex < 0 ||
            currentCell.RowIndex >= grid.Rows.Count)
        {
            return false;
        }

        var row = grid.Rows[currentCell.RowIndex];
        var sourceRecordIndex = ResolveSourceRecordIndex(grid, row);
        if (sourceRecordIndex is null)
        {
            unavailableReason = row.Tag is CsvEditRowId rowId && rowId.IsInserted
                ? "This pending inserted row has no source location until Apply completes."
                : "The current visual row does not expose a stable source record identity.";
            return false;
        }

        int? columnIndex = null;
        if (currentCell.ColumnIndex >= 0 &&
            currentCell.ColumnIndex < grid.Columns.Count &&
            !CsvGridRowHeaderBehavior.IsPresentationColumn(
                grid.Columns[currentCell.ColumnIndex]))
        {
            columnIndex = currentCell.ColumnIndex;
        }

        address = new CsvSourceNavigationAddress(
            sourceRecordIndex.Value,
            columnIndex);
        unavailableReason = string.Empty;
        return true;
    }

    private static int? ResolveSourceRecordIndex(
        DataGridView grid,
        DataGridViewRow row)
    {
        if (row.Tag is CsvEditRowId editRowId)
        {
            return editRowId.SourceRecordIndex;
        }

        if (row.Tag is int sourceRecordIndex && sourceRecordIndex >= 0)
        {
            return sourceRecordIndex;
        }

        var indicatorColumn = grid.Columns[CsvGridRowPresentation.RowIndicatorColumnName];
        if (indicatorColumn is null ||
            row.Index < 0 ||
            indicatorColumn.Index < 0 ||
            indicatorColumn.Index >= row.Cells.Count)
        {
            return null;
        }

        var label = Convert.ToString(
            row.Cells[indicatorColumn.Index].Value,
            CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        var numericPrefix = new string(label
            .TakeWhile(static character => char.IsDigit(character))
            .ToArray());
        return int.TryParse(
                numericPrefix,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var logicalRecordNumber) &&
            logicalRecordNumber > 0
            ? logicalRecordNumber - 1
            : null;
    }

    private static bool MatchesEditBaseline(
        CsvEditSessionBaseline baseline,
        ActiveDocumentSnapshot currentSnapshot,
        out string conflictMessage)
    {
        var sameDocument = baseline.DocumentPath.Length > 0 ||
                           currentSnapshot.DocumentPath.Length > 0
            ? string.Equals(
                baseline.DocumentPath,
                currentSnapshot.DocumentPath,
                StringComparison.OrdinalIgnoreCase)
            : string.Equals(
                baseline.DisplayName,
                currentSnapshot.DisplayName,
                StringComparison.Ordinal);

        if (!sameDocument)
        {
            conflictMessage =
                "Go to source blocked: another Notepad++ document is active. Return to the edited CSV first.";
            return false;
        }

        if (baseline.CodePage != currentSnapshot.CodePage)
        {
            conflictMessage =
                "Go to source blocked: the editor code page changed after Edit mode started. Revert or refresh first.";
            return false;
        }

        if (!string.Equals(
                baseline.ContentSha256,
                currentSnapshot.ContentSha256,
                StringComparison.Ordinal))
        {
            conflictMessage =
                "Go to source blocked: the editor buffer changed after Edit mode started. Revert or refresh first.";
            return false;
        }

        conflictMessage = string.Empty;
        return true;
    }

    private static bool CurrentGridRowMatchesFreshRecord(
        DataGridView grid,
        int sourceRecordIndex,
        CsvParseResult parseResult)
    {
        var currentCell = grid.CurrentCell;
        if (currentCell is null ||
            sourceRecordIndex < 0 ||
            sourceRecordIndex >= parseResult.Records.Count)
        {
            return false;
        }

        var record = parseResult.Records[sourceRecordIndex];
        if (record.Index != sourceRecordIndex)
        {
            return false;
        }

        var row = grid.Rows[currentCell.RowIndex];
        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (CsvGridRowHeaderBehavior.IsPresentationColumn(column))
            {
                continue;
            }

            var expected = column.Index < record.Cells.Count
                ? record.Cells[column.Index].Value
                : string.Empty;
            var displayed = Convert.ToString(
                row.Cells[column.Index].Value,
                CultureInfo.InvariantCulture) ?? string.Empty;
            if (!string.Equals(displayed, expected, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string DescribeBlockedPlan(CsvSourceNavigationStatus status) =>
        status switch
        {
            CsvSourceNavigationStatus.UnsupportedCodePage =>
                "Go to source blocked: the current Scintilla code page has no explicit byte-position profile.",
            CsvSourceNavigationStatus.EditorByteLengthMismatch =>
                "Go to source blocked: strict encoded length does not match Scintilla's reported document length.",
            CsvSourceNavigationStatus.PositionEncodingFailed =>
                "Go to source blocked: the source character span could not be mapped losslessly to Scintilla bytes.",
            CsvSourceNavigationStatus.SourceRecordUnavailable =>
                "Go to source blocked: the source logical record is no longer available.",
            CsvSourceNavigationStatus.SourceColumnUnavailable =>
                "Go to source blocked: the selected projected cell has no raw source field.",
            CsvSourceNavigationStatus.DocumentIdentityChanged =>
                "Go to source blocked: another Notepad++ document is active.",
            CsvSourceNavigationStatus.CodePageChanged =>
                "Go to source blocked: the editor code page changed.",
            CsvSourceNavigationStatus.ContentChanged =>
                "Go to source blocked: the editor content changed. Refresh the table first.",
            _ => "Go to source was not completed. No document content was changed."
        };
}
