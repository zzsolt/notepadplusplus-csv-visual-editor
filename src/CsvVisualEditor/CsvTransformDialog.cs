namespace CsvVisualEditor;

using CsvVisualEditor.Core;

internal enum CsvTransformScope { SelectedCells, CurrentColumn, AllDataCells }

/// <summary>Preview/accept UI only; callbacks operate on the pending model.</summary>
internal sealed class CsvTransformDialog : Form
{
    private readonly ComboBox _operation = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly ComboBox _scope = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly TextBox _find = new() { Width = 230 };
    private readonly TextBox _replacement = new() { Width = 230 };
    private readonly CheckBox _matchCase = new() { Text = "Match case", Checked = true, AutoSize = true };
    private readonly Button _preview = new() { Text = "Preview", AutoSize = true };
    private readonly Button _accept = new() { Text = "Accept changes", AutoSize = true, Enabled = false };
    private readonly Label _summary = new() { AutoSize = true, MaximumSize = new Size(780, 0) };
    private readonly DataGridView _samples = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect
    };
    private readonly Func<CsvTransformScope, CsvCellTransform, CsvCellTransformPlan> _create;
    private readonly Action<CsvCellTransformPlan> _apply;
    private CsvCellTransformPlan? _plan;

    internal CsvTransformDialog(
        Func<CsvTransformScope, CsvCellTransform, CsvCellTransformPlan> create,
        Action<CsvCellTransformPlan> apply)
    {
        _create = create;
        _apply = apply;
        Text = "Transform CSV cells";
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        Size = new Size(860, 600);
        MinimumSize = new Size(680, 480);

        _operation.Items.AddRange(["Replace text (literal)", "Trim outer whitespace", "UPPERCASE", "lowercase"]);
        _scope.Items.AddRange(["Selected cells / rows", "Current column", "All data cells"]);
        _operation.SelectedIndex = 0;
        _scope.SelectedIndex = 0;
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        options.Controls.AddRange([
            new Label { Text = "Operation:", AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, _operation,
            new Label { Text = "Scope:", AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, _scope]);
        var textOptions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        textOptions.Controls.AddRange([
            new Label { Text = "Find:", AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, _find,
            new Label { Text = "Replace with:", AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, _replacement,
            _matchCase]);
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.AddRange([cancel, _accept, _preview]);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 6, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label
        {
            Text = "Preview first. Accept changes updates pending edits; the toolbar Apply writes to Notepad++.\nHeaders are excluded when Header = First record. Casing is culture independent.",
            AutoSize = true, Padding = new Padding(0, 0, 0, 10)
        }, 0, 0);
        layout.Controls.Add(options, 0, 1);
        layout.Controls.Add(textOptions, 0, 2);
        layout.Controls.Add(_summary, 0, 3);
        layout.Controls.Add(_samples, 0, 4);
        layout.Controls.Add(buttons, 0, 5);
        _samples.Columns.Add("Record", "Record ID");
        _samples.Columns.Add("Column", "CSV column");
        _samples.Columns.Add("Before", "Before");
        _samples.Columns.Add("After", "After");
        _samples.Columns[0].FillWeight = 35;
        _samples.Columns[1].FillWeight = 35;
        Controls.Add(layout);
        AcceptButton = _preview;
        CancelButton = cancel;

        _operation.SelectedIndexChanged += (_, _) => InvalidatePreview();
        _scope.SelectedIndexChanged += (_, _) => InvalidatePreview();
        _find.TextChanged += (_, _) => InvalidatePreview();
        _replacement.TextChanged += (_, _) => InvalidatePreview();
        _matchCase.CheckedChanged += (_, _) => InvalidatePreview();
        _preview.Click += (_, _) => BuildPreview();
        _accept.Click += (_, _) => AcceptChanges();
        InvalidatePreview();
    }

    private void InvalidatePreview()
    {
        _plan = null;
        _accept.Enabled = false;
        _samples.Rows.Clear();
        _summary.Text = "Choose an operation and scope, then click Preview.";
        _find.Enabled = _replacement.Enabled = _matchCase.Enabled = _operation.SelectedIndex == 0;
    }

    private void BuildPreview()
    {
        InvalidatePreview();
        UseWaitCursor = true;
        try
        {
            _plan = _create((CsvTransformScope)_scope.SelectedIndex,
                new CsvCellTransform((CsvCellTransformKind)_operation.SelectedIndex,
                    _find.Text, _replacement.Text, _matchCase.Checked));
            foreach (var change in _plan.Changes.Take(50))
            {
                var id = change.Address.RowId;
                _samples.Rows.Add(id.IsInserted ? $"new:{-id.Value}" : $"{id.SourceRecordIndex!.Value + 1}",
                    change.Address.ColumnIndex + 1, Sample(change.Before), Sample(change.After));
            }

            _summary.Text = $"{_plan.TargetCellCount:N0} target cells; {_plan.Changes.Count:N0} changes in {_plan.ChangedRowCount:N0} rows. " +
                "Showing the first 50 changes; long values are shortened, spaces = ·; other whitespace is escaped. Lengths count UTF-16 units.";
            _accept.Enabled = _plan.Changes.Count > 0;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _summary.Text = "Preview could not be created. Check the scope and find text, or select a smaller scope (250,000 cells / 16 Mi characters maximum).";
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void AcceptChanges()
    {
        if (_plan is null) return;
        try
        {
            _apply(_plan);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            InvalidatePreview();
            _summary.Text = "The preview is no longer valid. Check the Edit session and create a new preview.";
        }
    }

    internal static string Sample(string value)
    {
        var length = Math.Min(value.Length, 240);
        if (length < value.Length && length > 0 && char.IsHighSurrogate(value[length - 1])) length--;
        var builder = new System.Text.StringBuilder();
        builder.Append('⟦');
        foreach (var character in value.AsSpan(0, length))
        {
            builder.Append(character switch
            {
                ' ' => "·",
                '\t' => "\\t",
                '\r' => "\\r",
                '\n' => "\\n",
                '\\' => "\\\\",
                '·' => "\\u00B7",
                '⟦' => "\\u27E6",
                '⟧' => "\\u27E7",
                _ when char.IsWhiteSpace(character) || char.IsControl(character) =>
                    "\\u" + ((int)character).ToString("X4", System.Globalization.CultureInfo.InvariantCulture),
                _ => character.ToString()
            });
        }
        if (length < value.Length) builder.Append('…');
        builder.Append("⟧ (").Append(value.Length).Append(')');
        return builder.ToString();
    }
}
