namespace CsvVisualEditor.Core.Tests;

using System.Globalization;
using CsvVisualEditor.Localization;
using Xunit;

public sealed class LocalizationCatalogTests
{
    public static IEnumerable<object[]> Codes => L10n.Languages.Select(static language => language.Code)
        .Distinct().Select(static code => new object[] { code });

    [Theory]
    [MemberData(nameof(Codes))]
    public void EveryShippedCatalogIsCompleteAndItsFormatsAreUsable(string code)
    {
        Assert.True(L10n.HasCompleteCatalog(code), "Missing or invalid catalog: " + code);
        object[] values = Enumerable.Repeat<object>(1m, 12).ToArray();
        foreach (var key in Enum.GetValues<TextKey>())
        {
            var english = L10n.GetForLanguage("en", key);
            var text = L10n.GetForLanguage(code, key);
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.True(MessageFormat.HasSameArguments(english, text), code + "/" + key);
            Assert.NotNull(string.Format(CultureInfo.InvariantCulture, text, values));
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void InvalidKeysFailConsistently(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => L10n.Get((TextKey)value));
        Assert.Throws<ArgumentOutOfRangeException>(() => L10n.GetForLanguage("en", (TextKey)value));
        Assert.Throws<ArgumentOutOfRangeException>(() => L10n.Format((TextKey)value, 1));
    }

    [Fact]
    public void LanguageCodeCasingDoesNotCreateAnotherCatalog()
    {
        Assert.True(L10n.HasCompleteCatalog("HU"));
        Assert.Equal(L10n.GetForLanguage("hu", TextKey.Common_Cancel), L10n.GetForLanguage("HU", TextKey.Common_Cancel));
        Assert.Equal("Cancel", L10n.GetForLanguage("unrecognized", TextKey.Common_Cancel));
    }
}
