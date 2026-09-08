namespace CsvVisualEditor;

using System.Drawing.Drawing2D;

/// <summary>Original vector-drawn command icons; no external image/font dependency.</summary>
internal static class CsvCommandIcons
{
    internal static Bitmap Create(string command, Color foreground, int size)
    {
        var image = new Bitmap(size, size);
        using var g = Graphics.FromImage(image);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.ScaleTransform(size / 20f, size / 20f);
        using var ink = new Pen(foreground, 1.35f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var accent = new Pen(Color.FromArgb(40, 155, 210), 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var good = new Pen(Color.FromArgb(65, 170, 100), 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var warn = new Pen(Color.FromArgb(225, 125, 45), 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        void Line(Pen pen, float x1, float y1, float x2, float y2) => g.DrawLine(pen, x1, y1, x2, y2);
        void Page(float x, float y) { g.DrawRectangle(ink, x, y, 9, 12); Line(ink, x + 2, y + 4, x + 7, y + 4); Line(ink, x + 2, y + 7, x + 7, y + 7); }
        if (command.StartsWith("Copy", StringComparison.Ordinal)) { Page(3, 3); Page(7, 6); }
        else if (command.StartsWith("Paste", StringComparison.Ordinal)) { g.DrawRectangle(ink, 4, 4, 12, 14); g.DrawRectangle(accent, 7, 2, 6, 4); Line(ink, 7, 10, 13, 10); Line(ink, 7, 13, 13, 13); }
        else if (command.StartsWith("Cut", StringComparison.Ordinal)) { g.DrawEllipse(ink, 2, 12, 5, 5); g.DrawEllipse(ink, 12, 12, 5, 5); Line(accent, 5, 3, 14, 14); Line(accent, 15, 3, 6, 14); }
        else if (command.StartsWith("Source", StringComparison.Ordinal)) { Line(ink, 5, 4, 2, 10); Line(ink, 2, 10, 5, 16); Line(ink, 15, 4, 18, 10); Line(ink, 18, 10, 15, 16); Line(accent, 6, 10, 13, 10); Line(accent, 10, 7, 13, 10); Line(accent, 10, 13, 13, 10); }
        else if (command is "Edit" or "Exit Edit") { g.DrawPolygon(ink, [new PointF(4, 12), new(12, 3), new(16, 7), new(8, 16), new(3, 17)]); Line(accent, 11, 5, 14, 8); }
        else if (command.StartsWith("Add Row", StringComparison.Ordinal) || command.StartsWith("Delete Row", StringComparison.Ordinal)) { g.DrawRectangle(ink, 2, 3, 15, 13); Line(ink, 2, 7, 17, 7); Line(ink, 7, 3, 7, 16); Line(command.StartsWith("Add", StringComparison.Ordinal) ? good : warn, 11, 13, 18, 13); if (command.StartsWith("Add", StringComparison.Ordinal)) Line(good, 14.5f, 9.5f, 14.5f, 17); }
        else if (command.StartsWith("Apply", StringComparison.Ordinal)) { Line(good, 3, 10, 8, 15); Line(good, 8, 15, 17, 4); }
        else if (command.StartsWith("Revert", StringComparison.Ordinal)) { g.DrawArc(warn, 5, 5, 12, 11, 205, 280); Line(warn, 3, 3, 3, 9); Line(warn, 3, 9, 9, 9); }
        else if (command.StartsWith("Refresh", StringComparison.Ordinal)) { g.DrawArc(accent, 3, 3, 14, 14, 40, 285); Line(accent, 17, 3, 17, 8); Line(accent, 17, 8, 12, 8); }
        else if (command.StartsWith("Transform", StringComparison.Ordinal)) { Line(ink, 4, 16, 14, 6); Line(accent, 4, 3, 4, 7); Line(accent, 2, 5, 6, 5); Line(warn, 14, 1, 14, 4); Line(warn, 17, 5, 19, 5); }
        else if (command.StartsWith("Diagnostics", StringComparison.Ordinal)) { g.DrawEllipse(accent, 2, 2, 16, 16); Line(ink, 10, 9, 10, 14); using var dot = new SolidBrush(foreground); g.FillEllipse(dot, 9, 5, 2, 2); }
        else if (command == "Filter and sort") { g.DrawPolygon(ink, [new PointF(2, 3), new(18, 3), new(12, 10), new(12, 16), new(8, 18), new(8, 10)]); Line(accent, 4, 3, 16, 3); }
        else if (command == "Column summary") { Line(ink, 3, 3, 3, 17); Line(ink, 3, 17, 18, 17); g.DrawRectangle(accent, 6, 10, 2, 5); g.DrawRectangle(ink, 11, 6, 2, 9); g.DrawRectangle(accent, 16, 3, 2, 12); }
        else if (command == "Search") { g.DrawEllipse(ink, 3, 3, 10, 10); Line(ink, 12, 12, 17, 17); }
        else if (command == "Previous match") { Line(ink, 5, 12, 10, 7); Line(ink, 10, 7, 15, 12); }
        else if (command == "Next match") { Line(ink, 5, 8, 10, 13); Line(ink, 10, 13, 15, 8); }
        else if (command.StartsWith("Show spaces", StringComparison.Ordinal)) { using var dot = new SolidBrush(warn.Color); g.FillEllipse(dot, 8, 8, 3, 3); Line(ink, 2, 5, 2, 15); Line(ink, 18, 5, 18, 15); }
        else { Line(ink, 5, 5, 15, 15); Line(ink, 5, 15, 15, 5); }
        return image;
    }
}
