namespace CsvVisualEditor.Core;

/// <summary>
/// Generates stable fallback column names for CSV data that has no header row.
/// </summary>
public static class TableColumnNameGenerator
{
    /// <summary>
    /// Creates the names <c>Column 1</c> through <c>Column N</c>.
    /// </summary>
    /// <param name="columnCount">Number of names to create.</param>
    /// <returns>A read-only list containing exactly <paramref name="columnCount"/> names.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="columnCount"/> is negative.
    /// </exception>
    public static IReadOnlyList<string> Create(int columnCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columnCount);

        var names = new string[columnCount];
        for (var index = 0; index < columnCount; index++)
        {
            names[index] = $"Column {index + 1}";
        }

        return names;
    }
}
