namespace CsvVisualEditor.Core;

/// <summary>Structured optional presentation metadata, independent of UI language.</summary>
public enum CsvUserError
{
    FilterValueTooLong, InvalidDecimal, TooManyFilters, TooManySortLevels, DuplicateSortColumn,
    InvalidViewColumn, ClipboardTooLarge, ClipboardTooManyRows, ClipboardTooManyColumns,
    ClipboardNotRectangular, ClipboardTooManyCells, ClipboardControlCharacter, ProjectionTooManyColumns,
    ProjectionTooManyCells
}

public sealed class CsvErrorDetails
{
    private const string MetadataKey = "CsvVisualEditor.UserError";
    private CsvErrorDetails(CsvUserError code, object[] arguments)
    {
        Code = code;
        Arguments = Array.AsReadOnly((object[])arguments.Clone());
    }
    public CsvUserError Code { get; }
    public IReadOnlyList<object> Arguments { get; }
    public static T With<T>(T exception, CsvUserError code, params object[] arguments) where T : Exception
    {
        exception.Data[MetadataKey] = new CsvErrorDetails(code, arguments);
        return exception;
    }
    public static CsvErrorDetails? From(Exception exception) => exception.Data[MetadataKey] as CsvErrorDetails;
}
