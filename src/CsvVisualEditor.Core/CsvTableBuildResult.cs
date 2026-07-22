namespace CsvVisualEditor.Core;

/// <summary>
/// Host-independent outcome of converting decoded editor text into a safe visual table.
/// </summary>
public sealed record CsvTableBuildResult
{
    private CsvTableBuildResult(
        CsvTableBuildStatus status,
        CsvDialectDetectionResult? detectionResult,
        CsvParseResult? parseResult,
        CsvTableProjection? projection,
        bool delimiterWasAutomatic)
    {
        Status = status;
        DetectionResult = detectionResult;
        ParseResult = parseResult;
        Projection = projection;
        DelimiterWasAutomatic = delimiterWasAutomatic;
    }

    public CsvTableBuildStatus Status { get; }

    public CsvDialectDetectionResult? DetectionResult { get; }

    public CsvParseResult? ParseResult { get; }

    public CsvTableProjection? Projection { get; }

    public bool DelimiterWasAutomatic { get; }

    public static CsvTableBuildResult Empty() =>
        new(
            CsvTableBuildStatus.Empty,
            detectionResult: null,
            parseResult: null,
            projection: null,
            delimiterWasAutomatic: false);

    public static CsvTableBuildResult DelimiterSelectionRequired(
        CsvDialectDetectionResult detectionResult)
    {
        ArgumentNullException.ThrowIfNull(detectionResult);

        return new CsvTableBuildResult(
            CsvTableBuildStatus.DelimiterSelectionRequired,
            detectionResult,
            parseResult: null,
            projection: null,
            delimiterWasAutomatic: true);
    }

    public static CsvTableBuildResult Ready(
        CsvDialectDetectionResult? detectionResult,
        CsvParseResult parseResult,
        CsvTableProjection projection,
        bool delimiterWasAutomatic)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(projection);

        if (delimiterWasAutomatic && detectionResult is null)
        {
            throw new ArgumentException(
                "Automatic table builds must retain their detection result.",
                nameof(detectionResult));
        }

        return new CsvTableBuildResult(
            CsvTableBuildStatus.Ready,
            detectionResult,
            parseResult,
            projection,
            delimiterWasAutomatic);
    }
}

public enum CsvTableBuildStatus
{
    Empty,
    DelimiterSelectionRequired,
    Ready
}