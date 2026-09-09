namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using CsvVisualEditor.Localization;
using Xunit;

public sealed class LocalizationTests
{
    [Theory]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("unknown.xml", "en")]
    [InlineData("hungarian.xml", "hu")]
    [InlineData("HUNGARIAN.XML", "hu")]
    [InlineData("C:\\Notepad++\\localization\\japanese.xml", "ja")]
    [InlineData("../french.xml", "fr")]
    [InlineData("english_customizable.xml", "en")]
    [InlineData("hongKongCantonese.xml", "yue-Hant")]
    [InlineData("serbian.xml", "sr-Latn")]
    [InlineData("serbianCyrillic.xml", "sr-Cyrl")]
    [InlineData("uzbek.xml", "uz-Latn")]
    [InlineData("uzbekCyrillic.xml", "uz-Cyrl")]
    [InlineData("arabic.xml", "ar")]
    [InlineData("kurdish.xml", "ckb")]
    public void ResolveHostFilename(string? filename, string expected) =>
        Assert.Equal(expected, L10n.ResolveNativeLanguage(filename));

    [Fact]
    public void EveryInventoryEntryResolvesExactly()
    {
        Assert.Equal(94, L10n.Languages.Count);
        Assert.Equal(93, L10n.Languages.Select(static item => item.Code).Distinct().Count());
        foreach (var item in L10n.Languages)
            Assert.Equal(item.Code, L10n.ResolveNativeLanguage(item.NativeFilename));
        Assert.Equal(6, L10n.Languages.Count(static item => item.RightToLeft));
    }

    [Theory]
    [InlineData("Rows: {0:N0}", "{0:N0}: sor", true)]
    [InlineData("{0} / {1}", "{1} - {0}", true)]
    [InlineData("{{value}} {0}", "{{ertek}} {0}", true)]
    [InlineData("{0:N0}", "{0}", false)]
    [InlineData("{0} {0}", "{0}", false)]
    [InlineData("{0}", "{1}", false)]
    [InlineData("{0}", "{", false)]
    [InlineData("{0}", "0}", false)]
    [InlineData("Ready", "{0}", false)]
    [InlineData("Ready", "Kesz", true)]
    public void FormatContractIsExact(string source, string target, bool expected) =>
        Assert.Equal(expected, MessageFormat.HasSameArguments(source, target));

    [Fact]
    public void UnknownCatalogUsesEnglishWithoutChangingApplicationCulture()
    {
        Assert.False(L10n.HasCompleteCatalog("not-a-language"));
        Assert.Equal("Cancel", L10n.GetForLanguage("unknown", TextKey.Common_Cancel));
        Assert.True(L10n.HasCompleteCatalog("en"));
    }

    [Fact]
    public void ErrorMetadataPreservesOriginalExceptionAndCopiesArguments()
    {
        object[] args = [513, 512];
        var original = new ArgumentException("Invariant technical detail");
        var tagged = CsvErrorDetails.With(original, CsvUserError.ProjectionTooManyColumns, args);
        args[0] = 999;
        Assert.Same(original, tagged);
        Assert.Equal("Invariant technical detail", tagged.Message);
        var details = CsvErrorDetails.From(tagged)!;
        Assert.Equal(CsvUserError.ProjectionTooManyColumns, details.Code);
        Assert.Equal(513, details.Arguments[0]);
        Assert.Null(CsvErrorDetails.From(new InvalidOperationException("other")));
    }

    [Fact]
    public void DiagnosticFormattingArgumentsRetainRecordValueEquality()
    {
        var first = new CsvDiagnostic(CsvDiagnosticSeverity.Warning, CsvDiagnosticCodes.InconsistentFieldCount,
            "English diagnostic", 4, 1, 3, 2);
        var same = new CsvDiagnostic(CsvDiagnosticSeverity.Warning, CsvDiagnosticCodes.InconsistentFieldCount,
            "English diagnostic", 4, 1, 3, 2);
        Assert.Equal(first, same);
        Assert.Equal(3, first.ActualFieldCount);
        Assert.Equal(2, first.ExpectedFieldCount);
    }
}
