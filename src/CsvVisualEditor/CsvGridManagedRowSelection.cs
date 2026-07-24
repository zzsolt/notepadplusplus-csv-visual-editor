namespace CsvVisualEditor;

using CsvVisualEditor.Core;

/// <summary>
/// Immutable view of the plugin-owned row-header selection. The selected IDs
/// are stable model identities; no DataGridView rows, cells, or display indexes
/// are retained.
/// </summary>
internal readonly record struct CsvGridManagedSelectionSnapshot(
    bool HasExplicitRowHeaderContext,
    IReadOnlyList<CsvEditRowId> SelectedIds);

/// <summary>
/// Owns Ctrl/Shift row-header semantics independently from the transient
/// selection representation chosen by the docked WinForms host.
/// </summary>
internal sealed class CsvGridManagedRowSelection
{
    private readonly HashSet<CsvEditRowId> _selectedIds = [];
    private CsvEditRowId? _anchorId;

    public bool HasExplicitRowHeaderContext { get; private set; }

    public void ResetToCurrentRowFallback()
    {
        _selectedIds.Clear();
        _anchorId = null;
        HasExplicitRowHeaderContext = false;
    }

    public void ApplyRowHeaderGesture(
        IReadOnlyList<CsvEditRowId> visibleOrder,
        CsvEditRowId clickedId,
        bool control,
        bool shift)
    {
        ArgumentNullException.ThrowIfNull(visibleOrder);

        var clickedIndex = IndexOf(visibleOrder, clickedId);
        if (clickedIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(clickedId),
                clickedId,
                "The clicked row must be present in the current visible structural order.");
        }

        HasExplicitRowHeaderContext = true;

        if (shift)
        {
            var anchorIndex = _anchorId is CsvEditRowId anchorId
                ? IndexOf(visibleOrder, anchorId)
                : -1;
            if (anchorIndex < 0)
            {
                anchorIndex = clickedIndex;
                _anchorId = clickedId;
            }

            if (!control)
            {
                _selectedIds.Clear();
            }

            var firstIndex = Math.Min(anchorIndex, clickedIndex);
            var lastIndex = Math.Max(anchorIndex, clickedIndex);
            for (var index = firstIndex; index <= lastIndex; index++)
            {
                _selectedIds.Add(visibleOrder[index]);
            }

            return;
        }

        _anchorId = clickedId;
        if (control)
        {
            if (!_selectedIds.Add(clickedId))
            {
                _selectedIds.Remove(clickedId);
            }

            return;
        }

        _selectedIds.Clear();
        _selectedIds.Add(clickedId);
    }

    public CsvGridManagedSelectionSnapshot Capture()
    {
        return new CsvGridManagedSelectionSnapshot(
            HasExplicitRowHeaderContext,
            _selectedIds.ToArray());
    }

    private static int IndexOf(
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
