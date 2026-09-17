namespace CsvVisualEditor;

using CsvVisualEditor.Localization;

/// <summary>
/// Cell-level commands over the existing selection and command authorities. Native
/// WM_CONTEXTMENU routing also works in Notepad++'s modeless message loop.
/// </summary>
internal sealed class CsvGridContextMenu : NativeWindow, IDisposable
{
    private const int WmContextMenu = 0x007B;
    private readonly DataGridView _grid;
    private readonly Func<bool> _prepare;
    private readonly Func<bool> _isEditing;
    private readonly Func<object?> _context;
    private readonly Func<string, ToolStripButton?> _command;
    private long _revision;
    private bool _disposed;

    internal ContextMenuStrip Menu { get; } = new()
    {
        Name = "CsvCellContextMenu", ShowItemToolTips = true
    };

    internal CsvGridContextMenu(DataGridView grid, Func<bool> prepare,
        Func<bool> isEditing, Func<object?> context, Func<string, ToolStripButton?> command)
    {
        _grid = grid;
        _prepare = prepare;
        _isEditing = isEditing;
        _context = context;
        _command = command;
        grid.HandleCreated += HandleCreated;
        grid.HandleDestroyed += HandleDestroyed;
        grid.Disposed += GridDisposed;
        grid.SelectionChanged += Changed;
        grid.CurrentCellChanged += Changed;
        grid.ReadOnlyChanged += Changed;
        grid.Sorted += Changed;
        grid.CellValueChanged += CellChanged;
        grid.RowsAdded += RowsAdded;
        grid.RowsRemoved += RowsRemoved;
        grid.ColumnAdded += ColumnChanged;
        grid.ColumnRemoved += ColumnChanged;
        if (grid.IsHandleCreated) AssignHandle(grid.Handle);
    }

    private void HandleCreated(object? sender, EventArgs e) => AssignHandle(_grid.Handle);
    private void HandleDestroyed(object? sender, EventArgs e)
    {
        InvalidateContext();
        if (Handle != IntPtr.Zero) ReleaseHandle();
    }
    private void GridDisposed(object? sender, EventArgs e) => Dispose();
    private void Changed(object? sender, EventArgs e) => InvalidateContext();
    private void CellChanged(object? sender, DataGridViewCellEventArgs e) => InvalidateContext();
    private void RowsAdded(object? sender, DataGridViewRowsAddedEventArgs e) => InvalidateContext();
    private void RowsRemoved(object? sender, DataGridViewRowsRemovedEventArgs e) => InvalidateContext();
    private void ColumnChanged(object? sender, DataGridViewColumnEventArgs e) => InvalidateContext();

    internal void InvalidateContext()
    {
        _revision++;
        if (!_disposed && Menu.Visible) Menu.Close();
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmContextMenu && message.WParam == _grid.Handle)
        {
            var packed = message.LParam.ToInt64();
            // GET_X/Y_LPARAM must sign-extend coordinates on monitors left/above primary.
            ShowAt(new Point(unchecked((short)(packed & 0xffff)), unchecked((short)((packed >> 16) & 0xffff))));
            return;
        }
        base.WndProc(ref message);
    }

    internal bool ShowAt(Point screenPoint)
    {
        if (_disposed || !_grid.IsHandleCreated || !_grid.Visible) return false;
        int row, column;
        if (screenPoint == new Point(-1, -1))
        {
            row = _grid.CurrentCell?.RowIndex ?? -1;
            column = _grid.CurrentCell?.ColumnIndex ?? -1;
            if (!IsDataCell(row, column)) return false;
            var bounds = _grid.GetCellDisplayRectangle(column, row, true);
            if (bounds.IsEmpty) return false;
            screenPoint = _grid.PointToScreen(new Point(bounds.Left + 8, bounds.Bottom));
        }
        else
        {
            var point = _grid.PointToClient(screenPoint);
            var hit = _grid.HitTest(point.X, point.Y);
            row = hit.RowIndex;
            column = hit.ColumnIndex;
            if (hit.Type != DataGridViewHitTestType.Cell) return false;
        }
        if (!PrepareCell(row, column)) return false;
        Menu.Show(screenPoint);
        return true;
    }

    private bool IsDataCell(int row, int column) => !_grid.IsDisposed && _grid.Enabled &&
        row >= 0 && row < _grid.RowCount && column >= 0 && column < _grid.ColumnCount &&
        !_grid.Rows[row].IsNewRow && _grid.Columns[column].Name.StartsWith("CsvColumn", StringComparison.Ordinal) &&
        !CsvGridRowHeaderBehavior.IsPresentationColumn(_grid.Columns[column]);

    // Shared by native routing and regression tests. Opening a menu never executes
    // a mutating command; committing an in-progress editor only stages its value.
    internal bool PrepareCell(int row, int column)
    {
        if (_disposed || !IsDataCell(row, column)) return false;
        var target = _grid.Rows[row].Cells[column];
        var identity = _context();
        if (identity is null || !_prepare() || !ReferenceEquals(identity, _context()) ||
            !IsDataCell(row, column) || target.DataGridView != _grid ||
            !ReferenceEquals(target, _grid.Rows[row].Cells[column])) return false;

        var preserve = target.Selected;
        var selected = preserve ? _grid.SelectedCells.Cast<DataGridViewCell>().ToArray() : [];
        if (!preserve) CsvGridRowHeaderBehavior.ClearManagedSelection(_grid);
        _grid.CurrentCell = target;
        _grid.ClearSelection();
        if (preserve)
            foreach (var cell in selected) cell.Selected = true;
        else target.Selected = true;
        _grid.Focus();

        var revision = _revision;
        var editing = _isEditing();
        var rowTag = target.OwningRow?.Tag;
        bool IsCurrent() => !_disposed && !_grid.IsDisposed && _grid.Enabled &&
            _revision == revision && _isEditing() == editing && ReferenceEquals(identity, _context()) &&
            target.DataGridView == _grid && ReferenceEquals(_grid.CurrentCell, target) &&
            Equals(target.OwningRow?.Tag, rowTag);
        Populate(editing, IsCurrent);
        return true;
    }

    private void Populate(bool editing, Func<bool> isCurrent)
    {
        ClearItems();
        Menu.Text = L10n.Get(TextKey.Common_Table);
        Menu.AccessibleName = Menu.Text;
        Menu.RightToLeft = L10n.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No;
        var rectangle = HasCellRectangle();
        Add("CsvCellDetailsButton", isCurrent, shortcut: "Alt+Enter");
        Add("CsvGoToSourceButton", isCurrent);
        Separator();
        Add("CsvClipboardCopyButton", isCurrent, rectangle, "Ctrl+C");
        if (editing)
        {
            Add("CsvClipboardCutButton", isCurrent, rectangle, "Ctrl+X");
            Add("CsvClipboardPasteButton", isCurrent, rectangle, "Ctrl+V");
            Add("CsvTransformButton", isCurrent);
            Separator();
            var select = new ToolStripMenuItem(L10n.Get(TextKey.Rows_SelectCompleteRowsForDeletion))
            {
                Name = "CsvContextSelectRow", BackColor = Menu.BackColor, ForeColor = Menu.ForeColor
            };
            select.Click += (_, _) =>
            {
                if (isCurrent()) CsvGridRowHeaderBehavior.SelectWholeRow(_grid, _grid.CurrentCell!.RowIndex);
            };
            Menu.Items.Add(select);
            Add("CsvAddRowButton", isCurrent);
            Add("CsvDeleteRowButton", isCurrent);
            Separator();
            Add("CsvApplyButton", isCurrent);
            Add("CsvRevertAllButton", isCurrent);
        }
        else
        {
            Separator();
            Add("CsvColumnSummaryButton", isCurrent);
            Add("CsvDataViewButton", isCurrent);
            Add("CsvResetViewButton", isCurrent);
            Add("CsvRefreshButton", isCurrent);
        }
        Separator();
        Add("CsvShowSpacesButton", isCurrent);
    }

    private bool HasCellRectangle()
    {
        if (CsvGridRowHeaderBehavior.CaptureManagedSelection(_grid).SelectedIds.Count > 0) return false;
        var cells = _grid.SelectedCells.Cast<DataGridViewCell>().ToArray();
        if (cells.Length == 0 || cells.Any(cell => !IsDataCell(cell.RowIndex, cell.ColumnIndex))) return false;
        return (long)(cells.Max(cell => cell.RowIndex) - cells.Min(cell => cell.RowIndex) + 1) *
            (cells.Max(cell => cell.ColumnIndex) - cells.Min(cell => cell.ColumnIndex) + 1) == cells.Length;
    }

    private void Add(string name, Func<bool> isCurrent, bool allowed = true, string shortcut = "")
    {
        var source = _command(name);
        if (source is null || source.IsDisposed) return;
        var item = new ToolStripMenuItem(source.Text)
        {
            Name = name, Enabled = source.Enabled && allowed, Checked = source.Checked,
            ToolTipText = source.ToolTipText, ShortcutKeyDisplayString = shortcut,
            BackColor = Menu.BackColor, ForeColor = Menu.ForeColor,
            Image = source.Image is null ? null : (Image)source.Image.Clone()
        };
        item.Click += (_, _) =>
        {
            // Recheck the real command and context, not just the popup's old flag.
            if (allowed && isCurrent() && !source.IsDisposed && source.Enabled &&
                ReferenceEquals(source, _command(name))) source.PerformClick();
        };
        Menu.Items.Add(item);
    }

    private void Separator() => Menu.Items.Add(new ToolStripSeparator());

    internal void ApplyAppearance(Color background, Color foreground, ToolStripRenderer renderer)
    {
        InvalidateContext();
        Menu.BackColor = background;
        Menu.ForeColor = foreground;
        Menu.Renderer = renderer;
    }

    private void ClearItems()
    {
        foreach (var item in Menu.Items.Cast<ToolStripItem>().ToArray())
        {
            var image = item.Image;
            item.Image = null;
            image?.Dispose();
            item.Dispose();
        }
        Menu.Items.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _grid.HandleCreated -= HandleCreated;
        _grid.HandleDestroyed -= HandleDestroyed;
        _grid.Disposed -= GridDisposed;
        _grid.SelectionChanged -= Changed;
        _grid.CurrentCellChanged -= Changed;
        _grid.ReadOnlyChanged -= Changed;
        _grid.Sorted -= Changed;
        _grid.CellValueChanged -= CellChanged;
        _grid.RowsAdded -= RowsAdded;
        _grid.RowsRemoved -= RowsRemoved;
        _grid.ColumnAdded -= ColumnChanged;
        _grid.ColumnRemoved -= ColumnChanged;
        if (Handle != IntPtr.Zero) ReleaseHandle();
        ClearItems();
        Menu.Dispose();
        GC.SuppressFinalize(this);
    }
}
