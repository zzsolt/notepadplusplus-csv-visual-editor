namespace CsvVisualEditor;

using CsvVisualEditor.Localization;

using System.Windows.Forms;

/// <summary>
/// Owns the visual row-identity lane without mixing logical record labels with
/// the native DataGridView row-header glyph lane.
/// </summary>
internal static class CsvGridRowPresentation
{
    internal const string RowIndicatorColumnName = "CsvRowIndicator";

    private const int MinimumIndicatorWidthLogicalPixels = 36;
    private const int IndicatorMeasurementMarginLogicalPixels = 10;

    internal static void ConfigureNativeRowHeaders(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (grid.IsDisposed || grid.Disposing || !grid.RowHeadersVisible)
        {
            return;
        }

        // Native glyph sizing is retained, but only displayed headers participate.
        // All header values are null, so scanning every one of many thousands of rows
        // adds cost without changing the glyph lane width.
        grid.RowHeadersWidthSizeMode =
            DataGridViewRowHeadersWidthSizeMode.AutoSizeToDisplayedHeaders;
        grid.RowHeadersDefaultCellStyle.Padding = Padding.Empty;
        grid.ShowEditingIcon = true;
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
                throw new InvalidOperationException(L10n.Get(TextKey.Rows_TheRowIndicatorColumnCouldNotBeResolved));
        }
        else
        {
            column = new DataGridViewTextBoxColumn
            {
                Name = RowIndicatorColumnName,
                HeaderText = "#",
                ToolTipText = L10n.Get(TextKey.Rows_SourceLogicalRecordOrPendingStructuralRow),
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
        column.Tag ??= "#";
    }

    internal static bool HasRowIndicatorColumn(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return grid.Columns.Contains(RowIndicatorColumnName);
    }

    internal static bool IsRowIndicatorColumn(DataGridViewColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        return string.Equals(column.Name, RowIndicatorColumnName, StringComparison.Ordinal);
    }

    internal static void SetSizingLabel(DataGridView grid, string label)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(label);

        if (!HasRowIndicatorColumn(grid))
        {
            return;
        }

        var column = grid.Columns[RowIndicatorColumnName] ??
            throw new InvalidOperationException(L10n.Get(TextKey.Rows_TheRowIndicatorColumnCouldNotBeResolved));
        if (column.Tag is not string current || label.Length > current.Length)
        {
            column.Tag = label;
        }
    }

    internal static void ResetSizingLabel(DataGridView grid, string label)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(label);

        if (HasRowIndicatorColumn(grid))
        {
            var column = grid.Columns[RowIndicatorColumnName] ??
                throw new InvalidOperationException(L10n.Get(TextKey.Rows_TheRowIndicatorColumnCouldNotBeResolved));
            column.Tag = label;
        }
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
            throw new InvalidOperationException(L10n.Get(TextKey.Rows_TheRowMustBelongToADataGridView));
        var column = grid.Columns[RowIndicatorColumnName] ??
            throw new InvalidOperationException(L10n.Get(TextKey.Table_TheRowIndicatorColumnIsNotConfigured));

        SetDetachedRowIndicator(row, column.Index, label, toolTipText);
        SetSizingLabel(grid, label);
    }

    internal static void SetDetachedRowIndicator(
        DataGridViewRow row,
        int indicatorColumnIndex,
        string label,
        string toolTipText)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(toolTipText);

        row.HeaderCell.Value = null;
        var cell = row.Cells[indicatorColumnIndex];
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
            throw new InvalidOperationException(L10n.Get(TextKey.Rows_TheRowIndicatorColumnCouldNotBeResolved));
        column.DisplayIndex = 0;
        column.MinimumWidth = ScaleLogicalPixels(grid, MinimumIndicatorWidthLogicalPixels);
        column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;

        var sizingLabel = column.Tag as string ?? "#";
        var measured = TextRenderer.MeasureText(
            sizingLabel,
            grid.Font,
            Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
        var padding = column.DefaultCellStyle.Padding.Horizontal;
        column.Width = Math.Max(
            column.MinimumWidth,
            measured + padding +
            ScaleLogicalPixels(grid, IndicatorMeasurementMarginLogicalPixels));
    }

    private static int ScaleLogicalPixels(DataGridView grid, int logicalPixels)
    {
        var dpi = grid.DeviceDpi > 0 ? grid.DeviceDpi : 96;
        return Math.Max(1, (int)Math.Ceiling(logicalPixels * dpi / 96d));
    }
}
