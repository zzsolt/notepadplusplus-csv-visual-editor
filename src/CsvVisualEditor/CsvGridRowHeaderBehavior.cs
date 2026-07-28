namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

/// <summary>
/// Supplies one explicit complete-row selection model shared by the checkbox
/// selector, native row-header gestures, and the adjacent row-indicator column.
/// Stable row IDs are authoritative; DataGridView selection is presentation only.
/// </summary>
internal static class CsvGridRowHeaderBehavior
{
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

        CsvGridRowPresentation.ConfigureNativeRowHeaders(grid);
        if (ManagedSelections.TryGetValue(grid, out _))
        {
            return true;
        }

        var state = new CsvGridManagedRowSelection();
        ManagedSelections.Add(grid, state);

        var synchronizingColumns = false;
        void SynchronizeColumns()
        {
            if (synchronizingColumns || grid.IsDisposed || grid.Disposing)
            {
                return;
            }

            synchronizingColumns = true;
            try
            {
                SynchronizeSelectorColumn(grid);
                CsvGridRowPresentation.RefreshLayout(grid);
            }
            finally
            {
                synchronizingColumns = false;
            }
        }

        grid.ReadOnlyChanged += (_, _) => SynchronizeColumns();
        grid.DpiChangedAfterParent += (_, _) =>
            CsvGridRowPresentation.RefreshLayout(grid);
        grid.FontChanged += (_, _) =>
            CsvGridRowPresentation.RefreshLayout(grid);
        grid.ColumnRemoved += (_, _) =>
        {
            if (!CsvGridRowPresentation.HasRowIndicatorColumn(grid))
            {
                state.Clear();
            }
        };
        grid.RowsRemoved += (_, _) =>
        {
            if (grid.Rows.Count == 0)
            {
                state.Clear();
            }
        };
        grid.CellMouseDown += (_, eventArgs) =>
            OnGridMouseDown(grid, state, eventArgs);
        grid.CellPainting += (_, eventArgs) =>
            PaintSelectorCell(grid, state, eventArgs);

        return true;
    }

    internal static bool SelectWholeRow(DataGridView grid, int rowIndex)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (!IsValidDataRow(grid, rowIndex) ||
            grid.Rows[rowIndex].Tag is not CsvEditRowId rowId ||
            !ManagedSelections.TryGetValue(grid, out var state))
        {
            return false;
        }

        if (grid.IsCurrentCellInEditMode && !grid.EndEdit())
        {
            return false;
        }

        state.ReplaceWith(rowId);
        SynchronizeVisualSelection(grid, state, rowIndex);
        InvalidateSelectorColumn(grid);
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
        SynchronizeVisualSelection(grid, state, rowIndex);
        InvalidateSelectorColumn(grid);
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
            grid.Rows[rowIndex].Tag is not CsvEditRowId rowId ||
            !ManagedSelections.TryGetValue(grid, out var state))
        {
            return false;
        }

        state.ApplyRowHeaderGesture(
            rowId,
            GetVisibleStableOrder(grid),
            control,
            shift);
        SynchronizeVisualSelection(grid, state, rowIndex);
        InvalidateSelectorColumn(grid);
        return true;
    }

    internal static void ResetManagedSelectionForCurrentCell(DataGridView grid)
    {
        ClearManagedSelection(grid);
    }

    internal static void ClearManagedSelection(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (ManagedSelections.TryGetValue(grid, out var state))
        {
            state.Clear();
            grid.ClearSelection();
            InvalidateSelectorColumn(grid);
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

    internal static void SynchronizeTablePresentation(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        CsvGridRowPresentation.EnsureRowIndicatorColumn(grid);
        SynchronizeSelectorColumn(grid);
        CsvGridRowPresentation.RefreshLayout(grid);
    }

    internal static void RefreshPresentationLayout(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        CsvGridRowPresentation.RefreshLayout(grid);
    }

    internal static bool IsPresentationColumn(DataGridViewColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        return CsvGridRowPresentation.IsRowIndicatorColumn(column) ||
               string.Equals(
                   column.Name,
                   SelectorColumnName,
                   StringComparison.Ordinal);
    }

    private static void SynchronizeSelectorColumn(DataGridView grid)
    {
        if (!grid.ReadOnly &&
            CsvGridRowPresentation.HasRowIndicatorColumn(grid))
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
            var existing = grid.Columns[SelectorColumnName];
            if (existing is not null)
            {
                existing.DisplayIndex = grid.Columns.Count - 1;
            }

            return;
        }

        grid.Columns.Add(
            new DataGridViewCheckBoxColumn
            {
                Name = SelectorColumnName,
                HeaderText = "Select",
                ToolTipText = "Select complete rows for deletion",
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

    private static void OnGridMouseDown(
        DataGridView grid,
        CsvGridManagedRowSelection state,
        DataGridViewCellMouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Left ||
            !IsValidDataRow(grid, eventArgs.RowIndex))
        {
            return;
        }

        if (eventArgs.ColumnIndex == -1)
        {
            OnRowHeaderMouseDown(grid, state, eventArgs.RowIndex);
            return;
        }

        if (eventArgs.ColumnIndex < 0 ||
            eventArgs.ColumnIndex >= grid.Columns.Count)
        {
            return;
        }

        var clickedColumn = grid.Columns[eventArgs.ColumnIndex];
        if (CsvGridRowPresentation.IsRowIndicatorColumn(clickedColumn))
        {
            if (grid.Rows[eventArgs.RowIndex].Tag is CsvEditRowId)
            {
                OnRowHeaderMouseDown(grid, state, eventArgs.RowIndex);
            }
            else
            {
                SelectReadOnlyIndicatorRow(grid, eventArgs.RowIndex);
            }

            return;
        }

        if (string.Equals(
                clickedColumn.Name,
                SelectorColumnName,
                StringComparison.Ordinal))
        {
            OnSelectorMouseDown(grid, state, eventArgs.RowIndex);
            return;
        }

        // A normal data-cell click deliberately leaves complete-row selection
        // context. Delete therefore becomes unavailable until a selector or row
        // header explicitly selects one or more complete rows again.
        state.Clear();
        grid.ClearSelection();
        InvalidateSelectorColumn(grid);
    }

    private static void SelectReadOnlyIndicatorRow(
        DataGridView grid,
        int rowIndex)
    {
        ApplyReadOnlyIndicatorSelection(grid, rowIndex);
        QueueReadOnlyIndicatorSelection(grid, rowIndex);
    }

    private static void ApplyReadOnlyIndicatorSelection(
        DataGridView grid,
        int rowIndex)
    {
        var firstDataColumnIndex = FindFirstDataColumnIndex(grid);
        if (firstDataColumnIndex < 0 || !IsValidDataRow(grid, rowIndex))
        {
            return;
        }

        grid.ClearSelection();
        grid.CurrentCell = grid.Rows[rowIndex].Cells[firstDataColumnIndex];
        grid.Rows[rowIndex].Selected = true;
    }

    private static void QueueReadOnlyIndicatorSelection(
        DataGridView grid,
        int rowIndex)
    {
        if (!grid.IsHandleCreated || grid.IsDisposed || grid.Disposing)
        {
            return;
        }

        try
        {
            grid.BeginInvoke((Action)(() =>
            {
                if (!grid.IsDisposed && !grid.Disposing)
                {
                    // CellMouseDown precedes the DataGridView's own cell-focus
                    // processing. Reapply the row selection afterward so the
                    // dedicated number cell behaves like the native row header
                    // in the docked host rather than collapsing to one cell.
                    ApplyReadOnlyIndicatorSelection(grid, rowIndex);
                }
            }));
        }
        catch (InvalidOperationException)
        {
            // The handle can disappear while Notepad++ is closing the docked form.
        }
    }

    private static void OnSelectorMouseDown(
        DataGridView grid,
        CsvGridManagedRowSelection state,
        int rowIndex)
    {
        if (grid.Rows[rowIndex].Tag is not CsvEditRowId rowId)
        {
            return;
        }

        if (grid.IsCurrentCellInEditMode && !grid.EndEdit())
        {
            return;
        }

        state.Toggle(rowId);
        SynchronizeVisualSelection(grid, state, rowIndex);
        InvalidateSelectorColumn(grid);
        QueueVisualSynchronization(grid, state, rowIndex);
    }

    private static void OnRowHeaderMouseDown(
        DataGridView grid,
        CsvGridManagedRowSelection state,
        int rowIndex)
    {
        if (grid.Rows[rowIndex].Tag is not CsvEditRowId rowId)
        {
            return;
        }

        if (grid.IsCurrentCellInEditMode && !grid.EndEdit())
        {
            return;
        }

        var modifiers = Control.ModifierKeys;
        state.ApplyRowHeaderGesture(
            rowId,
            GetVisibleStableOrder(grid),
            control: (modifiers & Keys.Control) == Keys.Control,
            shift: (modifiers & Keys.Shift) == Keys.Shift);

        SynchronizeVisualSelection(grid, state, rowIndex);
        InvalidateSelectorColumn(grid);
        QueueVisualSynchronization(grid, state, rowIndex);
    }

    private static void QueueVisualSynchronization(
        DataGridView grid,
        CsvGridManagedRowSelection state,
        int preferredRowIndex)
    {
        if (!grid.IsHandleCreated || grid.IsDisposed || grid.Disposing)
        {
            return;
        }

        try
        {
            grid.BeginInvoke((Action)(() =>
            {
                if (grid.IsDisposed || grid.Disposing)
                {
                    return;
                }

                SynchronizeVisualSelection(grid, state, preferredRowIndex);
                InvalidateSelectorColumn(grid);
            }));
        }
        catch (InvalidOperationException)
        {
            // The handle can disappear while Notepad++ is closing the docked form.
        }
    }

    private static void SynchronizeVisualSelection(
        DataGridView grid,
        CsvGridManagedRowSelection state,
        int preferredRowIndex)
    {
        if (grid.Rows.Count == 0)
        {
            return;
        }

        CsvGridRowPresentation.ConfigureNativeRowHeaders(grid);
        var firstDataColumnIndex = FindFirstDataColumnIndex(grid);
        if (firstDataColumnIndex < 0)
        {
            return;
        }

        if (IsValidDataRow(grid, preferredRowIndex))
        {
            grid.CurrentCell = grid.Rows[preferredRowIndex].Cells[firstDataColumnIndex];
        }

        var selected = state.Capture(GetVisibleStableOrder(grid));
        var selectedIds = selected.SelectedIds.ToHashSet();

        grid.ClearSelection();
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (!row.IsNewRow &&
                row.Tag is CsvEditRowId rowId &&
                selectedIds.Contains(rowId))
            {
                row.Selected = true;
            }
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

    private static void InvalidateSelectorColumn(DataGridView grid)
    {
        if (!grid.Columns.Contains(SelectorColumnName))
        {
            return;
        }

        var selectorColumn = grid.Columns[SelectorColumnName];
        if (selectorColumn is not null)
        {
            grid.InvalidateColumn(selectorColumn.Index);
        }
    }

    private static int FindFirstDataColumnIndex(DataGridView grid)
    {
        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (!IsPresentationColumn(column))
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
