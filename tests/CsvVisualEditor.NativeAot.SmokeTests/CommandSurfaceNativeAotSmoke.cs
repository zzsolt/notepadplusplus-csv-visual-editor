namespace CsvVisualEditor.NativeAot.SmokeTests;

using System.Runtime.CompilerServices;

internal static class CommandSurfaceNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        using var commands = new ToolStrip();
        using var options = new ToolStrip();
        var edit = new ToolStripButton("Edit");
        var copy = new ToolStripButton("Copy");
        var apply = new ToolStripButton("Apply") { Enabled = false };
        commands.Items.AddRange([copy, edit, apply]);
        using var query = new TextBox();
        var combo = new ToolStripComboBox();
        combo.Items.AddRange(["All columns", "Name"]);
        combo.SelectedIndex = 0;
        options.Items.Add(combo);
        using var surface = new CsvCommandSurface(commands, options);
        var focusCalls = 0;
        surface.AddSearch(query, () => focusCalls++, _ => { }, () => true);
        surface.AddCombo(surface.Csv, "Column", combo);
        surface.ApplyAppearance(SystemColors.Control, Color.Black, 96);
        Require(surface.Menu.Items.Count == 6, "The full text menu must always exist.");
        Require(copy.DisplayStyle == ToolStripItemDisplayStyle.Image && copy.Image is not null,
            "The primary toolbar must use graphical icons.");
        Require(copy.ToolTipText!.StartsWith("Copy", StringComparison.Ordinal) && copy.AccessibleName == "Copy",
            "Icons must retain command names for hover and accessibility.");
        var menuCopy = surface.Edit.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == "Copy");
        var clicks = 0;
        copy.Click += (_, _) => clicks++;
        menuCopy.PerformClick();
        Require(clicks == 1, "Menu and icon must invoke the same command exactly once.");
        copy.Enabled = false;
        Require(!menuCopy.Enabled, "Menu availability must track the command.");
        menuCopy.PerformClick();
        Require(clicks == 1, "Disabled menu commands must not dispatch.");
        edit.Text = "Exit Edit";
        edit.Checked = true;
        var menuEdit = surface.Edit.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == "Exit Edit");
        Require(menuEdit.Checked, "Edit label and checked state must stay synchronized.");
        var find = (ToolStripMenuItem)surface.Search.DropDownItems[0];
        Require(find.DropDownItems.Count == 0, "Find must directly focus the search bar, not open a nested editor.");
        find.PerformClick();
        Require(focusCalls == 1, "Find must focus the one canonical search input.");
        query.Enabled = false;
        find.PerformClick();
        Require(focusCalls == 1 && !find.Enabled, "Disabled search must not dispatch.");
        var width = copy.Image!.Width;
        surface.ApplyAppearance(Color.FromArgb(32, 32, 32), Color.Gainsboro, 192);
        Require(copy.Image!.Width > width && surface.Menu.BackColor.R == 32,
            "Icons and menus must adapt to DPI and dark colors.");
        Require(surface.Edit.DropDown.ForeColor == Color.Gainsboro, "Dropdown text must track theme colors.");

        using var bitmap = new Bitmap(400, 100);
        using var graphics = Graphics.FromImage(bitmap);
        using var font = new Font(FontFamily.GenericSansSerif, 12);
        Require(CsvWhitespaceCellPainter.Draw(graphics, new RectangleF(0, 0, 400, 100), new Rectangle(0, 0, 400, 100),
            " a ", font, Color.Black, DataGridViewContentAlignment.MiddleLeft, showSpaces: false) == 0,
            "Hiding whitespace must suppress only markers.");
        Require(CsvWhitespaceCellPainter.DescribeSpaces("  a b ").StartsWith("Spaces: 4; leading: 2; trailing: 1.", StringComparison.Ordinal),
            "Space tooltips must give exact counts without source values.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
