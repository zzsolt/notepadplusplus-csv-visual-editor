namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;

/// <summary>
/// Result of parsing one decoded editor snapshot with an explicit CSV dialect.
/// </summary>
public sealed record CsvParseResult
{
    public CsvParseResult(
        CsvDialect dialect,
        IEnumerable<CsvRecord> records,
        IEnumerable<CsvDiagnostic> diagnostics,
        int? expectedFieldCount)
    {
        ArgumentNullException.ThrowIfNull(dialect);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(diagnostics);

        Dialect = dialect;
        Records = Array.AsReadOnly(records.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        ExpectedFieldCount = expectedFieldCount;
    }

    public CsvDialect Dialect { get; }

    public ReadOnlyCollection<CsvRecord> Records { get; }

    public ReadOnlyCollection<CsvDiagnostic> Diagnostics { get; }

    public int? ExpectedFieldCount { get; }

    public bool HasErrors =>
        Diagnostics.Any(static diagnostic => diagnostic.Severity == CsvDiagnosticSeverity.Error);
}
