namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;

/// <summary>
/// One display-only row that retains the originating logical-record identity.
/// </summary>
public sealed record CsvTableRow
{
    public CsvTableRow(
        int sourceRecordIndex,
        IEnumerable<string> values,
        CsvSourceSpan sourceSpan)
    {
        if (sourceRecordIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceRecordIndex),
                sourceRecordIndex,
                "Source record index cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(values);

        var copiedValues = values.ToArray();
        if (copiedValues.Any(static value => value is null))
        {
            throw new ArgumentException("Row values cannot contain null entries.", nameof(values));
        }

        SourceRecordIndex = sourceRecordIndex;
        Values = Array.AsReadOnly(copiedValues);
        SourceSpan = sourceSpan;
    }

    public int SourceRecordIndex { get; }

    public ReadOnlyCollection<string> Values { get; }

    public CsvSourceSpan SourceSpan { get; }
}