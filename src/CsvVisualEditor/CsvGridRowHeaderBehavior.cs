namespace CsvVisualEditor;

using System.Windows.Forms;

/// <summary>
/// Keeps the CSV table row header readable and makes row-header clicks select
/// logical rows without changing normal cell-click behavior. Every row-header
/// gesture is normalized to complete DataGridView rows before deletion state is
/// captured, including hosts that transiently expose Ctrl/Shift gestures as
/// selected cells instead of SelectedRows.
/// </summary>
internal static class CsvGridRowHeaderBehavior
{
    internal const int PreferredRowHeaderWidth = 112;

    public static bool TryAttach(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var grid = FindTableGrid(root);
        if (grid is null)
        {
            return false;
        }

        ConfigureWhenTableIsVisible(grid);
        grid.ColumnAdded += (_, _) => ConfigureWhenTableIsVisible(grid);
        grid.RowsAdded += (_, _) => ConfigureWhenTableIsVisible(grid);
        grid.RowHeaderMouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button != MouseButtons.Left)
            {
                return;
            }

            var modifiers = Control.ModifierKeys & (Keys.Control | Keys.Shift);
            if (modifiers == Keys.None)
            {
                SelectWholeRow(grid, eventArgs.RowIndex);
                return;
            }

            PromoteModifiedSelectionToWholeRows(grid, eventArgs.RowIndex);
        };

        return true;
    }

    internal static bool SelectWholeRow(DataGridView grid, int rowIndex)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (!IsValidDataRow(grid, rowIndex))
        {
            return false;
        }

        if (grid.IsCurrentCellInEditMode && !grid.EndEdit())
        {
            return false;
        }

        ConfigureWhenTableIsVisible(grid);
        var row = grid.Rows[rowIndex];
        grid.ClearSelection();
        grid.CurrentCell = row.Cells[0];
        row.Selected = true;
        return true;
    }

    /// <summary>
    /// Promotes the transient selection produced by a Ctrl/Shift row-header
    /// gesture to complete rows. Some real WinForms hosts can leave only one
    /// selected cell per intended row even while the visual multi-selection is
    /// visible; SelectedRows is then empty and must not trigger current-row
    /// fallback deletion.
    /// </summary>
    internal static bool PromoteModifiedSelectionToWholeRows(
        DataGridView grid,
        int clickedRowIndex)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (!IsValidDataRow(grid, clickedRowIndex))
        {
            return false;
        }

        if (grid.IsCurrentCellInEditMode && !grid.EndEdit())
        {
            return false;
        }

        var selectedRowIndexes = new SortedSet<int>();
        foreach (DataGridViewRow row in grid.SelectedRows)
        {
            if (IsValidDataRow(grid, row.Index))
            {
                selectedRowIndexes.Add(row.Index);
            }
        }

        foreach (DataGridViewCell cell in grid.SelectedCells)
        {
            if (IsValidDataRow(grid, cell.RowIndex))
            {
                selectedRowIndexes.Add(cell.RowIndex);
            }
        }

        if (selectedRowIndexes.Count == 0)
        {
            return false;
        }

        ConfigureWhenTableIsVisible(grid);
        grid.ClearSelection();
        foreach (var rowIndex in selectedRowIndexes)
        {
            grid.Rows[rowIndex].Selected = true;
        }

        return true;
    }

    private static bool IsValidDataRow(DataGridView grid, int rowIndex)
    {
        return rowIndex >= 0 &&
               rowIndex < grid.Rows.Count &&
               grid.Columns.Count > 0 &&
               !grid.Rows[rowIndex].IsNewRow;
    }

    private static void ConfigureWhenTableIsVisible(DataGridView grid)
    {
        if (!grid.RowHeadersVisible)
        {
            return;
        }

        grid.RowHeadersWidth = PreferredRowHeaderWidth;
        grid.RowHeadersWidthSizeMode =
            DataGridViewRowHeadersWidthSizeMode.EnableResizing;
        grid.SelectionMode = DataGridViewSelectionMode.RowHeaderSelect;
        grid.MultiSelect = true;
    }

    private static DataGridView? FindTableGrid(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is DataGridView grid &&
                grid.ClipboardCopyMode ==
                    DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText)
            {
                return grid;
            }

            var nestedGrid = FindTableGrid(child);
            if (nestedGrid is not null)
            {
                return nestedGrid;
            }
        }

        return null;
    }
}
