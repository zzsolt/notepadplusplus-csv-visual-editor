namespace CsvVisualEditor;

using CsvVisualEditor.Localization;

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
                L10n.Get(TextKey.Source_GoToSourceIsUnavailableUntilTheCurrent));
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
                L10n.Get(TextKey.Source_GoToSourceFailedTheActiveNotepadBuffer));
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
                L10n.Get(TextKey.Source_GoToSourceBlockedBecauseTheRetainedParse));
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
                L10n.Get(TextKey.Source_GoToSourceFailedWhileMovingTheScintilla));
            return true;
        }

        CsvGridClipboardController.ShowStatus(
            form,
            plan.Address.ColumnIndex is int columnIndex
                ? L10n.Format(TextKey.Source_SelectedSourceRecordColumnInNotepad, (plan.Address.SourceRecordIndex + 1).ToString(CultureInfo.CurrentCulture), (columnIndex + 1).ToString(CultureInfo.CurrentCulture))
                : L10n.Format(TextKey.Source_SelectedSourceRecordInNotepad, (plan.Address.SourceRecordIndex + 1).ToString(CultureInfo.CurrentCulture)));
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
            L10n.Get(TextKey.Source_GoToSourceRequiresACurrentCSVRow);

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
                ? L10n.Get(TextKey.Source_ThisPendingInsertedRowHasNoSourceLocation)
                : L10n.Get(TextKey.Source_TheCurrentVisualRowDoesNotExposeA);
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
                L10n.Get(TextKey.Source_GoToSourceBlockedAnotherNotepadDocumentIs),
            CsvSourceNavigationStatus.CodePageChanged =>
                L10n.Get(TextKey.Source_GoToSourceBlockedTheEditorCodePage),
            CsvSourceNavigationStatus.ContentChanged =>
                L10n.Get(TextKey.Source_GoToSourceBlockedTheActiveEditorContent),
            CsvSourceNavigationStatus.UnsupportedCodePage =>
                L10n.Get(TextKey.Source_GoToSourceBlockedTheCurrentScintillaCode),
            CsvSourceNavigationStatus.EditorByteLengthMismatch =>
                L10n.Get(TextKey.Source_GoToSourceBlockedStrictEncodedLengthDoes),
            CsvSourceNavigationStatus.PositionEncodingFailed =>
                L10n.Get(TextKey.Source_GoToSourceBlockedTheSourceCharacterSpan),
            CsvSourceNavigationStatus.SourceRecordUnavailable =>
                L10n.Get(TextKey.Source_GoToSourceBlockedTheSourceLogicalRecord),
            CsvSourceNavigationStatus.SourceColumnUnavailable =>
                L10n.Get(TextKey.Source_GoToSourceBlockedTheSelectedProjectedCell),
            _ => L10n.Get(TextKey.Source_GoToSourceWasNotCompletedNoDocument)
        };

    private sealed record NavigationBaseline(
        ActiveDocumentSnapshot Snapshot,
        CsvParseResult ParseResult);
}
