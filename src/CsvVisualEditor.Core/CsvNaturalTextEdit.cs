namespace CsvVisualEditor.Core;

/// <summary>
/// Maps an ordinary multiline text edit back to exact cell data. The unchanged
/// prefix/suffix keep their original CRLF/LF/CR sequences; only inserted line
/// breaks use the cell's preferred convention. Backslashes are always literal.
/// </summary>
public static class CsvNaturalTextEdit
{
    public static string ToDisplay(string value) =>
        Normalize(value).Replace("\n", "\r\n", StringComparison.Ordinal);

    public static string PreferredLineEnding(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\n') return "\n";
            if (value[i] == '\r') return i + 1 < value.Length && value[i + 1] == '\n' ? "\r\n" : "\r";
        }
        return "\r\n";
    }

    public static bool TryApply(string previous, string editedDisplay, string newLineEnding, out string value)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(editedDisplay);
        if (newLineEnding is not ("\r\n" or "\n" or "\r"))
            throw new ArgumentException(nameof(newLineEnding));
        value = string.Empty;
        if (previous.Length > CsvCellTextCodec.MaximumValueLength ||
            editedDisplay.Length > 2 * CsvCellTextCodec.MaximumValueLength) return false;

        var before = Normalize(previous);
        var after = Normalize(editedDisplay);
        if (before == after) { value = previous; return true; }
        var prefix = 0;
        while (prefix < before.Length && prefix < after.Length && before[prefix] == after[prefix]) prefix++;
        var suffix = 0;
        while (suffix < before.Length - prefix && suffix < after.Length - prefix &&
               before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix]) suffix++;

        var start = RawOffset(previous, prefix);
        var end = RawOffset(previous, before.Length - suffix);
        var insertedStart = RawOffset(editedDisplay, prefix);
        var insertedEnd = RawOffset(editedDisplay, after.Length - suffix);
        var replacement = editedDisplay.Substring(insertedStart, insertedEnd - insertedStart);
        // Explicit LF/CR input (for example a paste) is already exact data. Only
        // the native editor's ordinary CRLF input adopts the preferred convention.
        var withoutPairs = replacement.Replace("\r\n", string.Empty, StringComparison.Ordinal);
        if (!withoutPairs.Contains('\r') && !withoutPairs.Contains('\n'))
            replacement = replacement.Replace("\r\n", newLineEnding, StringComparison.Ordinal);
        if (start + replacement.Length + previous.Length - end > CsvCellTextCodec.MaximumValueLength) return false;
        value = string.Concat(previous.AsSpan(0, start), replacement.AsSpan(), previous.AsSpan(end));
        return true;
    }

    private static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    private static int RawOffset(string raw, int normalizedOffset)
    {
        var rawIndex = 0;
        for (var count = 0; count < normalizedOffset; count++, rawIndex++)
            if (raw[rawIndex] == '\r' && rawIndex + 1 < raw.Length && raw[rawIndex + 1] == '\n') rawIndex++;
        return rawIndex;
    }
}
