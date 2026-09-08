namespace CsvVisualEditor.NativeAot.SmokeTests;

using System.Runtime.CompilerServices;

/// <summary>Regresses fallback-glyph region heights seen in the real docking form.</summary>
internal static class SpaceBaselineNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        foreach (var family in new[] { SystemFonts.MessageBoxFont!.FontFamily.Name, "Consolas" })
        foreach (var dpi in new[] { 96, 120, 144, 168, 192 })
        foreach (var height in new[] { 25, 26, 41 })
        {
            using var bitmap = new Bitmap(520, 110);
            bitmap.SetResolution(dpi, dpi);
            using var graphics = Graphics.FromImage(bitmap);
            using var font = new Font(family, 9f);
            foreach (var value in new[] { "   ", "  alpha ", "a   b", " \u2003  " })
            {
                graphics.Clear(Color.White);
                var bounds = new RectangleF(8, 9, 500, height);
                CsvWhitespaceCellPainter.Draw(graphics, bounds, new Rectangle(0, 0, 520, 110),
                    value, font, Color.Black, DataGridViewContentAlignment.MiddleLeft);
                var dots = FindDots(bitmap);
                if (dots.Count != 3 || dots.Any(dot => dot.Top != dots[0].Top || dot.Bottom != dots[0].Bottom))
                    throw new InvalidOperationException($"Space markers must share one baseline: font={family}, dpi={dpi}, height={height}.");
            }
        }
        Console.WriteLine("Space baseline: equal vertical bounds in proportional/fallback glyph layouts PASS.");
    }

    private static List<Rectangle> FindDots(Bitmap bitmap)
    {
        var pixels = new HashSet<Point>();
        var color = CsvWhitespaceCellPainter.DotColor.ToArgb();
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).ToArgb() == color) pixels.Add(new Point(x, y));
        var result = new List<Rectangle>();
        var queue = new Queue<Point>();
        while (pixels.Count != 0)
        {
            var first = pixels.First();
            pixels.Remove(first);
            queue.Enqueue(first);
            var left = first.X;
            var right = first.X;
            var top = first.Y;
            var bottom = first.Y;
            while (queue.Count != 0)
            {
                var point = queue.Dequeue();
                left = Math.Min(left, point.X);
                right = Math.Max(right, point.X);
                top = Math.Min(top, point.Y);
                bottom = Math.Max(bottom, point.Y);
                for (var y = -1; y <= 1; y++)
                    for (var x = -1; x <= 1; x++)
                    {
                        var neighbor = new Point(point.X + x, point.Y + y);
                        if (pixels.Remove(neighbor)) queue.Enqueue(neighbor);
                    }
            }
            result.Add(Rectangle.FromLTRB(left, top, right + 1, bottom + 1));
        }
        return result;
    }
}
