namespace CsvVisualEditor.Core;

/// <summary>
/// One decoded CSV field and its raw source span in the active editor snapshot.
/// </summary>
public sealed record CsvCell
{
    public CsvCell(string value, CsvSourceSpan sourceSpan, bool isQuoted)
    {
        ArgumentNullException.ThrowIfNull(value);

        Value = value;
        SourceSpan = sourceSpan;
        IsQuoted = isQuoted;
    }

    public string Value { get; }

    public CsvSourceSpan SourceSpan { get; }

    public bool IsQuoted { get; }
}
