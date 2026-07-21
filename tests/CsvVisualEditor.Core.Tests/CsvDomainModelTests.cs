namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using Xunit;

public sealed class CsvDomainModelTests
{
    [Theory]
    [InlineData(',')]
    [InlineData(';')]
    [InlineData('\t')]
    public void CsvDialect_Create_AcceptsSupportedDelimiter(char delimiter)
    {
        var dialect = CsvDialect.Create(delimiter);

        Assert.Equal(delimiter, dialect.Delimiter);
        Assert.Equal('"', dialect.Quote);
        Assert.Equal(CsvHeaderMode.Unknown, dialect.HeaderMode);
    }

    [Fact]
    public void CsvDialect_Create_RejectsUnsupportedDelimiter()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CsvDialect.Create('|'));
    }

    [Fact]
    public void CsvDialect_Create_RejectsQuoteEqualToDelimiter()
    {
        Assert.Throws<ArgumentException>(() => CsvDialect.Create(',', quote: ','));
    }

    [Fact]
    public void CsvSourceSpan_RejectsNegativeValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CsvSourceSpan(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CsvSourceSpan(0, -1));
    }

    [Fact]
    public void DetectionOptions_RejectInvalidBoundsThroughDetector()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CsvDialectDetector.Detect(
                "a,b",
                new CsvDialectDetectionOptions { MaximumLogicalRecords = 0 }));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CsvDialectDetector.Detect(
                "a,b",
                new CsvDialectDetectionOptions { MinimumSuggestionScore = 101 }));
    }
}
