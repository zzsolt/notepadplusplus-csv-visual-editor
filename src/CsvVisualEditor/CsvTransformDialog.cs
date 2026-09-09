namespace CsvVisualEditor;

using CsvVisualEditor.Localization;

using CsvVisualEditor.Core;

internal enum CsvTransformScope { SelectedCells, CurrentColumn, AllDataCells }

/// <summary>Preview/accept UI only; callbacks operate on the pending model.</summary>
internal sealed class CsvTransformDialog : Form
{
    private readonly ComboBox _operation = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly ComboBox _scope = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly TextBox _find = new() { Width = 230 };
    private readonly TextBox _replacement = new() { Width = 230 };
    private readonly CheckBox _matchCase = new() { Text = L10n.Get(TextKey.Common_MatchCase), Checked = true, AutoSize = true };
    private readonly Button _preview = new() { Text = L10n.Get(TextKey.Common_Preview), AutoSize = true };
    private readonly Button _accept = new() { Text = L10n.Get(TextKey.Transform_AcceptChanges), AutoSize = true, Enabled = false };
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
    private readonly bool _showSpaces;

    internal CsvTransformDialog(
        Func<CsvTransformScope, CsvCellTransform, CsvCellTransformPlan> create,
        Action<CsvCellTransformPlan> apply,
        bool showSpaces = true)
    {
        _samples.Font = Font;
        _showSpaces = showSpaces;
        _create = create;
        _apply = apply;
        Text = L10n.Get(TextKey.Transform_TransformCSVCells);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        Size = new Size(860, 600);
        MinimumSize = new Size(680, 480);

        _operation.Items.AddRange([L10n.Get(TextKey.Transform_ReplaceTextLiteral), L10n.Get(TextKey.Transform_TrimOuterWhitespace), L10n.Get(TextKey.Transform_UPPERCASE), L10n.Get(TextKey.Transform_Lowercase)]);
        _scope.Items.AddRange([L10n.Get(TextKey.Transform_SelectedCellsRows), L10n.Get(TextKey.Transform_CurrentColumn), L10n.Get(TextKey.Transform_AllDataCells)]);
        _operation.SelectedIndex = 0;
        _scope.SelectedIndex = 0;
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        options.Controls.AddRange([
            new Label { Text = L10n.Get(TextKey.Transform_Operation), AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, _operation,
            new Label { Text = L10n.Get(TextKey.Transform_Scope), AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, _scope]);
        var textOptions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        textOptions.Controls.AddRange([
            new Label { Text = L10n.Get(TextKey.Transform_Find), AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, _find,
            new Label { Text = L10n.Get(TextKey.Transform_ReplaceWith), AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, _replacement,
            _matchCase]);
        var cancel = new Button { Text = L10n.Get(TextKey.Common_Cancel), AutoSize = true, DialogResult = DialogResult.Cancel };
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
            Text = L10n.Get(TextKey.Transform_PreviewFirstAcceptChangesUpdatesPendingEditsThe),
            AutoSize = true, Padding = new Padding(0, 0, 0, 10)
        }, 0, 0);
        layout.Controls.Add(options, 0, 1);
        layout.Controls.Add(textOptions, 0, 2);
        layout.Controls.Add(_summary, 0, 3);
        layout.Controls.Add(_samples, 0, 4);
        layout.Controls.Add(buttons, 0, 5);
        _samples.Columns.Add("Record", L10n.Get(TextKey.Transform_RecordID));
        _samples.Columns.Add("Column", L10n.Get(TextKey.Transform_CSVColumn));
        _samples.Columns.Add("Before", L10n.Get(TextKey.Transform_Before));
        _samples.Columns.Add("After", L10n.Get(TextKey.Transform_After));
        _samples.Columns[0].FillWeight = 35;
        _samples.Columns[1].FillWeight = 35;
        _samples.CellPainting += (_, e) =>
        {
            if (e.ColumnIndex is 2 or 3) CsvWhitespaceCellPainter.Paint(_samples, e, '·', showSpaces);
        };
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
        CsvLocalizationAppearance.Apply(this);
    }

    internal void ApplyTheme(Color background, Color foreground)
    {
        void Style(Control control)
        {
            control.BackColor = background;
            control.ForeColor = foreground;
            if (control is Button button) { button.UseVisualStyleBackColor = false; button.FlatStyle = FlatStyle.Flat; }
            foreach (Control child in control.Controls) Style(child);
        }
        Style(this);
        _samples.EnableHeadersVisualStyles = false;
        _samples.BackgroundColor = background;
        _samples.DefaultCellStyle.BackColor = background;
        _samples.DefaultCellStyle.ForeColor = foreground;
        _samples.ColumnHeadersDefaultCellStyle.BackColor = background;
        _samples.ColumnHeadersDefaultCellStyle.ForeColor = foreground;
        _samples.GridColor = SystemColors.GrayText;
    }

    private void InvalidatePreview()
    {
        _plan = null;
        _accept.Enabled = false;
        _samples.Rows.Clear();
        _summary.Text = L10n.Get(TextKey.Transform_ChooseAnOperationAndScopeThenClickPreview);
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
                _samples.Rows.Add(id.IsInserted ? L10n.Format(TextKey.Rows_NewIdentifier, -id.Value) : $"{id.SourceRecordIndex!.Value + 1}",
                    change.Address.ColumnIndex + 1, Sample(change.Before), Sample(change.After));
            }

            _summary.Text = L10n.Format(TextKey.Transform_TargetCellsChangesInRowsShowingTheFirst, _plan.TargetCellCount, _plan.Changes.Count, _plan.ChangedRowCount) + (_showSpaces ? L10n.Get(TextKey.Transform_SolidOrangeDotsMarkEmptySpaces) : L10n.Get(TextKey.Transform_SpaceIndicatorsAreHidden)) +
                L10n.Get(TextKey.Transform_OtherWhitespaceIsEscapedLengthsCountUTFUnits);
            _accept.Enabled = _plan.Changes.Count > 0;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _summary.Text = L10n.Get(TextKey.Transform_PreviewCouldNotBeCreatedCheckTheScope);
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
            _summary.Text = L10n.Get(TextKey.Transform_ThePreviewIsNoLongerValidCheckThe);
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
