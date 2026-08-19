namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using Npp.DotNet.Plugin;
using System.Globalization;
using System.Runtime.CompilerServices;

/// <summary>
/// Resolves the current grid row/cell to stable parser source identity and executes
/// one read-only Scintilla selection. The rendered immutable snapshot/parse result is
/// retained as the navigation baseline, so stale document/content/code-page state can
/// never be replaced by a fresh parse of an unrelated active document.
/// </summary>
internal static class CsvGridSourceNavigationController
{
    private static readonly ConditionalWeakTable<DataGridView, NavigationBaseline> Baselines = new();

    internal static void SetBaseline(
        Control root,
        ActiveDocumentSnapshot snapshot,
        CsvParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(parseResult);

        var grid = FindPrimaryTableGrid(root);
        if (grid is null)
        {
            return;
        }

        Baselines.Remove(grid);
        Baselines.Add(grid, new NavigationBaseline(snapshot, parseResult));
    }

    internal static void ClearBaseline(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var grid = FindPrimaryTableGrid(root);
        if (grid is not null)
        {
            Baselines.Remove(grid);
        }
    }

    internal static bool CanNavigate(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return Baselines.TryGetValue(grid, out _) &&
               TryResolveAddress(grid, out _, out _);
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

        if (!Baselines.TryGetValue(grid, out var baseline))
        {
            CsvGridClipboardController.ShowStatus(
                form,
                "Go to source is unavailable until the current visual table has a complete source baseline. Refresh the table first.");
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

        CsvSourceNavigationPlan plan;
        try
        {
            plan = CsvSourceNavigationPlanner.Create(
                baseline.Snapshot,
                currentSnapshot,
                baseline.ParseResult,
                address);

            if (plan.Status == CsvSourceNavigationStatus.SourceColumnUnavailable &&
                address.ColumnIndex is not null)
            {
                // A projected padded cell has no raw field span. Fall back to the
                // complete source record rather than inventing a field position.
                plan = CsvSourceNavigationPlanner.Create(
                    baseline.Snapshot,
                    currentSnapshot,
                    baseline.ParseResult,
                    new CsvSourceNavigationAddress(
                        address.SourceRecordIndex,
                        columnIndex: null));
            }
        }
        catch (InvalidOperationException)
        {
            CsvGridClipboardController.ShowStatus(
                form,
                "Go to source blocked because the retained parse result no longer matches its immutable source snapshot. Refresh the table first.");
            return true;
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
            // SCI_SETSEL is byte-oriented. The Core plan has already proven the
            // document/code-page/content baseline, exact strict byte length, and both
            // mapped source boundaries before this host call is permitted.
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

        // Row-header and # gestures deliberately leave a real data cell current so the
        // accepted DataGridView interaction remains usable. Whole-row visual selection
        // therefore carries row-level navigation intent more reliably than the current
        // column alone.
        int? columnIndex = null;
        if (!row.Selected &&
            currentCell.ColumnIndex >= 0 &&
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

        // Virtual read-only rows deliberately avoid materialized row Tags. Their # cell
        // is generated from the already-stable CsvTableRow.SourceRecordIndex, so this
        // bounded presentation fallback recovers that source identity without treating
        // display row index as source identity.
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

    private static DataGridView? FindPrimaryTableGrid(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is DataGridView grid &&
                CsvDataGridView.IsPrimaryTableGridCandidate(grid))
            {
                return grid;
            }

            var descendant = FindPrimaryTableGrid(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static string DescribeBlockedPlan(CsvSourceNavigationStatus status) =>
        status switch
        {
            CsvSourceNavigationStatus.DocumentIdentityChanged =>
                "Go to source blocked: another Notepad++ document is active. Return to the displayed CSV or Refresh the table.",
            CsvSourceNavigationStatus.CodePageChanged =>
                "Go to source blocked: the editor code page changed after this table was rendered. Refresh the table first.",
            CsvSourceNavigationStatus.ContentChanged =>
                "Go to source blocked: the active editor content changed after this table was rendered. Refresh the table first.",
            CsvSourceNavigationStatus.UnsupportedCodePage =>
                "Go to source blocked: the current Scintilla code page has no explicit byte-position profile.",
            CsvSourceNavigationStatus.EditorByteLengthMismatch =>
                "Go to source blocked: strict encoded length does not match Scintilla's reported document length.",
            CsvSourceNavigationStatus.PositionEncodingFailed =>
                "Go to source blocked: the source character span could not be mapped losslessly to Scintilla bytes.",
            CsvSourceNavigationStatus.SourceRecordUnavailable =>
                "Go to source blocked: the source logical record is unavailable in the retained parse result.",
            CsvSourceNavigationStatus.SourceColumnUnavailable =>
                "Go to source blocked: the selected projected cell has no raw source field.",
            _ => "Go to source was not completed. No document content was changed."
        };

    private sealed record NavigationBaseline(
        ActiveDocumentSnapshot Snapshot,
        CsvParseResult ParseResult);
}
