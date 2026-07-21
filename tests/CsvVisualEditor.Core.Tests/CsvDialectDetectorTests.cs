namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using Xunit;

public sealed class CsvDialectDetectorTests
{
    [Theory]
    [InlineData(',', "Name,Age,City\nAnna,42,Budapest\nBela,38,Szeged")]
    [InlineData(';', "Name;Age;City\nAnna;42;Budapest\nBela;38;Szeged")]
    [InlineData('\t', "Name\tAge\tCity\nAnna\t42\tBudapest\nBela\t38\tSzeged")]
    public void Detect_ConsistentMultiRecordData_ReturnsHighConfidence(
        char expectedDelimiter,
        string text)
    {
        var result = CsvDialectDetector.Detect(text);

        Assert.NotNull(result.SuggestedDialect);
        Assert.Equal(expectedDelimiter, result.SuggestedDialect.Delimiter);
        Assert.Equal(CsvDelimiterConfidence.High, result.Confidence);
        Assert.True(result.IsReliable);
    }

    [Fact]
    public void Detect_QuotedCommasInsideSemicolonFile_SelectsSemicolon()
    {
        const string text = "Name;Note\nAnna;\"one,two\"\nBela;\"three,four\"";

        var result = CsvDialectDetector.Detect(text);

        Assert.NotNull(result.SuggestedDialect);
        Assert.Equal(';', result.SuggestedDialect.Delimiter);
        Assert.Equal(CsvDelimiterConfidence.High, result.Confidence);
    }

    [Fact]
    public void Detect_MultilineQuotedField_UsesLogicalRecords()
    {
        const string text = "Id,Note\n1,\"first\nsecond\"\n2,end";

        var result = CsvDialectDetector.Detect(text);

        Assert.NotNull(result.SuggestedDialect);
        Assert.Equal(',', result.SuggestedDialect.Delimiter);
        Assert.True(result.IsReliable);
    }

    [Fact]
    public void Detect_SingleColumnText_ReturnsNoSuggestion()
    {
        const string text = "alpha\nbeta\ngamma";

        var result = CsvDialectDetector.Detect(text);

        Assert.Null(result.SuggestedDialect);
        Assert.Equal(CsvDelimiterConfidence.None, result.Confidence);
        Assert.Contains(
            result.Diagnostics,
            static diagnostic => diagnostic.Code == CsvDiagnosticCodes.NoReliableDelimiter);
    }

    [Fact]
    public void Detect_OneRecord_ReturnsOnlyLowConfidence()
    {
        var result = CsvDialectDetector.Detect("a,b,c");

        Assert.NotNull(result.SuggestedDialect);
        Assert.Equal(',', result.SuggestedDialect.Delimiter);
        Assert.Equal(CsvDelimiterConfidence.Low, result.Confidence);
        Assert.False(result.IsReliable);
    }

    [Fact]
    public void Detect_DecimalCommaProse_DoesNotClaimReliableCsv()
    {
        const string text =
            "A homerseklet 12,5 fok.\n" +
            "A feszultseg 3,3 volt.\n" +
            "A tavolsag 8,2 meter.";

        var result = CsvDialectDetector.Detect(text);

        Assert.False(result.IsReliable);
        Assert.True(result.SuggestedDialect is null || result.Confidence == CsvDelimiterConfidence.Low);

        var comma = result.Candidates.Single(static candidate => candidate.Delimiter == ',');
        Assert.Equal(3, comma.NumericAdjacentDelimiterCount);
    }

    [Fact]
    public void Detect_SemicolonDataWithDecimalCommas_SelectsSemicolon()
    {
        const string text = "Item;Value\nA;12,5\nB;3,3\nC;8,2";

        var result = CsvDialectDetector.Detect(text);

        Assert.NotNull(result.SuggestedDialect);
        Assert.Equal(';', result.SuggestedDialect.Delimiter);
        Assert.Equal(CsvDelimiterConfidence.High, result.Confidence);
    }

    [Fact]
    public void Detect_AmbiguousCandidates_ReturnsWarningRatherThanSilentCertainty()
    {
        const string text = "a,b;c\n1,2;3\n4,5;6";

        var result = CsvDialectDetector.Detect(text);

        Assert.Contains(
            result.Diagnostics,
            static diagnostic => diagnostic.Code == CsvDiagnosticCodes.AmbiguousDelimiter);
        Assert.NotEqual(CsvDelimiterConfidence.High, result.Confidence);
    }

    [Fact]
    public void Detect_LogicalRecordLimit_DoesNotParseMalformedSuffix()
    {
        const string text = "A,B\n1,2\n3,4\n\"unterminated,field";
        var options = new CsvDialectDetectionOptions { MaximumLogicalRecords = 3 };

        var result = CsvDialectDetector.Detect(text, options);

        Assert.NotNull(result.SuggestedDialect);
        Assert.Equal(',', result.SuggestedDialect.Delimiter);
        var comma = result.Candidates.Single(static candidate => candidate.Delimiter == ',');
        Assert.Equal(3, comma.SampledRecordCount);
        Assert.Equal(0, comma.ParseErrorCount);
    }

    [Fact]
    public void Detect_CharacterLimit_DoesNotBuildARecordFromTruncatedSuffix()
    {
        const string text = "A,B\n1,2\n3,4\n\"unterminated,field";
        var options = new CsvDialectDetectionOptions
        {
            MaximumLogicalRecords = 20,
            MaximumSampleCharacters = 12
        };

        var result = CsvDialectDetector.Detect(text, options);

        Assert.NotNull(result.SuggestedDialect);
        Assert.Equal(',', result.SuggestedDialect.Delimiter);
        var comma = result.Candidates.Single(static candidate => candidate.Delimiter == ',');
        Assert.Equal(3, comma.SampledRecordCount);
        Assert.Equal(0, comma.ParseErrorCount);
    }

    [Fact]
    public void Detect_ExposesEverySupportedCandidateScore()
    {
        var result = CsvDialectDetector.Detect("a,b\n1,2\n3,4");

        Assert.Equal(3, result.Candidates.Count);
        Assert.Equal(
            CsvDialect.SupportedDelimiters.OrderBy(static value => value),
            result.Candidates.Select(static candidate => candidate.Delimiter).OrderBy(static value => value));
    }
}
