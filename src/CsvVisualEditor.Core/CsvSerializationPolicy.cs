namespace CsvVisualEditor.Core;

/// <summary>
/// Structural policy used when a CSV record must be regenerated after an edit.
/// Unchanged records can still retain their original raw source representation.
/// </summary>
public sealed record CsvSerializationPolicy
{
    private const string DefaultNewLine = "\r\n";

    private CsvSerializationPolicy(
        char delimiter,
        char quote,
        string newLine,
        bool hasTerminalNewLine,
        bool hasLeadingBom)
    {
        Delimiter = delimiter;
        Quote = quote;
        NewLine = newLine;
        HasTerminalNewLine = hasTerminalNewLine;
        HasLeadingBom = hasLeadingBom;
    }

    public char Delimiter { get; }

    public char Quote { get; }

    public string NewLine { get; }

    public bool HasTerminalNewLine { get; }

    public bool HasLeadingBom { get; }

    public static CsvSerializationPolicy Create(
        CsvDialect dialect,
        string newLine,
        bool hasTerminalNewLine,
        bool hasLeadingBom = false)
    {
        ArgumentNullException.ThrowIfNull(dialect);
        ValidateNewLine(newLine);

        return new CsvSerializationPolicy(
            dialect.Delimiter,
            dialect.Quote,
            newLine,
            hasTerminalNewLine,
            hasLeadingBom);
    }

    public static CsvSerializationPolicy Detect(
        string sourceText,
        CsvDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentNullException.ThrowIfNull(dialect);

        return new CsvSerializationPolicy(
            dialect.Delimiter,
            dialect.Quote,
            FindFirstRecordSeparator(sourceText, dialect) ?? DefaultNewLine,
            EndsWithLineBreak(sourceText),
            sourceText.StartsWith('\uFEFF'));
    }

    private static string? FindFirstRecordSeparator(
        string sourceText,
        CsvDialect dialect)
    {
        var fieldAtStart = true;
        var inQuotedField = false;
        var afterClosingQuote = false;

        for (var index = 0; index < sourceText.Length; index++)
        {
            var current = sourceText[index];

            if (inQuotedField)
            {
                if (current != dialect.Quote)
                {
                    continue;
                }

                if (index + 1 < sourceText.Length &&
                    sourceText[index + 1] == dialect.Quote)
                {
                    index++;
                    continue;
                }

                inQuotedField = false;
                afterClosingQuote = true;
                continue;
            }

            if (afterClosingQuote)
            {
                if (current == dialect.Delimiter)
                {
                    fieldAtStart = true;
                    afterClosingQuote = false;
                    continue;
                }

                if (current is '\r' or '\n')
                {
                    return ReadLineBreak(sourceText, index);
                }

                fieldAtStart = false;
                afterClosingQuote = false;
                continue;
            }

            if (fieldAtStart && current == dialect.Quote)
            {
                fieldAtStart = false;
                inQuotedField = true;
                continue;
            }

            if (current == dialect.Delimiter)
            {
                fieldAtStart = true;
                continue;
            }

            if (current is '\r' or '\n')
            {
                return ReadLineBreak(sourceText, index);
            }

            fieldAtStart = false;
        }

        return null;
    }

    private static string ReadLineBreak(string text, int index)
    {
        return text[index] == '\r' &&
               index + 1 < text.Length &&
               text[index + 1] == '\n'
            ? "\r\n"
            : text[index].ToString();
    }

    private static bool EndsWithLineBreak(string value)
    {
        return value.EndsWith("\r\n", StringComparison.Ordinal) ||
               value.EndsWith('\r') ||
               value.EndsWith('\n');
    }

    private static void ValidateNewLine(string newLine)
    {
        ArgumentNullException.ThrowIfNull(newLine);

        if (newLine is not "\r\n" and not "\n" and not "\r")
        {
            throw new ArgumentException(
                "The record separator must be CRLF, LF, or CR.",
                nameof(newLine));
        }
    }
}
