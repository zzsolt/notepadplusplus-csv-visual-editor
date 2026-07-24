namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

/// <summary>
/// Keeps the CSV table row header readable and owns row-header gesture
/// handling. Ctrl/Shift semantics are tracked with stable row IDs rather than
/// trusting the transient DataGridView selection representation supplied by the
/// docked Notepad++ WinForms host.
/// </summary>
internal static class CsvGridRowHeaderBehavior
{
    internal const int PreferredRowHeaderWidth = 112;

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

        ConfigureWhenTableIsVisible(grid);
        if (ManagedSelections.TryGetValue(grid, out _))
        {
            return true;
        }

        var state = new CsvGridManagedRowSelection();
        ManagedSelections.Add(grid, state);

        grid.ColumnAdded += (_, _) => ConfigureWhenTableIsVisible(grid);
        grid.RowsAdded += (_, _) => ConfigureWhenTableIsVisible(grid);
        grid.RowsRemoved += (_, _) =>
        {
            if (grid.Rows.Count == 0)
            {
                state.ResetToCurrentRowFallback();
            }
        };
        grid.CellMouseDown += (_, eventArgs) =>
            OnCellMouseDown(grid, state, eventArgs);

        return true;
    }

    internal static bool SelectWholeRow(DataGridView grid, int rowIndex)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (!IsValidDataRow(grid, rowIndex))
        {
            return false;
        }

        if (ManagedSelections.TryGetValue(grid, out var state) &&
            grid.Rows[rowIndex].Tag is CsvEditRowId rowId)
        {
            state.ApplyRowHeaderGesture(
                GetVisibleStableOrder(grid),
                rowId,
                control: false,
                shift: false);
            return ApplyManagedSelectionNow(grid, state, rowId);
        }

        if (grid.IsCurrentCellInEditMode && !grid.EndEdit())
        {
            return false;
        }

        ConfigureWhenTableIsVisible(grid);
        var row = grid.Rows[rowIndex];
        grid.CurrentCell = row.Cells[0];
        grid.ClearSelection();
        row.Selected = true;
        return true;
    }

    internal static bool ApplyRowHeaderGestureForTesting(
        DataGridView grid,
        int rowIndex,
        bool control,
        bool shift)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (!IsValidDataRow(grid, rowIndex) ||
            grid.Rows[rowIndex].Tag is not CsvEditRowId clickedId ||
            !ManagedSelections.TryGetValue(grid, out var state))
        {
            return false;
        }

        state.ApplyRowHeaderGesture(
            GetVisibleStableOrder(grid),
            clickedId,
            control,
            shift);
        return ApplyManagedSelectionNow(grid, state, clickedId);
    }

    internal static void ResetManagedSelectionForCurrentCell(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (ManagedSelections.TryGetValue(grid, out var state))
        {
            state.ResetToCurrentRowFallback();
        }
    }

    internal static CsvGridManagedSelectionSnapshot CaptureManagedSelection(
        DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        return ManagedSelections.TryGetValue(grid, out var state)
            ? state.Capture()
            : new CsvGridManagedSelectionSnapshot(
                HasExplicitRowHeaderContext: false,
                SelectedIds: Array.Empty<CsvEditRowId>());
    }

    private static void OnCellMouseDown(
        DataGridView grid,
        CsvGridManagedRowSelection state,
        DataGridViewCellMouseEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0)
        {
            return;
        }

        if (eventArgs.ColumnIndex >= 0)
        {
            state.ResetToCurrentRowFallback();
            return;
        }

        if (!IsValidDataRow(grid, eventArgs.RowIndex) ||
            grid.Rows[eventArgs.RowIndex].Tag is not CsvEditRowId clickedId)
        {
            state.ResetToCurrentRowFallback();
            return;
        }

        if (grid.IsCurrentCellInEditMode && !grid.EndEdit())
        {
            return;
        }

        var modifiers = Control.ModifierKeys;
        state.ApplyRowHeaderGesture(
            GetVisibleStableOrder(grid),
            clickedId,
            control: (modifiers & Keys.Control) != Keys.None,
            shift: (modifiers & Keys.Shift) != Keys.None);

        QueueManagedSelectionUpdate(grid, state, clickedId);
    }

    private static void QueueManagedSelectionUpdate(
        DataGridView grid,
        CsvGridManagedRowSelection state,
        CsvEditRowId clickedId)
    {
        if (grid.IsDisposed || grid.Disposing)
        {
            return;
        }

        if (!grid.IsHandleCreated)
        {
            ApplyManagedSelectionNow(grid, state, clickedId);
            return;
        }

        try
        {
            grid.BeginInvoke(
                (Action)(() => ApplyManagedSelectionNow(grid, state, clickedId)));
        }
        catch (InvalidOperationException)
        {
            // The host can destroy the docking control between mouse input and
            // the deferred callback. No model mutation has occurred.
        }
    }

    private static bool ApplyManagedSelectionNow(
        DataGridView grid,
        CsvGridManagedRowSelection state,
        CsvEditRowId clickedId)
    {
        if (grid.IsDisposed || grid.Disposing || grid.Columns.Count == 0)
        {
            return false;
        }

        var clickedRow = FindRow(grid, clickedId);
        if (clickedRow is null)
        {
            return false;
        }

        if (grid.IsCurrentCellInEditMode && !grid.EndEdit())
        {
            return false;
        }

        ConfigureWhenTableIsVisible(grid);
        grid.CurrentCell = clickedRow.Cells[0];
        grid.ClearSelection();

        var selectedIds = state.Capture().SelectedIds.ToHashSet();
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.Tag is not CsvEditRowId rowId ||
                !selectedIds.Contains(rowId))
            {
                continue;
            }

            row.Selected = true;
            foreach (DataGridViewCell cell in row.Cells)
            {
                cell.Selected = true;
            }
        }

        return true;
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

    private static DataGridViewRow? FindRow(
        DataGridView grid,
        CsvEditRowId id)
    {
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.Tag is CsvEditRowId candidate && candidate == id)
            {
                return row;
            }
        }

        return null;
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
