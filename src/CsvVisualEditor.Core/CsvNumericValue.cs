namespace CsvVisualEditor.Core;

using System.Globalization;

/// <summary>
/// Explicit exact decimal interpretation for view-only comparisons. No implicit
/// locale, thousands separators, exponent, overflow, underflow or rounding.
/// </summary>
public static class CsvNumericValue
{
    public static bool TryParse(string? value, out decimal number)
    {
        number = 0;
        if (value is null) return false;
        var text = value.AsSpan().Trim();
        if (text.IsEmpty) return false;
        var negative = text[0] == '-';
        if (text[0] is '-' or '+') text = text[1..];
        if (text.IsEmpty) return false;
        var dot = -1;
        var digits = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is >= '0' and <= '9') { digits++; continue; }
            if (text[i] == '.' && dot < 0) { dot = i; continue; }
            return false;
        }
        if (digits == 0) return false;
        var integer = (dot < 0 ? text : text[..dot]).TrimStart('0');
        var fraction = dot < 0 ? ReadOnlySpan<char>.Empty : text[(dot + 1)..].TrimEnd('0');
        if (integer.Length > 29 || fraction.Length > 28) return false;
        if (integer.IsEmpty && fraction.IsEmpty) negative = false;
        Span<char> canonical = stackalloc char[64];
        var length = 0;
        if (negative) canonical[length++] = '-';
        if (integer.IsEmpty) canonical[length++] = '0';
        else { integer.CopyTo(canonical[length..]); length += integer.Length; }
        if (!fraction.IsEmpty)
        {
            canonical[length++] = '.';
            fraction.CopyTo(canonical[length..]);
            length += fraction.Length;
        }
        canonical = canonical[..length];
        if (!decimal.TryParse(canonical, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var parsed)) return false;
        // Decimal.TryParse may round a too-precise value. Require a lossless
        // canonical round trip; a false equality is worse than an explicit non-number.
        Span<char> formatted = stackalloc char[64];
        if (!parsed.TryFormat(formatted, out var written, "0.############################", CultureInfo.InvariantCulture) ||
            !canonical.SequenceEqual(formatted[..written])) return false;
        number = parsed;
        return true;
    }
}
