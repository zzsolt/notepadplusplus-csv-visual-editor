namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using System.Globalization;

/// <summary>A bounded snapshot of the current view, never a second editing model.</summary>
internal sealed class CsvColumnSummaryDialog : Form
{
    private readonly CsvTableProjection _projection;
    private readonly CsvTableViewResult _view;
    private readonly ComboBox _column;
    private readonly Label _counts = new() { Name = "CsvColumnCounts", AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false, Padding = new Padding(4, 8, 4, 8) };
    private readonly Label _range = new() { Name = "CsvColumnRange", AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false, Padding = new Padding(4, 0, 4, 8) };
    private readonly DataGridView _frequent = new()
    {
        Name = "CsvFrequentValues", Dock = DockStyle.Fill, ReadOnly = true,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false, RowHeadersVisible = false,
        AutoGenerateColumns = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.CellSelect, MultiSelect = false,
        ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable,
        BorderStyle = BorderStyle.FixedSingle, BackgroundColor = SystemColors.Window,
        CellBorderStyle = DataGridViewCellBorderStyle.Single
    };
    internal CsvColumnProfile? Profile { get; private set; }

    internal CsvColumnSummaryDialog(CsvTableProjection projection, CsvTableViewResult view,
        int columnIndex, Color background, Color foreground)
    {
        _projection = projection;
        _view = view;
        Text = "Column summary";
        Name = "CsvColumnSummaryDialog";
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = MaximizeBox = false;
        ClientSize = new Size(680, 520);
        MinimumSize = new Size(570, 430);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 7 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 7; i++) root.RowStyles.Add(new RowStyle(i == 4 ? SizeType.Percent : SizeType.AutoSize, i == 4 ? 100 : 0));
        _column = CsvDataToolStyle.Combo("CsvProfileColumn", projection.Columns.Select(static c => (object)$"{c.Index + 1}: {c.Name}").ToArray());
        root.Controls.Add(_column, 0, 0);
        var scope = new Label
        {
            Name = "CsvProfileScope", AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false,
            Padding = new Padding(4, 6, 4, 0),
            Text = $"Current view: {view.VisibleRowCount:N0} of {projection.DisplayedRowCount:N0} displayed rows." +
                (projection.IsRowLimited ? " Projection limited; this is NOT the whole file." : "")
        };
        root.Controls.Add(scope, 0, 1);
        root.Controls.Add(_counts, 0, 2);
        root.Controls.Add(_range, 0, 3);
        _frequent.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "Most frequent values (up to 20)", FillWeight = 70, MinimumWidth = 160 });
        _frequent.Columns.Add(new DataGridViewTextBoxColumn { Name = "Count", HeaderText = "Rows", FillWeight = 15, MinimumWidth = 70 });
        _frequent.Columns.Add(new DataGridViewTextBoxColumn { Name = "Kind", HeaderText = "Kind", FillWeight = 20, MinimumWidth = 90 });
        foreach (DataGridViewColumn c in _frequent.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
        _frequent.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
        _frequent.RowTemplate.Height = Math.Max(26, Font.Height + 10);
        _frequent.CellPainting += (_, e) => { if (e.ColumnIndex == 0) CsvWhitespaceCellPainter.Paint(_frequent, e); };
        _frequent.CellToolTipTextNeeded += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 0 || Profile is null || e.RowIndex >= Profile.MostFrequent.Count) return;
            var raw = Profile.MostFrequent[e.RowIndex].Value;
            e.ToolTipText = CsvWhitespaceCellPainter.DescribeSpaces(raw);
        };
        root.Controls.Add(_frequent, 0, 4);
        root.Controls.Add(new Label
        {
            AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false, Padding = new Padding(4, 8, 4, 8),
            Text = "Distinct values use exact, case-sensitive text, including empty values.\nRepeated rows = rows minus distinct values. Numeric range uses exact dot-decimal values."
        }, 0, 5);
        var close = CsvDataToolStyle.Button("Close", "CsvCloseSummary");
        close.DialogResult = DialogResult.OK;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(close);
        root.Controls.Add(buttons, 0, 6);
        Controls.Add(root);
        AcceptButton = CancelButton = close;
        _column.SelectedIndexChanged += (_, _) => RefreshProfile();
        if (projection.ColumnCount > 0) _column.SelectedIndex = Math.Clamp(columnIndex, 0, projection.ColumnCount - 1);
        CsvDataToolStyle.Apply(this, background, foreground);
        RefreshProfile();
    }

    private void RefreshProfile()
    {
        if (_column.SelectedIndex < 0) return;
        Profile = CsvColumnProfile.Build(_view, _column.SelectedIndex, _projection.ColumnCount);
        _counts.Text = $"Rows: {Profile.RowCount:N0}     Empty: {Profile.EmptyCount:N0}     Whitespace only: {Profile.WhitespaceOnlyCount:N0}\n" +
            $"Distinct: {Profile.DistinctCount:N0}     Repeated rows: {Profile.DuplicateOccurrenceCount:N0}     Numeric: {Profile.NumericCount:N0}";
        _range.Text = "Numeric minimum: " + Format(Profile.Minimum) + "     Maximum: " + Format(Profile.Maximum) +
            $"\nLongest value: {Profile.MaximumLength:N0} UTF-16 units. {Profile.RepeatedValueCount:N0} values appear more than once.";
        _frequent.Rows.Clear();
        foreach (var item in Profile.MostFrequent)
            _frequent.Rows.Add(item.Value, item.Count.ToString("N0", CultureInfo.CurrentCulture),
                item.Value.Length == 0 ? "Empty" : string.IsNullOrWhiteSpace(item.Value) ? "Whitespace" : "Value");
        _frequent.ClearSelection();
    }
    private static string Format(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "Not available";
}
