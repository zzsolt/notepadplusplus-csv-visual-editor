namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;
using System.Text;

/// <summary>
/// Immutable rectangular tab/newline clipboard payload used by multi-cell copy and paste.
/// Clipboard text is intentionally not parsed as CSV: spreadsheet applications expose a
/// plain tab-separated text representation for rectangular cell ranges.
/// </summary>
public sealed class CsvClipboardMatrix
{
    private readonly string[][] _rows;

    private CsvClipboardMatrix(string[][] rows, int columnCount)
    {
        _rows = rows;
        RowCount = rows.Length;
        ColumnCount = columnCount;
        Rows = Array.AsReadOnly(
            rows.Select(static row => (IReadOnlyList<string>)Array.AsReadOnly(row)).ToArray());
    }

    public int RowCount { get; }

    public int ColumnCount { get; }

    public bool IsSingleCell => RowCount == 1 && ColumnCount == 1;

    public ReadOnlyCollection<IReadOnlyList<string>> Rows { get; }

    public string this[int rowIndex, int columnIndex]
    {
        get
        {
            ValidateIndex(rowIndex, RowCount, nameof(rowIndex));
            ValidateIndex(columnIndex, ColumnCount, nameof(columnIndex));
            return _rows[rowIndex][columnIndex];
        }
    }

    public static CsvClipboardMatrix Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var physicalRows = SplitRows(text);
        var parsedRows = new string[physicalRows.Count][];
        var maximumColumns = 0;
        for (var rowIndex = 0; rowIndex < physicalRows.Count; rowIndex++)
        {
            var cells = physicalRows[rowIndex].Split('\t', StringSplitOptions.None);
            parsedRows[rowIndex] = cells;
            maximumColumns = Math.Max(maximumColumns, cells.Length);
        }

        maximumColumns = Math.Max(maximumColumns, 1);
        for (var rowIndex = 0; rowIndex < parsedRows.Length; rowIndex++)
        {
            if (parsedRows[rowIndex].Length == maximumColumns)
            {
                continue;
            }

            Array.Resize(ref parsedRows[rowIndex], maximumColumns);
            for (var columnIndex = 0; columnIndex < maximumColumns; columnIndex++)
            {
                parsedRows[rowIndex][columnIndex] ??= string.Empty;
            }
        }

        return new CsvClipboardMatrix(parsedRows, maximumColumns);
    }

    public string ToTabSeparatedText()
    {
        var builder = new StringBuilder();
        for (var rowIndex = 0; rowIndex < RowCount; rowIndex++)
        {
            if (rowIndex > 0)
            {
                builder.Append("\r\n");
            }

            for (var columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
            {
                if (columnIndex > 0)
                {
                    builder.Append('\t');
                }

                builder.Append(_rows[rowIndex][columnIndex]);
            }
        }

        return builder.ToString();
    }

    private static List<string> SplitRows(string text)
    {
        var rows = new List<string>();
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\r' && text[index] != '\n')
            {
                continue;
            }

            rows.Add(text[start..index]);
            if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
            }

            start = index + 1;
        }

        rows.Add(text[start..]);

        // Spreadsheet clipboard formats commonly terminate a copied rectangle with one
        // newline. That terminator is not an extra empty data row.
        if (rows.Count > 1 && rows[^1].Length == 0 && EndsWithLineBreak(text))
        {
            rows.RemoveAt(rows.Count - 1);
        }

        if (rows.Count == 0)
        {
            rows.Add(string.Empty);
        }

        return rows;
    }

    private static bool EndsWithLineBreak(string text) =>
        text.EndsWith('\r') || text.EndsWith('\n');

    private static void ValidateIndex(int value, int upperBound, string parameterName)
    {
        if (value < 0 || value >= upperBound)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Clipboard matrix index is outside the rectangle.");
        }
    }
}
