namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;

/// <summary>
/// Automatic delimiter suggestion together with confidence, diagnostics, and all candidate scores.
/// </summary>
public sealed record CsvDialectDetectionResult
{
    public CsvDialectDetectionResult(
        CsvDialect? suggestedDialect,
        CsvDelimiterConfidence confidence,
        IEnumerable<CsvDelimiterCandidateScore> candidates,
        IEnumerable<CsvDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(diagnostics);

        SuggestedDialect = suggestedDialect;
        Confidence = confidence;
        Candidates = Array.AsReadOnly(candidates.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public CsvDialect? SuggestedDialect { get; }

    public CsvDelimiterConfidence Confidence { get; }

    public ReadOnlyCollection<CsvDelimiterCandidateScore> Candidates { get; }

    public IReadOnlyList<CsvDiagnostic> Diagnostics { get; }

    public bool HasSuggestion => SuggestedDialect is not null;

    public bool IsReliable => Confidence is CsvDelimiterConfidence.Medium or CsvDelimiterConfidence.High;
}

public enum CsvDelimiterConfidence
{
    None,
    Low,
    Medium,
    High
}
