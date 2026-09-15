namespace CsvVisualEditor.Core;

using System.Globalization;
using System.Text;

/// <summary>
/// Lossless cell-text notation for an expanded editor. Existing line endings
/// never pass through the native edit control as line endings. A visible middle
/// dot means U+0020; literal dots and backslashes are escaped unambiguously.
/// </summary>
public static class CsvCellTextCodec
{
    public const int MaximumValueLength = 1_048_576;
    public const int MaximumEditorLength = MaximumValueLength * 6;

    public static string Encode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > MaximumValueLength)
            throw new ArgumentOutOfRangeException(nameof(value));
        var result = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case ' ': result.Append('\u00b7'); break;
                case '\\': result.Append("\\\\"); break;
                case '\t': result.Append("\\t"); break;
                case '\r': result.Append("\\r"); break;
                case '\n': result.Append("\\n"); break;
                default:
                    // Escape invisible formatting and surrogate code units as well.
                    // This also round-trips arbitrary UTF-16 without splitting data.
                    if (c == '\u00b7' || char.IsWhiteSpace(c) || char.IsControl(c) ||
                        char.IsSurrogate(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format)
                        result.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    else result.Append(c);
                    break;
            }
        }
        return result.ToString();
    }

    /// <summary>
    /// Decodes the complete editor value or returns false with no partial value.
    /// Literal spaces and newly typed line breaks are accepted. Error offsets are
    /// zero-based UTF-16 positions, or -1 for a size violation.
    /// </summary>
    public static bool TryDecode(string encoded, out string value, out int errorOffset)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        value = string.Empty;
        errorOffset = -1;
        if (encoded.Length > MaximumEditorLength) return false;
        var result = new StringBuilder(Math.Min(encoded.Length, MaximumValueLength));
        for (var i = 0; i < encoded.Length; i++)
        {
            var c = encoded[i];
            if (c == '\u00b7') c = ' ';
            else if (c == '\\')
            {
                var start = i;
                if (++i == encoded.Length) { errorOffset = start; return false; }
                switch (encoded[i])
                {
                    case '\\': c = '\\'; break;
                    case 't': c = '\t'; break;
                    case 'r': c = '\r'; break;
                    case 'n': c = '\n'; break;
                    case 'u':
                        if (i + 4 >= encoded.Length ||
                            !ushort.TryParse(encoded.AsSpan(i + 1, 4), NumberStyles.AllowHexSpecifier,
                                CultureInfo.InvariantCulture, out var code))
                        { errorOffset = start; return false; }
                        c = (char)code;
                        i += 4;
                        break;
                    default: errorOffset = start; return false;
                }
            }
            if (result.Length == MaximumValueLength) return false;
            result.Append(c);
        }
        value = result.ToString();
        errorOffset = -1;
        return true;
    }

    public static CsvCellTextMetrics Measure(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var spaces = 0; var tabs = 0; var crlf = 0; var lf = 0; var cr = 0;
        for (var i = 0; i < value.Length; i++)
        {
            switch (value[i])
            {
                case ' ': spaces++; break;
                case '\t': tabs++; break;
                case '\r':
                    if (i + 1 < value.Length && value[i + 1] == '\n') { crlf++; i++; }
                    else cr++;
                    break;
                case '\n': lf++; break;
            }
        }
        return new(value.Length, spaces, tabs, crlf, lf, cr);
    }
}

public readonly record struct CsvCellTextMetrics(
    int Utf16Length, int Spaces, int Tabs, int CrLf, int Lf, int Cr);
