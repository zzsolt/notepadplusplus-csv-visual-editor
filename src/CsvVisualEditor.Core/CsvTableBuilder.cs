namespace CsvVisualEditor.Core;

/// <summary>
/// Applies safe delimiter-selection policy, parsing, and bounded table projection to decoded text.
/// </summary>
public static class CsvTableBuilder
{
    public static CsvTableBuildResult Build(
        string text,
        CsvTableBuildOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        options ??= CsvTableBuildOptions.Default;
        options.Validate();

        if (text.Length == 0)
        {
            return CsvTableBuildResult.Empty();
        }

        CsvDialectDetectionResult? detectionResult = null;
        CsvDialect dialect;
        bool delimiterWasAutomatic;

        if (options.DelimiterOverride is char delimiterOverride)
        {
            delimiterWasAutomatic = false;
            dialect = CsvDialect.Create(
                delimiterOverride,
                headerMode: options.HeaderMode);
        }
        else
        {
            delimiterWasAutomatic = true;
            detectionResult = CsvDialectDetector.Detect(text);
            if (!detectionResult.IsReliable || detectionResult.SuggestedDialect is null)
            {
                return CsvTableBuildResult.DelimiterSelectionRequired(detectionResult);
            }

            dialect = CsvDialect.Create(
                detectionResult.SuggestedDialect.Delimiter,
                headerMode: options.HeaderMode);
        }

        var parseResult = CsvParser.Parse(text, dialect);
        var projection = CsvTableProjector.Create(
            parseResult,
            new CsvTableProjectionOptions
            {
                HeaderMode = options.HeaderMode,
                MaximumRows = options.MaximumRows,
                MaximumColumns = options.MaximumColumns,
                MaximumCells = options.MaximumCells
            });

        return CsvTableBuildResult.Ready(
            detectionResult,
            parseResult,
            projection,
            delimiterWasAutomatic);
    }
}