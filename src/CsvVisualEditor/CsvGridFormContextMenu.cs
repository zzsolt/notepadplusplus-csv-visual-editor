namespace CsvVisualEditor;

internal sealed partial class CsvGridForm
{
    private CsvGridContextMenu? _cellContextMenu;

    private void InstallCellContextMenu()
    {
        _cellContextMenu?.Dispose();
        _cellContextMenu = new CsvGridContextMenu(_grid,
            () =>
            {
                // Do not retarget a click through a delayed search refresh.
                if (_searchTimer.Enabled) { ApplyCurrentView(); return false; }
                return _projection is not null && _tabControl.SelectedTab == _tablePage && CommitPendingEdit();
            },
            () => _editMode,
            () => _editMode ? (object?)_rowEditModel : _lastViewResult,
            name => _toolStrip.Items.OfType<ToolStripButton>()
                .Concat(_viewToolStrip.Items.OfType<ToolStripButton>())
                .FirstOrDefault(button => button.Name == name));
    }
}
