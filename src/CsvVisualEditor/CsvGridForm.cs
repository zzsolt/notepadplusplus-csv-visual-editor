namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using Npp.DotNet.Plugin;
using Npp.DotNet.Plugin.Winforms;
using Npp.DotNet.Plugin.Winforms.Classes;
using System.Globalization;

internal sealed class CsvGridForm : DockingForm
{
    private const string FormTitle = "CSV Visual Editor";
    private const int DelimiterAutoIndex = 0;
    private const int DelimiterCommaIndex = 1;
    private const int DelimiterSemicolonIndex = 2;
    private const int DelimiterTabIndex = 3;
    private const int HeaderFirstRecordIndex = 0;
    private const int HeaderNoneIndex = 1;

    private static readonly NppTbMsg InitialDockPosition = NppTbMsg.DWS_DF_CONT_RIGHT;

    private readonly ToolStrip _toolStrip;
    private readonly ToolStripButton _refreshButton;
    private readonly ToolStripComboBox _delimiterCombo;
    private readonly ToolStripComboBox _headerCombo;
    private readonly DataGridView _grid;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _statusLabel;

    public CsvGridForm(int dialogId, string pluginModuleName, Icon formIcon)
        : base(dialogId, pluginModuleName, FormTitle, null, formIcon, InitialDockPosition)
    {
        _refreshButton = new ToolStripButton("Refresh")
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ToolTipText = "Read and render the current active Notepad++ editor buffer"
        };
        _refreshButton.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);

        _delimiterCombo = new ToolStripComboBox
        {
            AutoSize = false,
            DropDownStyle = ComboBoxStyle.DropDownList,
            ToolTipText = "Use reliable automatic detection or choose a delimiter explicitly",
            Width = 128
        };
        _delimiterCombo.Items.AddRange([
            "Auto detect",
            "Comma (,) ",
            "Semicolon (;)",
            "Tab"
        ]);
        _delimiterCombo.SelectedIndex = DelimiterAutoIndex;
        _delimiterCombo.SelectedIndexChanged += OnDisplayOptionChanged;

        _headerCombo = new ToolStripComboBox
        {
            AutoSize = false,
            DropDownStyle = ComboBoxStyle.DropDownList,
            ToolTipText = "Choose explicitly whether the first logical record is a header",
            Width = 150
        };
        _headerCombo.Items.AddRange([
            "First row is header",
            "No header row"
        ]);
        _headerCombo.SelectedIndex = HeaderFirstRecordIndex;
        _headerCombo.SelectedIndexChanged += OnDisplayOptionChanged;

        _toolStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };
        _toolStrip.Items.Add(_refreshButton);
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(new ToolStripLabel("Delimiter:"));
        _toolStrip.Items.Add(_delimiterCombo);
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(new ToolStripLabel("Header:"));
        _toolStrip.Items.Add(_headerCombo);

        _grid = new DataGridView
        {
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToOrderColumns = false,
            AllowUserToResizeColumns = true,
            AllowUserToResizeRows = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
            ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            Dock = DockStyle.Fill,
            EditMode = DataGridViewEditMode.EditProgrammatically,
            MultiSelect = true,
            ReadOnly = true,
            RowHeadersVisible = true,
            RowHeadersWidth = 62,
            SelectionMode = DataGridViewSelectionMode.CellSelect
        };
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        _grid.RowTemplate.Height = 22;

        _statusLabel = new ToolStripStatusLabel
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _statusStrip = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            SizingGrip = false
        };
        _statusStrip.Items.Add(_statusLabel);

        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(980, 560);
        Controls.Add(_grid);
        Controls.Add(_statusStrip);
        Controls.Add(_toolStrip);
        MinimumSize = new Size(520, 280);
        Text = FormTitle;
        ResumeLayout(performLayout: true);

        AttachEventHandlers();
        ShowBootstrapState();
        ToggleDarkMode(PluginData.Notepad.IsDarkModeEnabled());
    }

    public event EventHandler? RefreshRequested;

    public char? SelectedDelimiterOverride => _delimiterCombo.SelectedIndex switch
    {
        DelimiterAutoIndex => null,
        DelimiterCommaIndex => ',',
        DelimiterSemicolonIndex => ';',
        DelimiterTabIndex => '\t',
        _ => null
    };

    public CsvHeaderMode SelectedHeaderMode => _headerCombo.SelectedIndex switch
    {
        HeaderNoneIndex => CsvHeaderMode.NoHeader,
        _ => CsvHeaderMode.FirstRecord
    };

    public void ShowBootstrapState()
    {
        PrepareMetadataGrid();
        AddMetadataRow("Status", "Waiting for the active Notepad++ document.");
        _statusLabel.Text = "Plugin ready. Reading the active editor buffer...";
    }

    public void ShowEmptyDocument(ActiveDocumentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        PrepareTableGrid();
        _statusLabel.Text =
            $"{snapshot.DisplayName} — empty editor buffer; no CSV records to display.";
    }

    public void ShowDelimiterSelectionRequired(
        ActiveDocumentSnapshot snapshot,
        CsvDialectDetectionResult detectionResult)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(detectionResult);

        PrepareMetadataGrid();
        AddMetadataRow("Document", snapshot.DisplayName);
        AddMetadataRow("Automatic detection", "No reliable delimiter could be selected safely.");
        AddMetadataRow("Confidence", detectionResult.Confidence.ToString());
        AddMetadataRow(
            "Candidate scores",
            string.Join(
                ", ",
                detectionResult.Candidates.Select(
                    static candidate =>
                        $"{DelimiterDisplayName(candidate.Delimiter)}={candidate.Score}")));
        AddMetadataRow(
            "Action",
            "Choose Comma, Semicolon, or Tab from the Delimiter list above.");

        _statusLabel.Text =
            $"{snapshot.DisplayName} — automatic delimiter detection is not reliable; " +
            "manual selection is required.";
    }

    public void ShowVisualTable(
        ActiveDocumentSnapshot snapshot,
        CsvParseResult parseResult,
        CsvTableProjection projection,
        CsvDialectDetectionResult? detectionResult,
        bool delimiterWasAutomatic)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(projection);

        PrepareTableGrid();
        _grid.SuspendLayout();
        try
        {
            foreach (var column in projection.Columns)
            {
                _grid.Columns.Add(
                    new DataGridViewTextBoxColumn
                    {
                        Name = $"CsvColumn{column.Index}",
                        HeaderText = column.Name,
                        MinimumWidth = 70,
                        SortMode = DataGridViewColumnSortMode.NotSortable,
                        Width = 160
                    });
            }

            foreach (var row in projection.Rows)
            {
                var values = row.Values
                    .Select(static value => (object)value)
                    .ToArray();
                var gridRowIndex = _grid.Rows.Add(values);
                _grid.Rows[gridRowIndex].HeaderCell.Value =
                    (row.SourceRecordIndex + 1).ToString(CultureInfo.InvariantCulture);
            }
        }
        finally
        {
            _grid.ResumeLayout(performLayout: true);
        }

        var errorCount = parseResult.Diagnostics.Count(
            static diagnostic => diagnostic.Severity == CsvDiagnosticSeverity.Error);
        var warningCount = parseResult.Diagnostics.Count(
            static diagnostic => diagnostic.Severity == CsvDiagnosticSeverity.Warning);
        var displayedRows = projection.IsRowLimited
            ? $"showing {FormatNumber(projection.DisplayedRowCount)} of " +
              $"{FormatNumber(projection.TotalDataRecordCount)} rows"
            : $"{FormatNumber(projection.DisplayedRowCount)} rows";
        var delimiterSource = delimiterWasAutomatic
            ? $"automatic {detectionResult?.Confidence.ToString().ToLowerInvariant() ?? "unknown"} confidence"
            : "manual selection";
        var headerDescription = SelectedHeaderMode == CsvHeaderMode.FirstRecord
            ? "first row as header"
            : "no header row";
        var diagnosticDescription = errorCount == 0 && warningCount == 0
            ? "no parser diagnostics"
            : $"{errorCount} errors, {warningCount} warnings";

        _statusLabel.Text =
            $"{snapshot.DisplayName} — {displayedRows} × {FormatNumber(projection.ColumnCount)} columns — " +
            $"{parseResult.Dialect.DelimiterDisplayName}, {delimiterSource} — " +
            $"{headerDescription} — {diagnosticDescription}.";
    }

    public void ShowSnapshotError(string message)
    {
        PrepareMetadataGrid();
        AddMetadataRow("Snapshot error", message);
        _statusLabel.Text = "Active-document snapshot failed. No editor content was changed.";
    }

    public void ShowTableError(string message)
    {
        PrepareMetadataGrid();
        AddMetadataRow("Table error", message);
        _statusLabel.Text = "The visual table could not be produced. The editor content was not changed.";
    }

    public override void ToggleDarkMode(bool isDark)
    {
        if (isDark)
        {
            var theme = new DarkMode.DarkModeColors();
            BackColor = theme.SofterBackground;
            ForeColor = theme.Text;
            _toolStrip.BackColor = theme.SofterBackground;
            _toolStrip.ForeColor = theme.Text;
            _delimiterCombo.BackColor = theme.SofterBackground;
            _delimiterCombo.ForeColor = theme.Text;
            _headerCombo.BackColor = theme.SofterBackground;
            _headerCombo.ForeColor = theme.Text;
            _statusStrip.BackColor = theme.SofterBackground;
            _statusStrip.ForeColor = theme.Text;
            _grid.BackgroundColor = theme.SofterBackground;
            _grid.DefaultCellStyle.BackColor = theme.SofterBackground;
            _grid.DefaultCellStyle.ForeColor = theme.Text;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = theme.SofterBackground;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = theme.Text;
            _grid.RowHeadersDefaultCellStyle.BackColor = theme.SofterBackground;
            _grid.RowHeadersDefaultCellStyle.ForeColor = theme.Text;
            _grid.EnableHeadersVisualStyles = false;
        }
        else
        {
            BackColor = SystemColors.Control;
            ForeColor = SystemColors.ControlText;
            _toolStrip.BackColor = SystemColors.Control;
            _toolStrip.ForeColor = SystemColors.ControlText;
            _delimiterCombo.BackColor = SystemColors.Window;
            _delimiterCombo.ForeColor = SystemColors.WindowText;
            _headerCombo.BackColor = SystemColors.Window;
            _headerCombo.ForeColor = SystemColors.WindowText;
            _statusStrip.BackColor = SystemColors.Control;
            _statusStrip.ForeColor = SystemColors.ControlText;
            _grid.BackgroundColor = SystemColors.AppWorkspace;
            _grid.DefaultCellStyle.BackColor = SystemColors.Window;
            _grid.DefaultCellStyle.ForeColor = SystemColors.WindowText;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
            _grid.RowHeadersDefaultCellStyle.BackColor = SystemColors.Control;
            _grid.RowHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
            _grid.EnableHeadersVisualStyles = true;
        }

        Invalidate(invalidateChildren: true);
    }

    protected override void AttachEventHandlers()
    {
        base.AttachEventHandlers();
        _grid.Focus();
    }

    private void OnDisplayOptionChanged(object? sender, EventArgs e)
    {
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private void PrepareMetadataGrid()
    {
        _grid.Rows.Clear();
        _grid.Columns.Clear();
        _grid.RowHeadersVisible = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

        _grid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Property",
                HeaderText = "Property",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 120,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        _grid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Value",
                HeaderText = "Value",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 220,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
    }

    private void PrepareTableGrid()
    {
        _grid.Rows.Clear();
        _grid.Columns.Clear();
        _grid.RowHeadersVisible = true;
        _grid.MultiSelect = true;
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
    }

    private void AddMetadataRow(string property, string value)
    {
        _grid.Rows.Add(property, value);
    }

    private static string FormatNumber(long value)
    {
        return value.ToString("N0", CultureInfo.CurrentCulture);
    }

    private static string DelimiterDisplayName(char delimiter) => delimiter switch
    {
        ',' => "comma",
        ';' => "semicolon",
        '\t' => "tab",
        _ => $"U+{(int)delimiter:X4}"
    };
}