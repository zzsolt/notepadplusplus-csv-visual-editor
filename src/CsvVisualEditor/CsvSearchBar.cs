namespace CsvVisualEditor;

using CsvVisualEditor.Localization;

/// <summary>A single, keyboard-accessible search surface. Narrow docks use two rows.</summary>
internal sealed class CsvSearchBar : UserControl
{
    internal TextBox Query { get; } = new()
    {
        Name = "CsvSearchText", BorderStyle = BorderStyle.None,
        PlaceholderText = L10n.Get(TextKey.Search_SearchInTable), AccessibleName = L10n.Get(TextKey.Search_SearchInTable2),
        AccessibleDescription = L10n.Get(TextKey.Search_FiltersDisplayedRowsEnterNextMatchingCellShift),
        TabIndex = 0
    };
    internal ComboBox Column { get; } = new()
    {
        Name = "CsvSearchColumn", DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat, AccessibleName = L10n.Get(TextKey.Search_SearchColumn), TabIndex = 1
    };
    internal Button Previous { get; } = CreateButton("CsvPreviousMatch", L10n.Get(TextKey.Search_PreviousMatchingCell), 2);
    internal Button Next { get; } = CreateButton("CsvNextMatch", L10n.Get(TextKey.Search_NextMatchingCell), 3);
    internal Button Clear { get; } = CreateButton("CsvClearSearch", L10n.Get(TextKey.Search_ClearSearch), 4);
    internal Label ResultLabel { get; } = new()
    {
        Name = "CsvSearchResults", AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft,
        AccessibleName = L10n.Get(TextKey.Search_SearchResults), Text = string.Empty, UseMnemonic = false
    };
    private readonly Panel _field = new() { Name = "CsvSearchField" };
    private readonly PictureBox _magnifier = new() { SizeMode = PictureBoxSizeMode.CenterImage, TabStop = false };
    private readonly ToolTip _tips = new();
    private bool _layingOut;
    private bool _compact;
    private int _total;
    private int _currentIndex = -1;
    private bool _hasQuery;
    private bool _pending;
    private Color _border = SystemColors.ControlDark;
    private Color _accent = Color.FromArgb(0, 120, 212);

    internal event Action<bool>? NavigateRequested;
    internal event Action? ClearRequested;
    internal event Action? ReturnToGridRequested;

    internal CsvSearchBar()
    {
        Name = "CsvSearchBar";
        RightToLeft = L10n.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        MinimumSize = new Size(0, 36);
        _field.Controls.AddRange([_magnifier, Query, Clear]);
        Controls.AddRange([_field, Column, Previous, Next, ResultLabel]);
        _field.Paint += (_, e) =>
        {
            // All child windows are inset: none may erase a segment of this border.
            using var pen = new Pen(Query.Focused ? _accent : _border);
            e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, _field.Width - 1), Math.Max(0, _field.Height - 1));
        };
        _field.Click += (_, _) => Query.Focus();
        _magnifier.Click += (_, _) => FocusQuery();
        Query.Enter += (_, _) => _field.Invalidate();
        Query.Leave += (_, _) => _field.Invalidate();
        Query.TextChanged += (_, _) => UpdateButtons();
        Query.EnabledChanged += (_, _) => UpdateButtons();
        Query.KeyDown += (_, e) =>
        {
            if (e.KeyData == Keys.Enter || e.KeyData == (Keys.Shift | Keys.Enter))
            {
                NavigateRequested?.Invoke(e.Shift);
                e.SuppressKeyPress = true;
            }
            else if (e.KeyData == Keys.Escape)
            {
                if (Query.TextLength > 0) ClearRequested?.Invoke();
                else ReturnToGridRequested?.Invoke();
                e.SuppressKeyPress = true;
            }
        };
        Previous.Click += (_, _) => NavigateRequested?.Invoke(true);
        Next.Click += (_, _) => NavigateRequested?.Invoke(false);
        Clear.Click += (_, _) => { ClearRequested?.Invoke(); Query.Focus(); };
        _tips.SetToolTip(Query, L10n.Get(TextKey.Search_SearchInDisplayedCSVRowsCtrlFValues));
        _tips.SetToolTip(Column, L10n.Get(TextKey.Search_SearchAllDataColumnsOrChooseOneColumn));
        _tips.SetToolTip(Previous, L10n.Get(TextKey.Search_PreviousMatchingCellShiftFShiftEnter));
        _tips.SetToolTip(Next, L10n.Get(TextKey.Search_NextMatchingCellFEnter));
        _tips.SetToolTip(Clear, L10n.Get(TextKey.Search_ClearSearchEscKeepColumnScopeAndSorting));
        ApplyAppearance(SystemColors.Control, SystemColors.ControlText);
        UpdateButtons();
    }

    internal void FocusQuery()
    {
        if (!Query.Enabled) return;
        Query.Focus();
        Query.SelectAll();
    }

    internal void SetResults(int currentIndex, int total, bool hasQuery, bool pending = false)
    {
        _total = total;
        _pending = pending;
        _currentIndex = currentIndex;
        _hasQuery = hasQuery;
        UpdateResultText();
        UpdateButtons();
    }

    private void UpdateResultText()
    {
        var narrow = ClientSize.Width < Unit(340);
        ResultLabel.Text = _pending ? L10n.Get(TextKey.Search_Searching) : !_hasQuery ? string.Empty :
            _total == 0 ? L10n.Get(TextKey.Search_NoMatches) : _currentIndex >= 0 ?
                (narrow ? $"{_currentIndex + 1:N0} / {_total:N0}" : L10n.Format(TextKey.Search_Cells, _currentIndex + 1, _total)) : L10n.Format(TextKey.Search_Cells2, _total);
        _tips.SetToolTip(ResultLabel, ResultLabel.Text);
    }

    private void UpdateButtons()
    {
        Previous.Enabled = Next.Enabled = Query.Enabled && !_pending && _total > 0;
        Clear.Enabled = Query.Enabled && Query.TextLength > 0;
        Clear.Visible = Query.TextLength > 0;
    }

    internal void ApplyAppearance(Color background, Color foreground)
    {
        var dark = background.GetBrightness() < 0.5f;
        BackColor = background;
        ForeColor = foreground;
        var fieldColor = dark ? Color.FromArgb(38, 40, 43) : SystemColors.Window;
        _border = dark ? Color.FromArgb(100, 103, 108) : Color.FromArgb(170, 177, 184);
        _accent = dark ? Color.FromArgb(110, 195, 255) : Color.FromArgb(0, 120, 212);
        _field.BackColor = Query.BackColor = Clear.BackColor = _magnifier.BackColor = fieldColor;
        Query.ForeColor = foreground;
        Column.BackColor = fieldColor;
        Column.ForeColor = foreground;
        ResultLabel.ForeColor = foreground;
        SetImage(_magnifier, CsvCommandIcons.Create("Search", foreground, Unit(16)));
        SetButtonImage(Previous, "Previous match", foreground, background);
        SetButtonImage(Next, "Next match", foreground, background);
        SetButtonImage(Clear, "Clear", foreground, fieldColor);
        PerformLayout();
        Invalidate(true);
    }

    private void SetButtonImage(Button button, string command, Color foreground, Color background)
    {
        var old = button.Image;
        button.Image = CsvCommandIcons.Create(command, foreground, Unit(16));
        old?.Dispose();
        button.BackColor = background;
        button.ForeColor = foreground;
        button.FlatAppearance.MouseOverBackColor = Blend(background, foreground, 10);
        button.FlatAppearance.MouseDownBackColor = Blend(background, foreground, 20);
    }

    private static void SetImage(PictureBox box, Image image)
    {
        var old = box.Image;
        box.Image = image;
        old?.Dispose();
    }

    internal static Color Blend(Color background, Color foreground, int percent) => Color.FromArgb(
        (background.R * (100 - percent) + foreground.R * percent) / 100,
        (background.G * (100 - percent) + foreground.G * percent) / 100,
        (background.B * (100 - percent) + foreground.B * percent) / 100);

    private int Unit(int value) => Math.Max(1, (int)Math.Round(value * DeviceDpi / 96d));

    private int RowHeight => Math.Max(Unit(28), Math.Max(Query.PreferredHeight + Unit(10), Column.PreferredHeight + Unit(4)));
    private bool IsCompact(int width) => width < Unit(600);

    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = proposedSize.Width > 0 ? proposedSize.Width : Width;
        var row = RowHeight;
        return new Size(0, Unit(10) + row + (IsCompact(width) ? row + Unit(4) : 0));
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_layingOut || _field is null) return;
        _layingOut = true;
        try
        {
            var pad = Unit(6);
            var gap = Unit(6);
            var top = Unit(5);
            var height = RowHeight;
            var button = Unit(26);
            var width = Math.Max(1, ClientSize.Width - pad * 2);
            var compact = IsCompact(ClientSize.Width);
            var rowY = compact ? top + height + Unit(4) : top;
            var resultWidth = Unit(ClientSize.Width < Unit(340) ? 64 : 100);
            resultWidth = Math.Min(resultWidth, Math.Max(Unit(24), width - Unit(94) - 2 * button - 3 * gap));
            var scope = Math.Min(Unit(156), Math.Max(Unit(60), width - resultWidth - 2 * button - 3 * gap));
            // Pack the controls together instead of separating arrows/counter at
            // the far edge of a wide dock. Do not turn a query into a 750px field.
            var fieldWidth = compact ? width : Math.Min(Unit(420), Math.Max(1,
                width - scope - resultWidth - button * 2 - gap * 4));
            _field.SetBounds(pad, top, fieldWidth, height);
            var scopeX = compact ? pad : pad + fieldWidth + gap;
            Column.SetBounds(scopeX, rowY + Math.Max(0, (height - Column.PreferredHeight) / 2), scope, Column.PreferredHeight);
            ResultLabel.SetBounds(scopeX + scope + gap, rowY, resultWidth, height);
            var navX = ResultLabel.Right + gap;
            Previous.SetBounds(navX, rowY, button, height);
            Next.SetBounds(navX + button + gap, rowY, button, height);
            var icon = Unit(16);
            _magnifier.SetBounds(Unit(8), (height - icon) / 2, icon, icon);
            Clear.SetBounds(Math.Max(1, fieldWidth - button - Unit(4)), Unit(4), button, height - Unit(8));
            Query.SetBounds(Unit(30), Math.Max(Unit(4), (height - Query.PreferredHeight) / 2),
                Math.Max(1, fieldWidth - Unit(38) - button), Query.PreferredHeight);
            UpdateResultText();
            if (_compact != compact)
            {
                _compact = compact;
                Parent?.PerformLayout();
            }
        }
        finally { _layingOut = false; }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (Query.Enabled && keyData == (Keys.Control | Keys.F)) { FocusQuery(); return true; }
        if (Query.Enabled && (keyData == Keys.F3 || keyData == (Keys.Shift | Keys.F3)))
        {
            NavigateRequested?.Invoke((keyData & Keys.Shift) != 0);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyAppearance(BackColor, ForeColor);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tips.Dispose();
            _magnifier.Image?.Dispose();
            _magnifier.Image = null;
            foreach (var button in new[] { Previous, Next, Clear })
            {
                button.Image?.Dispose();
                button.Image = null;
            }
        }
        base.Dispose(disposing);
    }

    private static Button CreateButton(string id, string name, int tabIndex) => new()
    {
        Name = id,
        AccessibleName = name, FlatStyle = FlatStyle.Flat, TabIndex = tabIndex,
        FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false
    };
}
