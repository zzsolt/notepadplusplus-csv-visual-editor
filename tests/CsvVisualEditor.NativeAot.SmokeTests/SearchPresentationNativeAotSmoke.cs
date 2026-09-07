namespace CsvVisualEditor.NativeAot.SmokeTests;

using System.Runtime.CompilerServices;

internal static class SearchPresentationNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        using var grid = new CsvDataGridView { Size = new Size(400, 130), AllowUserToAddRows = false };
        grid.Columns.Add("CsvColumn0", "First");
        grid.Columns.Add("CsvColumn1", "Second");
        grid.Rows.Add("  TEST  ", "other");
        grid.SetSearchHighlight("test", null);
        Require(grid.IsSearchMatch(0, 0, "  TEST  "), "Cell search must match Core ordinal-ignore-case semantics.");
        Require(!grid.IsSearchMatch(0, 1, "other"), "A matching row must not highlight unrelated cells.");
        grid.SetSearchHighlight("test", 1);
        Require(!grid.IsSearchMatch(0, 0, "TEST"), "Column scope must exclude other columns.");
        Require(grid.IsSearchMatch(0, 1, "test"), "Scoped column must match.");
        grid.SetSearchHighlight("test", null);
        Require(!grid.IsSearchMatch(0, 1, "reordered"), "New render must invalidate row-index match cache.");
        grid.ClearSelection();
        using var bitmap = new Bitmap(400, 130);
        grid.DrawToBitmap(bitmap, new Rectangle(0, 0, 400, 130));
        Require(Count(bitmap, Color.DarkGoldenrod) > 0, "Real grid painting must show a matching-cell frame.");
        Require((string?)grid.Rows[0].Cells[0].Value == "  TEST  ", "Highlight must preserve raw CSV values.");
        grid.SetSearchHighlight(string.Empty, null);
        grid.DrawToBitmap(bitmap, new Rectangle(0, 0, 400, 130));
        Require(Count(bitmap, Color.DarkGoldenrod) == 0, "Clearing search must remove the frame.");

        foreach (var dpi in new[] { 96, 144, 192 })
        {
            using var sample = new Bitmap(40, 40);
            sample.SetResolution(dpi, dpi);
            using var graphics = Graphics.FromImage(sample);
            using var pen = new Pen(CsvWhitespaceCellPainter.DotColor, Math.Max(1, (int)Math.Round(dpi / 96f)));
            int? expected = null;
            foreach (var offset in new[] { 0f, 0.2f, 0.49f, 0.7f, 0.95f })
            {
                graphics.Clear(Color.White);
                CsvWhitespaceCellPainter.DrawSpaceMarker(graphics, pen, 12 + offset, 16 + offset);
                var pixels = Count(sample, CsvWhitespaceCellPainter.DotColor);
                Require(pixels > 0 && (!expected.HasValue || pixels == expected),
                    "Fractional text positions must not vary the space marker raster size.");
                expected = pixels;
            }
        }
    }

    private static int Count(Bitmap bitmap, Color target)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).ToArgb() == target.ToArgb()) count++;
        return count;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
