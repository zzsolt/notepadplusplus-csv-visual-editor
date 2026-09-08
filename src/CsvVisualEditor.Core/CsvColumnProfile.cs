namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;

public sealed record CsvValueFrequency(string Value, int Count);

/// <summary>Read-only exact statistics for one column of the supplied visible view.</summary>
public sealed class CsvColumnProfile
{
    private CsvColumnProfile(int rowCount, int emptyCount, int whitespaceCount,
        int numericCount, int distinctCount, int repeatedValues, int maximumLength,
        decimal? minimum, decimal? maximum, CsvValueFrequency[] frequent)
    {
        RowCount = rowCount;
        EmptyCount = emptyCount;
        WhitespaceOnlyCount = whitespaceCount;
        NumericCount = numericCount;
        DistinctCount = distinctCount;
        RepeatedValueCount = repeatedValues;
        MaximumLength = maximumLength;
        Minimum = minimum;
        Maximum = maximum;
        MostFrequent = Array.AsReadOnly(frequent);
    }

    public int RowCount { get; }
    public int EmptyCount { get; }
    public int WhitespaceOnlyCount { get; }
    public int NumericCount { get; }
    public int NonNumericCount => RowCount - NumericCount;
    public int DistinctCount { get; }
    public int RepeatedValueCount { get; }
    public int DuplicateOccurrenceCount => RowCount - DistinctCount;
    public int MaximumLength { get; }
    public decimal? Minimum { get; }
    public decimal? Maximum { get; }
    public ReadOnlyCollection<CsvValueFrequency> MostFrequent { get; }

    public static CsvColumnProfile Build(CsvTableViewResult view, int columnIndex, int columnCount, int sampleLimit = 20)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (columnCount < 0) throw new ArgumentOutOfRangeException(nameof(columnCount));
        if (columnIndex < 0 || columnIndex >= columnCount) throw new ArgumentOutOfRangeException(nameof(columnIndex));
        if (sampleLimit < 0 || sampleLimit > 100) throw new ArgumentOutOfRangeException(nameof(sampleLimit));
        var frequencies = new Dictionary<string, int>(StringComparer.Ordinal);
        var empty = 0;
        var whitespace = 0;
        var numeric = 0;
        var maximumLength = 0;
        decimal? minimum = null, maximum = null;
        foreach (var row in view.Rows)
        {
            if (columnIndex >= row.Values.Count) throw new ArgumentOutOfRangeException(nameof(columnIndex));
            var value = row.Values[columnIndex];
            if (value.Length == 0) empty++;
            else if (string.IsNullOrWhiteSpace(value)) whitespace++;
            maximumLength = Math.Max(maximumLength, value.Length);
            frequencies.TryGetValue(value, out var count);
            frequencies[value] = count + 1;
            if (CsvNumericValue.TryParse(value, out var number))
            {
                numeric++;
                minimum = minimum.HasValue ? Math.Min(minimum.Value, number) : number;
                maximum = maximum.HasValue ? Math.Max(maximum.Value, number) : number;
            }
        }
        // Exact ordinal value order breaks frequency ties deterministically. Values
        // are existing immutable strings, not copied into persistent settings/logs.
        var sample = frequencies.OrderByDescending(static entry => entry.Value)
            .ThenBy(static entry => entry.Key, StringComparer.Ordinal)
            .Take(sampleLimit).Select(static entry => new CsvValueFrequency(entry.Key, entry.Value)).ToArray();
        return new(view.Rows.Count, empty, whitespace, numeric, frequencies.Count,
            frequencies.Count(static entry => entry.Value > 1), maximumLength, minimum, maximum, sample);
    }
}
