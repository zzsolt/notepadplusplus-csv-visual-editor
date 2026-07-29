namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;
using System.Text;

/// <summary>
/// One explicitly supported Scintilla code-page profile. Profiles describe
/// decoded-text representability only; they do not by themselves authorize a
/// Notepad++ host write.
/// </summary>
public sealed class CsvEncodingProfile
{
    internal CsvEncodingProfile(int codePage, string webName)
    {
        if (codePage <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(codePage));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(webName);
        CodePage = codePage;
        WebName = webName;
    }

    public int CodePage { get; }

    public string WebName { get; }

    internal Encoding CreateStrictEncoding()
    {
        return CodePage switch
        {
            CsvEncodingProfiles.Utf8CodePage =>
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            CsvEncodingProfiles.AsciiCodePage => CreateStrictAscii(),
            CsvEncodingProfiles.Windows1250CodePage or
            CsvEncodingProfiles.Windows1252CodePage => CreateStrictCodePage(CodePage),
            _ => throw new NotSupportedException(
                $"Scintilla code page {CodePage} has no explicit encoding profile.")
        };
    }

    private static Encoding CreateStrictAscii()
    {
        var encoding = (Encoding)Encoding.ASCII.Clone();
        encoding.EncoderFallback = EncoderFallback.ExceptionFallback;
        encoding.DecoderFallback = DecoderFallback.ExceptionFallback;
        return encoding;
    }

    private static Encoding CreateStrictCodePage(int codePage)
    {
        var encoding = CodePagesEncodingProvider.Instance.GetEncoding(codePage) ??
            throw new NotSupportedException(
                $"The runtime did not provide code page {codePage}.");
        var strictEncoding = (Encoding)encoding.Clone();
        strictEncoding.EncoderFallback = EncoderFallback.ExceptionFallback;
        strictEncoding.DecoderFallback = DecoderFallback.ExceptionFallback;
        return strictEncoding;
    }
}

/// <summary>
/// Explicit allow-list of code pages evaluated by the milestone 0.10
/// host-independent representability layer.
/// </summary>
public static class CsvEncodingProfiles
{
    public const int Utf8CodePage = 65001;
    public const int AsciiCodePage = 20127;
    public const int Windows1250CodePage = 1250;
    public const int Windows1252CodePage = 1252;

    private static readonly CsvEncodingProfile Utf8 =
        new(Utf8CodePage, "utf-8");
    private static readonly CsvEncodingProfile Ascii =
        new(AsciiCodePage, "us-ascii");
    private static readonly CsvEncodingProfile Windows1250 =
        new(Windows1250CodePage, "windows-1250");
    private static readonly CsvEncodingProfile Windows1252 =
        new(Windows1252CodePage, "windows-1252");
    private static readonly ReadOnlyCollection<CsvEncodingProfile> Profiles =
        Array.AsReadOnly([Utf8, Ascii, Windows1250, Windows1252]);

    public static IReadOnlyList<CsvEncodingProfile> Supported => Profiles;

    public static bool TryGet(int codePage, out CsvEncodingProfile? profile)
    {
        profile = codePage switch
        {
            Utf8CodePage => Utf8,
            AsciiCodePage => Ascii,
            Windows1250CodePage => Windows1250,
            Windows1252CodePage => Windows1252,
            _ => null
        };

        return profile is not null;
    }
}
