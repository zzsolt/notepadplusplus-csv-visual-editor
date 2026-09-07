namespace CsvVisualEditor;

using CsvVisualEditor.Core;

internal sealed partial class CsvGridForm
{
    private CsvCommandSurface? _commandSurface;
    private readonly ToolStripButton _spacesButton = new("Show spaces")
    {
        Name = "CsvShowSpacesButton", CheckOnClick = true, Checked = true,
        ToolTipText = "Show small hollow orange indicators for real spaces. Display only; CSV values do not change."
    };
    private bool _spaceHandlerAttached;

    internal void InstallCommandSurface()
    {
        _commandSurface?.Dispose();
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
        var surface = new CsvCommandSurface(_toolStrip, _viewToolStrip);
        _commandSurface = surface;
        surface.AddCombo(surface.Csv, "&Delimiter", _delimiterCombo);
        surface.AddCombo(surface.Csv, "&Header", _headerCombo);
        surface.AddCombo(surface.Search, "Search &column", _searchColumnCombo);
        surface.AddSearch(_searchBox);
        var table = new ToolStripMenuItem("Show &table");
        table.Click += (_, _) => _tabControl.SelectedTab = _tablePage;
        surface.Table.DropDownItems.Add(table);
        var sort = new ToolStripMenuItem("&Sort by");
        surface.View.DropDownItems.Add(sort);
        surface.View.DropDownOpening += (_, _) => sort.Enabled = !_editMode && _projection is not null;
        sort.DropDownOpening += (_, _) =>
        {
            foreach (ToolStripItem old in sort.DropDownItems.Cast<ToolStripItem>().ToArray()) old.Dispose();
            if (_projection is null) return;
            var original = new ToolStripMenuItem("Original order") { Checked = _sortColumnIndex is null };
            original.Click += (_, _) => SetMenuSort(null, CsvTableSortDirection.None);
            sort.DropDownItems.Add(original);
            foreach (var column in _projection.Columns)
            {
                var index = column.Index;
                var menu = new ToolStripMenuItem(column.Name.Replace("&", "&&", StringComparison.Ordinal));
                foreach (var direction in new[] { CsvTableSortDirection.Ascending, CsvTableSortDirection.Descending })
                {
                    var item = new ToolStripMenuItem(direction.ToString())
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
            _topPanel.RowCount = 3;
            _topPanel.RowStyles.Clear();
            for (var index = 0; index < 3; index++) _topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _topPanel.SetCellPosition(_toolStrip, new TableLayoutPanelCellPosition(0, 1));
            _topPanel.SetCellPosition(_viewToolStrip, new TableLayoutPanelCellPosition(0, 2));
            _topPanel.Controls.Add(surface.Menu, 0, 0);
            MainMenuStrip = surface.Menu;
            RefreshCommandAppearance();
        }
        finally { _topPanel.ResumeLayout(true); }
    }

    private void SetMenuSort(int? column, CsvTableSortDirection direction)
    {
        if (_editMode || _projection is null || column.HasValue && (column.Value < 0 || column.Value >= _projection.Columns.Count)) return;
        _sortColumnIndex = column;
        _sortDirection = direction;
        _clearSearchButton.Enabled = _searchBox.Text.Length > 0 || column.HasValue;
        ApplyCurrentView();
    }

    private void RefreshCommandAppearance() => _commandSurface?.ApplyAppearance(BackColor, ForeColor, DeviceDpi);

}
