namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using CsvVisualEditor.Localization;

/// <summary>
/// A detached, lossless view/editor for exactly one cell. It never writes to a
/// model or a host. Only the caller may accept Result after checking its context.
/// </summary>
internal sealed class CsvCellDetailsDialog : Form
{
    private readonly string _original;
    private readonly bool _editable;
    private readonly TextBox _editor = new()
    {
        Name = "CsvCellValueEditor", Dock = DockStyle.Fill, Multiline = true,
        AcceptsReturn = true, AcceptsTab = false, ScrollBars = ScrollBars.Both,
        WordWrap = true, MaxLength = int.MaxValue, HideSelection = false
    };
    private readonly TextBox _preview = new()
    {
        Name = "CsvCellValuePreview", Dock = DockStyle.Fill, Multiline = true,
        ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = true,
        MaxLength = int.MaxValue
    };
    private readonly Label _metrics = new() { Name = "CsvCellMetrics", AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false };
    private readonly Label _validation = new() { Name = "CsvCellValidation", AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false };
    private readonly Button _accept = new() { Name = "CsvCellAccept", AutoSize = true };
    private readonly System.Windows.Forms.Timer _validationTimer = new() { Interval = 150 };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly TableLayoutPanel _layout;

    internal string? Result { get; private set; }
    internal string EditorText { get => _editor.Text; set => _editor.Text = value; }
    internal bool CanAccept => _accept.Enabled;

    internal CsvCellDetailsDialog(string value, string location, bool editable, Color background, Color foreground)
    {
        _original = value;
        _editable = editable;
        Text = L10n.Get(TextKey.Cell_Title);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        Size = new Size(800, 600);
        MinimumSize = new Size(520, 400);

        var identity = new Label { Text = location, AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false };
        var guidance = new Label
        {
            Text = L10n.Get(editable ? TextKey.Cell_EditHint : TextKey.Cell_ReadOnlyHint),
            AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false
        };
        var notation = new Label
        {
            Text = L10n.Get(TextKey.Cell_NotationHint), AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false
        };
        var exactPage = new TabPage(L10n.Get(TextKey.Cell_ExactText));
        var previewPage = new TabPage(L10n.Get(TextKey.Common_Preview));
        exactPage.Controls.Add(_editor);
        previewPage.Controls.Add(_preview);
        _tabs.TabPages.AddRange([exactPage, previewPage]);
        var wrap = new CheckBox { Text = L10n.Get(TextKey.Cell_WordWrap), Checked = true, AutoSize = true };
        wrap.CheckedChanged += (_, _) => _editor.WordWrap = _preview.WordWrap = wrap.Checked;
        var close = new Button
        {
            Text = L10n.Get(editable ? TextKey.Common_Cancel : TextKey.Common_Close),
            Name = "CsvCellCancel", AutoSize = true, DialogResult = DialogResult.Cancel
        };
        _accept.Text = L10n.Get(TextKey.Transform_AcceptChanges);
        _accept.Visible = editable;
        _accept.Click += (_, _) => AcceptValue();
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
            FlowDirection = FlowDirection.RightToLeft
        };
        buttons.Controls.AddRange([close, _accept, wrap]);
        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 7, ColumnCount = 1
        };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 7; i++)
            _layout.RowStyles.Add(i == 3 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
        _layout.Controls.Add(identity, 0, 0);
        _layout.Controls.Add(guidance, 0, 1);
        _layout.Controls.Add(notation, 0, 2);
        _layout.Controls.Add(_tabs, 0, 3);
        _layout.Controls.Add(_metrics, 0, 4);
        _layout.Controls.Add(_validation, 0, 5);
        _layout.Controls.Add(buttons, 0, 6);
        Controls.Add(_layout);
        CancelButton = close;
        // Enter must insert a newline, never accept the dialog inadvertently.
        AcceptButton = null;
        _editor.ReadOnly = !editable;
        _editor.Text = CsvCellTextCodec.Encode(value);
        _editor.AccessibleName = L10n.Get(TextKey.Cell_ExactText);
        _preview.AccessibleName = L10n.Get(TextKey.Common_Preview);
        _editor.TextChanged += (_, _) =>
        {
            _accept.Enabled = false;
            _validationTimer.Stop();
            _validationTimer.Start();
        };
        _validationTimer.Tick += (_, _) => ValidateValue();
        _tabs.SelectedIndexChanged += (_, _) => ValidateValue();
        Resize += (_, _) => WrapLabels();
        Shown += (_, _) => { WrapLabels(); _editor.Focus(); _editor.Select(0, 0); };
        ApplyTheme(this, background, foreground);
        CsvLocalizationAppearance.Apply(this);
        // CSV content and its escape notation must never be mirrored by UI language.
        _editor.RightToLeft = _preview.RightToLeft = RightToLeft.No;
        _editor.TextAlign = _preview.TextAlign = HorizontalAlignment.Left;
        ValidateValue();
    }

    private void WrapLabels()
    {
        var width = Math.Max(100, _layout.ClientSize.Width - _layout.Padding.Horizontal - 12);
        foreach (var label in _layout.Controls.OfType<Label>()) label.MaximumSize = new Size(width, 0);
    }

    internal bool ValidateValue()
    {
        _validationTimer.Stop();
        if (!CsvCellTextCodec.TryDecode(_editor.Text, out var value, out var offset))
        {
            _accept.Enabled = false;
            _preview.Clear();
            _metrics.Text = string.Empty;
            _validation.Text = offset < 0
                ? L10n.Format(TextKey.Cell_TooLarge, CsvCellTextCodec.MaximumValueLength)
                : L10n.Format(TextKey.Cell_InvalidEscape, offset + 1);
            return false;
        }
        var counts = CsvCellTextCodec.Measure(value);
        _metrics.Text = L10n.Format(TextKey.Cell_Metrics, counts.Utf16Length, counts.Spaces,
            counts.Tabs, counts.CrLf, counts.Lf, counts.Cr);
        _validation.Text = value.Length == 0 ? L10n.Get(TextKey.Cell_EmptyValue) :
            string.IsNullOrWhiteSpace(value) ? L10n.Get(TextKey.Cell_WhitespaceOnly) : string.Empty;
        // Preview normalization is display-only. Result is always decoded from Exact text.
        _preview.Text = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n').Replace("\n", "\r\n", StringComparison.Ordinal);
        _accept.Enabled = _editable && !string.Equals(value, _original, StringComparison.Ordinal);
        return true;
    }

    internal bool AcceptValue()
    {
        if (!_editable || !ValidateValue() || !_accept.Enabled ||
            !CsvCellTextCodec.TryDecode(_editor.Text, out var value, out _)) return false;
        Result = value;
        DialogResult = DialogResult.OK;
        Close();
        return true;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Enter)) { AcceptValue(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _validationTimer.Dispose();
        base.Dispose(disposing);
    }

    private static void ApplyTheme(Control control, Color background, Color foreground)
    {
        control.BackColor = background;
        control.ForeColor = foreground;
        if (control is Button button) { button.UseVisualStyleBackColor = false; button.FlatStyle = FlatStyle.Flat; }
        foreach (Control child in control.Controls) ApplyTheme(child, background, foreground);
    }
}
