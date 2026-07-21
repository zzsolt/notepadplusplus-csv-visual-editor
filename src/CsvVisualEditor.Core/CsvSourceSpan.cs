namespace CsvVisualEditor.Core;

/// <summary>
/// Zero-based character span in the decoded editor text.
/// </summary>
public readonly record struct CsvSourceSpan
{
    public CsvSourceSpan(int start, int length)
    {
        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, "Span start cannot be negative.");
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "Span length cannot be negative.");
        }

        Start = start;
        Length = length;
    }

    public int Start { get; }

    public int Length { get; }

    public int End => checked(Start + Length);
}
