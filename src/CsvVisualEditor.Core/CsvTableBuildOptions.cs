namespace CsvVisualEditor.Core;

/// <summary>
/// Defines the explicit user choices and visual limits used to build a table from decoded CSV text.
/// </summary>
public sealed record CsvTableBuildOptions
{
    public static CsvTableBuildOptions Default { get; } = new();

    public char? DelimiterOverride { get; init; }

    public CsvHeaderMode HeaderMode { get; init; } = CsvHeaderMode.FirstRecord;

    public int MaximumRows { get; init; } = 10_000;

    public int MaximumColumns { get; init; } = 512;

    internal void Validate()
    {
        if (DelimiterOverride is char delimiter && !CsvDialect.SupportedDelimiters.Contains(delimiter))
        {
            throw new ArgumentOutOfRangeException(
                nameof(DelimiterOverride),
                delimiter,
                "Supported delimiter overrides are comma, semicolon, and tab.");
        }

        new CsvTableProjectionOptions
        {
            HeaderMode = HeaderMode,
            MaximumRows = MaximumRows,
            MaximumColumns = MaximumColumns
        }.Validate();
    }
}