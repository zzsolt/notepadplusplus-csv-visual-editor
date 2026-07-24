namespace CsvVisualEditor;

using CsvVisualEditor.Core;

/// <summary>
/// Immutable stable row IDs currently marked through the explicit selector
/// column. The DataGridView cells are presentation only and are never retained.
/// </summary>
internal readonly record struct CsvGridManagedSelectionSnapshot(
    bool HasExplicitRowHeaderContext,
    IReadOnlyList<CsvEditRowId> SelectedIds);

/// <summary>
/// Stores explicit checkbox-style row marks independently from DataGridView
/// selection collections. Stable model IDs are the only retained identities.
/// </summary>
internal sealed class CsvGridManagedRowSelection
{
    private readonly HashSet<CsvEditRowId> _selectedIds = [];

    public void Toggle(CsvEditRowId id)
    {
        if (!_selectedIds.Add(id))
        {
            _selectedIds.Remove(id);
        }
    }

    public bool IsSelected(CsvEditRowId id) => _selectedIds.Contains(id);

    public void Clear() => _selectedIds.Clear();

    public CsvGridManagedSelectionSnapshot Capture(
        IReadOnlyList<CsvEditRowId> visibleOrder)
    {
        ArgumentNullException.ThrowIfNull(visibleOrder);

        var visibleIds = visibleOrder.ToHashSet();
        _selectedIds.RemoveWhere(id => !visibleIds.Contains(id));

        var ordered = visibleOrder
            .Where(_selectedIds.Contains)
            .ToArray();

        return new CsvGridManagedSelectionSnapshot(
            HasExplicitRowHeaderContext: ordered.Length > 0,
            SelectedIds: ordered);
    }
}
