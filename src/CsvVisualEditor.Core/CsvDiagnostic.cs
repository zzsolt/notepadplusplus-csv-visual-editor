namespace CsvVisualEditor.Core;

/// <summary>
/// Structured, non-sensitive information about malformed or ambiguous CSV input.
/// </summary>
public sealed record CsvDiagnostic
{
    public CsvDiagnostic(
        CsvDiagnosticSeverity severity,
        string code,
        string message,
        int characterOffset,
        int? recordIndex = null,
        int? actualFieldCount = null, int? expectedFieldCount = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Diagnostic code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Diagnostic message is required.", nameof(message));
        }

        if (characterOffset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(characterOffset),
                characterOffset,
                "Character offset cannot be negative.");
        }

        if (recordIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recordIndex),
                recordIndex,
                "Record index cannot be negative.");
        }

        Severity = severity;
        Code = code;
        Message = message;
        CharacterOffset = characterOffset;
        RecordIndex = recordIndex;
        ActualFieldCount = actualFieldCount;
        ExpectedFieldCount = expectedFieldCount;
    }

    public CsvDiagnosticSeverity Severity { get; }

    public string Code { get; }

    public string Message { get; }

    public int? ActualFieldCount { get; }

    public int? ExpectedFieldCount { get; }

    public int CharacterOffset { get; }

    public int? RecordIndex { get; }
}

public enum CsvDiagnosticSeverity
{
    Information,
    Warning,
    Error
}

public static class CsvDiagnosticCodes
{
    public const string UnexpectedQuoteInUnquotedField = "CSV001";
    public const string UnexpectedCharacterAfterClosingQuote = "CSV002";
    public const string UnterminatedQuotedField = "CSV003";
    public const string InconsistentFieldCount = "CSV004";
    public const string LeadingBomRemoved = "CSV005";
    public const string LowDelimiterConfidence = "CSVD001";
    public const string NoReliableDelimiter = "CSVD002";
    public const string AmbiguousDelimiter = "CSVD003";
}
