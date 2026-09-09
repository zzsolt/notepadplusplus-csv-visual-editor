namespace CsvVisualEditor.Localization;

using System.Text;

/// <summary>Validates composite-format placeholders without regex or reflection.</summary>
public static class MessageFormat
{
    public static bool HasSameArguments(string source, string target)
    {
        try
        {
            _ = CompositeFormat.Parse(source);
            _ = CompositeFormat.Parse(target);
            return Arguments(source).Order(StringComparer.Ordinal)
                .SequenceEqual(Arguments(target).Order(StringComparer.Ordinal));
        }
        catch (FormatException) { return false; }
    }

    private static IEnumerable<string> Arguments(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '{') continue;
            if (i + 1 < text.Length && text[i + 1] == '{') { i++; continue; }
            var start = i;
            while (++i < text.Length && text[i] != '}') { }
            if (i >= text.Length) throw new FormatException();
            yield return text[start..(i + 1)];
        }
    }
}
