namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using Xunit;

public sealed class CsvEncodingApplyPolicyTests
{
    private const string Hungarian =
        "\u00c1rv\u00edzt\u0171r\u0151 t\u00fck\u00f6rf\u00far\u00f3g\u00e9p";

    [Fact]
    public void Utf8Only_Utf8Text_IsReady()
    {
        var result = CsvEncodingApplyPolicy.Utf8Only.Evaluate(
            65001,
            $"A,B\n{Hungarian},\u6771\u4eac\n");

        Assert.Equal(CsvEncodingApplyPreflightStatus.Ready, result.Status);
        Assert.True(result.IsReady);
        Assert.NotNull(result.Representability);
        Assert.True(result.EncodedByteCount > 0);
    }

    [Theory]
    [InlineData(20127, "us-ascii")]
    [InlineData(1250, "windows-1250")]
    [InlineData(1252, "windows-1252")]
    public void Utf8Only_KnownLegacyCodePage_IsNotHostEnabled(
        int codePage,
        string webName)
    {
        var result = CsvEncodingApplyPolicy.Utf8Only.Evaluate(codePage, "A,B\n1,2");

        Assert.Equal(CsvEncodingApplyPreflightStatus.HostWriteNotEnabled, result.Status);
        Assert.Equal(webName, result.EncodingWebName);
        Assert.Null(result.Representability);
        Assert.Null(result.EncodedByteCount);
    }

    [Fact]
    public void CustomPolicy_EnabledWindows1250Hungarian_IsReady()
    {
        var policy = CsvEncodingApplyPolicy.Create([65001, 1250]);

        var result = policy.Evaluate(1250, Hungarian);

        Assert.True(result.IsReady);
        Assert.Equal(CsvEncodingApplyPreflightStatus.Ready, result.Status);
        Assert.Equal([1250, 65001], policy.HostWriteEnabledCodePages);
    }

    [Fact]
    public void CustomPolicy_EnabledWindows1250Cjk_IsBlocked()
    {
        var policy = CsvEncodingApplyPolicy.Create([1250]);

        var result = policy.Evaluate(1250, "\u6771\u4eac");

        Assert.Equal(CsvEncodingApplyPreflightStatus.TextNotRepresentable, result.Status);
        Assert.False(result.IsReady);
        Assert.Equal(
            CsvEncodingRepresentabilityStatus.TextNotRepresentable,
            result.Representability?.Status);
    }

    [Fact]
    public void UnsupportedCurrentCodePage_IsReportedBeforeHostWrite()
    {
        var result = CsvEncodingApplyPolicy.Utf8Only.Evaluate(932, "A,B\n1,2");

        Assert.Equal(CsvEncodingApplyPreflightStatus.UnsupportedCodePage, result.Status);
        Assert.Null(result.EncodingWebName);
    }

    [Fact]
    public void Create_UnsupportedEnabledCodePage_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CsvEncodingApplyPolicy.Create([65001, 932]));
    }

    [Fact]
    public void Create_NormalizesDuplicateCodePages()
    {
        var policy = CsvEncodingApplyPolicy.Create([1250, 65001, 1250]);

        Assert.Equal([1250, 65001], policy.HostWriteEnabledCodePages);
    }
}
