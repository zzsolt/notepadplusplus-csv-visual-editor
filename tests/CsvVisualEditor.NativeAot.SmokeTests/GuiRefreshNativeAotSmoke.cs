namespace CsvVisualEditor.NativeAot.SmokeTests;

using System.Runtime.CompilerServices;
using CsvVisualEditor.Core;

internal static class GuiRefreshNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        Directory.CreateDirectory("artifacts/ui-review");
        TestConsecutiveSpaces();
        TestSearchBar();
        TestGridValues();
        TestAbout();
        TestSurfaceLifetime();
        Console.WriteLine("GUI refresh: consecutive-space pixel separation, responsive search, About and command lifecycle PASS.");
    }

    private static void TestConsecutiveSpaces()
    {
        foreach (var family in new[] { SystemFonts.MessageBoxFont!.FontFamily.Name, "Consolas" })
        foreach (var dpi in new[] { 96, 120, 144, 168, 192 })
        foreach (var fontSize in new[] { 9f, 10f, 12f })
        {
            using var bitmap = new Bitmap(1000, 110);
            bitmap.SetResolution(dpi, dpi);
            using var graphics = Graphics.FromImage(bitmap);
            using var font = new Font(family, fontSize);
            foreach (var value in new[] { "   ", "   alpha", "alpha   ", "a   b", new string(' ', 40) })
            {
                graphics.Clear(Color.White);
                var expected = value.Count(c => c == ' ');
                var drawn = CsvWhitespaceCellPainter.Draw(graphics, new RectangleF(12, 12, 970, 80),
                    new Rectangle(0, 0, 1000, 110), value, font, Color.Black, DataGridViewContentAlignment.MiddleLeft);
                var components = OrangeComponents(bitmap);
                Require(drawn == expected && components.Count == expected,
                    $"Each space must remain a separate dot: dpi={dpi}, font={fontSize}, expected={expected}, drawn={drawn}, components={components.Count}.");
                Require(components.All(area => area == components[0]), "All dots in a run must occupy the same pixel area.");
                if (fontSize == 9 && value == "   " && family != "Consolas") bitmap.Save($"artifacts/ui-review/three-spaces-{dpi}.png");
                if (fontSize == 10 && value == "   " && family == "Consolas") bitmap.Save($"artifacts/ui-review/three-spaces-consolas-{dpi}.png");
            }
        }
    }

    private static List<int> OrangeComponents(Bitmap bitmap)
    {
        var pixels = new HashSet<Point>();
        var color = CsvWhitespaceCellPainter.DotColor.ToArgb();
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).ToArgb() == color) pixels.Add(new Point(x, y));
        var components = new List<int>();
        var queue = new Queue<Point>();
        while (pixels.Count > 0)
        {
            var first = pixels.First();
            pixels.Remove(first);
            queue.Enqueue(first);
            var area = 0;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                area++;
                for (var dy = -1; dy <= 1; dy++)
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var neighbor = new Point(current.X + dx, current.Y + dy);
                        if (pixels.Remove(neighbor)) queue.Enqueue(neighbor);
                    }
            }
            components.Add(area);
        }
        return components;
    }

    private static void TestSearchBar()
    {
        foreach (var width in new[] { 260, 400, 750 })
        foreach (var dark in new[] { false, true })
        {
            using var form = new Form { ClientSize = new Size(width, 180), ShowInTaskbar = false };
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 1 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var bar = new CsvSearchBar();
            layout.Controls.Add(bar, 0, 0);
            form.Controls.Add(layout);
            bar.Column.Items.AddRange(["All columns", "Value", "A&B"]);
            bar.Column.SelectedIndex = 0;
            bar.Query.Text = "alpha";
            bar.SetResults(1, 7, true);
            bar.ApplyAppearance(dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control,
                dark ? Color.Gainsboro : Color.Black);
            var navigation = 0;
            bar.NavigateRequested += backwards => navigation += backwards ? -1 : 1;
            bar.ClearRequested += () => bar.Query.Clear();
            form.Show();
            Application.DoEvents();
            form.PerformLayout();
            bar.PerformLayout();
            using var bitmap = new Bitmap(width, Math.Max(1, bar.Height));
            bar.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.Save($"artifacts/ui-review/search-{width}-{(dark ? "dark" : "light")}.png");
            Require(bar.Query.Width >= 90, "The query must remain usable at narrow dock widths.");
            Require(bar.Query.Right < bar.Clear.Left, "Query and clear button must not overlap.");
            Require(bar.Column.Right <= bar.ResultLabel.Left, "Scope and result count must not overlap.");
            Require(bar.ResultLabel.Right <= bar.Previous.Left, "Results and navigation must not overlap.");
            Require(bar.Next.Right <= bar.ClientSize.Width, "Navigation must stay inside the search bar.");
            Require(bar.Next.Bottom <= bar.ClientSize.Height, "Narrow-dock second row must not be clipped.");
            var narrow = bar.ClientSize.Width < Math.Max(1, (int)Math.Round(340 * bar.DeviceDpi / 96d));
            Require(bar.ResultLabel.Text == (narrow ? "2 / 7" : "2 / 7 cells"),
                "Results must identify the current cell and total, using the compact caption only at narrow widths.");
            Require(bar.Column.Width >= (int)Math.Round(90 * bar.DeviceDpi / 96d),
                "The compact result caption must leave enough width for the column selector.");
            bar.Next.PerformClick();
            bar.Previous.PerformClick();
            Require(navigation == 0, "Next and previous must dispatch once each.");
            bar.Clear.PerformClick();
            Require(bar.Query.TextLength == 0, "Clear must update the actual search input.");
            bar.Query.Enabled = false;
            Require(!bar.Next.Enabled && !bar.Previous.Enabled && !bar.Clear.Enabled, "Unavailable search must disable commands.");
            form.Close();
        }
    }

    private static void TestGridValues()
    {
        foreach (var dark in new[] { false, true })
        {
            using var form = new Form { ClientSize = new Size(720, 190), ShowInTaskbar = false };
            var background = dark ? Color.FromArgb(32, 32, 32) : Color.White;
            var foreground = dark ? Color.Gainsboro : Color.Black;
            var grid = new CsvDataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = false, BackgroundColor = background,
                EnableHeadersVisualStyles = false, GridColor = Color.Gray, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };
            grid.DefaultCellStyle.BackColor = background;
            grid.DefaultCellStyle.ForeColor = foreground;
            grid.ColumnHeadersDefaultCellStyle.BackColor = background;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = foreground;
            grid.Columns.Add("CsvColumn0", "Value");
            grid.Columns.Add("CsvColumn1", "Description");
            grid.Columns[0].Width = 320;
            grid.Columns[1].Width = 330;
            grid.Rows.Add("   ", "Three empty spaces");
            grid.Rows.Add(" sample ", "A matching value");
            grid.Rows.Add("", "An empty value");
            var view = new CsvTableViewResult(
                [new CsvTableRow(0, ["   ", "Three empty spaces"], new CsvSourceSpan(0, 1)),
                 new CsvTableRow(1, [" sample ", "A matching value"], new CsvSourceSpan(1, 1)),
                 new CsvTableRow(2, ["", "An empty value"], new CsvSourceSpan(2, 1))],
                3, "sample", null, null, CsvTableSortDirection.None);
            grid.SetSearchResults(CsvCellSearchIndex.Create(view));
            form.Controls.Add(grid);
            form.Show();
            grid.ClearSelection();
            grid.CurrentCell = grid.Rows[1].Cells[0];
            Application.DoEvents();
            using var image = new Bitmap(grid.Width, grid.Height);
            grid.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
            using var cell = image.Clone(grid.GetCellDisplayRectangle(0, 0, false), image.PixelFormat);
            var components = OrangeComponents(cell);
            image.Save($"artifacts/ui-review/grid-{(dark ? "dark" : "light")}.png");
            Require(components.Count == 3 && components.All(area => area >= 4),
                "Actual grid value font must expose three separated, visible solid dots, not one-pixel specks.");
            Require((string?)grid.Rows[0].Cells[0].Value == "   ", "Monospaced value display must preserve real spaces.");
            form.Close();
        }
    }

    private static void TestAbout()
    {
        foreach (var dark in new[] { false, true })
        {
            using var about = new CsvAboutDialog(dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control,
                dark ? Color.Gainsboro : SystemColors.ControlText);
            about.Show();
            Application.DoEvents();
            var controls = Descendants(about).ToArray();
            Require(controls.Any(c => c.Text.Contains(CsvAboutDialog.DeveloperName, StringComparison.Ordinal)), "About must show the developer.");
            Require(controls.Any(c => c.Text.Contains(CsvAboutDialog.ContactEmail, StringComparison.Ordinal)), "About must show the approved contact.");
            Require(controls.Any(c => c.Text == CsvAboutDialog.SupportText), "About must include the support invitation.");
            Require(CsvAboutDialog.DisplayVersion.Length > 0, "Version must be available in Native AOT.");
            using var image = new Bitmap(about.Width, about.Height);
            about.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
            image.Save($"artifacts/ui-review/about-{(dark ? "dark" : "light")}.png");
            Require(about.CancelButton is Button close && close.Focused, "About must focus Close, not select version text.");
            about.Close();
        }
    }

    private static void TestSurfaceLifetime()
    {
        using var strip = new ToolStrip();
        var copy = new ToolStripButton("Copy") { ToolTipText = "Copy the selected cells" };
        strip.Items.Add(copy);
        var count = 0;
        copy.Click += (_, _) => count++;
        var originalTip = copy.ToolTipText;
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var surface = new CsvCommandSurface(strip);
            surface.ApplyAppearance(SystemColors.Control, Color.Black, 96);
            var command = surface.Edit.DropDownItems.OfType<ToolStripMenuItem>().Single();
            command.PerformClick();
            Require(copy.ToolTipText == originalTip, "Reinstalling the menu must not accumulate tooltip prefixes.");
            surface.Dispose();
            surface.Dispose();
        }
        Require(count == 3, "Reinstall/dispose must not duplicate handlers.");
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
