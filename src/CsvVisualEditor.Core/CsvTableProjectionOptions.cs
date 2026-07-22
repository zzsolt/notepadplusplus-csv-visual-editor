namespace CsvVisualEditor.Core;

/// <summary>
/// Defines explicit header interpretation and bounded visual-table limits.
/// </summary>
public sealed record CsvTableProjectionOptions
{
    public static CsvTableProjectionOptions Default { get; } = new();

    public CsvHeaderMode HeaderMode { get; init; } = CsvHeaderMode.FirstRecord;

    public int MaximumRows { get; init; } = 10_000;

    public int MaximumColumns { get; init; } = 512;

    internal void Validate()
    {
        if (HeaderMode is not CsvHeaderMode.FirstRecord and not CsvHeaderMode.NoHeader)
        {
            throw new ArgumentOutOfRangeException(
                nameof(HeaderMode),
                HeaderMode,
                "The visual table requires an explicit FirstRecord or NoHeader mode.");
        }

        if (MaximumRows <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumRows),
                MaximumRows,
                "At least one data row must be allowed for display.");
        }

        if (MaximumColumns <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumColumns),
                MaximumColumns,
                "At least one column must be allowed for display.");
        }
    }
}