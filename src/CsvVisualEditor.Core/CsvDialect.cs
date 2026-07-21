namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;

/// <summary>
/// Defines the structural characters and header interpretation used to parse CSV text.
/// </summary>
public sealed record CsvDialect
{
    private static readonly ReadOnlyCollection<char> Delimiters =
        Array.AsReadOnly([',', ';', '\t']);

    private CsvDialect(char delimiter, char quote, CsvHeaderMode headerMode)
    {
        Delimiter = delimiter;
        Quote = quote;
        HeaderMode = headerMode;
    }

    public char Delimiter { get; }

    public char Quote { get; }

    public CsvHeaderMode HeaderMode { get; }

    public static IReadOnlyList<char> SupportedDelimiters => Delimiters;

    public static CsvDialect Create(
        char delimiter,
        char quote = '"',
        CsvHeaderMode headerMode = CsvHeaderMode.Unknown)
    {
        if (!Delimiters.Contains(delimiter))
        {
            throw new ArgumentOutOfRangeException(
                nameof(delimiter),
                delimiter,
                "Supported delimiters are comma, semicolon, and tab.");
        }

        if (quote is '\r' or '\n' || quote == delimiter)
        {
            throw new ArgumentException(
                "The quote character must differ from the delimiter and cannot be a line break.",
                nameof(quote));
        }

        return new CsvDialect(delimiter, quote, headerMode);
    }

    public string DelimiterDisplayName => Delimiter switch
    {
        ',' => "comma",
        ';' => "semicolon",
        '\t' => "tab",
        _ => $"U+{(int)Delimiter:X4}"
    };
}

public enum CsvHeaderMode
{
    Unknown,
    FirstRecord,
    NoHeader
}
