namespace CsvVisualEditor;

using CsvVisualEditor.Core;

/// <summary>
/// One immutable stable row target copied from the plugin-owned row-header
/// selection. DisplayIndex is used only to choose a neighboring row after a
/// successful model mutation; Id is the only persistent model identity.
/// </summary>
internal readonly record struct CsvGridSelectedRow(
    CsvEditRowId Id,
    int DisplayIndex);

/// <summary>
/// Converts the managed row-header selection into deterministic stable IDs.
/// The DataGridView SelectedRows/SelectedCells collections are deliberately not
/// authoritative because the real docked host can represent one row-header
/// gesture differently from standalone WinForms.
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
