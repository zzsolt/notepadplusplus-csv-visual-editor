namespace CsvVisualEditor.Core;

using System.Text;

/// <summary>
/// Parses decoded CSV text as logical records. Physical line breaks inside quoted fields
/// are preserved as cell content rather than treated as record separators.
/// </summary>
public static class CsvParser
{
    public static CsvParseResult Parse(
        string text,
        CsvDialect dialect,
        CsvParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(dialect);

        options ??= CsvParseOptions.Default;

        var records = new List<CsvRecord>();
        var diagnostics = new List<CsvDiagnostic>();
        var cells = new List<CsvCell>();
        var fieldBuilder = new StringBuilder();

        var position = 0;
        if (options.RemoveLeadingBom && text.StartsWith('\uFEFF'))
        {
            diagnostics.Add(new CsvDiagnostic(
                CsvDiagnosticSeverity.Information,
                CsvDiagnosticCodes.LeadingBomRemoved,
                "A leading Unicode BOM marker was excluded from the first field.",
                characterOffset: 0));
            position = 1;
        }

        var recordIndex = 0;
        var recordStart = position;
        var fieldStart = position;
        var fieldAtStart = true;
        var fieldIsQuoted = false;
        var inQuotedField = false;
        var afterClosingQuote = false;
        var recordHasData = false;

        while (position < text.Length)
        {
            var current = text[position];

            if (inQuotedField)
            {
                if (current == dialect.Quote)
                {
                    if (position + 1 < text.Length && text[position + 1] == dialect.Quote)
                    {
                        fieldBuilder.Append(dialect.Quote);
                        position += 2;
                        continue;
                    }

                    inQuotedField = false;
                    afterClosingQuote = true;
                    position++;
                    continue;
                }

                fieldBuilder.Append(current);
                position++;
                continue;
            }

            if (afterClosingQuote)
            {
                if (current == dialect.Delimiter)
                {
                    AddCell(position);
                    recordHasData = true;
                    position++;
                    ResetField(position);
                    continue;
                }

                if (IsLineBreak(current))
                {
                    AddCell(position);
                    AddRecord(position);
                    position = ConsumeLineBreak(text, position);
                    ResetRecord(position);
                    continue;
                }

                diagnostics.Add(new CsvDiagnostic(
                    CsvDiagnosticSeverity.Error,
                    CsvDiagnosticCodes.UnexpectedCharacterAfterClosingQuote,
                    "Only a delimiter, record separator, or end of text may follow a closing quote.",
                    position,
                    recordIndex));

                fieldBuilder.Append(current);
                afterClosingQuote = false;
                fieldAtStart = false;
                recordHasData = true;
                position++;
                continue;
            }

            if (fieldAtStart && current == dialect.Quote)
            {
                fieldIsQuoted = true;
                inQuotedField = true;
                fieldAtStart = false;
                recordHasData = true;
                position++;
                continue;
            }

            if (current == dialect.Delimiter)
            {
                AddCell(position);
                recordHasData = true;
                position++;
                ResetField(position);
                continue;
            }

            if (IsLineBreak(current))
            {
                AddCell(position);
                AddRecord(position);
                position = ConsumeLineBreak(text, position);
                ResetRecord(position);
                continue;
            }

            if (current == dialect.Quote)
            {
                diagnostics.Add(new CsvDiagnostic(
                    CsvDiagnosticSeverity.Error,
                    CsvDiagnosticCodes.UnexpectedQuoteInUnquotedField,
                    "A quote appeared inside an unquoted field.",
                    position,
                    recordIndex));
            }

            fieldBuilder.Append(current);
            fieldAtStart = false;
            recordHasData = true;
            position++;
        }

        if (inQuotedField)
        {
            diagnostics.Add(new CsvDiagnostic(
                CsvDiagnosticSeverity.Error,
                CsvDiagnosticCodes.UnterminatedQuotedField,
                "The final quoted field was not terminated before the end of text.",
                text.Length,
                recordIndex));
        }

        if (recordHasData || fieldStart < text.Length || afterClosingQuote)
        {
            AddCell(text.Length);
            AddRecord(text.Length);
        }

        var expectedFieldCount = DetermineExpectedFieldCount(records);
        if (options.ReportInconsistentFieldCounts && expectedFieldCount.HasValue)
        {
            foreach (var record in records.Where(static record => !record.IsBlank))
            {
                if (record.FieldCount == expectedFieldCount.Value)
                {
                    continue;
                }

                diagnostics.Add(new CsvDiagnostic(
                    CsvDiagnosticSeverity.Warning,
                    CsvDiagnosticCodes.InconsistentFieldCount,
                    $"Record has {record.FieldCount} fields; expected {expectedFieldCount.Value} based on the sampled mode.",
                    record.SourceSpan.Start,
                    record.Index));
            }
        }

        return new CsvParseResult(dialect, records, diagnostics, expectedFieldCount);

        void AddCell(int endExclusive)
        {
            cells.Add(new CsvCell(
                fieldBuilder.ToString(),
                new CsvSourceSpan(fieldStart, endExclusive - fieldStart),
                fieldIsQuoted));
        }

        void AddRecord(int endExclusive)
        {
            records.Add(new CsvRecord(
                recordIndex,
                cells,
                new CsvSourceSpan(recordStart, endExclusive - recordStart)));
            recordIndex++;
        }

        void ResetField(int start)
        {
            fieldBuilder.Clear();
            fieldStart = start;
            fieldAtStart = true;
            fieldIsQuoted = false;
            inQuotedField = false;
            afterClosingQuote = false;
        }

        void ResetRecord(int start)
        {
            cells.Clear();
            recordStart = start;
            recordHasData = false;
            ResetField(start);
        }
    }

    private static int? DetermineExpectedFieldCount(IReadOnlyCollection<CsvRecord> records)
    {
        var nonBlankRecords = records
            .Where(static record => !record.IsBlank)
            .ToArray();

        if (nonBlankRecords.Length == 0)
        {
            return null;
        }

        return nonBlankRecords
            .GroupBy(static record => record.FieldCount)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Min(record => record.Index))
            .First()
            .Key;
    }

    private static bool IsLineBreak(char value) => value is '\r' or '\n';

    private static int ConsumeLineBreak(string text, int position)
    {
        if (text[position] == '\r' &&
            position + 1 < text.Length &&
            text[position + 1] == '\n')
        {
            return position + 2;
        }

        return position + 1;
    }
}
