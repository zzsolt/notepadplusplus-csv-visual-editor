namespace CsvVisualEditor;

using CsvVisualEditor.Localization;

using CsvVisualEditor.Core;

internal sealed partial class CsvGridForm
{
    private CsvCommandSurface? _commandSurface;
    private readonly ToolStripButton _spacesButton = new(L10n.Get(TextKey.View_ShowSpaces))
    {
        Name = "CsvShowSpacesButton", CheckOnClick = true, Checked = true,
        ToolTipText = L10n.Get(TextKey.Menu_ShowSpacesSmallSolidOrangeDotsDisplayOnly)
    };
    private bool _spaceHandlerAttached;

    internal void InstallCommandSurface()
    {
        _commandSurface?.Dispose();
        // Clipboard toolbar reconstruction clears the interpretation strip.
        // Restore view tools before binding the permanent menu, without adding
        // duplicate event handlers when the command surface is reinstalled.
        InstallDataTools();
        if (!_spaceHandlerAttached)
        {
            _spaceHandlerAttached = true;
            _spacesButton.CheckedChanged += (_, _) =>
            {
                if (_grid is CsvDataGridView csvGrid) csvGrid.ShowWhitespace = _spacesButton.Checked;
                _grid.Invalidate();
            };
            DpiChangedAfterParent += (_, _) => RefreshCommandAppearance();
        }
        if (!_toolStrip.Items.Contains(_spacesButton)) _toolStrip.Items.Add(_spacesButton);
        _clearSearchButton.Text = L10n.Get(TextKey.View_Reset);
        _clearSearchButton.ToolTipText = L10n.Get(TextKey.Menu_ResetViewClearSearchColumnConditionsAndAll);
        var surface = new CsvCommandSurface(_toolStrip, _viewToolStrip);
        surface.Menu.CanOverflow = true; // Keep About and trailing menus reachable in a narrow dock.
        _commandSurface = surface;
        surface.AddCombo(surface.Csv, L10n.Get(TextKey.Menu_Delimiter), _delimiterCombo);
        surface.AddCombo(surface.Csv, L10n.Get(TextKey.Menu_Header), _headerCombo);
        surface.AddCombo(surface.Search, L10n.Get(TextKey.Menu_SearchColumn), _searchColumnCombo);
        surface.AddSearch(_searchBox, FocusSearch, NavigateSearch,
            () => !_editMode && _searchResults?.Count > 0);
        surface.About.Click += (_, _) =>
        {
            using var dialog = new CsvAboutDialog(BackColor, ForeColor);
            dialog.ShowDialog(this);
        };
        var table = new ToolStripMenuItem(L10n.Get(TextKey.Menu_ShowTable));
        table.Click += (_, _) => _tabControl.SelectedTab = _tablePage;
        surface.Table.DropDownItems.Add(table);
        var sort = new ToolStripMenuItem(L10n.Get(TextKey.Menu_SortBy));
        surface.View.DropDownItems.Add(sort);
        surface.View.DropDownOpening += (_, _) => sort.Enabled = !_editMode && _projection is not null;
        sort.DropDownOpening += (_, _) =>
        {
            foreach (ToolStripItem old in sort.DropDownItems.Cast<ToolStripItem>().ToArray()) old.Dispose();
            if (_projection is null) return;
            var original = new ToolStripMenuItem(L10n.Get(TextKey.Common_OriginalOrder)) { Checked = _sortColumnIndex is null && _dataView.SortKeys.Count == 0 };
            original.Click += (_, _) => SetMenuSort(null, CsvTableSortDirection.None);
            sort.DropDownItems.Add(original);
            foreach (var column in _projection.Columns)
            {
                var index = column.Index;
                var menu = new ToolStripMenuItem(CsvUiText.ColumnName(column).Replace("&", "&&", StringComparison.Ordinal));
                foreach (var direction in new[] { CsvTableSortDirection.Ascending, CsvTableSortDirection.Descending })
                {
                    var item = new ToolStripMenuItem(CsvUiText.SortDirection(direction))
                    {
                        Checked = _sortColumnIndex == index && _sortDirection == direction
                    };
                    item.Click += (_, _) => SetMenuSort(index, direction);
                    menu.DropDownItems.Add(item);
                }
                sort.DropDownItems.Add(menu);
            }
            surface.RefreshMenuColors();
        };
        _topPanel.SuspendLayout();
        try
        {
            _topPanel.RowCount = 4;
            _topPanel.RowStyles.Clear();
            for (var index = 0; index < 4; index++) _topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _topPanel.SetCellPosition(_toolStrip, new TableLayoutPanelCellPosition(0, 1));
            _topPanel.SetCellPosition(_viewToolStrip, new TableLayoutPanelCellPosition(0, 2));
            _topPanel.SetCellPosition(_searchBar, new TableLayoutPanelCellPosition(0, 3));
            _topPanel.Controls.Add(surface.Menu, 0, 0);
            MainMenuStrip = surface.Menu;
            RefreshCommandAppearance();
        }
        finally { _topPanel.ResumeLayout(true); }
    }

    private void SetMenuSort(int? column, CsvTableSortDirection direction)
    {
        if (_editMode || _projection is null || column.HasValue && (column.Value < 0 || column.Value >= _projection.Columns.Count)) return;
        _dataView = new CsvDataViewDefinition(_dataView.Filters, _dataView.Combination);
        _sortColumnIndex = column;
        _sortDirection = direction;
        ApplyCurrentView();
    }

    private void RefreshCommandAppearance()
    {
        _commandSurface?.ApplyAppearance(BackColor, ForeColor, DeviceDpi);
        _searchBar.ApplyAppearance(BackColor, ForeColor);
        _noMatchesLabel.BackColor = _grid.BackgroundColor;
        _noMatchesLabel.ForeColor = ForeColor;
        _diagnosticsEmpty.BackColor = _diagnosticsGrid.BackgroundColor;
        _diagnosticsEmpty.ForeColor = ForeColor;
        _grid.GridColor = CsvSearchBar.Blend(_grid.BackgroundColor, ForeColor, 20);
        _diagnosticsGrid.GridColor = _grid.GridColor;
    }
}
