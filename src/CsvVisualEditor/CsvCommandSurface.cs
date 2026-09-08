namespace CsvVisualEditor;

/// <summary>One command source, two presentations: icons and a permanent text menu.</summary>
internal sealed class CsvCommandSurface : IDisposable
{
    private readonly ToolStrip[] _strips;
    private readonly List<Action> _detach = [];
    private readonly List<(ToolStripButton Button, ToolStripMenuItem Item)> _bindings = [];
    private bool _disposed;
    private Color _background = SystemColors.Control;
    private Color _foreground = SystemColors.ControlText;

    internal MenuStrip Menu { get; } = new() { Dock = DockStyle.Fill, GripStyle = ToolStripGripStyle.Hidden, ShowItemToolTips = true };
    internal ToolStripMenuItem Table { get; } = new("&Table");
    internal ToolStripMenuItem Edit { get; } = new("&Edit");
    internal ToolStripMenuItem View { get; } = new("&View");
    internal ToolStripMenuItem Csv { get; } = new("&CSV");
    internal ToolStripMenuItem Search { get; } = new("&Search");
    internal ToolStripMenuItem About { get; } = new("&About");

    internal CsvCommandSurface(params ToolStrip[] strips)
    {
        _strips = strips;
        Menu.Items.AddRange([Table, Edit, View, Csv, Search, About]);
        foreach (var button in strips.SelectMany(static strip => strip.Items.OfType<ToolStripButton>()))
        {
            var text = button.Text ?? string.Empty;
            var parent = text.StartsWith("Refresh", StringComparison.Ordinal) || text == "Source" ? Table :
                text.StartsWith("Diagnostics", StringComparison.Ordinal) || text.StartsWith("Show spaces", StringComparison.Ordinal) || (text is "Reset view" or "Filter and sort" or "Column summary") ? View :
                text == "Clear" ? Search : Edit;
            Bind(parent, button);
        }
    }

    private void Bind(ToolStripMenuItem parent, ToolStripButton button)
    {
        var item = new ToolStripMenuItem();
        // Never capture a stale availability explanation or repeatedly prepend names
        // every time the command surface is installed.
        var caption = button.Text ?? string.Empty;
        if (string.IsNullOrEmpty(button.ToolTipText)) button.ToolTipText = caption;
        else if (!button.ToolTipText.StartsWith(caption, StringComparison.Ordinal))
            button.ToolTipText = caption + ": " + button.ToolTipText;
        void Sync()
        {
            item.Text = button.Text;
            item.Enabled = button.Enabled;
            item.Checked = button.Checked;
            button.AccessibleName = button.Text;
            item.ToolTipText = button.ToolTipText;
            button.AccessibleDescription = button.ToolTipText;
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

    internal void AddCombo(ToolStripMenuItem parent, string text, ToolStripComboBox source) =>
        AddCombo(parent, text, source.ComboBox);

    internal void AddCombo(ToolStripMenuItem parent, string text, ComboBox source)
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
                // Column names are data, not menu mnemonics.
                var caption = (Convert.ToString(source.Items[index]) ?? string.Empty).Replace("&", "&&", StringComparison.Ordinal);
                var item = new ToolStripMenuItem(caption)
                {
                    Checked = source.SelectedIndex == index, ForeColor = _foreground, BackColor = _background
                };
                item.Click += (_, _) => { if (source.Enabled && selected < source.Items.Count) source.SelectedIndex = selected; };
                menu.DropDownItems.Add(item);
            }
            StyleMenu(menu);
        };
    }

    internal void AddSearch(TextBox source, Action focus, Action<bool> navigate, Func<bool> canNavigate)
    {
        var find = new ToolStripMenuItem("&Find in table") { ShortcutKeyDisplayString = "Ctrl+F" };
        var next = new ToolStripMenuItem("&Next matching cell") { ShortcutKeyDisplayString = "F3" };
        var previous = new ToolStripMenuItem("&Previous matching cell") { ShortcutKeyDisplayString = "Shift+F3" };
        var clear = new ToolStripMenuItem("&Clear search") { ShortcutKeyDisplayString = "Esc in search" };
        Search.DropDownItems.Insert(0, find);
        Search.DropDownItems.Insert(1, next);
        Search.DropDownItems.Insert(2, previous);
        Search.DropDownItems.Insert(3, clear);
        find.Click += (_, _) => { if (source.Enabled) focus(); };
        next.Click += (_, _) => { if (source.Enabled && canNavigate()) navigate(false); };
        previous.Click += (_, _) => { if (source.Enabled && canNavigate()) navigate(true); };
        clear.Click += (_, _) => { if (source.Enabled) source.Clear(); };
        void Sync()
        {
            find.Enabled = source.Enabled;
            next.Enabled = previous.Enabled = source.Enabled && canNavigate();
            clear.Enabled = source.Enabled && source.TextLength > 0;
        }
        EventHandler enabled = (_, _) => Sync();
        source.EnabledChanged += enabled;
        Search.DropDownOpening += enabled;
        _detach.Add(() => { source.EnabledChanged -= enabled; Search.DropDownOpening -= enabled; });
        Sync();
    }

    internal void ApplyAppearance(Color background, Color foreground, int dpi)
    {
        _background = background;
        _foreground = foreground;
        var renderer = new ToolStripProfessionalRenderer(new Palette(background, foreground)) { RoundedEdges = false };
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
        if (_disposed) return;
        _disposed = true;
        foreach (var detach in _detach) detach();
        foreach (var (button, item) in _bindings)
        {
            item.Image = null;
            var image = button.Image;
            button.Image = null;
            image?.Dispose();
        }
        _detach.Clear();
        _bindings.Clear();
        Menu.Dispose();
    }

    private sealed class Palette(Color background, Color foreground) : ProfessionalColorTable
    {
        private readonly Color _hover = Color.FromArgb(
            (background.R * 4 + foreground.R) / 5,
            (background.G * 4 + foreground.G) / 5,
            (background.B * 4 + foreground.B) / 5);
        public override Color MenuBorder => _hover;
        public override Color SeparatorDark => _hover;
        public override Color SeparatorLight => background;
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
