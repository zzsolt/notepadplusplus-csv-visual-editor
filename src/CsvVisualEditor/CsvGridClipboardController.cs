namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

internal sealed class CsvGridClipboardController
{
    private readonly DataGridView _grid;
    private readonly Func<bool> _isEditMode;
    private readonly Func<CsvRowEditModel?> _getModel;
    private readonly Func<bool> _commitPendingEdit;
    private readonly Action _refreshView;
    private readonly Action<string> _showStatus;

    public CsvGridClipboardController(
        DataGridView grid,
        Func<bool> isEditMode,
        Func<CsvRowEditModel?> getModel,
        Func<bool> commitPendingEdit,
        Action refreshView,
        Action<string> showStatus)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        _isEditMode = isEditMode ?? throw new ArgumentNullException(nameof(isEditMode));
        _getModel = getModel ?? throw new ArgumentNullException(nameof(getModel));
        _commitPendingEdit = commitPendingEdit ?? throw new ArgumentNullException(nameof(commitPendingEdit));
        _refreshView = refreshView ?? throw new ArgumentNullException(nameof(refreshView));
        _showStatus = showStatus ?? throw new ArgumentNullException(nameof(showStatus));
    }

    public void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Control || e.Alt)
        {
            return;
        }

        if (e.KeyCode == Keys.C && TryCopy())
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.V && TryPaste())
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    public bool TryCopy()
    {
        var rectangle = CaptureRectangle(useCurrentCellWhenEmpty: true);
        if (rectangle is null)
        {
            _showStatus("Copy requires one contiguous rectangular selection of CSV data cells.");
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

                var value = _grid.Rows[rectangle.Value.StartRow + rowOffset]
                    .Cells[rectangle.Value.StartColumn + columnOffset].Value;
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
            }
        }

        try
        {
            Clipboard.SetText(builder.ToString(), TextDataFormat.UnicodeText);
            _showStatus($"Copied {rectangle.Value.RowCount} × {rectangle.Value.ColumnCount} CSV cells.");
        }
        catch (ExternalException)
        {
            _showStatus("The Windows clipboard is temporarily unavailable. No data was changed.");
        }

        return true;
    }

    public bool TryPaste()
    {
        if (!_isEditMode())
        {
            _showStatus("Paste is available only in Edit mode.");
            return true;
        }

        var model = _getModel();
        if (model is null)
        {
            _showStatus("Paste is unavailable for the current table.");
            return true;
        }

        if (!_commitPendingEdit())
        {
            _showStatus("The active cell edit could not be committed. Correct the value before pasting.");
            return true;
        }

        string clipboardText;
        try
        {
            if (!Clipboard.ContainsText())
            {
                _showStatus("The clipboard does not contain plain text cells.");
                return true;
            }

            clipboardText = Clipboard.GetText(TextDataFormat.UnicodeText);
        }
        catch (ExternalException)
        {
            _showStatus("The Windows clipboard is temporarily unavailable. No data was changed.");
            return true;
        }

        var matrix = CsvClipboardMatrix.Parse(clipboardText);
        var selectedRectangle = CaptureRectangle(useCurrentCellWhenEmpty: true);
        if (selectedRectangle is null)
        {
            _showStatus("Paste requires one contiguous rectangular selection of CSV data cells.");
            return true;
        }

        var target = selectedRectangle.Value;
        if (target.CellCount == 1 && !matrix.IsSingleCell)
        {
            target = new GridRectangle(target.StartRow, target.StartColumn, matrix.RowCount, matrix.ColumnCount);
        }

        var orderedRowIds = _grid.Rows.Cast<DataGridViewRow>()
            .Select(static row => row.Tag)
            .OfType<CsvEditRowId>()
            .ToArray();
        if (orderedRowIds.Length != _grid.Rows.Count)
        {
            _showStatus("Paste targets are unavailable outside the stable Edit-mode row model.");
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
            _showStatus(plan.Status switch
            {
                CsvClipboardPasteStatus.ShapeMismatch => "Paste blocked: clipboard and selected rectangles have different dimensions.",
                CsvClipboardPasteStatus.TargetOutsideSession => "Paste blocked: the target rectangle extends beyond the CSV table.",
                _ => "Paste blocked: there is no editable target rectangle."
            });
            return true;
        }

        var changed = plan.Apply(model);
        var addresses = plan.Edits.Select(static edit => edit.Address).ToArray();
        _refreshView();
        RestoreSelection(addresses);
        _showStatus(changed == 0
            ? "Paste completed; target values were already identical."
            : $"Pasted {changed} changed cells into the pending edit session.");
        return true;
    }

    private GridRectangle? CaptureRectangle(bool useCurrentCellWhenEmpty)
    {
        var selected = _grid.SelectedCells.Cast<DataGridViewCell>()
            .Where(IsCsvDataCell)
            .ToArray();
        if (selected.Length == 0 && useCurrentCellWhenEmpty &&
            _grid.CurrentCell is DataGridViewCell current && IsCsvDataCell(current))
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
        var rectangle = new GridRectangle(firstRow, firstColumn, lastRow - firstRow + 1, lastColumn - firstColumn + 1);
        if (selected.Length != rectangle.CellCount)
        {
            return null;
        }

        for (var rowIndex = firstRow; rowIndex <= lastRow; rowIndex++)
        {
            for (var columnIndex = firstColumn; columnIndex <= lastColumn; columnIndex++)
            {
                var cell = _grid.Rows[rowIndex].Cells[columnIndex];
                if (!IsCsvDataCell(cell) || !cell.Selected)
                {
                    return null;
                }
            }
        }

        return rectangle;
    }

    private bool IsCsvDataCell(DataGridViewCell cell) =>
        cell.RowIndex >= 0 && cell.ColumnIndex >= 0 &&
        !CsvGridRowHeaderBehavior.IsPresentationColumn(_grid.Columns[cell.ColumnIndex]);

    private void RestoreSelection(IReadOnlyList<CsvCellAddress> addresses)
    {
        _grid.ClearSelection();
        DataGridViewCell? first = null;
        foreach (var address in addresses)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Tag is CsvEditRowId rowId && rowId == address.RowId)
                {
                    var cell = row.Cells[address.ColumnIndex];
                    cell.Selected = true;
                    first ??= cell;
                    break;
                }
            }
        }

        if (first is not null)
        {
            _grid.CurrentCell = first;
        }
    }

    private readonly record struct GridRectangle(int StartRow, int StartColumn, int RowCount, int ColumnCount)
    {
        public int CellCount => checked(RowCount * ColumnCount);
    }
}
