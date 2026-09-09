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
    public const int MaximumTextCharacters = 16 * 1024 * 1024;
    private const int MaximumRows = 10_000;
    private const int MaximumColumns = 512;
    private const int MaximumCells = 250_000;

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
        if (text.Length > MaximumTextCharacters)
            throw CsvErrorDetails.With(new FormatException("The clipboard text exceeds the safe 16 Mi UTF-16-character limit."), CsvUserError.ClipboardTooLarge);
        ValidateControlCharacters(text);

        var physicalRows = SplitRows(text);
        if (physicalRows.Count > MaximumRows)
        {
            throw CsvErrorDetails.With(new FormatException("The clipboard contains more rows than the editor can paste safely."), CsvUserError.ClipboardTooManyRows);
        }

        var parsedRows = new string[physicalRows.Count][];
        var expectedColumns = -1;
        for (var rowIndex = 0; rowIndex < physicalRows.Count; rowIndex++)
        {
            var cells = physicalRows[rowIndex].Split('\t', MaximumColumns + 1, StringSplitOptions.None);
            if (cells.Length > MaximumColumns)
            {
                throw CsvErrorDetails.With(new FormatException("The clipboard contains more columns than the editor can paste safely."), CsvUserError.ClipboardTooManyColumns);
            }

            expectedColumns = expectedColumns < 0 ? cells.Length : expectedColumns;
            if (cells.Length != expectedColumns)
            {
                throw CsvErrorDetails.With(new FormatException("Clipboard rows must form one rectangular cell matrix."), CsvUserError.ClipboardNotRectangular);
            }

            if ((long)(rowIndex + 1) * expectedColumns > MaximumCells)
            {
                throw CsvErrorDetails.With(new FormatException("The clipboard cell matrix exceeds the safe paste limit."), CsvUserError.ClipboardTooManyCells);
            }

            parsedRows[rowIndex] = cells;
        }

        expectedColumns = Math.Max(expectedColumns, 1);
        return new CsvClipboardMatrix(parsedRows, expectedColumns);
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

            if (rows.Count >= MaximumRows)
                throw CsvErrorDetails.With(new FormatException("The clipboard contains more rows than the editor can paste safely."), CsvUserError.ClipboardTooManyRows);
            rows.Add(text[start..index]);
            if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
            }

            start = index + 1;
        }

        // One terminal line break terminates the rectangle; it is not a new row.
        // Enforce the row limit before allocating the next substring, including tail.
        if (start < text.Length)
        {
            if (rows.Count >= MaximumRows)
                throw CsvErrorDetails.With(new FormatException("The clipboard contains more rows than the editor can paste safely."), CsvUserError.ClipboardTooManyRows);
            rows.Add(text[start..]);
        }
        else if (rows.Count == 0) rows.Add(string.Empty);

        return rows;
    }

    private static void ValidateControlCharacters(string text)
    {
        foreach (var character in text)
        {
            if (character == '\t' || character == '\r' || character == '\n')
            {
                continue;
            }

            if (character == '\0' || char.IsControl(character))
            {
                throw CsvErrorDetails.With(new FormatException("The clipboard contains an unsupported control character."), CsvUserError.ClipboardControlCharacter);
            }
        }
    }

    private static void ValidateIndex(int value, int upperBound, string parameterName)
    {
        if (value < 0 || value >= upperBound)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Clipboard matrix index is outside the rectangle.");
        }
    }
}
