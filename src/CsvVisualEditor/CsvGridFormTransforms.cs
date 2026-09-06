namespace CsvVisualEditor;

using CsvVisualEditor.Core;

internal sealed partial class CsvGridForm
{
    internal void ShowTransforms()
    {
        if (!_editMode || _rowEditModel is null || !CommitPendingEdit()) return;

        var model = _rowEditModel;
        var selectedRows = CsvGridRowHeaderBehavior.CaptureManagedSelection(_grid).SelectedIds.ToHashSet();
        var selectedCells = _grid.SelectedCells.Cast<DataGridViewCell>()
            .Where(cell => cell.OwningRow?.Tag is CsvEditRowId && cell.ColumnIndex < model.ColumnCount && cell.ColumnIndex >= 0)
            .Select(cell => new CsvCellAddress((CsvEditRowId)cell.OwningRow!.Tag!, cell.ColumnIndex))
            .ToHashSet();
        var currentColumn = _grid.CurrentCell?.ColumnIndex ?? -1;

        IEnumerable<CsvCellAddress> GetTargets(CsvTransformScope scope)
        {
            if (!_editMode || !ReferenceEquals(_rowEditModel, model))
                throw new InvalidOperationException("The Edit session changed.");
            if (scope == CsvTransformScope.CurrentColumn && (currentColumn < 0 || currentColumn >= model.ColumnCount))
                throw new InvalidOperationException("Select a CSV data column.");

            // Stable model order and physical columns; presentation cells are never targets.
            foreach (var row in model.GetVisibleRows())
                for (var column = 0; column < model.ColumnCount; column++)
                {
                    var address = new CsvCellAddress(row.Id, column);
                    if (scope == CsvTransformScope.AllDataCells ||
                        (scope == CsvTransformScope.CurrentColumn && column == currentColumn) ||
                        (scope == CsvTransformScope.SelectedCells && (selectedRows.Contains(row.Id) || selectedCells.Contains(address))))
                        yield return address;
                }
        }

        using var dialog = new CsvTransformDialog(
            (scope, transform) => CsvCellTransformPlan.Create(model, GetTargets(scope), transform),
            plan =>
            {
                if (!_editMode || !ReferenceEquals(_rowEditModel, model))
                    throw new InvalidOperationException("The Edit session changed.");
                var changed = plan.Apply(model);
                // Update in place to preserve cell/complete-row selection and the viewport.
                var gridRows = _grid.Rows.Cast<DataGridViewRow>()
                    .Where(static row => row.Tag is CsvEditRowId)
                    .ToDictionary(static row => (CsvEditRowId)row.Tag!);
                _suppressGridChanges = true;
                try
                {
                    foreach (var change in plan.Changes)
                        if (gridRows.TryGetValue(change.Address.RowId, out var row))
                            row.Cells[change.Address.ColumnIndex].Value = change.After;
                }
                finally { _suppressGridChanges = false; }
                UpdateDirtyIndicators();
                _statusLabel.Text = $"Transformed {changed:N0} cells in the pending Edit session. Use Apply to update Notepad++, or Revert All to discard pending edits.";
            });
        dialog.ShowDialog(this);
        _grid.Focus();
    }
}
