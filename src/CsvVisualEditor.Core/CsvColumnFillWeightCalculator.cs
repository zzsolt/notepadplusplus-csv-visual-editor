namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;

/// <summary>
/// Calculates bounded relative DataGridView fill weights from projected headers and sampled cell content.
/// </summary>
public static class CsvColumnFillWeightCalculator
{
    public const int DefaultMaximumSampledRows = 256;
    public const float MinimumFillWeight = 8f;
    public const float MaximumFillWeight = 60f;

    public static ReadOnlyCollection<float> Calculate(
        CsvTableProjection projection,
        int maximumSampledRows = DefaultMaximumSampledRows)
    {
        ArgumentNullException.ThrowIfNull(projection);

        if (maximumSampledRows <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumSampledRows),
                maximumSampledRows,
                "The maximum sampled-row count must be positive.");
        }

        var extents = projection.Columns
            .Select(static column => MeasureLongestLine(column.Name))
            .ToArray();

        foreach (var rowIndex in SelectSampleIndexes(
                     projection.DisplayedRowCount,
                     maximumSampledRows))
        {
            var row = projection.Rows[rowIndex];
            for (var columnIndex = 0; columnIndex < extents.Length; columnIndex++)
            {
                extents[columnIndex] = Math.Max(
                    extents[columnIndex],
                    MeasureLongestLine(row.Values[columnIndex]));
            }
        }

        var weights = extents
            .Select(static extent => Math.Clamp(
                (float)extent,
                MinimumFillWeight,
                MaximumFillWeight))
            .ToArray();

        return Array.AsReadOnly(weights);
    }

    private static IEnumerable<int> SelectSampleIndexes(
        int rowCount,
        int maximumSampledRows)
    {
        if (rowCount <= maximumSampledRows)
        {
            return Enumerable.Range(0, rowCount);
        }

        if (maximumSampledRows == 1)
        {
            return [0];
        }

        var indexes = new int[maximumSampledRows];
        var lastIndex = rowCount - 1;
        var denominator = maximumSampledRows - 1d;

        for (var sampleIndex = 0; sampleIndex < maximumSampledRows; sampleIndex++)
        {
            indexes[sampleIndex] = (int)Math.Round(
                sampleIndex * lastIndex / denominator,
                MidpointRounding.AwayFromZero);
        }

        return indexes;
    }

    private static int MeasureLongestLine(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var longest = 0;
        var current = 0;

        foreach (var character in value)
        {
            if (character is '\r' or '\n')
            {
                longest = Math.Max(longest, current);
                current = 0;
                continue;
            }

            current++;
        }

        return Math.Max(longest, current);
    }
}
