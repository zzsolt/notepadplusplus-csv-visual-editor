namespace CsvVisualEditor.NativeAot.SmokeTests;

using System.Runtime.CompilerServices;

internal static class WhitespacePaintingNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        using var bitmap = new Bitmap(500, 120);
        using var graphics = Graphics.FromImage(bitmap);
        using var font = new Font(FontFamily.GenericSansSerif, 12);
        var bounds = new RectangleF(10, 10, 460, 60);
        var clip = new Rectangle(10, 10, 460, 60);
        graphics.Clear(Color.White);
        Require(CsvWhitespaceCellPainter.Draw(graphics, bounds, clip, "  x  ", font,
            Color.Black, DataGridViewContentAlignment.MiddleLeft) == 4,
            "Painter must mark leading and trailing spaces.");
        Require(CountOrange(bitmap) > 0, "Painter must produce actual orange pixels.");
        var normalDotPixels = CountOrange(bitmap);
        Require(bitmap.GetPixel(0, 0).ToArgb() == Color.White.ToArgb(), "Painter must preserve clipping.");
        using (var highDpi = new Bitmap(500, 120))
        {
            highDpi.SetResolution(192, 192);
            using var highGraphics = Graphics.FromImage(highDpi);
            highGraphics.Clear(Color.White);
            Require(CsvWhitespaceCellPainter.Draw(highGraphics, bounds, clip, "  x  ", font,
                Color.Black, DataGridViewContentAlignment.MiddleLeft) == 4,
                "High DPI must preserve space count.");
            Require(CountOrange(highDpi) > normalDotPixels, "Dots must scale with DPI.");
        }
        Require(CsvWhitespaceCellPainter.Draw(graphics, bounds, clip, new string(' ', 40) + "x", font,
            Color.Black, DataGridViewContentAlignment.MiddleLeft) == 40,
            "Space measurement must support multiple 32-range batches.");

        graphics.Clear(Color.Black);
        Require(CsvWhitespaceCellPainter.Draw(graphics, bounds, clip, "a b", font,
            Color.White, DataGridViewContentAlignment.MiddleLeft) == 1,
            "Painter must mark internal spaces on a dark background.");
        Require(CountOrange(bitmap) > 0, "Dark background must retain orange dots.");

        graphics.Clear(Color.Blue);
        Require(CsvWhitespaceCellPainter.Draw(graphics, bounds, clip, CsvTransformDialog.Sample("  x  "), font,
            Color.White, DataGridViewContentAlignment.MiddleLeft, marker: '·') == 4,
            "Transform must paint original spaces only, not its length separator.");
        Require(CountOrange(bitmap) > 0, "Selection background must retain orange dots.");
        Require(CsvWhitespaceCellPainter.Draw(graphics, bounds, clip, CsvTransformDialog.Sample("·"), font,
            Color.White, DataGridViewContentAlignment.MiddleLeft, marker: '·') == 0,
            "A literal middle dot must not masquerade as a space marker.");
        Require(CsvWhitespaceCellPainter.Draw(graphics, bounds, clip, "", font,
            Color.White, DataGridViewContentAlignment.MiddleLeft) == 0,
            "Empty data must not create a dot.");

        // Exercise the real primary-grid event route and preserve raw clipboard input.
        using var grid = new CsvDataGridView { Size = new Size(400, 130), AllowUserToAddRows = false };
        grid.Columns.Add("CsvColumn0", "Value");
        grid.Columns[0].Width = 250;
        grid.Rows.Add("  x  ");
        grid.ClearSelection();
        using var gridBitmap = new Bitmap(400, 130);
        grid.DrawToBitmap(gridBitmap, new Rectangle(0, 0, 400, 130));
        Require(CountOrange(gridBitmap) > 0, "Primary grid must route painting to space dots.");
        Require((string?)grid.Rows[0].Cells[0].Value == "  x  " &&
            (string?)grid.Rows[0].Cells[0].FormattedValue == "  x  ",
            "Painting must not change raw or formatted clipboard/edit values.");

        using var virtualGrid = new CsvDataGridView
        {
            Size = new Size(400, 130), AllowUserToAddRows = false, VirtualMode = true
        };
        virtualGrid.Columns.Add("CsvColumn0", "Value");
        virtualGrid.Columns[0].Width = 250;
        virtualGrid.CellValueNeeded += (_, e) => e.Value = " x ";
        virtualGrid.RowCount = 1;
        virtualGrid.ClearSelection();
        virtualGrid.DrawToBitmap(gridBitmap, new Rectangle(0, 0, 400, 130));
        Require(CountOrange(gridBitmap) > 0, "Virtual grid must also paint whitespace.");
    }

    private static int CountOrange(Bitmap bitmap)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                if (color.R > 200 && color.G > 70 && color.G < 190 && color.B < 80) count++;
            }
        return count;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
