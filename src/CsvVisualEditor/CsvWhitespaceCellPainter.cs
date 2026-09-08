namespace CsvVisualEditor;

using System.Drawing.Drawing2D;

/// <summary>Paint-only space dots. Never changes Value, FormattedValue or clipboard data.</summary>
internal static class CsvWhitespaceCellPainter
{
    internal static readonly Color DotColor = Color.DarkOrange;
    internal const int MaximumPaintCharacters = 1024;

    internal static void Paint(DataGridView grid, DataGridViewCellPaintingEventArgs e, char marker = ' ', bool showSpaces = true, bool searchMatch = false)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.FormattedValue is not string value ||
            e.CellStyle is not { } style || e.Graphics is not { } graphics ||
            grid.IsCurrentCellInEditMode && grid.CurrentCellAddress == new Point(e.ColumnIndex, e.RowIndex))
            return;

        // Let WinForms own background, borders, selection, focus and error glyphs.
        var deferred = DataGridViewPaintParts.ContentForeground | DataGridViewPaintParts.Focus | DataGridViewPaintParts.ErrorIcon;
        e.Paint(e.ClipBounds, e.PaintParts & ~deferred);
        if (searchMatch && (e.State & DataGridViewElementStates.Selected) == 0 &&
            (e.PaintParts & DataGridViewPaintParts.Background) != 0)
        {
            var state = graphics.Save();
            try
            {
                graphics.SetClip(e.ClipBounds, CombineMode.Intersect);
                using var tint = new SolidBrush(Color.FromArgb(35, 235, 170, 35));
                var interior = Rectangle.Inflate(e.CellBounds, -1, -1);
                if (interior.Width > 0 && interior.Height > 0) graphics.FillRectangle(tint, interior);
            }
            finally { graphics.Restore(state); }
        }
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
        e.Paint(e.ClipBounds, e.PaintParts & (DataGridViewPaintParts.Focus | DataGridViewPaintParts.ErrorIcon));
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
            if (positions.Count == 0) return 0;
            using var dotBrush = new SolidBrush(DotColor);
            // Reserve at least one background pixel between adjacent dots, even in
            // narrow proportional fonts. Compute once per font/DPI, not per glyph.
            var spaceAdvance = graphics.MeasureString(" ", font, int.MaxValue, format).Width;
            var diameter = Math.Min(Math.Max(1, (int)Math.Floor(spaceAdvance) - 1),
                Math.Max(2, (int)Math.Round(2 * graphics.DpiX / 96f)));
            graphics.SmoothingMode = SmoothingMode.None;
            graphics.PixelOffsetMode = PixelOffsetMode.None;
            var drawn = 0;
            // GDI+ supports at most 32 measurable character ranges per batch.
            // The same layout/font renders and measures, including trailing spaces.
            for (var start = 0; start < positions.Count; start += 32)
            {
                var ranges = new CharacterRange[Math.Min(32, positions.Count - start)];
                for (var offset = 0; offset < ranges.Length; offset++)
                    ranges[offset] = new CharacterRange(positions[start + offset], 1);
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
                        DrawSpaceMarker(graphics, dotBrush, x, y, diameter);
                        drawn++;
                    }
                }
                finally { foreach (var region in regions) region.Dispose(); }
            }
            return drawn;
        }
        finally { graphics.Restore(state); }
    }

    internal static void DrawSpaceMarker(Graphics graphics, Brush brush, float centerX, float centerY, int diameter = 0)
    {
        // An outlined 2px ellipse occupies 3+ pixels and fuses adjacent spaces in
        // proportional UI fonts. A filled, integer-aligned dot stays within its box.
        if (diameter <= 0) diameter = Math.Max(2, (int)Math.Round(2 * graphics.DpiX / 96f));
        var x = (int)Math.Round(centerX - diameter / 2f, MidpointRounding.AwayFromZero);
        var y = (int)Math.Round(centerY - diameter / 2f, MidpointRounding.AwayFromZero);
        if (diameter <= 2) graphics.FillRectangle(brush, x, y, diameter, diameter);
        else graphics.FillEllipse(brush, x, y, diameter, diameter);
    }

    internal static string DescribeSpaces(string value)
    {
        var count = 0;
        foreach (var character in value) if (character == ' ') count++;
        var leading = 0;
        while (leading < value.Length && value[leading] == ' ') leading++;
        var trailing = 0;
        while (trailing < value.Length && value[value.Length - trailing - 1] == ' ') trailing++;
        return $"Spaces: {count}; leading: {leading}; trailing: {trailing}. Length: {value.Length} UTF-16 units.\nOrange solid dots mark empty spaces, not CSV characters. Leading/trailing counts overlap for an all-space cell.";
    }
}
