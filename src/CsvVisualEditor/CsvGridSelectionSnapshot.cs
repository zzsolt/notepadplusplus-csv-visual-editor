namespace CsvVisualEditor;

using CsvVisualEditor.Core;

/// <summary>
/// One immutable stable complete-row target copied from the synchronized
/// selector/row-header selection. DisplayIndex is used only to choose a
/// neighboring row after a successful model mutation; Id is the only
/// persistent model identity.
/// </summary>
internal readonly record struct CsvGridSelectedRow(
    CsvEditRowId Id,
    int DisplayIndex);

/// <summary>
/// Converts explicit complete-row selection into deterministic stable IDs.
/// Ordinary data-cell focus is never a deletion target: Delete is available
/// only after a selector click or row-header gesture selected complete rows.
/// </summary>
internal static class CsvGridSelectionSnapshot
{
    public static IReadOnlyList<CsvGridSelectedRow> Capture(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var managed = CsvGridRowHeaderBehavior.CaptureManagedSelection(grid);
        if (!managed.HasExplicitRowHeaderContext)
        {
            return Array.Empty<CsvGridSelectedRow>();
        }

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
}
