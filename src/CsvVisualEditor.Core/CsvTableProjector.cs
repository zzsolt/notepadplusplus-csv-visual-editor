namespace CsvVisualEditor.Core;

using System.Text;

/// <summary>
/// Converts immutable parser output into a bounded rectangular view without changing source records.
/// </summary>
public static class CsvTableProjector
{
    public static CsvTableProjection Create(
        CsvParseResult parseResult,
        CsvTableProjectionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        options ??= CsvTableProjectionOptions.Default;
        options.Validate();

        var headerRecord = options.HeaderMode == CsvHeaderMode.FirstRecord && parseResult.Records.Count > 0
            ? parseResult.Records[0]
            : null;
        var dataRecordStart = headerRecord is null ? 0 : 1;
        var totalDataRecordCount = parseResult.Records.Count - dataRecordStart;
        var columnCount = parseResult.Records.Count == 0
            ? 0
            : parseResult.Records.Max(static record => record.FieldCount);

        if (columnCount > options.MaximumColumns)
        {
            throw new InvalidOperationException(
                $"The parsed document contains {columnCount:N0} columns, which exceeds the " +
                $"current visual-table limit of {options.MaximumColumns:N0}. No columns were hidden.");
        }

        if (columnCount > options.MaximumCells)
        {
            throw new InvalidOperationException(
                $"One visual row would require {columnCount:N0} cells, which exceeds the current " +
                $"aggregate cell limit of {options.MaximumCells:N0}. No columns were hidden.");
        }

        var columnNames = CreateColumnNames(headerRecord, columnCount);
        var columns = columnNames
            .Select(static (name, index) => new CsvTableColumn(index, name))
            .ToArray();

        var rowsAllowedByCellBudget = columnCount == 0
            ? options.MaximumRows
            : options.MaximumCells / columnCount;
        var rowsToDisplay = Math.Min(
            totalDataRecordCount,
            Math.Min(options.MaximumRows, rowsAllowedByCellBudget));
        var rows = new CsvTableRow[rowsToDisplay];
        for (var displayIndex = 0; displayIndex < rowsToDisplay; displayIndex++)
        {
            var sourceRecord = parseResult.Records[dataRecordStart + displayIndex];
            var values = new string[columnCount];
            Array.Fill(values, string.Empty);

            for (var columnIndex = 0; columnIndex < sourceRecord.Cells.Count; columnIndex++)
            {
                values[columnIndex] = sourceRecord.Cells[columnIndex].Value;
            }

            rows[displayIndex] = new CsvTableRow(
                sourceRecord.Index,
                values,
                sourceRecord.SourceSpan);
        }

        return new CsvTableProjection(
            columns,
            rows,
            totalDataRecordCount,
            headerRecord?.Index,
            isRowLimited: totalDataRecordCount > rowsToDisplay);
    }

    private static IReadOnlyList<string> CreateColumnNames(CsvRecord? headerRecord, int columnCount)
    {
        var fallbackNames = TableColumnNameGenerator.Create(columnCount);
        if (headerRecord is null)
        {
            return fallbackNames;
        }

        var names = new string[columnCount];
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < columnCount; index++)
        {
            var rawName = index < headerRecord.Cells.Count
                ? headerRecord.Cells[index].Value
                : string.Empty;
            var baseName = NormalizeHeader(rawName);
            if (baseName.Length == 0)
            {
                baseName = fallbackNames[index];
            }

            names[index] = MakeUnique(baseName, usedNames);
        }

        return names;
    }

    private static string NormalizeHeader(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var previousWasWhitespace = false;
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString().Trim();
    }

    private static string MakeUnique(string baseName, ISet<string> usedNames)
    {
        if (usedNames.Add(baseName))
        {
            return baseName;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{baseName} ({suffix})";
            if (usedNames.Add(candidate))
            {
                return candidate;
            }
        }
    }
}