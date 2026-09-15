namespace CsvVisualEditor.Localization;

using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

/// <summary>
/// Embedded, keyed interface resources. Display language never changes data parsing,
/// document values, CurrentCulture, or the process-wide culture of Notepad++.
/// </summary>
public static class L10n
{
    private sealed record Catalog(LanguageDefinition Language, string[] Values, CultureInfo Culture, bool Complete);
    private static readonly ConcurrentDictionary<string, Catalog> Cache = new(StringComparer.Ordinal);
    private static Catalog _current = EnglishCatalog();

    public static string LanguageCode => Volatile.Read(ref _current).Language.Code;
    public static string LanguageName => Volatile.Read(ref _current).Language.Name;
    public static bool IsRightToLeft => Volatile.Read(ref _current).Language.RightToLeft;
    public static CultureInfo FormattingCulture => Volatile.Read(ref _current).Culture;
    public static IReadOnlyList<LanguageDefinition> Languages => LanguageInventory.All;
    public static IReadOnlySet<string> TranslatedLanguageCodes => TranslationPolicy.SupportedCodes;

    /// <summary>Resolve the host's native-language file, not its OS or document encoding.</summary>
    public static string ResolveNativeLanguage(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return "en";
        // Only known basenames map to compiled inventory entries; no resource/file paths
        // are constructed from the host-provided string.
        var normalized = filename.Trim().Replace('\\', '/');
        normalized = normalized[(normalized.LastIndexOf('/') + 1)..];
        foreach (var language in LanguageInventory.All)
            if (string.Equals(language.NativeFilename, normalized, StringComparison.OrdinalIgnoreCase))
                return language.Code;
        return "en";
    }

    public static void InitializeFromNativeLanguage(string? filename) => SetLanguage(ResolveNativeLanguage(filename));

    public static void SetLanguage(string? code)
    {
        var definition = LanguageInventory.All.FirstOrDefault(language =>
            string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase)) ?? LanguageInventory.English;
        var catalog = definition.Code == "en" || !TranslationPolicy.SupportedCodes.Contains(definition.Code)
            ? EnglishCatalog()
            : Cache.GetOrAdd(definition.Code, _ => Load(definition));
        Volatile.Write(ref _current, catalog);
    }

    public static string Get(TextKey key)
    {
        var index = (int)key;
        if ((uint)index >= (uint)EnglishText.Values.Length) throw new ArgumentOutOfRangeException(nameof(key));
        return Volatile.Read(ref _current).Values[index];
    }

    public static string Format(TextKey key, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var catalog = Volatile.Read(ref _current);
        var index = (int)key;
        if ((uint)index >= (uint)EnglishText.Values.Length) throw new ArgumentOutOfRangeException(nameof(key));
        try { return string.Format(catalog.Culture, catalog.Values[index], arguments); }
        catch (FormatException) when (catalog.Language.Code != "en")
        {
            // Defensive against corrupted resources. The build gate rejects such files;
            // fallback still prevents an unusable plugin in a manually modified build.
            return string.Format(CultureInfo.InvariantCulture, EnglishText.Values[index], arguments);
        }
    }

    public static string GetForLanguage(string code, TextKey key)
    {
        var index = (int)key;
        if ((uint)index >= (uint)EnglishText.Values.Length) throw new ArgumentOutOfRangeException(nameof(key));
        var definition = LanguageInventory.All.FirstOrDefault(language =>
            string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase)) ?? LanguageInventory.English;
        var catalog = definition.Code == "en" || !TranslationPolicy.SupportedCodes.Contains(definition.Code)
            ? EnglishCatalog()
            : Cache.GetOrAdd(definition.Code, _ => Load(definition));
        return catalog.Values[index];
    }

    /// <summary>Useful for automated coverage: a known language may not rely on fallback.</summary>
    public static bool HasCompleteCatalog(string code)
    {
        var definition = LanguageInventory.All.FirstOrDefault(language =>
            string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase));
        return definition is not null && TranslationPolicy.SupportedCodes.Contains(definition.Code) &&
            (definition.Code == "en" || Cache.GetOrAdd(definition.Code, _ => Load(definition)).Complete);
    }

    private static Catalog EnglishCatalog() => new(LanguageInventory.English, EnglishText.Values,
        CultureInfo.InvariantCulture, true);

    private static Catalog Load(LanguageDefinition definition)
    {
        var values = (string[])EnglishText.Values.Clone();
        var complete = true;
        try
        {
            using var stream = typeof(L10n).Assembly.GetManifestResourceStream(
                "CsvVisualEditor.Localization.Catalogs." + definition.Code + ".json");
            if (stream is null) return EnglishCatalog() with { Complete = false };
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.GetProperty("language").GetString() != definition.Code)
                return EnglishCatalog() with { Complete = false };
            var messages = document.RootElement.GetProperty("messages");
            for (var index = 0; index < values.Length; index++)
            {
                if (!messages.TryGetProperty(EnglishText.Keys[index], out var entry) ||
                    !entry.TryGetProperty("text", out var textElement) ||
                    !entry.TryGetProperty("sourceHash", out var hashElement)) { complete = false; continue; }
                var text = textElement.GetString();
                if (string.IsNullOrWhiteSpace(text) || hashElement.GetString() != EnglishText.SourceHashes[index] ||
                    !MessageFormat.HasSameArguments(values[index], text)) { complete = false; continue; }
                values[index] = text;
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or
            KeyNotFoundException or IOException or FormatException)
        {
            return EnglishCatalog() with { Complete = false };
        }
        CultureInfo culture;
        try { culture = CultureInfo.GetCultureInfo(definition.Code); }
        catch (CultureNotFoundException) { culture = CultureInfo.InvariantCulture; }
        return new Catalog(definition, values, culture, complete);
    }
}

public sealed record LanguageDefinition(string NativeFilename, string Code, string Name, bool RightToLeft);
