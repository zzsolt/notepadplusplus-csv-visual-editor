namespace CsvVisualEditor;

using CsvVisualEditor.Core;

internal sealed partial class CsvGridForm
{
    private CsvDataViewDefinition _dataView = CsvDataViewDefinition.Empty;
    private readonly ToolStripButton _dataViewButton = new("Filter and sort")
    {
        Name = "CsvDataViewButton", ToolTipText = "Filter and sort: combine column rules and up to three text/numeric sort levels. View only."
    };
    private readonly ToolStripButton _profileButton = new("Column summary")
    {
        Name = "CsvColumnSummaryButton", ToolTipText = "Column summary: counts, distinct values, numeric range and frequent values in the current view."
    };

    private void InstallDataTools()
    {
        _viewToolStrip.Items.Add(new ToolStripSeparator());
        _viewToolStrip.Items.Add(_dataViewButton);
        _viewToolStrip.Items.Add(_profileButton);
        _dataViewButton.Click += (_, _) => ShowDataViewDialog();
        _profileButton.Click += (_, _) => ShowColumnSummary();
    }

    internal void ShowDataViewDialog()
    {
        if (_editMode || _projection is null)
        {
            _statusLabel.Text = "Filter and sort requires a ready, read-only table. Exit Edit mode first.";
            return;
        }
        // Flush a pending query before capturing its immutable dialog context.
        if (_searchTimer.Enabled) ApplyCurrentView();
        var keys = _dataView.SortKeys.Count > 0 ? _dataView.SortKeys.ToArray() :
            _sortColumnIndex.HasValue && _sortDirection != CsvTableSortDirection.None
                ? new[] { new CsvSortKey(_sortColumnIndex.Value, _sortDirection) } : Array.Empty<CsvSortKey>();
        var definition = new CsvDataViewDefinition(_dataView.Filters, _dataView.Combination, keys);
        using var dialog = new CsvDataViewDialog(_projection, definition, _searchBox.Text,
            _searchColumnCombo.SelectedIndex > 0 ? _searchColumnCombo.SelectedIndex - 1 : null, BackColor, ForeColor);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result is null) return;
        _dataView = dialog.Result;
        _sortColumnIndex = null;
        _sortDirection = CsvTableSortDirection.None;
        ApplyCurrentView();
    }

    internal void ShowColumnSummary()
    {
        if (_editMode || _projection is null || _projection.ColumnCount == 0)
        {
            _statusLabel.Text = "Column summary requires a ready, read-only table. Exit Edit mode first.";
            return;
        }
        if (_searchTimer.Enabled) ApplyCurrentView();
        if (_lastViewResult is null) return;
        var column = _grid.CurrentCell?.ColumnIndex ?? 0;
        if (column < 0 || column >= _projection.ColumnCount) column = 0;
        using var dialog = new CsvColumnSummaryDialog(_projection, _lastViewResult, column, BackColor, ForeColor);
        dialog.ShowDialog(this);
    }

    private void UpdateDataToolAvailability()
    {
        _dataViewButton.Enabled = _profileButton.Enabled = !_editMode && _projection?.ColumnCount > 0;
        _dataViewButton.Checked = _dataView.IsActive;
        _dataViewButton.ToolTipText = _dataView.IsActive
            ? $"Filter and sort: {_dataView.Filters.Count} conditions, {_dataView.SortKeys.Count} sort levels. Reset view clears all rules."
            : "Filter and sort: combine column rules and text/numeric sort levels. View only; CSV unchanged.";
    }
}
