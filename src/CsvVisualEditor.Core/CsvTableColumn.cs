namespace CsvVisualEditor.Core;

/// <summary>
/// One display-only visual table column.
/// </summary>
public sealed record CsvTableColumn
{
    public CsvTableColumn(int index, string name, bool isGeneratedName = false)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Column index cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Index = index;
        Name = name;
        IsGeneratedName = isGeneratedName;
    }

    public int Index { get; }

    public string Name { get; }

    /// <summary>Only synthetic labels may be translated; actual header text remains data.</summary>
    public bool IsGeneratedName { get; }
}