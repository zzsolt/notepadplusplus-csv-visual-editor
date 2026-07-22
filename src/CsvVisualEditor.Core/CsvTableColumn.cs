namespace CsvVisualEditor.Core;

/// <summary>
/// One display-only visual table column.
/// </summary>
public sealed record CsvTableColumn
{
    public CsvTableColumn(int index, string name)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Column index cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Index = index;
        Name = name;
    }

    public int Index { get; }

    public string Name { get; }
}