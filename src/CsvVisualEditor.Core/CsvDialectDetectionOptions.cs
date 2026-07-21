namespace CsvVisualEditor.Core;

/// <summary>
/// Bounds automatic dialect detection so the result is deterministic and suitable for interactive use.
/// </summary>
public sealed record CsvDialectDetectionOptions
{
    public static CsvDialectDetectionOptions Default { get; } = new();

    public int MaximumLogicalRecords { get; init; } = 20;

    public int MaximumSampleCharacters { get; init; } = 1_048_576;

    public int MinimumSuggestionScore { get; init; } = 35;

    internal void Validate()
    {
        if (MaximumLogicalRecords <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumLogicalRecords),
                MaximumLogicalRecords,
                "At least one logical record must be sampled.");
        }

        if (MaximumSampleCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumSampleCharacters),
                MaximumSampleCharacters,
                "At least one character must be available for sampling.");
        }

        if (MinimumSuggestionScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumSuggestionScore),
                MinimumSuggestionScore,
                "Minimum score must be between 0 and 100.");
        }
    }
}
