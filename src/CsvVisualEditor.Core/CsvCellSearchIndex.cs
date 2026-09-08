namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;

/// <summary>A visible data-cell address, never a presentation-column address.</summary>
public readonly record struct CsvSearchCell(int RowIndex, int ColumnIndex);

/// <summary>
/// Immutable, value-free search results for one rendered view. Uses the exact effective
/// query and ordinal-ignore-case comparison of the row filter. The existing projection
/// limits bound the number of addresses; painting and navigation never rescan CSV text.
/// </summary>
public sealed class CsvCellSearchIndex
{
    private readonly CsvSearchCell[] _cells;

    private CsvCellSearchIndex(CsvSearchCell[] cells)
    {
        _cells = cells;
        Cells = Array.AsReadOnly(cells);
    }

    public ReadOnlyCollection<CsvSearchCell> Cells { get; }
    public int Count => _cells.Length;

    public static CsvCellSearchIndex Create(CsvTableViewResult view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var cells = new List<CsvSearchCell>();
        if (view.EffectiveSearchText.Length == 0) return new([]);
        for (var row = 0; row < view.Rows.Count; row++)
        {
            var values = view.Rows[row].Values;
            var first = view.SearchColumnIndex ?? 0;
            var last = view.SearchColumnIndex.HasValue ? first + 1 : values.Count;
            for (var column = first; column < last; column++)
            {
                if (column < 0 || column >= values.Count) continue;
                if (values[column].Contains(view.EffectiveSearchText, StringComparison.OrdinalIgnoreCase))
                    cells.Add(new(row, column));
            }
        }
        return new(cells.ToArray());
    }

    public int FindIndex(int row, int column)
    {
        var position = LowerBound(row, column);
        return position < Count && _cells[position] == new CsvSearchCell(row, column) ? position : -1;
    }

    /// <summary>Next/previous cell in visible row-major order, wrapping at both ends.</summary>
    public int MoveFrom(int row, int column, bool backwards)
    {
        if (Count == 0) return -1;
        if (row < 0 || column < 0) return backwards ? Count - 1 : 0;
        var position = LowerBound(row, column);
        var exact = position < Count && _cells[position] == new CsvSearchCell(row, column);
        if (backwards) return position == 0 ? Count - 1 : position - 1;
        if (exact) position++;
        return position == Count ? 0 : position;
    }

    private int LowerBound(int row, int column)
    {
        var low = 0;
        var high = Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            var cell = _cells[middle];
            if (cell.RowIndex < row || cell.RowIndex == row && cell.ColumnIndex < column)
                low = middle + 1;
            else high = middle;
        }
        return low;
    }
}
