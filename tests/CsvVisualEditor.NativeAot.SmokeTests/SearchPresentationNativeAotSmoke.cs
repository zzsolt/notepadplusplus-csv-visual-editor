namespace CsvVisualEditor.NativeAot.SmokeTests;

using System.Runtime.CompilerServices;
using CsvVisualEditor.Core;

internal static class SearchPresentationNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        using var grid = new CsvDataGridView { Size = new Size(400, 130), AllowUserToAddRows = false };
        grid.Columns.Add("CsvColumn0", "First");
        grid.Columns.Add("CsvColumn1", "Second");
        grid.Rows.Add("  TEST  ", "other");
        var view = new CsvTableViewResult(
            [new CsvTableRow(0, ["  TEST  ", "other"], new CsvSourceSpan(0, 1))], 1,
            "test", null, null, CsvTableSortDirection.None);
        var results = CsvCellSearchIndex.Create(view);
        grid.SetSearchResults(results);
        Require(grid.IsSearchMatch(0, 0), "Indexed cell must match Core ordinal-ignore-case semantics.");
        Require(!grid.IsSearchMatch(0, 1), "A matching row must not highlight unrelated cells.");
        grid.CurrentCell = grid.Rows[0].Cells[1];
        grid.ClearSelection();
        using var bitmap = new Bitmap(400, 130);
        grid.DrawToBitmap(bitmap, new Rectangle(0, 0, 400, 130));
        Require(Count(bitmap, Color.DarkGoldenrod) > 0, "Real grid painting must show a matching-cell frame.");
        Require((string?)grid.Rows[0].Cells[0].Value == "  TEST  ", "Highlight must preserve raw CSV values.");
        grid.SetSearchResults(null);
        grid.DrawToBitmap(bitmap, new Rectangle(0, 0, 400, 130));
        Require(Count(bitmap, Color.DarkGoldenrod) == 0, "Clearing search must remove the frame.");
        grid.SetSearchResults(results);
        grid.Rows[0].Cells[0].Value = "changed";
        Require(!grid.IsSearchMatch(0, 0), "In-place changes must invalidate indexed matches.");
        grid.SetSearchResults(results);
        grid.Rows.Clear();
        Require(!grid.IsSearchMatch(0, 0), "Removing rows must invalidate indexed matches.");

        foreach (var dpi in new[] { 96, 120, 144, 168, 192 })
        {
            using var sample = new Bitmap(80, 80);
            sample.SetResolution(dpi, dpi);
            using var graphics = Graphics.FromImage(sample);
            using var brush = new SolidBrush(CsvWhitespaceCellPainter.DotColor);
            int? expected = null;
            foreach (var offset in new[] { 0f, 0.2f, 0.49f, 0.7f, 0.95f })
            {
                graphics.Clear(Color.White);
                CsvWhitespaceCellPainter.DrawSpaceMarker(graphics, brush, 12 + offset, 16 + offset);
                var pixels = Count(sample, CsvWhitespaceCellPainter.DotColor);
                Require(pixels > 0 && (!expected.HasValue || pixels == expected),
                    "Fractional positions must not vary the space marker raster size.");
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
