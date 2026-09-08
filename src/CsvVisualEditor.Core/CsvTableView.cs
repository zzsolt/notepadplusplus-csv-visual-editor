namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;

public enum CsvTableSortDirection
{
    None,
    Ascending,
    Descending
}

public sealed record CsvTableViewOptions
{
    public string SearchText { get; init; } = string.Empty;

    public CsvDataViewDefinition DataView { get; init; } = CsvDataViewDefinition.Empty;

    public int? SearchColumnIndex { get; init; }

    public int? SortColumnIndex { get; init; }

    public CsvTableSortDirection SortDirection { get; init; } = CsvTableSortDirection.None;
}

public sealed record CsvTableViewResult
{
    public CsvTableViewResult(
        IEnumerable<CsvTableRow> rows,
        int totalRowCount,
        string effectiveSearchText,
        int? searchColumnIndex,
        int? sortColumnIndex,
        CsvTableSortDirection sortDirection,
        CsvDataViewDefinition? dataView = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(effectiveSearchText);

        if (totalRowCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalRowCount));
        }

        var copiedRows = rows.ToArray();
        if (copiedRows.Length > totalRowCount)
        {
            throw new ArgumentException(
                "The visible row count cannot exceed the total row count.",
                nameof(rows));
        }

        Rows = Array.AsReadOnly(copiedRows);
        TotalRowCount = totalRowCount;
        EffectiveSearchText = effectiveSearchText;
        SearchColumnIndex = searchColumnIndex;
        SortColumnIndex = sortColumnIndex;
        SortDirection = sortDirection;
        DataView = dataView ?? CsvDataViewDefinition.Empty;
    }

    public ReadOnlyCollection<CsvTableRow> Rows { get; }

    public int TotalRowCount { get; }

    public int VisibleRowCount => Rows.Count;

    public string EffectiveSearchText { get; }

    public int? SearchColumnIndex { get; }

    public int? SortColumnIndex { get; }

    public CsvTableSortDirection SortDirection { get; }

    public CsvDataViewDefinition DataView { get; }

    public bool IsFiltered => EffectiveSearchText.Length > 0 || DataView.Filters.Count > 0;

    public bool IsSorted =>
        SortColumnIndex is not null && SortDirection != CsvTableSortDirection.None;
}

public static class CsvTableViewBuilder
{
    public static CsvTableViewResult Build(
        CsvTableProjection projection,
        CsvTableViewOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(projection);
        options ??= new CsvTableViewOptions();
        ArgumentNullException.ThrowIfNull(options.SearchText);
        ArgumentNullException.ThrowIfNull(options.DataView);
        options.DataView.ValidateColumns(projection.ColumnCount);
        if (options.DataView.SortKeys.Count > 0 && (options.SortColumnIndex.HasValue || options.SortDirection != CsvTableSortDirection.None))
            throw new ArgumentException("Use either advanced sort levels or the single-column sort, not both.", nameof(options));
        if (options.SortDirection is not (CsvTableSortDirection.None or
            CsvTableSortDirection.Ascending or CsvTableSortDirection.Descending))
            throw new ArgumentOutOfRangeException(nameof(options.SortDirection));

        ValidateColumnIndex(
            options.SearchColumnIndex,
            projection.ColumnCount,
            nameof(options.SearchColumnIndex));
        ValidateColumnIndex(
            options.SortColumnIndex,
            projection.ColumnCount,
            nameof(options.SortColumnIndex));

        if (options.SortDirection != CsvTableSortDirection.None &&
            options.SortColumnIndex is null)
        {
            throw new ArgumentException(
                "A sort column is required when a sort direction is selected.",
                nameof(options));
        }

        var effectiveSearchText = options.SearchText.Trim();
        var indexedRows = projection.Rows
            .Select(static (row, index) => new IndexedRow(row, index));

        if (effectiveSearchText.Length > 0)
        {
            indexedRows = indexedRows.Where(entry => MatchesSearch(
                entry.Row,
                effectiveSearchText,
                options.SearchColumnIndex));
        }

        if (options.DataView.Filters.Count > 0)
            indexedRows = indexedRows.Where(entry => options.DataView.Matches(entry.Row));

        indexedRows = options.DataView.SortKeys.Count > 0
            ? ApplyAdvancedSort(indexedRows, options.DataView.SortKeys)
            : ApplySort(indexedRows, options.SortColumnIndex, options.SortDirection);

        return new CsvTableViewResult(
            indexedRows.Select(static entry => entry.Row),
            projection.DisplayedRowCount,
            effectiveSearchText,
            options.SearchColumnIndex,
            options.DataView.SortKeys.FirstOrDefault()?.ColumnIndex ?? options.SortColumnIndex,
            options.DataView.SortKeys.FirstOrDefault()?.Direction ?? options.SortDirection,
            options.DataView);
    }

    private static IEnumerable<IndexedRow> ApplyAdvancedSort(
        IEnumerable<IndexedRow> source, IReadOnlyList<CsvSortKey> keys)
    {
        var rows = source.ToArray();
        // Parse each numeric key once per surviving row, never O(n log n) times
        // in the comparator. The original projection index is the stable tie key.
        var numbers = new Dictionary<int, decimal?>[keys.Count];
        for (var k = 0; k < keys.Count; k++)
        {
            numbers[k] = new Dictionary<int, decimal?>();
            if (keys[k].Kind != CsvSortKind.Number) continue;
            foreach (var entry in rows)
                numbers[k].Add(entry.OriginalIndex,
                    CsvNumericValue.TryParse(entry.Row.Values[keys[k].ColumnIndex], out var n) ? n : null);
        }
        Array.Sort(rows, (left, right) =>
        {
            for (var k = 0; k < keys.Count; k++)
            {
                var key = keys[k];
                int comparison;
                if (key.Kind == CsvSortKind.Number)
                {
                    var a = numbers[k][left.OriginalIndex];
                    var b = numbers[k][right.OriginalIndex];
                    // Missing/invalid numbers stay LAST in either direction.
                    if (a.HasValue != b.HasValue) return a.HasValue ? -1 : 1;
                    comparison = a.HasValue ? a.Value.CompareTo(b!.Value) : 0;
                }
                else comparison = StringComparer.OrdinalIgnoreCase.Compare(
                    left.Row.Values[key.ColumnIndex], right.Row.Values[key.ColumnIndex]);
                if (comparison != 0)
                    return key.Direction == CsvTableSortDirection.Ascending
                        ? Math.Sign(comparison) : -Math.Sign(comparison);
            }
            return left.OriginalIndex.CompareTo(right.OriginalIndex);
        });
        return rows;
    }

    private static IEnumerable<IndexedRow> ApplySort(
        IEnumerable<IndexedRow> rows,
        int? columnIndex,
        CsvTableSortDirection direction)
    {
        if (columnIndex is null || direction == CsvTableSortDirection.None)
        {
            return rows;
        }

        return direction == CsvTableSortDirection.Ascending
            ? rows.OrderBy(
                    entry => entry.Row.Values[columnIndex.Value],
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(static entry => entry.OriginalIndex)
            : rows.OrderByDescending(
                    entry => entry.Row.Values[columnIndex.Value],
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(static entry => entry.OriginalIndex);
    }

    private static bool MatchesSearch(
        CsvTableRow row,
        string searchText,
        int? columnIndex)
    {
        if (columnIndex is not null)
        {
            return row.Values[columnIndex.Value].Contains(
                searchText,
                StringComparison.OrdinalIgnoreCase);
        }

        return row.Values.Any(value => value.Contains(
            searchText,
            StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateColumnIndex(
        int? index,
        int columnCount,
        string parameterName)
    {
        if (index is null)
        {
            return;
        }

        if (index < 0 || index >= columnCount)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                index,
                "The column index must identify an existing projected column.");
        }
    }

    private sealed record IndexedRow(CsvTableRow Row, int OriginalIndex);
}
