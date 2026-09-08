namespace CsvVisualEditor;

using CsvVisualEditor.Core;

/// <summary>A modal draft: only Apply view publishes a validated immutable definition.</summary>
internal sealed class CsvDataViewDialog : Form
{
    private readonly CsvTableProjection _projection;
    private readonly string _searchText;
    private readonly int? _searchColumn;
    private readonly Color _background;
    private readonly Color _foreground;
    private readonly FlowLayoutPanel _filters = RowsPanel("CsvFilterRows");
    private readonly FlowLayoutPanel _sorts = RowsPanel("CsvSortRows");
    private readonly ComboBox _combination = CsvDataToolStyle.Combo("CsvFilterCombination", "Match ALL conditions", "Match ANY condition");
    private readonly Label _result = new() { Name = "CsvViewPreview", AutoSize = true, UseMnemonic = false, Dock = DockStyle.Fill, Padding = new Padding(4) };
    private readonly Button _addFilter = CsvDataToolStyle.Button("Add condition", "CsvAddFilter");
    private readonly Button _addSort = CsvDataToolStyle.Button("Add sort level", "CsvAddSort");
    private readonly List<FilterRow> _filterRows = [];
    private readonly List<SortRow> _sortRows = [];
    private readonly ToolTip _tips = new();
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill, Name = "CsvViewRuleTabs" };

    internal CsvDataViewDefinition? Result { get; private set; }
    internal CsvTableViewResult? Preview { get; private set; }

    internal CsvDataViewDialog(CsvTableProjection projection, CsvDataViewDefinition definition,
        string searchText, int? searchColumn, Color background, Color foreground)
    {
        _projection = projection;
        _searchText = searchText;
        _searchColumn = searchColumn;
        _background = background;
        _foreground = foreground;
        Text = "Filter and sort";
        Name = "CsvDataViewDialog";
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(820, 530);
        MinimumSize = new Size(740, 460);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 4 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var intro = new Label
        {
            AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false, Padding = new Padding(4, 0, 4, 10),
            Text = $"View only - CSV values and source order are unchanged. {_projection.DisplayedRowCount:N0} displayed rows.\n" +
                (projection.IsRowLimited ? "The projection is limited; undisplayed rows are not included. " : "") +
                "Rules also respect the current search. Cancel keeps the active view."
        };
        root.Controls.Add(intro, 0, 0);
        root.Controls.Add(_tabs, 0, 1);
        root.Controls.Add(_result, 0, 2);
        var filterPage = new TabPage("Filters") { Padding = new Padding(8) };
        var sortPage = new TabPage("Sorting") { Padding = new Padding(8) };
        _tabs.TabPages.AddRange([filterPage, sortPage]);
        var filterLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filterLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        filterLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        filterLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        filterLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var modeRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        _combination.Dock = DockStyle.None;
        _combination.Width = 210;
        modeRow.Controls.AddRange([_combination, _addFilter]);
        filterLayout.Controls.Add(modeRow, 0, 0);
        var headings = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 5, Margin = Padding.Empty };
        headings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
        headings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29));
        headings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        headings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        headings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
        headings.Padding = new Padding(0, 0, SystemInformation.VerticalScrollBarWidth + 6, 0);
        headings.Controls.Add(CsvDataToolStyle.Label("Column"), 0, 0);
        headings.Controls.Add(CsvDataToolStyle.Label("Condition"), 1, 0);
        headings.Controls.Add(CsvDataToolStyle.Label("Value (literal, including spaces)"), 2, 0);
        headings.Controls.Add(CsvDataToolStyle.Label("Case"), 3, 0);
        filterLayout.Controls.Add(headings, 0, 1);
        filterLayout.Controls.Add(_filters, 0, 2);
        var hint = new Label { AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false, Padding = new Padding(4), Text = "Empty = zero characters. Whitespace only = non-empty spaces/tabs/line breaks.\nNumbers use a decimal dot (-12.5), without grouping or exponents; precision must be exact." };
        filterLayout.Controls.Add(hint, 0, 3);
        filterPage.Controls.Add(filterLayout);
        var sortLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        sortLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sortLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sortLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sortLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var sortHeading = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        sortHeading.Controls.AddRange([_addSort, CsvDataToolStyle.Label("Top level wins; later levels break ties. Maximum 3.")]);
        sortLayout.Controls.Add(sortHeading, 0, 0);
        sortLayout.Controls.Add(_sorts, 0, 1);
        sortLayout.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false, Padding = new Padding(4), Text = "Text: case-insensitive ordinal order. Number: exact decimals; invalid/empty values last.\nEqual keys retain source order. Remove all levels to restore source order." }, 0, 2);
        sortPage.Controls.Add(sortLayout);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancel = CsvDataToolStyle.Button("Cancel", "CsvCancelView");
        cancel.DialogResult = DialogResult.Cancel;
        var apply = CsvDataToolStyle.Button("Apply view", "CsvApplyView");
        var preview = CsvDataToolStyle.Button("Preview", "CsvPreviewView");
        var reset = CsvDataToolStyle.Button("Reset rules", "CsvResetRules");
        buttons.Controls.AddRange([cancel, apply, preview, reset]);
        root.Controls.Add(buttons, 0, 3);
        Controls.Add(root);
        CancelButton = cancel;
        AcceptButton = apply;
        _combination.SelectedIndex = definition.Combination == CsvFilterCombination.All ? 0 : 1;
        foreach (var condition in definition.Filters) AddFilter(condition);
        foreach (var key in definition.SortKeys) AddSort(key);
        _combination.SelectedIndexChanged += (_, _) => DraftChanged();
        _addFilter.Click += (_, _) => AddFilter(null);
        _addSort.Click += (_, _) => AddSort(null);
        preview.Click += (_, _) => ValidateDraft(close: false);
        apply.Click += (_, _) => ValidateDraft(close: true);
        reset.Click += (_, _) =>
        {
            foreach (var row in _filterRows) row.Dispose();
            foreach (var row in _sortRows) row.Dispose();
            _filterRows.Clear();
            _sortRows.Clear();
            _combination.SelectedIndex = 0;
            DraftChanged();
        };
        _filters.SizeChanged += (_, _) => FitRows(_filters);
        _sorts.SizeChanged += (_, _) => FitRows(_sorts);
        CsvDataToolStyle.Apply(this, background, foreground);
        DraftChanged();
    }

    private static FlowLayoutPanel RowsPanel(string name) => new()
    {
        Name = name, Dock = DockStyle.Fill, AutoScroll = true,
        FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0)
    };

    private static void FitRows(FlowLayoutPanel panel)
    {
        var width = Math.Max(100, panel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 6);
        foreach (Control row in panel.Controls) row.Width = width;
    }

    private void AddFilter(CsvColumnFilter? condition)
    {
        if (_projection.ColumnCount == 0 || _filterRows.Count >= CsvDataViewDefinition.MaximumFilters) return;
        var row = new FilterRow(_projection, condition, DraftChanged, _tips);
        row.Remove.Click += (_, _) => { _filterRows.Remove(row); row.Dispose(); DraftChanged(); };
        _filterRows.Add(row);
        _filters.Controls.Add(row);
        CsvDataToolStyle.Apply(row, _background, _foreground);
        FitRows(_filters);
        DraftChanged();
    }

    private void AddSort(CsvSortKey? key)
    {
        if (_projection.ColumnCount == 0 || _sortRows.Count >= CsvDataViewDefinition.MaximumSortKeys) return;
        var row = new SortRow(_projection, key, DraftChanged);
        row.Remove.Click += (_, _) => { _sortRows.Remove(row); row.Dispose(); DraftChanged(); };
        _sortRows.Add(row);
        _sorts.Controls.Add(row);
        CsvDataToolStyle.Apply(row, _background, _foreground);
        FitRows(_sorts);
        DraftChanged();
    }

    private void DraftChanged()
    {
        Preview = null;
        Result = null;
        _result.ForeColor = _foreground;
        _result.Text = $"{_filterRows.Count} / 8 conditions; {_sortRows.Count} / 3 sort levels. Preview to check matching rows.";
        _addFilter.Enabled = _projection.ColumnCount > 0 && _filterRows.Count < CsvDataViewDefinition.MaximumFilters;
        _addSort.Enabled = _projection.ColumnCount > 0 && _sortRows.Count < CsvDataViewDefinition.MaximumSortKeys;
    }

    private void ValidateDraft(bool close)
    {
        try
        {
            var definition = new CsvDataViewDefinition(_filterRows.Select(static row => row.Read()),
                _combination.SelectedIndex == 0 ? CsvFilterCombination.All : CsvFilterCombination.Any,
                _sortRows.Select(static row => row.Read()));
            var view = CsvTableViewBuilder.Build(_projection, new CsvTableViewOptions
            {
                SearchText = _searchText, SearchColumnIndex = _searchColumn, DataView = definition
            });
            Preview = view;
            _result.ForeColor = _foreground;
            _result.Text = $"Preview: {view.VisibleRowCount:N0} of {view.TotalRowCount:N0} displayed rows. CSV unchanged.";
            if (!close) return;
            Result = definition;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ArgumentException exception)
        {
            Preview = null;
            Result = null;
            _result.ForeColor = _background.GetBrightness() < .5f ? Color.LightSalmon : Color.DarkRed;
            _result.Text = "Check the rules: " + exception.Message.Split('\n')[0];
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tips.Dispose();
        base.Dispose(disposing);
    }

    private sealed class FilterRow : TableLayoutPanel
    {
        private static readonly (CsvFilterOperator Op, string Text)[] Operators =
        [
            (CsvFilterOperator.Contains, "Contains"), (CsvFilterOperator.DoesNotContain, "Does not contain"),
            (CsvFilterOperator.Equals, "Equals"), (CsvFilterOperator.DoesNotEqual, "Does not equal"),
            (CsvFilterOperator.StartsWith, "Starts with"), (CsvFilterOperator.EndsWith, "Ends with"),
            (CsvFilterOperator.IsEmpty, "Is empty"), (CsvFilterOperator.IsNotEmpty, "Is not empty"),
            (CsvFilterOperator.IsWhitespace, "Is whitespace only"), (CsvFilterOperator.IsNotWhitespace, "Is not whitespace only"),
            (CsvFilterOperator.NumberEquals, "Number ="), (CsvFilterOperator.GreaterThan, "Number >"),
            (CsvFilterOperator.GreaterThanOrEqual, "Number >="), (CsvFilterOperator.LessThan, "Number <"),
            (CsvFilterOperator.LessThanOrEqual, "Number <=")
        ];
        private readonly ComboBox _column;
        private readonly ComboBox _operator;
        private readonly TextBox _value = new() { Name = "CsvFilterValue", AccessibleName = "Filter value", Dock = DockStyle.Fill, Margin = new Padding(4, 5, 4, 5), MaxLength = CsvColumnFilter.MaximumValueLength };
        private readonly CheckBox _case = new() { Text = "Aa", AccessibleName = "Match case", AutoSize = true, Anchor = AnchorStyles.None };
        internal Button Remove { get; } = new() { Text = "X", Name = "CsvRemoveFilter", AccessibleName = "Remove condition", Dock = DockStyle.Fill, Margin = new Padding(4) };

        internal FilterRow(CsvTableProjection projection, CsvColumnFilter? condition, Action changed, ToolTip tips)
        {
            Name = "CsvFilterRow";
            AutoSize = true;
            ColumnCount = 5;
            RowCount = 1;
            Margin = new Padding(0, 2, 0, 2);
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29));
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
            ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            _column = CsvDataToolStyle.Combo("CsvFilterColumn", projection.Columns.Select(static c => (object)$"{c.Index + 1}: {c.Name}").ToArray());
            _operator = CsvDataToolStyle.Combo("CsvFilterOperator", Operators.Select(static entry => (object)entry.Text).ToArray());
            Controls.Add(_column, 0, 0); Controls.Add(_operator, 1, 0); Controls.Add(_value, 2, 0);
            Controls.Add(_case, 3, 0); Controls.Add(Remove, 4, 0);
            if (condition is not null)
            {
                _column.SelectedIndex = condition.ColumnIndex;
                _operator.SelectedIndex = Array.FindIndex(Operators, entry => entry.Op == condition.Operation);
                _value.Text = condition.Value;
                _case.Checked = condition.MatchCase;
            }
            void Sync()
            {
                var op = Operators[_operator.SelectedIndex].Op;
                _value.Enabled = CsvColumnFilter.RequiresValue(op);
                _case.Enabled = _value.Enabled && !CsvColumnFilter.IsNumeric(op);
            }
            _operator.SelectedIndexChanged += (_, _) => { Sync(); changed(); };
            _column.SelectedIndexChanged += (_, _) => changed();
            _value.TextChanged += (_, _) => changed();
            _case.CheckedChanged += (_, _) => changed();
            tips.SetToolTip(_case, "Case-sensitive text comparison. Spaces in values are literal.");
            tips.SetToolTip(_value, "Literal text, including spaces. Numeric rules require an exact decimal with a dot.");
            Sync();
        }
        internal CsvColumnFilter Read() => new(_column.SelectedIndex, Operators[_operator.SelectedIndex].Op, _value.Text, _case.Checked);
    }

    private sealed class SortRow : TableLayoutPanel
    {
        private readonly ComboBox _column;
        private readonly ComboBox _kind = CsvDataToolStyle.Combo("CsvSortKind", "Text", "Number (decimal)");
        private readonly ComboBox _direction = CsvDataToolStyle.Combo("CsvSortDirection", "Ascending", "Descending");
        internal Button Remove { get; } = new() { Text = "X", Name = "CsvRemoveSort", AccessibleName = "Remove sort level", Dock = DockStyle.Fill, Margin = new Padding(4) };
        internal SortRow(CsvTableProjection projection, CsvSortKey? key, Action changed)
        {
            Name = "CsvSortRow"; AutoSize = true; ColumnCount = 4; RowCount = 1; Margin = new Padding(0, 2, 0, 2);
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            _column = CsvDataToolStyle.Combo("CsvSortColumn", projection.Columns.Select(static c => (object)$"{c.Index + 1}: {c.Name}").ToArray());
            Controls.Add(_column, 0, 0); Controls.Add(_kind, 1, 0); Controls.Add(_direction, 2, 0); Controls.Add(Remove, 3, 0);
            if (key is not null)
            {
                _column.SelectedIndex = key.ColumnIndex;
                _kind.SelectedIndex = key.Kind == CsvSortKind.Text ? 0 : 1;
                _direction.SelectedIndex = key.Direction == CsvTableSortDirection.Ascending ? 0 : 1;
            }
            _column.SelectedIndexChanged += (_, _) => changed();
            _kind.SelectedIndexChanged += (_, _) => changed();
            _direction.SelectedIndexChanged += (_, _) => changed();
        }
        internal CsvSortKey Read() => new(_column.SelectedIndex,
            _direction.SelectedIndex == 0 ? CsvTableSortDirection.Ascending : CsvTableSortDirection.Descending,
            _kind.SelectedIndex == 0 ? CsvSortKind.Text : CsvSortKind.Number);
    }
}
