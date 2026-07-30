namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Application-level Ctrl+C/Ctrl+V bridge for the CSV grid. The bridge resolves the
/// focused DataGridView at message time, keeps DataGridView objects presentation-only,
/// and sends paste operations exclusively to the pending CsvRowEditModel.
/// </summary>
internal sealed class CsvGridClipboardController : IMessageFilter
{
    private const int WmKeyDown = 0x0100;
    private static int _registered;

    [ModuleInitializer]
    internal static void Initialize()
    {
        if (Interlocked.Exchange(ref _registered, 1) != 0)
        {
            return;
        }

        if (Application.MessageLoop)
        {
            Application.AddMessageFilter(new CsvGridClipboardController());
            return;
        }

        EventHandler? registerOnIdle = null;
        registerOnIdle = (_, _) =>
        {
            Application.Idle -= registerOnIdle;
            Application.AddMessageFilter(new CsvGridClipboardController());
        };
        Application.Idle += registerOnIdle;
    }

    public bool PreFilterMessage(ref Message message)
    {
        if (message.Msg != WmKeyDown ||
            (Control.ModifierKeys & Keys.Control) != Keys.Control ||
            (Control.ModifierKeys & Keys.Alt) == Keys.Alt)
        {
            return false;
        }

        var key = (Keys)(int)message.WParam;
        if (key is not Keys.C and not Keys.V)
        {
            return false;
        }

        var grid = FindGrid(Control.FromHandle(message.HWnd));
        if (grid is null || grid.FindForm() is not CsvGridForm form)
        {
            return false;
        }

        // While a cell's text editor is active, preserve the standard text-level
        // copy/paste behavior. Rectangle commands operate when the grid owns focus.
        if (grid.IsCurrentCellInEditMode)
        {
            return false;
        }

        return key == Keys.C
            ? TryCopy(grid, form)
            : TryPaste(grid, form);
    }

    private static bool TryCopy(DataGridView grid, CsvGridForm form)
    {
        var rectangle = CaptureRectangle(grid, useCurrentCellWhenEmpty: true);
        if (rectangle is null)
        {
            ShowStatus(form, "Copy requires one contiguous rectangular selection of CSV data cells.");
            return true;
        }

        var builder = new StringBuilder();
        for (var rowOffset = 0; rowOffset < rectangle.Value.RowCount; rowOffset++)
        {
            if (rowOffset > 0)
            {
                builder.Append("\r\n");
            }

            for (var columnOffset = 0; columnOffset < rectangle.Value.ColumnCount; columnOffset++)
            {
                if (columnOffset > 0)
                {
                    builder.Append('\t');
                }

                var value = grid.Rows[rectangle.Value.StartRow + rowOffset]
                    .Cells[rectangle.Value.StartColumn + columnOffset].Value;
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
            }
        }

        try
        {
            Clipboard.SetText(builder.ToString(), TextDataFormat.UnicodeText);
            ShowStatus(
                form,
                $"Copied {rectangle.Value.RowCount.ToString(CultureInfo.CurrentCulture)} × " +
                $"{rectangle.Value.ColumnCount.ToString(CultureInfo.CurrentCulture)} CSV cells.");
        }
        catch (ExternalException)
        {
            ShowStatus(form, "The Windows clipboard is temporarily unavailable. No data was changed.");
        }

        return true;
    }

    private static bool TryPaste(DataGridView grid, CsvGridForm form)
    {
        if (!form.IsEditMode)
        {
            ShowStatus(form, "Paste is available only in Edit mode.");
            return true;
        }

        var model = form.RowEditModel;
        if (model is null)
        {
            ShowStatus(form, "Paste is unavailable for the current table.");
            return true;
        }

        if (CsvGridRowHeaderBehavior.CaptureManagedSelection(grid).SelectedIds.Count > 0)
        {
            ShowStatus(form, "Paste requires a CSV-cell rectangle, not complete-row deletion selection.");
            return true;
        }

        if (!form.CommitPendingEdit())
        {
            ShowStatus(form, "The active cell edit could not be committed. Correct the value before pasting.");
            return true;
        }

        string clipboardText;
        try
        {
            if (!Clipboard.ContainsText())
            {
                ShowStatus(form, "The clipboard does not contain plain text cells.");
                return true;
            }

            clipboardText = Clipboard.GetText(TextDataFormat.UnicodeText);
            if (clipboardText.Length == 0)
            {
                clipboardText = Clipboard.GetText(TextDataFormat.Text);
            }
        }
        catch (ExternalException)
        {
            ShowStatus(form, "The Windows clipboard is temporarily unavailable. No data was changed.");
            return true;
        }

        var matrix = CsvClipboardMatrix.Parse(clipboardText);
        var selectedRectangle = CaptureRectangle(grid, useCurrentCellWhenEmpty: true);
        if (selectedRectangle is null)
        {
            ShowStatus(form, "Paste requires one contiguous rectangular selection of CSV data cells.");
            return true;
        }

        var target = selectedRectangle.Value;
        if (target.CellCount == 1 && !matrix.IsSingleCell)
        {
            target = new GridRectangle(
                target.StartRow,
                target.StartColumn,
                matrix.RowCount,
                matrix.ColumnCount);
        }

        var orderedRowIds = grid.Rows
            .Cast<DataGridViewRow>()
            .Select(static row => row.Tag)
            .OfType<CsvEditRowId>()
            .ToArray();
        if (orderedRowIds.Length != grid.Rows.Count)
        {
            ShowStatus(form, "Paste targets are unavailable outside the stable Edit-mode row model.");
            return true;
        }

        var plan = CsvClipboardPastePlan.Create(
            model,
            orderedRowIds,
            target.StartRow,
            target.StartColumn,
            target.RowCount,
            target.ColumnCount,
            matrix);
        if (!plan.IsReady)
        {
            ShowStatus(form, plan.Status switch
            {
                CsvClipboardPasteStatus.ShapeMismatch =>
                    "Paste blocked: clipboard and selected rectangles have different dimensions.",
                CsvClipboardPasteStatus.TargetOutsideSession =>
                    "Paste blocked: the target rectangle extends beyond the CSV table.",
                _ => "Paste blocked: there is no editable target rectangle."
            });
            return true;
        }

        var changed = plan.Apply(model);
        SynchronizeGridValues(grid, plan.Edits);
        RestoreSelection(grid, plan.Edits.Select(static edit => edit.Address).ToArray());
        ShowStatus(
            form,
            changed == 0
                ? "Paste completed; all target values were already identical."
                : $"Pasted {changed.ToString(CultureInfo.CurrentCulture)} changed cells into the pending edit session. Apply writes them to Notepad++."
        );
        return true;
    }

    private static void SynchronizeGridValues(
        DataGridView grid,
        IReadOnlyList<CsvClipboardCellEdit> edits)
    {
        var rowsById = grid.Rows
            .Cast<DataGridViewRow>()
            .Where(static row => row.Tag is CsvEditRowId)
            .ToDictionary(static row => (CsvEditRowId)row.Tag!, static row => row);

        foreach (var edit in edits)
        {
            if (rowsById.TryGetValue(edit.Address.RowId, out var row))
            {
                row.Cells[edit.Address.ColumnIndex].Value = edit.Value;
            }
        }
    }

    private static GridRectangle? CaptureRectangle(
        DataGridView grid,
        bool useCurrentCellWhenEmpty)
    {
        var selected = grid.SelectedCells
            .Cast<DataGridViewCell>()
            .Where(cell => IsCsvDataCell(grid, cell))
            .ToArray();
        if (selected.Length == 0 &&
            useCurrentCellWhenEmpty &&
            grid.CurrentCell is DataGridViewCell current &&
            IsCsvDataCell(grid, current))
        {
            selected = [current];
        }

        if (selected.Length == 0)
        {
            return null;
        }

        var firstRow = selected.Min(static cell => cell.RowIndex);
        var lastRow = selected.Max(static cell => cell.RowIndex);
        var firstColumn = selected.Min(static cell => cell.ColumnIndex);
        var lastColumn = selected.Max(static cell => cell.ColumnIndex);
        var rectangle = new GridRectangle(
            firstRow,
            firstColumn,
            lastRow - firstRow + 1,
            lastColumn - firstColumn + 1);
        if (selected.Length != rectangle.CellCount)
        {
            return null;
        }

        for (var rowIndex = firstRow; rowIndex <= lastRow; rowIndex++)
        {
            for (var columnIndex = firstColumn; columnIndex <= lastColumn; columnIndex++)
            {
                var cell = grid.Rows[rowIndex].Cells[columnIndex];
                if (!IsCsvDataCell(grid, cell) || !cell.Selected)
                {
                    return null;
                }
            }
        }

        return rectangle;
    }

    private static bool IsCsvDataCell(DataGridView grid, DataGridViewCell cell) =>
        cell.RowIndex >= 0 &&
        cell.ColumnIndex >= 0 &&
        cell.RowIndex < grid.Rows.Count &&
        cell.ColumnIndex < grid.Columns.Count &&
        !CsvGridRowHeaderBehavior.IsPresentationColumn(grid.Columns[cell.ColumnIndex]);

    private static void RestoreSelection(
        DataGridView grid,
        IReadOnlyList<CsvCellAddress> addresses)
    {
        grid.ClearSelection();
        DataGridViewCell? first = null;
        var rowsById = grid.Rows
            .Cast<DataGridViewRow>()
            .Where(static row => row.Tag is CsvEditRowId)
            .ToDictionary(static row => (CsvEditRowId)row.Tag!, static row => row);

        foreach (var address in addresses)
        {
            if (!rowsById.TryGetValue(address.RowId, out var row) ||
                address.ColumnIndex < 0 ||
                address.ColumnIndex >= grid.Columns.Count)
            {
                continue;
            }

            var cell = row.Cells[address.ColumnIndex];
            cell.Selected = true;
            first ??= cell;
        }

        if (first is not null)
        {
            grid.CurrentCell = first;
        }
    }

    private static DataGridView? FindGrid(Control? control)
    {
        while (control is not null)
        {
            if (control is DataGridView grid)
            {
                return grid;
            }

            control = control.Parent;
        }

        return null;
    }

    private static void ShowStatus(CsvGridForm form, string message)
    {
        foreach (var control in EnumerateControls(form))
        {
            if (control is StatusStrip strip)
            {
                var label = strip.Items.OfType<ToolStripStatusLabel>().FirstOrDefault();
                if (label is not null)
                {
                    label.Text = message;
                    return;
                }
            }
        }
    }

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in EnumerateControls(child))
            {
                yield return descendant;
            }
        }
    }

    private readonly record struct GridRectangle(
        int StartRow,
        int StartColumn,
        int RowCount,
        int ColumnCount)
    {
        public int CellCount => checked(RowCount * ColumnCount);
    }
}
