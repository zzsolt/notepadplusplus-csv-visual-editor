namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Spreadsheet clipboard service called directly by the CSV DataGridView command
/// path and by the active cell editor's WM_PASTE hook. It deliberately does not
/// depend on Application.AddMessageFilter because Notepad++ owns the native message loop.
/// </summary>
internal static class CsvGridClipboardController
{

    internal static bool TryHandleGridCommand(
        DataGridView grid,
        CsvGridForm form,
        Keys keyData)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(form);

        var key = keyData & Keys.KeyCode;
        if (key == Keys.C)
        {
            return grid.IsCurrentCellInEditMode
                ? false
                : TryCopy(grid, form);
        }

        if (key != Keys.V)
        {
            return false;
        }

        if (!TryReadClipboardText(form, out var clipboardText))
        {
            return true;
        }

        var target = CsvClipboardCommandRouting.ResolvePasteTarget(
            grid.IsCurrentCellInEditMode,
            clipboardText);
        if (target == CsvClipboardPasteRoutingTarget.InCellEditor)
        {
            return false;
        }

        return TryPasteText(grid, form, clipboardText);
    }

    internal static bool TryHandleEditingControlPaste(
        DataGridView grid,
        CsvGridForm form,
        out string clipboardText)
    {
        clipboardText = string.Empty;
        if (!TryReadClipboardText(form, out var text))
        {
            return true;
        }

        if (CsvClipboardCommandRouting.ResolvePasteTarget(
                isCellEditorActive: true,
                text) != CsvClipboardPasteRoutingTarget.Grid)
        {
            return false;
        }

        clipboardText = text;
        return true;
    }

    internal static bool TryPasteText(
        DataGridView grid,
        CsvGridForm form,
        string clipboardText)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(clipboardText);

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

        CsvClipboardMatrix matrix;
        try
        {
            matrix = CsvClipboardMatrix.Parse(clipboardText);
        }
        catch (FormatException exception)
        {
            ShowStatus(form, $"Paste blocked: {exception.Message} No data was changed.");
            return true;
        }

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

        int changed;
        try
        {
            changed = plan.Apply(model);
        }
        catch (InvalidOperationException)
        {
            ShowStatus(form, "Paste blocked because the pending edit model changed. No partial paste was retained.");
            return true;
        }

        SynchronizeGridValues(grid, plan.Edits);
        RestoreSelection(grid, plan.Edits.Select(static edit => edit.Address).ToArray());
        form.RefreshClipboardEditState();
        ShowStatus(
            form,
            changed == 0
                ? "Paste completed; all target values were already identical."
                : $"Pasted {changed.ToString(CultureInfo.CurrentCulture)} changed cells into the pending edit session. Apply writes them to Notepad++."
        );
        return true;
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

                var value = Convert.ToString(
                    grid.Rows[rectangle.Value.StartRow + rowOffset]
                        .Cells[rectangle.Value.StartColumn + columnOffset].Value,
                    CultureInfo.InvariantCulture) ?? string.Empty;
                if (ContainsClipboardDelimiter(value))
                {
                    ShowStatus(
                        form,
                        "Copy blocked: one selected cell contains a tab or line break that cannot be represented unambiguously as plain spreadsheet text.");
                    return true;
                }

                builder.Append(value);
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

    private static bool TryReadClipboardText(
        CsvGridForm form,
        out string clipboardText)
    {
        clipboardText = string.Empty;
        try
        {
            if (!Clipboard.ContainsText())
            {
                ShowStatus(form, "The clipboard does not contain plain text cells.");
                return false;
            }

            clipboardText = Clipboard.GetText(TextDataFormat.UnicodeText);
            if (clipboardText.Length == 0)
            {
                clipboardText = Clipboard.GetText(TextDataFormat.Text);
            }

            return true;
        }
        catch (ExternalException)
        {
            ShowStatus(form, "The Windows clipboard is temporarily unavailable. No data was changed.");
            return false;
        }
    }

    private static bool ContainsClipboardDelimiter(string value) =>
        value.Contains('\t') || value.Contains('\r') || value.Contains('\n');

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

    internal static void ShowStatus(CsvGridForm form, string message)
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
