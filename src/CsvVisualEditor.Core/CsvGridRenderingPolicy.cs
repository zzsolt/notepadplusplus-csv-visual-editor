namespace CsvVisualEditor.Core;

/// <summary>
/// Selects a lightweight read-only DataGridView representation before the number
/// of materialized WinForms cell objects becomes large enough to stall the host UI.
/// Editing remains backed by the complete stable row model and is materialized only
/// after the user explicitly enters Edit mode.
/// </summary>
public static class CsvGridRenderingPolicy
{
    public const int VirtualRowThreshold = 1_000;
    public const int VirtualCellThreshold = 40_000;

    public static bool ShouldUseVirtualReadOnlyRows(int rowCount, int columnCount)
    {
        if (rowCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowCount));
        }

        if (columnCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columnCount));
        }

        if (rowCount == 0 || columnCount == 0)
        {
            return false;
        }

        return rowCount >= VirtualRowThreshold ||
               (long)rowCount * columnCount >= VirtualCellThreshold;
    }
}
