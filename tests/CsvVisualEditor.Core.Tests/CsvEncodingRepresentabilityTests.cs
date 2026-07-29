namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using Xunit;

public sealed class CsvEncodingRepresentabilityTests
{
    private const string Hungarian =
        "\u00c1rv\u00edzt\u0171r\u0151 t\u00fck\u00f6rf\u00far\u00f3g\u00e9p";
    private const string CjkAndEmoji = "\u6771\u4eac \U0001F600";

    [Fact]
    public void SupportedProfiles_AreExplicitAndDeterministic()
    {
        Assert.Equal(
            [65001, 20127, 1250, 1252],
            CsvEncodingProfiles.Supported.Select(static profile => profile.CodePage));
        Assert.Equal(
            ["utf-8", "us-ascii", "windows-1250", "windows-1252"],
            CsvEncodingProfiles.Supported.Select(static profile => profile.WebName));
    }

    [Fact]
    public void Evaluate_Utf8_RoundTripsAsciiHungarianCjkAndEmoji()
    {
        var text = $"A,B\r\n{Hungarian},{CjkAndEmoji}\n";

        var result = CsvEncodingRepresentability.Evaluate(65001, text);

        Assert.Equal(CsvEncodingRepresentabilityStatus.ExactRoundTrip, result.Status);
        Assert.True(result.IsExact);
        Assert.Equal("utf-8", result.EncodingWebName);
        Assert.NotNull(result.EncodedByteCount);
        Assert.True(result.EncodedByteCount > text.Length);
    }

    [Fact]
    public void Evaluate_Ascii_RoundTripsSevenBitCsv()
    {
        const string text = "Name,Value\r\nAlpha,123\n";

        var result = CsvEncodingRepresentability.Evaluate(20127, text);

        Assert.True(result.IsExact);
        Assert.Equal((long)text.Length, result.EncodedByteCount);
    }

    [Fact]
    public void Evaluate_Ascii_RejectsNonAsciiWithoutReplacement()
    {
        var result = CsvEncodingRepresentability.Evaluate(20127, Hungarian);

        Assert.Equal(CsvEncodingRepresentabilityStatus.TextNotRepresentable, result.Status);
        Assert.False(result.IsExact);
        Assert.Null(result.EncodedByteCount);
    }

    [Fact]
    public void Evaluate_Windows1250_RoundTripsHungarianText()
    {
        var text = $"N\u00e9v;Megjegyz\u00e9s\r\n{Hungarian};\"id\u00e9zet\"\n";

        var result = CsvEncodingRepresentability.Evaluate(1250, text);

        Assert.True(result.IsExact);
        Assert.Equal("windows-1250", result.EncodingWebName);
        Assert.Equal((long)text.Length, result.EncodedByteCount);
    }

    [Fact]
    public void Evaluate_Windows1250_RejectsCjkAndEmoji()
    {
        var result = CsvEncodingRepresentability.Evaluate(1250, CjkAndEmoji);

        Assert.Equal(CsvEncodingRepresentabilityStatus.TextNotRepresentable, result.Status);
        Assert.Null(result.EncodedByteCount);
    }

    [Fact]
    public void Evaluate_Windows1252_RoundTripsWesternEuropeanText()
    {
        const string text = "Caf\u00e9,\u20ac,\u2013,na\u00efve\r\n";

        var result = CsvEncodingRepresentability.Evaluate(1252, text);

        Assert.True(result.IsExact);
        Assert.Equal("windows-1252", result.EncodingWebName);
        Assert.Equal((long)text.Length, result.EncodedByteCount);
    }

    [Fact]
    public void Evaluate_Windows1252_RejectsCjk()
    {
        var result = CsvEncodingRepresentability.Evaluate(1252, "\u6771\u4eac");

        Assert.Equal(CsvEncodingRepresentabilityStatus.TextNotRepresentable, result.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(932)]
    public void Evaluate_UnsupportedCodePage_IsSanitized(int codePage)
    {
        var result = CsvEncodingRepresentability.Evaluate(codePage, "secret-value");

        Assert.Equal(CsvEncodingRepresentabilityStatus.UnsupportedCodePage, result.Status);
        Assert.Equal(codePage, result.CodePage);
        Assert.Null(result.EncodingWebName);
        Assert.Null(result.EncodedByteCount);
        Assert.DoesNotContain("secret-value", result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_Utf8_RejectsUnpairedSurrogate()
    {
        var result = CsvEncodingRepresentability.Evaluate(65001, "A\ud800B");

        Assert.Equal(CsvEncodingRepresentabilityStatus.TextNotRepresentable, result.Status);
        Assert.Null(result.EncodedByteCount);
    }

    [Theory]
    [InlineData(65001)]
    [InlineData(20127)]
    [InlineData(1250)]
    [InlineData(1252)]
    public void Evaluate_EmptyText_RoundTrips(int codePage)
    {
        var result = CsvEncodingRepresentability.Evaluate(codePage, string.Empty);

        Assert.True(result.IsExact);
        Assert.Equal(0L, result.EncodedByteCount);
    }

    [Fact]
    public void Evaluate_RepeatedCalls_AreDeterministic()
    {
        var first = CsvEncodingRepresentability.Evaluate(1250, Hungarian);
        var second = CsvEncodingRepresentability.Evaluate(1250, Hungarian);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Evaluate_NullText_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CsvEncodingRepresentability.Evaluate(65001, null!));
    }
}
