namespace CsvVisualEditor;

using CsvVisualEditor.Core;

/// <summary>
/// Immutable stable row IDs currently selected through either the explicit
/// selector column or a row-header gesture. DataGridView cells and rows are
/// presentation only and are never retained.
/// </summary>
internal readonly record struct CsvGridManagedSelectionSnapshot(
    bool HasExplicitRowHeaderContext,
    IReadOnlyList<CsvEditRowId> SelectedIds);

/// <summary>
/// Stores explicit complete-row selection independently from DataGridView
/// selection collections. Stable model IDs are the only retained identities.
/// </summary>
internal sealed class CsvGridManagedRowSelection
{
    private readonly HashSet<CsvEditRowId> _selectedIds = [];
    private CsvEditRowId? _anchorId;

    public void Toggle(CsvEditRowId id)
    {
        if (!_selectedIds.Add(id))
        {
            _selectedIds.Remove(id);
        }

        _anchorId = id;
    }

    public void ReplaceWith(CsvEditRowId id)
    {
        _selectedIds.Clear();
        _selectedIds.Add(id);
        _anchorId = id;
    }

    public void ApplyRowHeaderGesture(
        CsvEditRowId id,
        IReadOnlyList<CsvEditRowId> visibleOrder,
        bool control,
        bool shift)
    {
        ArgumentNullException.ThrowIfNull(visibleOrder);

        if (!shift)
        {
            if (control)
            {
                Toggle(id);
            }
            else
            {
                ReplaceWith(id);
            }

            return;
        }

        var clickedIndex = FindIndex(visibleOrder, id);
        var anchorIndex = _anchorId is CsvEditRowId anchorId
            ? FindIndex(visibleOrder, anchorId)
            : -1;
        if (clickedIndex < 0 || anchorIndex < 0)
        {
            if (control)
            {
                _selectedIds.Add(id);
                _anchorId = id;
            }
            else
            {
                ReplaceWith(id);
            }

            return;
        }

        if (!control)
        {
            _selectedIds.Clear();
        }

        var first = Math.Min(anchorIndex, clickedIndex);
        var last = Math.Max(anchorIndex, clickedIndex);
        for (var index = first; index <= last; index++)
        {
            _selectedIds.Add(visibleOrder[index]);
        }
    }

    public bool IsSelected(CsvEditRowId id) => _selectedIds.Contains(id);

    public void Clear()
    {
        _selectedIds.Clear();
        _anchorId = null;
    }

    public CsvGridManagedSelectionSnapshot Capture(
        IReadOnlyList<CsvEditRowId> visibleOrder)
    {
        ArgumentNullException.ThrowIfNull(visibleOrder);

        var visibleIds = visibleOrder.ToHashSet();
        _selectedIds.RemoveWhere(id => !visibleIds.Contains(id));
        if (_anchorId is CsvEditRowId anchorId && !visibleIds.Contains(anchorId))
        {
            _anchorId = null;
        }

        var ordered = visibleOrder
            .Where(_selectedIds.Contains)
            .ToArray();

        return new CsvGridManagedSelectionSnapshot(
            HasExplicitRowHeaderContext: ordered.Length > 0,
            SelectedIds: ordered);
    }

    private static int FindIndex(
        IReadOnlyList<CsvEditRowId> visibleOrder,
        CsvEditRowId id)
    {
        for (var index = 0; index < visibleOrder.Count; index++)
        {
            if (visibleOrder[index] == id)
            {
                return index;
            }
        }

        return -1;
    }
}
