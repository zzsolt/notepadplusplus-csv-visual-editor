namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

/// <summary>
/// Keeps row headers readable and supplies an explicit checkbox-style selector
/// column while Edit mode is active. Batch deletion targets come from stable
/// row IDs marked in this column, never from DataGridView selection semantics.
/// </summary>
internal static class CsvGridRowHeaderBehavior
{
    internal const int PreferredRowHeaderWidth = 112;
    internal const string SelectorColumnName = "CsvRowSelector";

    private const int SelectorColumnWidth = 58;

    private static readonly ConditionalWeakTable<
        DataGridView,
        CsvGridManagedRowSelection> ManagedSelections = new();

    public static bool TryAttach(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var grid = FindTableGrid(root);
        if (grid is null)
        {
            return false;
        }

        ConfigureRowHeaders(grid);
        if (ManagedSelections.TryGetValue(grid, out _))
        {
            SynchronizeSelectorColumn(grid);
            return true;
        }

        var state = new CsvGridManagedRowSelection();
        ManagedSelections.Add(grid, state);

        var synchronizingColumn = false;
        void SynchronizeColumn()
        {
            if (synchronizingColumn || grid.IsDisposed || grid.Disposing)
            {
                return;
            }

            synchronizingColumn = true;
            try
            {
                SynchronizeSelectorColumn(grid);
            }
            finally
            {
                synchronizingColumn = false;
            }
        }

        grid.ReadOnlyChanged += (_, _) => SynchronizeColumn();
        grid.ColumnAdded += (_, _) =>
        {
            ConfigureRowHeaders(grid);
            SynchronizeColumn();
        };
        grid.ColumnRemoved += (_, _) => SynchronizeColumn();
        grid.RowsRemoved += (_, _) =>
        {
            if (grid.Rows.Count == 0)
            {
                state.Clear();
            }
        };
        grid.CellMouseDown += (_, eventArgs) =>
            OnSelectorMouseDown(grid, state, eventArgs);
        grid.CellPainting += (_, eventArgs) =>
            PaintSelectorCell(grid, state, eventArgs);
        grid.RowHeaderMouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == MouseButtons.Left &&
                (Control.ModifierKeys & (Keys.Control | Keys.Shift)) == Keys.None)
            {
                SelectWholeRow(grid, eventArgs.RowIndex);
            }
        };

        SynchronizeColumn();
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

        ConfigureRowHeaders(grid);
        var row = grid.Rows[rowIndex];
        var firstDataColumnIndex = FindFirstDataColumnIndex(grid);
        if (firstDataColumnIndex < 0)
        {
            return false;
        }

        grid.CurrentCell = row.Cells[firstDataColumnIndex];
        grid.ClearSelection();
        row.Selected = true;
        return true;
    }

    internal static bool ToggleSelectorForTesting(
        DataGridView grid,
        int rowIndex)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (!IsValidDataRow(grid, rowIndex) ||
            grid.Rows[rowIndex].Tag is not CsvEditRowId rowId ||
            !ManagedSelections.TryGetValue(grid, out var state))
        {
            return false;
        }

        state.Toggle(rowId);
        InvalidateSelectorCell(grid, rowIndex);
        return true;
    }

    internal static void ResetManagedSelectionForCurrentCell(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (ManagedSelections.TryGetValue(grid, out var state))
        {
            state.Clear();
            grid.Invalidate();
        }
    }

    internal static CsvGridManagedSelectionSnapshot CaptureManagedSelection(
        DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (!ManagedSelections.TryGetValue(grid, out var state))
        {
            return new CsvGridManagedSelectionSnapshot(
                HasExplicitRowHeaderContext: false,
                SelectedIds: Array.Empty<CsvEditRowId>());
        }

        return state.Capture(GetVisibleStableOrder(grid));
    }

    internal static bool HasSelectorColumn(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return grid.Columns.Contains(SelectorColumnName);
    }

    private static void SynchronizeSelectorColumn(DataGridView grid)
    {
        if (!grid.ReadOnly && grid.Columns.Count > 0)
        {
            EnsureSelectorColumn(grid);
        }
        else
        {
            RemoveSelectorColumn(grid);
        }
    }

    private static void EnsureSelectorColumn(DataGridView grid)
    {
        if (grid.Columns.Contains(SelectorColumnName))
        {
            return;
        }

        grid.Columns.Add(
            new DataGridViewCheckBoxColumn
            {
                Name = SelectorColumnName,
                HeaderText = "Select",
                ToolTipText = "Mark rows to delete together",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = SelectorColumnWidth,
                MinimumWidth = SelectorColumnWidth,
                ReadOnly = true,
                Resizable = DataGridViewTriState.False,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                ThreeState = false
            });
    }

    private static void RemoveSelectorColumn(DataGridView grid)
    {
        if (grid.Columns.Contains(SelectorColumnName))
        {
            grid.Columns.Remove(SelectorColumnName);
        }

        if (ManagedSelections.TryGetValue(grid, out var state))
        {
            state.Clear();
        }
    }

    private static void OnSelectorMouseDown(
        DataGridView grid,
        CsvGridManagedRowSelection state,
        DataGridViewCellMouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Left ||
            eventArgs.RowIndex < 0 ||
            eventArgs.ColumnIndex < 0 ||
            eventArgs.ColumnIndex >= grid.Columns.Count ||
            !string.Equals(
                grid.Columns[eventArgs.ColumnIndex].Name,
                SelectorColumnName,
                StringComparison.Ordinal) ||
            !IsValidDataRow(grid, eventArgs.RowIndex) ||
            grid.Rows[eventArgs.RowIndex].Tag is not CsvEditRowId rowId)
        {
            return;
        }

        if (grid.IsCurrentCellInEditMode && !grid.EndEdit())
        {
            return;
        }

        state.Toggle(rowId);
        InvalidateSelectorCell(grid, eventArgs.RowIndex);

        var firstDataColumnIndex = FindFirstDataColumnIndex(grid);
        if (firstDataColumnIndex >= 0)
        {
            grid.ClearSelection();
            grid.CurrentCell = grid.Rows[eventArgs.RowIndex].Cells[firstDataColumnIndex];
            grid.CurrentCell.Selected = true;
        }
    }

    private static void PaintSelectorCell(
        DataGridView grid,
        CsvGridManagedRowSelection state,
        DataGridViewCellPaintingEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0 ||
            eventArgs.ColumnIndex < 0 ||
            eventArgs.ColumnIndex >= grid.Columns.Count ||
            !string.Equals(
                grid.Columns[eventArgs.ColumnIndex].Name,
                SelectorColumnName,
                StringComparison.Ordinal))
        {
            return;
        }

        var graphics = eventArgs.Graphics;
        if (graphics is null)
        {
            return;
        }

        eventArgs.Paint(
            eventArgs.CellBounds,
            DataGridViewPaintParts.Background |
            DataGridViewPaintParts.Border |
            DataGridViewPaintParts.SelectionBackground);

        var isChecked = grid.Rows[eventArgs.RowIndex].Tag is CsvEditRowId rowId &&
                        state.IsSelected(rowId);
        const int glyphSize = 14;
        var glyphBounds = new Rectangle(
            eventArgs.CellBounds.Left + (eventArgs.CellBounds.Width - glyphSize) / 2,
            eventArgs.CellBounds.Top + (eventArgs.CellBounds.Height - glyphSize) / 2,
            glyphSize,
            glyphSize);
        ControlPaint.DrawCheckBox(
            graphics,
            glyphBounds,
            isChecked ? ButtonState.Checked : ButtonState.Normal);
        eventArgs.Handled = true;
    }

    private static void InvalidateSelectorCell(DataGridView grid, int rowIndex)
    {
        if (!grid.Columns.Contains(SelectorColumnName))
        {
            return;
        }

        var selectorColumn = grid.Columns[SelectorColumnName];
        if (selectorColumn is not null)
        {
            grid.InvalidateCell(selectorColumn.Index, rowIndex);
        }
    }

    private static int FindFirstDataColumnIndex(DataGridView grid)
    {
        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (!string.Equals(
                    column.Name,
                    SelectorColumnName,
                    StringComparison.Ordinal))
            {
                return column.Index;
            }
        }

        return -1;
    }

    private static IReadOnlyList<CsvEditRowId> GetVisibleStableOrder(
        DataGridView grid)
    {
        var result = new List<CsvEditRowId>(grid.Rows.Count);
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (!row.IsNewRow && row.Tag is CsvEditRowId rowId)
            {
                result.Add(rowId);
            }
        }

        return result;
    }

    private static bool IsValidDataRow(DataGridView grid, int rowIndex)
    {
        return rowIndex >= 0 &&
               rowIndex < grid.Rows.Count &&
               grid.Columns.Count > 0 &&
               !grid.Rows[rowIndex].IsNewRow;
    }

    private static void ConfigureRowHeaders(DataGridView grid)
    {
        if (!grid.RowHeadersVisible)
        {
            return;
        }

        grid.RowHeadersWidth = PreferredRowHeaderWidth;
        grid.RowHeadersWidthSizeMode =
            DataGridViewRowHeadersWidthSizeMode.EnableResizing;
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
