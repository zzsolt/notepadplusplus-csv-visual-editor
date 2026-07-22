namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;

/// <summary>
/// Rectangular, display-only projection of parsed logical CSV records.
/// </summary>
public sealed record CsvTableProjection
{
    public CsvTableProjection(
        IEnumerable<CsvTableColumn> columns,
        IEnumerable<CsvTableRow> rows,
        int totalDataRecordCount,
        int? headerSourceRecordIndex,
        bool isRowLimited)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        if (totalDataRecordCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalDataRecordCount),
                totalDataRecordCount,
                "Total data-record count cannot be negative.");
        }

        if (headerSourceRecordIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(headerSourceRecordIndex),
                headerSourceRecordIndex,
                "Header source-record index cannot be negative.");
        }

        var copiedColumns = columns.ToArray();
        var copiedRows = rows.ToArray();

        if (copiedColumns.Any(static column => column is null))
        {
            throw new ArgumentException("Columns cannot contain null values.", nameof(columns));
        }

        if (copiedRows.Any(static row => row is null))
        {
            throw new ArgumentException("Rows cannot contain null values.", nameof(rows));
        }

        if (copiedRows.Any(row => row.Values.Count != copiedColumns.Length))
        {
            throw new ArgumentException(
                "Every projected row must contain exactly one value per projected column.",
                nameof(rows));
        }

        if (copiedRows.Length > totalDataRecordCount)
        {
            throw new ArgumentException(
                "Displayed row count cannot exceed total data-record count.",
                nameof(rows));
        }

        Columns = Array.AsReadOnly(copiedColumns);
        Rows = Array.AsReadOnly(copiedRows);
        TotalDataRecordCount = totalDataRecordCount;
        HeaderSourceRecordIndex = headerSourceRecordIndex;
        IsRowLimited = isRowLimited;
    }

    public ReadOnlyCollection<CsvTableColumn> Columns { get; }

    public ReadOnlyCollection<CsvTableRow> Rows { get; }

    public int TotalDataRecordCount { get; }

    public int? HeaderSourceRecordIndex { get; }

    public bool IsRowLimited { get; }

    public int ColumnCount => Columns.Count;

    public int DisplayedRowCount => Rows.Count;
}