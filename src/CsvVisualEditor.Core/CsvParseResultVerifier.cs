namespace CsvVisualEditor.Core;

/// <summary>
/// Verifies that a parser result was produced from the exact decoded editor
/// snapshot supplied to a later safety-sensitive operation.
/// </summary>
internal static class CsvParseResultVerifier
{
    public static void EnsureMatchesSnapshot(
        string snapshotText,
        CsvParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(snapshotText);
        ArgumentNullException.ThrowIfNull(parseResult);

        var reparsed = CsvParser.Parse(snapshotText, parseResult.Dialect);
        if (!AreEquivalent(parseResult, reparsed))
        {
            throw new InvalidOperationException(
                "The parser result does not match the active editor snapshot exactly.");
        }
    }

    private static bool AreEquivalent(
        CsvParseResult expected,
        CsvParseResult actual)
    {
        if (expected.Dialect != actual.Dialect ||
            expected.ExpectedFieldCount != actual.ExpectedFieldCount ||
            expected.Records.Count != actual.Records.Count ||
            expected.Diagnostics.Count != actual.Diagnostics.Count)
        {
            return false;
        }

        for (var recordIndex = 0;
             recordIndex < expected.Records.Count;
             recordIndex++)
        {
            if (!AreEquivalent(
                    expected.Records[recordIndex],
                    actual.Records[recordIndex]))
            {
                return false;
            }
        }

        for (var diagnosticIndex = 0;
             diagnosticIndex < expected.Diagnostics.Count;
             diagnosticIndex++)
        {
            if (!AreEquivalent(
                    expected.Diagnostics[diagnosticIndex],
                    actual.Diagnostics[diagnosticIndex]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEquivalent(CsvRecord expected, CsvRecord actual)
    {
        if (expected.Index != actual.Index ||
            expected.SourceSpan != actual.SourceSpan ||
            expected.Cells.Count != actual.Cells.Count)
        {
            return false;
        }

        for (var cellIndex = 0; cellIndex < expected.Cells.Count; cellIndex++)
        {
            var expectedCell = expected.Cells[cellIndex];
            var actualCell = actual.Cells[cellIndex];
            if (!string.Equals(
                    expectedCell.Value,
                    actualCell.Value,
                    StringComparison.Ordinal) ||
                expectedCell.SourceSpan != actualCell.SourceSpan ||
                expectedCell.IsQuoted != actualCell.IsQuoted)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEquivalent(
        CsvDiagnostic expected,
        CsvDiagnostic actual)
    {
        return expected.Severity == actual.Severity &&
               string.Equals(expected.Code, actual.Code, StringComparison.Ordinal) &&
               string.Equals(expected.Message, actual.Message, StringComparison.Ordinal) &&
               expected.CharacterOffset == actual.CharacterOffset &&
               expected.RecordIndex == actual.RecordIndex;
    }
}
