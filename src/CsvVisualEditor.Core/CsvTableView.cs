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
        CsvTableSortDirection sortDirection)
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
    }

    public ReadOnlyCollection<CsvTableRow> Rows { get; }

    public int TotalRowCount { get; }

    public int VisibleRowCount => Rows.Count;

    public string EffectiveSearchText { get; }

    public int? SearchColumnIndex { get; }

    public int? SortColumnIndex { get; }

    public CsvTableSortDirection SortDirection { get; }

    public bool IsFiltered => EffectiveSearchText.Length > 0;

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

        indexedRows = ApplySort(
            indexedRows,
            options.SortColumnIndex,
            options.SortDirection);

        return new CsvTableViewResult(
            indexedRows.Select(static entry => entry.Row),
            projection.DisplayedRowCount,
            effectiveSearchText,
            options.SearchColumnIndex,
            options.SortColumnIndex,
            options.SortDirection);
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
