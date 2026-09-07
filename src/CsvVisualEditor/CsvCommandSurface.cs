namespace CsvVisualEditor;

/// <summary>One command source, two presentations: icons and a permanent text menu.</summary>
internal sealed class CsvCommandSurface : IDisposable
{
    private readonly ToolStrip[] _strips;
    private readonly List<Action> _detach = [];
    private readonly List<(ToolStripButton Button, ToolStripMenuItem Item)> _bindings = [];
    private Color _background = SystemColors.Control;
    private Color _foreground = SystemColors.ControlText;

    internal MenuStrip Menu { get; } = new() { Dock = DockStyle.Fill, GripStyle = ToolStripGripStyle.Hidden, ShowItemToolTips = true };
    internal ToolStripMenuItem Table { get; } = new("&Table");
    internal ToolStripMenuItem Edit { get; } = new("&Edit");
    internal ToolStripMenuItem View { get; } = new("&View");
    internal ToolStripMenuItem Csv { get; } = new("&CSV");
    internal ToolStripMenuItem Search { get; } = new("&Search");

    internal CsvCommandSurface(params ToolStrip[] strips)
    {
        _strips = strips;
        Menu.Items.AddRange([Table, Edit, View, Csv, Search]);
        foreach (var button in strips.SelectMany(static strip => strip.Items.OfType<ToolStripButton>()))
        {
            var text = button.Text ?? string.Empty;
            var parent = text.StartsWith("Refresh", StringComparison.Ordinal) || text == "Source" ? Table :
                text.StartsWith("Diagnostics", StringComparison.Ordinal) || text.StartsWith("Show spaces", StringComparison.Ordinal) ? View :
                text == "Clear" ? Search : Edit;
            Bind(parent, button);
        }
    }

    private void Bind(ToolStripMenuItem parent, ToolStripButton button)
    {
        var item = new ToolStripMenuItem();
        var description = button.ToolTipText;
        void Sync()
        {
            item.Text = button.Text;
            item.Enabled = button.Enabled;
            item.Checked = button.Checked;
            button.AccessibleName = button.Text;
            button.ToolTipText = button.Text + "\n" + description;
        }
        EventHandler sync = (_, _) => Sync();
        button.EnabledChanged += sync;
        button.TextChanged += sync;
        button.CheckedChanged += sync;
        item.Click += (_, _) => { if (button.Enabled) button.PerformClick(); };
        _detach.Add(() => { button.EnabledChanged -= sync; button.TextChanged -= sync; button.CheckedChanged -= sync; });
        _bindings.Add((button, item));
        parent.DropDownItems.Add(item);
        Sync();
    }

    internal void AddCombo(ToolStripMenuItem parent, string text, ToolStripComboBox source)
    {
        var menu = new ToolStripMenuItem(text);
        parent.DropDownItems.Add(menu);
        EventHandler enabled = (_, _) => menu.Enabled = source.Enabled;
        source.EnabledChanged += enabled;
        _detach.Add(() => source.EnabledChanged -= enabled);
        menu.Enabled = source.Enabled;
        menu.DropDownOpening += (_, _) =>
        {
            foreach (ToolStripItem old in menu.DropDownItems.Cast<ToolStripItem>().ToArray()) old.Dispose();
            for (var index = 0; index < source.Items.Count; index++)
            {
                var selected = index;
                var item = new ToolStripMenuItem(Convert.ToString(source.Items[index]))
                {
                    Checked = source.SelectedIndex == index, ForeColor = _foreground, BackColor = _background
                };
                item.Click += (_, _) => { if (source.Enabled && selected < source.Items.Count) source.SelectedIndex = selected; };
                menu.DropDownItems.Add(item);
            }
            StyleMenu(menu);
        };
    }

    internal void AddSearch(ToolStripTextBox source)
    {
        var menu = new ToolStripMenuItem("&Find in table");
        var input = new ToolStripTextBox { Width = 220, AccessibleName = "Search text" };
        menu.DropDownItems.Add(new ToolStripLabel("Search text:"));
        menu.DropDownItems.Add(input);
        Search.DropDownItems.Insert(0, menu);
        EventHandler enabled = (_, _) => menu.Enabled = source.Enabled;
        source.EnabledChanged += enabled;
        _detach.Add(() => source.EnabledChanged -= enabled);
        menu.Enabled = source.Enabled;
        menu.DropDownOpening += (_, _) =>
        {
            input.Enabled = source.Enabled;
            input.Text = source.Text;
            input.BackColor = _background;
            input.ForeColor = _foreground;
            input.Focus();
        };
        input.TextChanged += (_, _) => { if (source.Enabled) source.Text = input.Text; };
    }

    internal void ApplyAppearance(Color background, Color foreground, int dpi)
    {
        _background = background;
        _foreground = foreground;
        var renderer = new ToolStripProfessionalRenderer(new Palette(background, foreground));
        Menu.Renderer = renderer;
        Menu.BackColor = background;
        Menu.ForeColor = foreground;
        foreach (var strip in _strips) { strip.Renderer = renderer; strip.BackColor = background; strip.ForeColor = foreground; strip.ShowItemToolTips = true; }
        foreach (var (button, item) in _bindings)
        {
            var old = button.Image;
            button.Image = CsvCommandIcons.Create(button.Text ?? string.Empty, foreground, Math.Max(16, 18 * dpi / 96));
            item.Image = button.Image;
            button.DisplayStyle = ToolStripItemDisplayStyle.Image;
            button.ImageScaling = ToolStripItemImageScaling.None;
            button.Padding = new Padding(Math.Max(3, 4 * dpi / 96));
            button.AutoSize = true;
            old?.Dispose();
        }
        foreach (ToolStripMenuItem top in Menu.Items) StyleMenu(top);
    }

    private void StyleMenu(ToolStripMenuItem item)
    {
        item.BackColor = _background;
        item.ForeColor = _foreground;
        item.DropDown.BackColor = _background;
        item.DropDown.ForeColor = _foreground;
        item.DropDown.Renderer = Menu.Renderer;
        foreach (var child in item.DropDownItems.OfType<ToolStripMenuItem>()) StyleMenu(child);
    }

    internal void RefreshMenuColors()
    {
        foreach (ToolStripMenuItem top in Menu.Items) StyleMenu(top);
    }

    public void Dispose()
    {
        foreach (var detach in _detach) detach();
        foreach (var (button, item) in _bindings)
        {
            item.Image = null;
            var image = button.Image;
            button.Image = null;
            image?.Dispose();
        }
        Menu.Dispose();
    }

    private sealed class Palette(Color background, Color foreground) : ProfessionalColorTable
    {
        private readonly Color _hover = Color.FromArgb(
            (background.R * 4 + foreground.R) / 5,
            (background.G * 4 + foreground.G) / 5,
            (background.B * 4 + foreground.B) / 5);
        public override Color ToolStripDropDownBackground => background;
        public override Color ImageMarginGradientBegin => background;
        public override Color ImageMarginGradientMiddle => background;
        public override Color ImageMarginGradientEnd => background;
        public override Color MenuStripGradientBegin => background;
        public override Color MenuStripGradientEnd => background;
        public override Color ToolStripGradientBegin => background;
        public override Color ToolStripGradientMiddle => background;
        public override Color ToolStripGradientEnd => background;
        public override Color MenuItemSelected => _hover;
        public override Color MenuItemSelectedGradientBegin => _hover;
        public override Color MenuItemSelectedGradientEnd => _hover;
        public override Color MenuItemPressedGradientBegin => _hover;
        public override Color MenuItemPressedGradientMiddle => _hover;
        public override Color MenuItemPressedGradientEnd => _hover;
        public override Color ButtonSelectedGradientBegin => _hover;
        public override Color ButtonSelectedGradientMiddle => _hover;
        public override Color ButtonSelectedGradientEnd => _hover;
        public override Color ButtonCheckedGradientBegin => _hover;
        public override Color ButtonCheckedGradientMiddle => _hover;
        public override Color ButtonCheckedGradientEnd => _hover;
        public override Color ButtonPressedGradientBegin => _hover;
        public override Color ButtonPressedGradientMiddle => _hover;
        public override Color ButtonPressedGradientEnd => _hover;
    }
}
