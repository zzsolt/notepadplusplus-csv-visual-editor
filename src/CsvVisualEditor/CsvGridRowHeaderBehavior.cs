namespace CsvVisualEditor;

using System.Windows.Forms;

/// <summary>
/// Keeps the CSV table row header readable and makes row-header clicks select
/// logical rows without changing normal cell-click behavior. Unmodified clicks
/// are explicitly normalized; Ctrl and Shift clicks retain the built-in
/// DataGridView multi-selection semantics.
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
            }
        };

        return true;
    }

    internal static bool SelectWholeRow(DataGridView grid, int rowIndex)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (rowIndex < 0 ||
            rowIndex >= grid.Rows.Count ||
            grid.Columns.Count == 0)
        {
            return false;
        }

        if (grid.IsCurrentCellInEditMode && !grid.EndEdit())
        {
            return false;
        }

        var row = grid.Rows[rowIndex];
        grid.ClearSelection();
        grid.CurrentCell = row.Cells[0];
        row.Selected = true;
        return true;
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
