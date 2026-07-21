namespace CsvVisualEditor.Core;

/// <summary>
/// Explainable score for one delimiter candidate.
/// </summary>
public sealed record CsvDelimiterCandidateScore
{
    public CsvDelimiterCandidateScore(
        char delimiter,
        int score,
        int sampledRecordCount,
        int modeFieldCount,
        int consistentRecordCount,
        int multiFieldRecordCount,
        int delimiterCount,
        int numericAdjacentDelimiterCount,
        int parseErrorCount)
    {
        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, "Score must be between 0 and 100.");
        }

        Delimiter = delimiter;
        Score = score;
        SampledRecordCount = sampledRecordCount;
        ModeFieldCount = modeFieldCount;
        ConsistentRecordCount = consistentRecordCount;
        MultiFieldRecordCount = multiFieldRecordCount;
        DelimiterCount = delimiterCount;
        NumericAdjacentDelimiterCount = numericAdjacentDelimiterCount;
        ParseErrorCount = parseErrorCount;
    }

    public char Delimiter { get; }

    public int Score { get; }

    public int SampledRecordCount { get; }

    public int ModeFieldCount { get; }

    public int ConsistentRecordCount { get; }

    public int MultiFieldRecordCount { get; }

    public int DelimiterCount { get; }

    public int NumericAdjacentDelimiterCount { get; }

    public int ParseErrorCount { get; }
}
