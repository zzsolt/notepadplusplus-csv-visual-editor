namespace CsvVisualEditor;

using System.ComponentModel;
using System.Drawing.Drawing2D;
using CsvVisualEditor.Core;

/// <summary>
/// DataGridView clipboard-command seam that works inside the native Notepad++ host.
/// Notepad++ can translate Ctrl+C/Ctrl+X/Ctrl+V into WM_COPY/WM_CUT/WM_PASTE before
/// WinForms command-key preprocessing runs, so both managed command keys and the
/// native clipboard messages are handled directly by the focused grid window.
/// </summary>
internal sealed class CsvDataGridView : DataGridView
{
    internal CsvDataGridView()
    {
        DoubleBuffered = true;
        // Inherit the UI font. A forced value font also changed header metrics and
        // made small tables look unrelated to the surrounding Notepad++ controls.
    }

    private const int WmKeyDown = 0x0100;
    private const int WmCut = 0x0300;
    private const int WmCopy = 0x0301;
    private const int WmPaste = 0x0302;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool ShowWhitespace { get; set; } = true;

    private CsvCellSearchIndex? _searchResults;

    internal void SetSearchResults(CsvCellSearchIndex? results)
    {
        if (ReferenceEquals(_searchResults, results)) return;
        _searchResults = results;
        Invalidate();
    }

    internal bool IsSearchMatch(int row, int column) =>
        row >= 0 && column >= 0 && _searchResults?.FindIndex(row, column) >= 0;

    // A view index cannot survive in-place mutations, row removal or native sorting.
    protected override void OnCellValueChanged(DataGridViewCellEventArgs e)
    {
        SetSearchResults(null);
        base.OnCellValueChanged(e);
    }

    protected override void OnRowsRemoved(DataGridViewRowsRemovedEventArgs e)
    {
        SetSearchResults(null);
        base.OnRowsRemoved(e);
    }

    protected override void OnRowsAdded(DataGridViewRowsAddedEventArgs e)
    {
        SetSearchResults(null);
        base.OnRowsAdded(e);
    }

    protected override void OnSorted(EventArgs e)
    {
        SetSearchResults(null);
        base.OnSorted(e);
    }

    protected override void OnCellMouseEnter(DataGridViewCellEventArgs e)
    {
        // Unbound non-virtual grids do not request CellToolTipTextNeeded.
        if (!VirtualMode && e.RowIndex >= 0 && e.ColumnIndex >= 0 &&
            Columns[e.ColumnIndex].Name.StartsWith("CsvColumn", StringComparison.Ordinal) &&
            Rows[e.RowIndex].Cells[e.ColumnIndex].Value is string value)
            Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = CsvUiText.DescribeSpaces(value);
        base.OnCellMouseEnter(e);
    }

    protected override void OnCellToolTipTextNeeded(DataGridViewCellToolTipTextNeededEventArgs e)
    {
        base.OnCellToolTipTextNeeded(e);
        if (e.RowIndex >= 0 && e.ColumnIndex >= 0 &&
            Columns[e.ColumnIndex].Name.StartsWith("CsvColumn", StringComparison.Ordinal) &&
            Rows[e.RowIndex].Cells[e.ColumnIndex].Value is string value)
            e.ToolTipText = CsvUiText.DescribeSpaces(value);
    }

    protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
    {
        base.OnCellPainting(e);
        if (e.Handled || e.RowIndex < 0 || e.ColumnIndex < 0 ||
            !Columns[e.ColumnIndex].Name.StartsWith("CsvColumn", StringComparison.Ordinal) ||
            IsCurrentCellInEditMode && CurrentCellAddress == new Point(e.ColumnIndex, e.RowIndex)) return;
        var match = IsSearchMatch(e.RowIndex, e.ColumnIndex);
        CsvWhitespaceCellPainter.Paint(this, e, showSpaces: ShowWhitespace, searchMatch: match);
        if (!match || (e.PaintParts & DataGridViewPaintParts.ContentForeground) == 0 ||
            e.Graphics is not { } graphics) return;
        var state = graphics.Save();
        try
        {
            // Intersect, never replace the caller's clip (partial scrolling/repaints).
            graphics.SetClip(Rectangle.Intersect(e.ClipBounds, e.CellBounds), CombineMode.Intersect);
            var active = CurrentCellAddress == new Point(e.ColumnIndex, e.RowIndex);
            var selected = (e.State & DataGridViewElementStates.Selected) != 0;
            var dark = (selected ? e.CellStyle?.SelectionBackColor : e.CellStyle?.BackColor)?.GetBrightness() < 0.5f;
            var color = active ? (dark || selected ? Color.Gold : Color.FromArgb(0, 120, 212)) :
                (dark ? Color.Gold : Color.DarkGoldenrod);
            using var pen = new Pen(color, Math.Max(1, (active ? 2f : 1f) * DeviceDpi / 96f));
            var inset = Math.Max(3, (int)Math.Round(3 * DeviceDpi / 96f));
            var bounds = Rectangle.Inflate(e.CellBounds, -inset, -inset);
            if (bounds.Width > 0 && bounds.Height > 0) graphics.DrawRectangle(pen, bounds);
        }
        finally { graphics.Restore(state); }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Func<Keys, bool>? SearchCommandHandler { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Func<Keys, bool>? ClipboardCommandHandler { get; set; }

    /// <summary>
    /// Identifies the primary CSV table independently from its current selection mode.
    /// The accepted row-header behavior deliberately uses RowHeaderSelect, so UI
    /// discovery must never require CellSelect or mutate selection semantics merely to
    /// locate the table. The diagnostics grid has row headers disabled and is excluded.
    /// </summary>
    internal static bool IsPrimaryTableGridCandidate(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return grid is CsvDataGridView && grid.RowHeadersVisible;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (SearchCommandHandler?.Invoke(keyData) == true) return true;
        if (IsClipboardCommand(keyData) &&
            ClipboardCommandHandler?.Invoke(keyData) == true)
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmKeyDown &&
            SearchCommandHandler?.Invoke((Keys)message.WParam.ToInt32() | ModifierKeys) == true) return;
        if (TryResolveNativeClipboardCommand(
                message.Msg,
                message.WParam,
                ModifierKeys,
                out var keyData) &&
            ClipboardCommandHandler?.Invoke(keyData) == true)
        {
            return;
        }

        base.WndProc(ref message);
    }

    internal static bool TryResolveNativeClipboardCommand(
        int messageId,
        IntPtr wParam,
        Keys modifierKeys,
        out Keys keyData)
    {
        keyData = Keys.None;
        switch (messageId)
        {
            case WmCopy:
                keyData = Keys.Control | Keys.C;
                return true;
            case WmCut:
                keyData = Keys.Control | Keys.X;
                return true;
            case WmPaste:
                keyData = Keys.Control | Keys.V;
                return true;
            case WmKeyDown:
                return TryResolveCommandKey(
                    (Keys)wParam.ToInt32(),
                    modifierKeys,
                    out keyData);
            default:
                return false;
        }
    }

    private static bool IsClipboardCommand(Keys keyData)
    {
        var modifiers = keyData & Keys.Modifiers;
        return TryResolveCommandKey(
            keyData & Keys.KeyCode,
            modifiers,
            out _);
    }

    private static bool TryResolveCommandKey(
        Keys keyCode,
        Keys modifierKeys,
        out Keys keyData)
    {
        keyData = Keys.None;
        var modifiers = modifierKeys & Keys.Modifiers;
        if ((modifiers & Keys.Control) != Keys.Control ||
            (modifiers & Keys.Alt) == Keys.Alt ||
            keyCode is not (Keys.C or Keys.X or Keys.V))
        {
            return false;
        }

        keyData = modifiers | keyCode;
        return true;
    }
}
