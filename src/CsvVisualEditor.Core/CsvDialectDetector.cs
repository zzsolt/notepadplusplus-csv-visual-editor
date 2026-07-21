namespace CsvVisualEditor.Core;

/// <summary>
/// Scores comma, semicolon, and tab candidates by logical-record field-count consistency.
/// </summary>
public static class CsvDialectDetector
{
    public static CsvDialectDetectionResult Detect(
        string text,
        CsvDialectDetectionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        options ??= CsvDialectDetectionOptions.Default;
        options.Validate();

        var candidates = CsvDialect.SupportedDelimiters
            .Select(delimiter => ScoreCandidate(text, delimiter, options))
            .OrderByDescending(static candidate => candidate.Score)
            .ThenBy(static candidate => CandidateOrder(candidate.Delimiter))
            .ToArray();

        var diagnostics = new List<CsvDiagnostic>();
        var best = candidates[0];
        var second = candidates[1];
        var scoreGap = best.Score - second.Score;

        if (best.Score < options.MinimumSuggestionScore)
        {
            diagnostics.Add(new CsvDiagnostic(
                CsvDiagnosticSeverity.Warning,
                CsvDiagnosticCodes.NoReliableDelimiter,
                "No supported delimiter produced a sufficiently reliable multi-field structure.",
                characterOffset: 0));

            return new CsvDialectDetectionResult(
                suggestedDialect: null,
                CsvDelimiterConfidence.None,
                candidates,
                diagnostics);
        }

        var confidence = DetermineConfidence(best, scoreGap);
        if (scoreGap < 10)
        {
            diagnostics.Add(new CsvDiagnostic(
                CsvDiagnosticSeverity.Warning,
                CsvDiagnosticCodes.AmbiguousDelimiter,
                "The two highest delimiter scores are close; a user override should remain available.",
                characterOffset: 0));
        }

        if (confidence == CsvDelimiterConfidence.Low)
        {
            diagnostics.Add(new CsvDiagnostic(
                CsvDiagnosticSeverity.Warning,
                CsvDiagnosticCodes.LowDelimiterConfidence,
                "A delimiter suggestion is available, but the sampled structure is not strong enough for automatic trust.",
                characterOffset: 0));
        }

        return new CsvDialectDetectionResult(
            CsvDialect.Create(best.Delimiter),
            confidence,
            candidates,
            diagnostics);
    }

    private static CsvDelimiterCandidateScore ScoreCandidate(
        string text,
        char delimiter,
        CsvDialectDetectionOptions options)
    {
        var dialect = CsvDialect.Create(delimiter);
        var parseResult = CsvParser.ParseSample(
            text,
            dialect,
            options.MaximumLogicalRecords,
            options.MaximumSampleCharacters);

        var sampledRecords = parseResult.Records
            .Where(static record => !record.IsBlank)
            .ToArray();

        var sampledRecordCount = sampledRecords.Length;
        var modeFieldCount = 0;
        var consistentRecordCount = 0;
        var multiFieldRecordCount = 0;

        if (sampledRecordCount > 0)
        {
            var modeGroup = sampledRecords
                .GroupBy(static record => record.FieldCount)
                .OrderByDescending(static group => group.Count())
                .ThenBy(static group => group.Min(record => record.Index))
                .First();

            modeFieldCount = modeGroup.Key;
            consistentRecordCount = modeGroup.Count();
            multiFieldRecordCount = sampledRecords.Count(static record => record.FieldCount > 1);
        }

        var sampledCharacterEnd = parseResult.Records.Count > 0
            ? parseResult.Records[^1].SourceSpan.End
            : Math.Min(text.Length, options.MaximumSampleCharacters);
        var delimiterStatistics = CountDelimitersOutsideQuotes(
            text,
            delimiter,
            dialect.Quote,
            sampledCharacterEnd);
        var parseErrorCount = parseResult.Diagnostics.Count(
            static diagnostic => diagnostic.Severity == CsvDiagnosticSeverity.Error);

        var score = CalculateScore(
            delimiter,
            sampledRecordCount,
            modeFieldCount,
            consistentRecordCount,
            multiFieldRecordCount,
            delimiterStatistics.DelimiterCount,
            delimiterStatistics.NumericAdjacentCount,
            parseErrorCount);

        return new CsvDelimiterCandidateScore(
            delimiter,
            score,
            sampledRecordCount,
            modeFieldCount,
            consistentRecordCount,
            multiFieldRecordCount,
            delimiterStatistics.DelimiterCount,
            delimiterStatistics.NumericAdjacentCount,
            parseErrorCount);
    }

    private static int CalculateScore(
        char delimiter,
        int sampledRecordCount,
        int modeFieldCount,
        int consistentRecordCount,
        int multiFieldRecordCount,
        int delimiterCount,
        int numericAdjacentDelimiterCount,
        int parseErrorCount)
    {
        if (sampledRecordCount == 0 || modeFieldCount <= 1 || multiFieldRecordCount == 0)
        {
            return 0;
        }

        var consistencyRatio = consistentRecordCount / (double)sampledRecordCount;
        var inconsistentRecordCount = sampledRecordCount - consistentRecordCount;

        var score = 20;
        score += (int)Math.Round(45 * consistencyRatio, MidpointRounding.AwayFromZero);
        score += Math.Min(20, multiFieldRecordCount * 4);
        score += Math.Min(15, delimiterCount);
        score -= inconsistentRecordCount * 8;
        score -= parseErrorCount * 15;

        if (delimiter == ',' &&
            delimiterCount > 0 &&
            numericAdjacentDelimiterCount * 4 >= delimiterCount * 3 &&
            modeFieldCount <= 2)
        {
            score -= 35;
        }

        if (sampledRecordCount == 1)
        {
            score = Math.Min(score, 45);
        }
        else if (sampledRecordCount == 2)
        {
            score = Math.Min(score, 70);
        }

        return Math.Clamp(score, 0, 100);
    }

    private static CsvDelimiterConfidence DetermineConfidence(
        CsvDelimiterCandidateScore best,
        int scoreGap)
    {
        if (best.Score >= 80 && scoreGap >= 15 && best.SampledRecordCount >= 3)
        {
            return CsvDelimiterConfidence.High;
        }

        if (best.Score >= 60 && scoreGap >= 10 && best.SampledRecordCount >= 2)
        {
            return CsvDelimiterConfidence.Medium;
        }

        return CsvDelimiterConfidence.Low;
    }

    private static (int DelimiterCount, int NumericAdjacentCount) CountDelimitersOutsideQuotes(
        string text,
        char delimiter,
        char quote,
        int maximumExclusive)
    {
        var delimiterCount = 0;
        var numericAdjacentCount = 0;
        var inQuotes = false;
        var scanEnd = Math.Min(text.Length, maximumExclusive);

        for (var index = 0; index < scanEnd; index++)
        {
            var current = text[index];
            if (current == quote)
            {
                if (inQuotes && index + 1 < scanEnd && text[index + 1] == quote)
                {
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (inQuotes || current != delimiter)
            {
                continue;
            }

            delimiterCount++;
            if (delimiter == ',' &&
                index > 0 &&
                index + 1 < scanEnd &&
                char.IsDigit(text[index - 1]) &&
                char.IsDigit(text[index + 1]))
            {
                numericAdjacentCount++;
            }
        }

        return (delimiterCount, numericAdjacentCount);
    }

    private static int CandidateOrder(char delimiter) => delimiter switch
    {
        ',' => 0,
        ';' => 1,
        '\t' => 2,
        _ => 3
    };
}
