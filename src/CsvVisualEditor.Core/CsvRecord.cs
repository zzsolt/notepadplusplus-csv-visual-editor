namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;

/// <summary>
/// One logical CSV record. A record may span multiple physical lines when a quoted field contains line breaks.
/// </summary>
public sealed record CsvRecord
{
    public CsvRecord(int index, IEnumerable<CsvCell> cells, CsvSourceSpan sourceSpan)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Record index cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(cells);

        var copiedCells = cells.ToArray();
        if (copiedCells.Any(static cell => cell is null))
        {
            throw new ArgumentException("Cells cannot contain null values.", nameof(cells));
        }

        Index = index;
        Cells = Array.AsReadOnly(copiedCells);
        SourceSpan = sourceSpan;
    }

    public int Index { get; }

    public ReadOnlyCollection<CsvCell> Cells { get; }

    public CsvSourceSpan SourceSpan { get; }

    public int FieldCount => Cells.Count;

    public bool IsBlank =>
        Cells.Count == 1 &&
        Cells[0].Value.Length == 0 &&
        !Cells[0].IsQuoted;
}
