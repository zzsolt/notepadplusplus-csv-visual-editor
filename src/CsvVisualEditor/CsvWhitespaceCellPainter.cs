namespace CsvVisualEditor;

using System.Drawing.Drawing2D;

/// <summary>Paint-only space dots. Never changes Value, FormattedValue or clipboard data.</summary>
internal static class CsvWhitespaceCellPainter
{
    internal static readonly Color DotColor = Color.DarkOrange;
    internal const int MaximumPaintCharacters = 1024;

    internal static void Paint(DataGridView grid, DataGridViewCellPaintingEventArgs e, char marker = ' ', bool showSpaces = true)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.FormattedValue is not string value ||
            e.CellStyle is not { } style || e.Graphics is not { } graphics ||
            grid.IsCurrentCellInEditMode && grid.CurrentCellAddress == new Point(e.ColumnIndex, e.RowIndex))
            return;

        // Let WinForms own background, borders, selection, focus and error glyphs.
        e.Paint(e.ClipBounds, e.PaintParts & ~DataGridViewPaintParts.ContentForeground);
        if ((e.PaintParts & DataGridViewPaintParts.ContentForeground) != 0)
        {
            var padding = style.Padding;
            var bounds = new RectangleF(e.CellBounds.Left + padding.Left + 2,
                e.CellBounds.Top + padding.Top + 1,
                e.CellBounds.Width - padding.Horizontal - 4,
                e.CellBounds.Height - padding.Vertical - 2);
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                var selected = (e.State & DataGridViewElementStates.Selected) != 0;
                Draw(graphics, bounds, Rectangle.Intersect(e.ClipBounds, e.CellBounds),
                    value, style.Font ?? grid.Font,
                    selected ? style.SelectionForeColor : style.ForeColor,
                    style.Alignment, grid.RightToLeft == RightToLeft.Yes, marker, showSpaces);
            }
        }
        e.Handled = true;
    }

    internal static int Draw(Graphics graphics, RectangleF bounds, Rectangle clip,
        string value, Font font, Color foreground, DataGridViewContentAlignment alignment,
        bool rightToLeft = false, char marker = ' ', bool showSpaces = true)
    {
        var length = Math.Min(value.Length, MaximumPaintCharacters);
        if (length < value.Length && length > 0 && char.IsHighSurrogate(value[length - 1])) length--;
        var source = value[..length];
        // Keep the grid single-line. These are display escapes only, not CSV changes.
        var text = source.Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);
        if (length < value.Length) text += "…";
        var positions = new List<int>();
        for (var index = 0; index < text.Length; index++)
            if (showSpaces && text[index] == marker) positions.Add(index);
        if (marker != ' ') text = text.Replace(marker, ' ');

        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
        if (rightToLeft) format.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
        format.Trimming = StringTrimming.EllipsisCharacter;
        format.Alignment = alignment switch
        {
            DataGridViewContentAlignment.TopCenter or DataGridViewContentAlignment.MiddleCenter or DataGridViewContentAlignment.BottomCenter => StringAlignment.Center,
            DataGridViewContentAlignment.TopRight or DataGridViewContentAlignment.MiddleRight or DataGridViewContentAlignment.BottomRight => StringAlignment.Far,
            _ => StringAlignment.Near
        };
        format.LineAlignment = alignment switch
        {
            DataGridViewContentAlignment.TopLeft or DataGridViewContentAlignment.TopCenter or DataGridViewContentAlignment.TopRight => StringAlignment.Near,
            DataGridViewContentAlignment.BottomLeft or DataGridViewContentAlignment.BottomCenter or DataGridViewContentAlignment.BottomRight => StringAlignment.Far,
            _ => StringAlignment.Center
        };

        var state = graphics.Save();
        try
        {
            graphics.SetClip(clip, CombineMode.Intersect);
            graphics.SetClip(bounds, CombineMode.Intersect);
            using var textBrush = new SolidBrush(foreground);
            graphics.DrawString(text, font, textBrush, bounds, format);
            using var dotPen = new Pen(DotColor, Math.Max(1, (int)Math.Round(graphics.DpiX / 96f)));
            graphics.SmoothingMode = SmoothingMode.None;
            graphics.PixelOffsetMode = PixelOffsetMode.None;
            var drawn = 0;
            // GDI+ supports at most 32 measurable character ranges per batch.
            // The same layout/font renders and measures, including trailing spaces.
            for (var start = 0; start < positions.Count; start += 32)
            {
                var ranges = positions.Skip(start).Take(32).Select(static index => new CharacterRange(index, 1)).ToArray();
                format.SetMeasurableCharacterRanges(ranges);
                var regions = graphics.MeasureCharacterRanges(text, font, bounds, format);
                try
                {
                    foreach (var region in regions)
                    {
                        var area = region.GetBounds(graphics);
                        var x = area.Left + area.Width / 2;
                        var y = area.Top + area.Height / 2;
                        if (area.Width <= 0 || area.Height <= 0 || !bounds.Contains(x, y) || !clip.Contains((int)x, (int)y)) continue;
                        DrawSpaceMarker(graphics, dotPen, x, y);
                        drawn++;
                    }
                }
                finally { foreach (var region in regions) region.Dispose(); }
            }
            return drawn;
        }
        finally { graphics.Restore(state); }
    }

    internal static void DrawSpaceMarker(Graphics graphics, Pen pen, float centerX, float centerY)
    {
        // One fixed device-pixel shape per DPI, independent of glyph advance.
        // Caller sets non-antialiased pixel geometry once for the whole paint pass.
        var diameter = Math.Max(2, (int)Math.Round(2 * graphics.DpiX / 96f));
        graphics.DrawEllipse(pen, (int)Math.Round(centerX - diameter / 2f),
            (int)Math.Round(centerY - diameter / 2f), diameter, diameter);
    }

    internal static string DescribeSpaces(string value)
    {
        var count = 0;
        foreach (var character in value) if (character == ' ') count++;
        var leading = 0;
        while (leading < value.Length && value[leading] == ' ') leading++;
        var trailing = 0;
        while (trailing < value.Length && value[value.Length - trailing - 1] == ' ') trailing++;
        return $"Spaces: {count}; leading: {leading}; trailing: {trailing}. Length: {value.Length} UTF-16 units.\nOrange hollow dots mark empty spaces, not CSV characters. Leading/trailing counts overlap for an all-space cell.";
    }
}
