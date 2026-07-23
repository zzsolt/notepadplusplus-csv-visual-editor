namespace CsvVisualEditor;

using CsvVisualEditor.Core;

/// <summary>
/// One immutable stable row target copied from the current DataGridView
/// selection. DisplayIndex is used only to choose a neighboring row after a
/// successful model mutation; Id is the only persistent model identity.
/// </summary>
internal readonly record struct CsvGridSelectedRow(
    CsvEditRowId Id,
    int DisplayIndex);

/// <summary>
/// Converts transient DataGridView selection state into a deterministic stable
/// ID snapshot before any structural mutation occurs.
/// </summary>
internal static class CsvGridSelectionSnapshot
{
    public static IReadOnlyList<CsvGridSelectedRow> Capture(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var selectedById = new Dictionary<CsvEditRowId, int>();
        foreach (DataGridViewRow row in grid.SelectedRows)
        {
            if (row.Tag is CsvEditRowId rowId)
            {
                selectedById.TryAdd(rowId, row.Index);
            }
        }

        if (selectedById.Count == 0 &&
            grid.CurrentRow is { Tag: CsvEditRowId currentRowId } currentRow)
        {
            selectedById.Add(currentRowId, currentRow.Index);
        }

        return selectedById
            .Select(static pair => new CsvGridSelectedRow(pair.Key, pair.Value))
            .OrderBy(static selected => selected.DisplayIndex)
            .ToArray();
    }
}
