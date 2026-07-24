namespace CsvVisualEditor;

using CsvVisualEditor.Core;

/// <summary>
/// One immutable stable row target copied from the explicit selector marks.
/// DisplayIndex is used only to choose a neighboring row after a successful
/// model mutation; Id is the only persistent model identity.
/// </summary>
internal readonly record struct CsvGridSelectedRow(
    CsvEditRowId Id,
    int DisplayIndex);

/// <summary>
/// Converts explicit checkbox-style row marks into deterministic stable IDs.
/// DataGridView SelectedRows and SelectedCells are deliberately irrelevant.
/// When no row is marked, the accepted single-current-row fallback is used.
/// </summary>
internal static class CsvGridSelectionSnapshot
{
    public static IReadOnlyList<CsvGridSelectedRow> Capture(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var managed = CsvGridRowHeaderBehavior.CaptureManagedSelection(grid);
        if (managed.HasExplicitRowHeaderContext)
        {
            var selectedIds = managed.SelectedIds.ToHashSet();
            var selectedRows = new List<CsvGridSelectedRow>(selectedIds.Count);
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.Tag is CsvEditRowId rowId &&
                    selectedIds.Contains(rowId))
                {
                    selectedRows.Add(new CsvGridSelectedRow(rowId, row.Index));
                }
            }

            return selectedRows;
        }

        if (grid.CurrentRow is { Tag: CsvEditRowId currentRowId } currentRow)
        {
            return [new CsvGridSelectedRow(currentRowId, currentRow.Index)];
        }

        return Array.Empty<CsvGridSelectedRow>();
    }
}
