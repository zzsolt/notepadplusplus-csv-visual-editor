namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using CsvVisualEditor.Localization;
using System.Globalization;

internal sealed partial class CsvGridForm
{
    private readonly ToolStripButton _cellDetailsButton = new(L10n.Get(TextKey.Cell_Title))
    {
        Name = "CsvCellDetailsButton", ToolTipText = L10n.Get(TextKey.Cell_OpenHint), Enabled = false
    };
    private bool _cellDetailsAttached;

    private void InstallCellDetails()
    {
        if (!_toolStrip.Items.Contains(_cellDetailsButton)) _toolStrip.Items.Add(_cellDetailsButton);
        if (!_cellDetailsAttached)
        {
            _cellDetailsAttached = true;
            _cellDetailsButton.Click += (_, _) => ShowCellDetails();
            _grid.CurrentCellChanged += (_, _) => UpdateCellDetailsAvailability();
            _tabControl.SelectedIndexChanged += (_, _) => UpdateCellDetailsAvailability();
        }
        UpdateCellDetailsAvailability();
    }

    private bool HasDataCell() => _projection is not null && _tabControl.SelectedTab == _tablePage &&
        _grid.CurrentCell is { RowIndex: >= 0, ColumnIndex: >= 0 } cell &&
        cell.RowIndex < _grid.RowCount && cell.ColumnIndex < _projection.ColumnCount &&
        cell.OwningColumn is { } physicalColumn &&
        !CsvGridRowHeaderBehavior.IsPresentationColumn(physicalColumn);

    private void UpdateCellDetailsAvailability() => _cellDetailsButton.Enabled = HasDataCell();

    private bool TryHandleCellDetailsKey(Keys keyData)
    {
        if (keyData != (Keys.Alt | Keys.Enter) || !_grid.ContainsFocus) return false;
        ShowCellDetails();
        return true;
    }

    internal void ShowCellDetails()
    {
        // Resolve a delayed filter before capturing the selected physical cell.
        if (_searchTimer.Enabled) ApplyCurrentView();
        if (!HasDataCell())
        {
            _statusLabel.Text = L10n.Get(TextKey.Cell_SelectCell);
            return;
        }
        if (!CommitPendingEdit()) return;
        var cell = _grid.CurrentCell!;
        var column = cell.ColumnIndex;
        var gridRow = cell.OwningRow;
        if (gridRow is null) return;
        var snapshot = _snapshot;
        var model = _rowEditModel;
        var editable = _editMode && model is not null && gridRow.Tag is CsvEditRowId;
        var rowId = editable ? (CsvEditRowId)gridRow.Tag! : default;
        var before = editable ? model!.GetRow(rowId).Values[column] :
            Convert.ToString(cell.Value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (before.Length > CsvCellTextCodec.MaximumValueLength)
        {
            _statusLabel.Text = L10n.Format(TextKey.Cell_TooLarge, CsvCellTextCodec.MaximumValueLength);
            return;
        }
        var rowName = editable
            ? rowId.IsInserted ? L10n.Format(TextKey.Rows_NewIdentifier, -rowId.Value) :
                (rowId.SourceRecordIndex!.Value + 1).ToString(CultureInfo.InvariantCulture)
            : _usingVirtualReadOnlyRows
                ? (_virtualReadOnlyRows[cell.RowIndex].SourceRecordIndex + 1).ToString(CultureInfo.InvariantCulture)
                : gridRow.Tag is int record ? (record + 1).ToString(CultureInfo.InvariantCulture)
                : (cell.RowIndex + 1).ToString(CultureInfo.InvariantCulture);
        var location = L10n.Format(TextKey.Cell_Location, rowName, column + 1);
        using var dialog = new CsvCellDetailsDialog(before, location, editable, BackColor, ForeColor);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result is not { } after) return;
        try
        {
            // A modal window still pumps messages: async refresh or host callbacks
            // must never let it edit a replacement model or a different cell.
            if (!editable || !_editMode || model is null || !ReferenceEquals(_rowEditModel, model) ||
                !ReferenceEquals(_snapshot, snapshot) || cell.DataGridView != _grid || cell.OwningRow != gridRow ||
                gridRow.Tag is not CsvEditRowId currentId || currentId != rowId ||
                !string.Equals(model.GetRow(rowId).Values[column], before, StringComparison.Ordinal))
                throw new InvalidOperationException();
            var change = CsvCellValueChange.Create(model, new(rowId, column), after);
            if (!change.Apply(model)) return;
            _suppressGridChanges = true;
            try { cell.Value = after; }
            finally { _suppressGridChanges = false; }
            UpdateDirtyIndicators();
            _statusLabel.Text = L10n.Get(TextKey.Cell_ChangeStaged);
            _grid.Focus();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _statusLabel.Text = L10n.Get(TextKey.Cell_ContextChanged);
        }
    }
}
