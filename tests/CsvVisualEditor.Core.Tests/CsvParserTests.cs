namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using Xunit;

public sealed class CsvParserTests
{
    [Fact]
    public void Parse_SimpleCommaSeparatedText_ReturnsRowsAndCells()
    {
        var result = CsvParser.Parse("Name,Age\r\nAnna,42", CsvDialect.Create(','));

        Assert.False(result.HasErrors);
        Assert.Equal(2, result.Records.Count);
        Assert.Equal(["Name", "Age"], Values(result.Records[0]));
        Assert.Equal(["Anna", "42"], Values(result.Records[1]));
        Assert.Equal(2, result.ExpectedFieldCount);
    }

    [Theory]
    [InlineData(';')]
    [InlineData('\t')]
    public void Parse_SupportedAlternativeDelimiter_UsesExplicitDialect(char delimiter)
    {
        var text = $"A{delimiter}B\n1{delimiter}2";

        var result = CsvParser.Parse(text, CsvDialect.Create(delimiter));

        Assert.False(result.HasErrors);
        Assert.Equal(["A", "B"], Values(result.Records[0]));
        Assert.Equal(["1", "2"], Values(result.Records[1]));
    }

    [Fact]
    public void Parse_EmptyAndTrailingFields_PreservesEveryField()
    {
        var result = CsvParser.Parse("a,,c,", CsvDialect.Create(','));

        var record = Assert.Single(result.Records);
        Assert.Equal(["a", "", "c", ""], Values(record));
    }

    [Fact]
    public void Parse_QuotedDelimiterAndDoubledQuote_DecodesValues()
    {
        var result = CsvParser.Parse(
            "Name,Note\nAnna,\"one,two and \"\"quoted\"\"\"",
            CsvDialect.Create(','));

        Assert.False(result.HasErrors);
        Assert.Equal("one,two and \"quoted\"", result.Records[1].Cells[1].Value);
        Assert.True(result.Records[1].Cells[1].IsQuoted);
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    public void Parse_LineBreakInsideQuotedField_KeepsOneLogicalRecord(string lineBreak)
    {
        var text = $"Id,Note{lineBreak}1,\"first{lineBreak}second\"{lineBreak}2,end";

        var result = CsvParser.Parse(text, CsvDialect.Create(','));

        Assert.False(result.HasErrors);
        Assert.Equal(3, result.Records.Count);
        Assert.Equal($"first{lineBreak}second", result.Records[1].Cells[1].Value);
    }

    [Fact]
    public void Parse_BlankPhysicalRecord_IsRepresentedWithoutAddingTrailingPhantomRecord()
    {
        var result = CsvParser.Parse("a,b\r\n\r\n1,2\r\n", CsvDialect.Create(','));

        Assert.Equal(3, result.Records.Count);
        Assert.True(result.Records[1].IsBlank);
        Assert.Equal(["1", "2"], Values(result.Records[2]));
    }

    [Fact]
    public void Parse_EmptyText_ReturnsNoRecords()
    {
        var result = CsvParser.Parse(string.Empty, CsvDialect.Create(','));

        Assert.Empty(result.Records);
        Assert.Null(result.ExpectedFieldCount);
    }

    [Fact]
    public void Parse_UnterminatedQuotedField_ReturnsPartialValueAndError()
    {
        var result = CsvParser.Parse("a,\"unfinished", CsvDialect.Create(','));

        Assert.True(result.HasErrors);
        Assert.Equal("unfinished", result.Records[0].Cells[1].Value);
        Assert.True(result.Diagnostics.Any(
            static diagnostic => diagnostic.Code == CsvDiagnosticCodes.UnterminatedQuotedField));
    }

    [Fact]
    public void Parse_QuoteInsideUnquotedField_ReturnsDiagnosticWithoutLosingCharacter()
    {
        var result = CsvParser.Parse("a\"b,c", CsvDialect.Create(','));

        Assert.True(result.HasErrors);
        Assert.Equal("a\"b", result.Records[0].Cells[0].Value);
        Assert.True(result.Diagnostics.Any(
            static diagnostic => diagnostic.Code == CsvDiagnosticCodes.UnexpectedQuoteInUnquotedField));
    }

    [Fact]
    public void Parse_TextAfterClosingQuote_ReturnsDiagnosticAndPreservesText()
    {
        var result = CsvParser.Parse("\"a\"x,b", CsvDialect.Create(','));

        Assert.True(result.HasErrors);
        Assert.Equal("ax", result.Records[0].Cells[0].Value);
        Assert.True(result.Diagnostics.Any(
            static diagnostic => diagnostic.Code == CsvDiagnosticCodes.UnexpectedCharacterAfterClosingQuote));
    }

    [Fact]
    public void Parse_InconsistentWidths_ReportsModeBasedWarning()
    {
        var result = CsvParser.Parse("a,b\n1,2\n3,4,5", CsvDialect.Create(','));

        Assert.False(result.HasErrors);
        Assert.Equal(2, result.ExpectedFieldCount);
        var diagnostic = Assert.Single(result.Diagnostics.Where(
            static item => item.Code == CsvDiagnosticCodes.InconsistentFieldCount));
        Assert.Equal(2, diagnostic.RecordIndex);
        Assert.Equal(CsvDiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void Parse_LeadingBom_RemovesMarkerAndReportsInformation()
    {
        var result = CsvParser.Parse("\uFEFFA,B\n1,2", CsvDialect.Create(','));

        Assert.Equal("A", result.Records[0].Cells[0].Value);
        Assert.True(result.Diagnostics.Any(
            static diagnostic => diagnostic.Code == CsvDiagnosticCodes.LeadingBomRemoved));
        Assert.Equal(1, result.Records[0].SourceSpan.Start);
    }

    [Fact]
    public void Parse_SourceSpans_ReferToRawDecodedText()
    {
        const string text = "a,b\r\n\"x,y\",z";

        var result = CsvParser.Parse(text, CsvDialect.Create(','));

        Assert.Equal(new CsvSourceSpan(0, 3), result.Records[0].SourceSpan);
        Assert.Equal(new CsvSourceSpan(0, 1), result.Records[0].Cells[0].SourceSpan);
        Assert.Equal(new CsvSourceSpan(2, 1), result.Records[0].Cells[1].SourceSpan);
        Assert.Equal(new CsvSourceSpan(5, 7), result.Records[1].SourceSpan);
        Assert.Equal(new CsvSourceSpan(5, 5), result.Records[1].Cells[0].SourceSpan);
        Assert.Equal(new CsvSourceSpan(11, 1), result.Records[1].Cells[1].SourceSpan);
    }

    [Fact]
    public void Parse_WideRecord_PreservesAllColumns()
    {
        var text = string.Join(',', Enumerable.Range(0, 1_000));

        var result = CsvParser.Parse(text, CsvDialect.Create(','));

        var record = Assert.Single(result.Records);
        Assert.Equal(1_000, record.FieldCount);
        Assert.Equal("0", record.Cells[0].Value);
        Assert.Equal("999", record.Cells[^1].Value);
    }

    private static string[] Values(CsvRecord record) =>
        record.Cells.Select(static cell => cell.Value).ToArray();
}
