namespace CsvVisualEditor;

using CsvVisualEditor.Localization;

internal sealed partial class CsvGridForm
{
    private void FocusSearch()
    {
        if (!_searchBox.Enabled) return;
        _tabControl.SelectedTab = _tablePage;
        _searchBar.FocusQuery();
    }

    private void ClearSearch()
    {
        if (!_searchBox.Enabled) return;
        _searchTimer.Stop();
        _updatingViewControls = true;
        try { _searchBox.Clear(); }
        finally { _updatingViewControls = false; }
        // The close affordance clears just the query. Reset view is a separate command.
        ApplyCurrentView();
    }

    private void NavigateSearch(bool backwards)
    {
        if (_editMode || _projection is null || !_searchBox.Enabled) return;
        if (_searchTimer.Enabled)
        {
            // Enter immediately after typing must use the new query, not stale results.
            ApplyCurrentView();
            if (_searchResults?.Count > 0)
                SelectSearchResult(backwards ? _searchResults.Count - 1 : 0);
            return;
        }
        if (_searchResults is null) return;
        var address = _grid.CurrentCellAddress;
        SelectSearchResult(_searchResults.MoveFrom(address.Y, address.X, backwards));
    }

    private void SelectSearchResult(int index)
    {
        if (_searchResults is null || index < 0 || index >= _searchResults.Count) return;
        var match = _searchResults.Cells[index];
        if (match.RowIndex >= _grid.RowCount || match.ColumnIndex >= _grid.ColumnCount) return;
        var cell = _grid.Rows[match.RowIndex].Cells[match.ColumnIndex];
        _grid.ClearSelection();
        _grid.CurrentCell = cell;
        cell.Selected = true;
        // CurrentCell scrolls into view. Do not steal keyboard focus from the search box.
        UpdateSearchSummary();
    }

    private void UpdateSearchSummary()
    {
        var hasQuery = !_editMode && _lastViewResult?.EffectiveSearchText.Length > 0;
        var address = _grid.CurrentCellAddress;
        _searchBar.SetResults(_searchResults?.FindIndex(address.Y, address.X) ?? -1,
            _searchResults?.Count ?? 0, hasQuery, _searchTimer?.Enabled == true);
        _noMatchesLabel.Visible = !_editMode && _projection is not null && _lastViewResult?.IsFiltered == true && _lastViewResult.VisibleRowCount == 0;
        _noMatchesLabel.Text = L10n.Get(TextKey.Search_NoMatchingRowsAdjustTheSearchOrUse);
        if (_noMatchesLabel.Visible) _noMatchesLabel.BringToFront();
    }

    private bool TryHandleSearchKey(Keys keyData)
    {
        if (!_searchBox.Enabled) return false;
        if (keyData == (Keys.Control | Keys.F)) { FocusSearch(); return true; }
        if (keyData == Keys.F3 || keyData == (Keys.Shift | Keys.F3))
        {
            NavigateSearch((keyData & Keys.Shift) != 0);
            return true;
        }
        return false;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) =>
        TryHandleSearchKey(keyData) || base.ProcessCmdKey(ref msg, keyData);
}
