namespace CsvVisualEditor.Core.Tests;

using System.Globalization;
using CsvVisualEditor.Core;
using CsvVisualEditor.Localization;
using Xunit;

[CollectionDefinition("Localization state", DisableParallelization = true)]
public sealed class LocalizationStateCollection { }

[Collection("Localization state")]
public sealed class LocalizationCultureTests
{
    [Fact]
    public void HostLanguageSelectionNeverChangesDataOrApplicationCulture()
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        var defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        var defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            foreach (var definition in L10n.Languages)
            {
                var translated = L10n.TranslatedLanguageCodes.Contains(definition.Code);
                L10n.InitializeFromNativeLanguage(definition.NativeFilename);
                Assert.Equal(translated ? definition.Code : "en", L10n.LanguageCode);
                Assert.Equal(translated && definition.RightToLeft, L10n.IsRightToLeft);
                if (!translated) Assert.Equal("Cancel", L10n.Get(TextKey.Common_Cancel));
                Assert.Same(culture, CultureInfo.CurrentCulture);
                Assert.Same(uiCulture, CultureInfo.CurrentUICulture);
                Assert.Same(defaultCulture, CultureInfo.DefaultThreadCurrentCulture);
                Assert.Same(defaultUiCulture, CultureInfo.DefaultThreadCurrentUICulture);
                Assert.True(CsvNumericValue.TryParse("-12.5", out var number));
                Assert.Equal(-12.5m, number);
                Assert.False(CsvNumericValue.TryParse("-12,5", out _));
            }
            L10n.InitializeFromNativeLanguage("future-custom-language.xml");
            Assert.Equal("en", L10n.LanguageCode);
            Assert.Equal("Cancel", L10n.Get(TextKey.Common_Cancel));
        }
        finally { L10n.SetLanguage("en"); }
    }
}
