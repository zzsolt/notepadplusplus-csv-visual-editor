namespace CsvVisualEditor.Core;

/// <summary>
/// Controls parser behaviors that are independent of the Notepad++ host.
/// </summary>
public sealed record CsvParseOptions
{
    public static CsvParseOptions Default { get; } = new();

    public bool RemoveLeadingBom { get; init; } = true;

    public bool ReportInconsistentFieldCounts { get; init; } = true;
}
