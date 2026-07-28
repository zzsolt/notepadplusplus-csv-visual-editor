namespace CsvVisualEditor;

using System.Windows.Forms;

/// <summary>
/// Owns the visual row-identity lane without mixing logical record labels with
/// the native DataGridView row-header glyph lane. Native row headers remain the
/// current-row indicator and row-selection hit target; a normal read-only grid
/// column renders aligned row labels and pending structural markers.
/// </summary>
internal static class CsvGridRowPresentation
{
    internal const string RowIndicatorColumnName = "CsvRowIndicator";

    private const int MinimumIndicatorWidthLogicalPixels = 36;

    internal static void ConfigureNativeRowHeaders(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (grid.IsDisposed || grid.Disposing || !grid.RowHeadersVisible)
        {
            return;
        }

        // DataGridViewRowHeaderCell reserves a native leading lane for the
        // current-row arrow/pencil/star before it lays out any text. Keeping
        // labels out of that cell lets the framework own glyph, theme, DPI,
        // high-contrast, and accessibility behavior without optical shifts.
        grid.RowHeadersWidthSizeMode =
            DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders;
        grid.RowHeadersDefaultCellStyle.Padding = Padding.Empty;
        grid.ShowEditingIcon = true;

        // This plugin never stores row-level ErrorText. Disabling an unused
        // error-icon lane avoids reserving additional native header capacity.
        grid.ShowRowErrors = false;
        grid.SelectionMode = DataGridViewSelectionMode.RowHeaderSelect;
        grid.MultiSelect = true;
    }

    internal static void EnsureRowIndicatorColumn(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        DataGridViewColumn column;
        if (grid.Columns.Contains(RowIndicatorColumnName))
        {
            column = grid.Columns[RowIndicatorColumnName] ??
                throw new InvalidOperationException("The row-indicator column could not be resolved.");
        }
        else
        {
            column = new DataGridViewTextBoxColumn
            {
                Name = RowIndicatorColumnName,
                HeaderText = "#",
                ToolTipText = "Source logical record or pending structural row",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ReadOnly = true,
                Resizable = DataGridViewTriState.False,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    NullValue = string.Empty,
                    Padding = new Padding(3, 0, 3, 0),
                    WrapMode = DataGridViewTriState.False
                }
            };
            column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.Columns.Add(column);
        }

        column.ReadOnly = true;
        if (column.DisplayIndex != 0)
        {
            column.Frozen = false;
            column.DisplayIndex = 0;
        }

        column.Frozen = true;
        column.MinimumWidth = ScaleLogicalPixels(grid, MinimumIndicatorWidthLogicalPixels);
    }

    internal static bool HasRowIndicatorColumn(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return grid.Columns.Contains(RowIndicatorColumnName);
    }

    internal static bool IsRowIndicatorColumn(DataGridViewColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        return string.Equals(
            column.Name,
            RowIndicatorColumnName,
            StringComparison.Ordinal);
    }

    internal static void SetRowIndicator(
        DataGridViewRow row,
        string label,
        string toolTipText)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(toolTipText);

        var grid = row.DataGridView ??
            throw new InvalidOperationException("The row must belong to a DataGridView.");
        var column = grid.Columns[RowIndicatorColumnName] ??
            throw new InvalidOperationException("The row-indicator column is not configured.");

        // A null native row-header value is intentional: the native cell paints
        // only its framework-owned current-row glyph while the adjacent normal
        // cell provides one stable, aligned label column for every row.
        row.HeaderCell.Value = null;
        var cell = row.Cells[column.Index];
        cell.Value = label;
        cell.ToolTipText = toolTipText;
    }

    internal static void RefreshLayout(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (grid.IsDisposed || grid.Disposing)
        {
            return;
        }

        ConfigureNativeRowHeaders(grid);
        if (!HasRowIndicatorColumn(grid))
        {
            return;
        }

        var column = grid.Columns[RowIndicatorColumnName] ??
            throw new InvalidOperationException("The row-indicator column could not be resolved.");
        column.DisplayIndex = 0;
        column.MinimumWidth = ScaleLogicalPixels(grid, MinimumIndicatorWidthLogicalPixels);
        column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;

        // All row labels are bounded by the table's 10,000-row limit. One
        // explicit content measurement after a render is deterministic and
        // avoids a permanent mode-sized band or continuous auto-size work.
        grid.AutoResizeColumn(
            column.Index,
            DataGridViewAutoSizeColumnMode.AllCells);
        if (column.Width < column.MinimumWidth)
        {
            column.Width = column.MinimumWidth;
        }
    }

    private static int ScaleLogicalPixels(DataGridView grid, int logicalPixels)
    {
        var dpi = grid.DeviceDpi > 0 ? grid.DeviceDpi : 96;
        return Math.Max(
            1,
            (int)Math.Ceiling(logicalPixels * dpi / 96d));
    }
}
