namespace CsvVisualEditor;

using CsvVisualEditor.Localization;

using CsvVisualEditor.Core;

internal sealed partial class CsvGridForm
{
    private bool _dataToolHandlersAttached;
    private readonly ToolStripSeparator _dataToolsSeparator = new();
    private CsvDataViewDefinition _dataView = CsvDataViewDefinition.Empty;
    private readonly ToolStripButton _dataViewButton = new(L10n.Get(TextKey.Filter_Title))
    {
        Name = "CsvDataViewButton", ToolTipText = L10n.Get(TextKey.DataTools_FilterAndSortCombineColumnRulesAndUp)
    };
    private readonly ToolStripButton _profileButton = new(L10n.Get(TextKey.Summary_Title))
    {
        Name = "CsvColumnSummaryButton", ToolTipText = L10n.Get(TextKey.DataTools_ColumnSummaryCountsDistinctValuesNumericRangeAnd)
    };

    private void InstallDataTools()
    {
        CsvDataToolCommands.Attach(_viewToolStrip, _dataViewButton, _profileButton, _dataToolsSeparator);
        if (_dataToolHandlersAttached) return;
        _dataToolHandlersAttached = true;
        _dataViewButton.Click += (_, _) => ShowDataViewDialog();
        _profileButton.Click += (_, _) => ShowColumnSummary();
    }

    internal void ShowDataViewDialog()
    {
        if (_editMode || _projection is null)
        {
            _statusLabel.Text = L10n.Get(TextKey.DataTools_FilterAndSortRequiresAReadyReadOnly);
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
            _statusLabel.Text = L10n.Get(TextKey.DataTools_ColumnSummaryRequiresAReadyReadOnlyTable);
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
            ? L10n.Format(TextKey.DataTools_FilterAndSortConditionsSortLevelsResetView, _dataView.Filters.Count, _dataView.SortKeys.Count)
            : L10n.Get(TextKey.DataTools_FilterAndSortCombineColumnRulesAndText);
    }
}
