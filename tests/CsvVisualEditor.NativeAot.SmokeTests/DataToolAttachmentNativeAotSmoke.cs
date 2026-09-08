namespace CsvVisualEditor.NativeAot.SmokeTests;

using System.Runtime.CompilerServices;

internal static class DataToolAttachmentNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        using var strip = new ToolStrip();
        using var filters = new ToolStripButton("Filter and sort");
        using var summary = new ToolStripButton("Column summary");
        using var separator = new ToolStripSeparator();
        var filterCalls = 0;
        var summaryCalls = 0;
        filters.Click += (_, _) => filterCalls++;
        summary.Click += (_, _) => summaryCalls++;
        for (var pass = 0; pass < 3; pass++)
        {
            // This is the same clear/reattach path used by the actual clipboard
            // toolbar and CsvGridForm.InstallCommandSurface, not a one-time setup.
            strip.Items.Clear();
            CsvDataToolCommands.Attach(strip, filters, summary, separator);
            CsvDataToolCommands.Attach(strip, filters, summary, separator);
            using var surface = new CsvCommandSurface(strip);
            surface.ApplyAppearance(SystemColors.Control, Color.Black, 96);
            if (strip.Items.Count != 3 || filters.Owner != strip || summary.Owner != strip ||
                filters.Image is null || summary.Image is null)
                throw new InvalidOperationException("Data tools must survive toolbar reconstruction exactly once with icons.");
            surface.View.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == "Filter and sort").PerformClick();
            surface.View.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == "Column summary").PerformClick();
            if (filterCalls != pass + 1 || summaryCalls != pass + 1)
                throw new InvalidOperationException("Rebuilt data-tool menus must dispatch once, without losing or duplicating commands.");
        }
        Console.WriteLine("Data-tool attachment: toolbar clear/rebuild, icon/menu restoration and idempotent attachment PASS.");
    }
}
