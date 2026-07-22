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
    private const int TableColumnMinimumWidth = 90;
    private const int SearchDelayMilliseconds = 250;

    private static readonly NppTbMsg InitialDockPosition = NppTbMsg.DWS_DF_CONT_RIGHT;

    private readonly ToolStrip _toolStrip;
    private readonly ToolStrip _viewToolStrip;
    private readonly ToolStripButton _refreshButton;
    private readonly ToolStripComboBox _delimiterCombo;
    private readonly ToolStripComboBox _headerCombo;
    private readonly ToolStripButton _editButton;
    private readonly ToolStripButton _applyButton;
    private readonly ToolStripButton _revertAllButton;
    private readonly ToolStripLabel _dirtyLabel;
    private readonly ToolStripTextBox _searchBox;
    private readonly ToolStripComboBox _searchColumnCombo;
    private readonly ToolStripButton _clearSearchButton;
    private readonly ToolStripButton _diagnosticsButton;
    private readonly TableLayoutPanel _topPanel;
    private readonly TabControl _tabControl;
    private readonly TabPage _tablePage;
    private readonly TabPage _diagnosticsPage;
    private readonly DataGridView _grid;
    private readonly DataGridView _diagnosticsGrid;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly System.Windows.Forms.Timer _searchTimer;

    private ActiveDocumentSnapshot? _snapshot;
    private CsvParseResult? _parseResult;
    private CsvTableProjection? _projection;
    private CsvDialectDetectionResult? _detectionResult;
    private CsvEditSession? _editSession;
    private CsvTableViewResult? _lastViewResult;
    private bool _delimiterWasAutomatic;
    private bool _updatingViewControls;
    private bool _suppressGridChanges;
    private bool _editMode;
    private int? _sortColumnIndex;
    private CsvTableSortDirection _sortDirection = CsvTableSortDirection.None;

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
            "Comma (,)",
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

        _editButton = new ToolStripButton("Edit")
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Enabled = false,
            ToolTipText = "Enter explicit cell-editing mode"
        };
        _editButton.Click += (_, _) => ToggleEditMode();

        _applyButton = new ToolStripButton("Apply")
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Enabled = false,
            ToolTipText = "Apply all grid edits to the active editor as one undoable action"
        };
        _applyButton.Click += (_, _) => RequestApply();

        _revertAllButton = new ToolStripButton("Revert All")
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Enabled = false,
            ToolTipText = "Discard every pending grid edit"
        };
        _revertAllButton.Click += (_, _) => RevertAllEdits();

        _dirtyLabel = new ToolStripLabel("0 changes")
        {
            ToolTipText = "Pending cell and record changes"
        };

        _toolStrip = new ToolStrip
        {
            Dock = DockStyle.Fill,
            GripStyle = ToolStripGripStyle.Hidden
        };
        _toolStrip.Items.Add(_refreshButton);
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(new ToolStripLabel("Delimiter:"));
        _toolStrip.Items.Add(_delimiterCombo);
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(new ToolStripLabel("Header:"));
        _toolStrip.Items.Add(_headerCombo);
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(_editButton);
        _toolStrip.Items.Add(_applyButton);
        _toolStrip.Items.Add(_revertAllButton);
        _toolStrip.Items.Add(_dirtyLabel);

        _searchBox = new ToolStripTextBox
        {
            AutoSize = false,
            Enabled = false,
            ToolTipText = "Filter visible rows without changing the source CSV",
            Width = 180
        };
        _searchBox.TextChanged += OnSearchTextChanged;

        _searchColumnCombo = new ToolStripComboBox
        {
            AutoSize = false,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = false,
            ToolTipText = "Search all columns or only one selected column",
            Width = 160
        };
        _searchColumnCombo.Items.Add("All columns");
        _searchColumnCombo.SelectedIndex = 0;
        _searchColumnCombo.SelectedIndexChanged += OnSearchColumnChanged;

        _clearSearchButton = new ToolStripButton("Clear")
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Enabled = false,
            ToolTipText = "Clear the current search filter and view-only sort"
        };
        _clearSearchButton.Click += (_, _) => ClearViewOptions();

        _diagnosticsButton = new ToolStripButton("Diagnostics (0)")
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ToolTipText = "Show detailed parser and delimiter diagnostics"
        };

        _viewToolStrip = new ToolStrip
        {
            Dock = DockStyle.Fill,
            GripStyle = ToolStripGripStyle.Hidden
        };
        _viewToolStrip.Items.Add(new ToolStripLabel("Search:"));
        _viewToolStrip.Items.Add(_searchBox);
        _viewToolStrip.Items.Add(new ToolStripLabel("In:"));
        _viewToolStrip.Items.Add(_searchColumnCombo);
        _viewToolStrip.Items.Add(_clearSearchButton);
        _viewToolStrip.Items.Add(new ToolStripSeparator());
        _viewToolStrip.Items.Add(_diagnosticsButton);

        _topPanel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 2
        };
        _topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _topPanel.Controls.Add(_toolStrip, 0, 0);
        _topPanel.Controls.Add(_viewToolStrip, 0, 1);

        _grid = CreateReadOnlyGrid(showRowHeaders: true);
        _grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
        _grid.ColumnHeaderMouseClick += OnTableColumnHeaderMouseClick;
        _grid.CellValueChanged += OnGridCellValueChanged;

        _diagnosticsGrid = CreateReadOnlyGrid(showRowHeaders: false);
        _diagnosticsGrid.MultiSelect = false;
        _diagnosticsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        ConfigureDiagnosticsGrid();

        _tablePage = new TabPage("Table")
        {
            Padding = Padding.Empty
        };
        _tablePage.Controls.Add(_grid);

        _diagnosticsPage = new TabPage("Diagnostics (0)")
        {
            Padding = Padding.Empty
        };
        _diagnosticsPage.Controls.Add(_diagnosticsGrid);

        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill
        };
        _tabControl.TabPages.Add(_tablePage);
        _tabControl.TabPages.Add(_diagnosticsPage);
        _diagnosticsButton.Click += (_, _) => _tabControl.SelectedTab = _diagnosticsPage;

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

        _searchTimer = new System.Windows.Forms.Timer
        {
            Interval = SearchDelayMilliseconds
        };
        _searchTimer.Tick += OnSearchTimerTick;

        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(980, 560);
        Controls.Add(_tabControl);
        Controls.Add(_statusStrip);
        Controls.Add(_topPanel);
        MinimumSize = new Size(620, 340);
        Text = FormTitle;
        ResumeLayout(performLayout: true);

        AttachEventHandlers();
        ShowBootstrapState();
        ToggleDarkMode(PluginData.Notepad.IsDarkModeEnabled());
    }

    public event EventHandler? RefreshRequested;

    public event EventHandler? ApplyRequested;

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

    public CsvEditSession? EditSession => _editSession;

    public bool IsEditMode => _editMode;

    public bool CommitPendingEdit()
    {
        return !_grid.IsCurrentCellInEditMode || _grid.EndEdit();
    }

    public void ShowBootstrapState()
    {
        ResetVisualTableContext();
        PrepareMetadataGrid();
        AddMetadataRow("Status", "Waiting for the active Notepad++ document.");
        _statusLabel.Text = "Plugin ready. Reading the active editor buffer...";
    }

    public void ShowEmptyDocument(ActiveDocumentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        ResetVisualTableContext();
        PrepareTableGrid();
        _tabControl.SelectedTab = _tablePage;
        _statusLabel.Text =
            $"{snapshot.DisplayName} — empty editor buffer; no CSV records to display.";
    }

    public void ShowDelimiterSelectionRequired(
        ActiveDocumentSnapshot snapshot,
        CsvDialectDetectionResult detectionResult)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(detectionResult);

        ResetVisualTableContext();
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
        PopulateDiagnostics(detectionResult.Diagnostics);
        _tabControl.SelectedTab = _tablePage;

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

        ResetEditState(clearSession: true);
        _snapshot = snapshot;
        _parseResult = parseResult;
        _projection = projection;
        _detectionResult = detectionResult;
        _delimiterWasAutomatic = delimiterWasAutomatic;
        _sortColumnIndex = null;
        _sortDirection = CsvTableSortDirection.None;

        PrepareTableGrid();
        PopulateTableColumns(projection);
        PopulateSearchColumns(projection);
        PopulateDiagnostics(GetAllDiagnostics(detectionResult, parseResult));
        TryCreateEditSession(snapshot, parseResult, projection);
        UpdateControlAvailability();
        ApplyCurrentView();
        _tabControl.SelectedTab = _tablePage;
    }

    public void ShowSnapshotError(string message)
    {
        ResetVisualTableContext();
        PrepareMetadataGrid();
        AddMetadataRow("Snapshot error", message);
        _statusLabel.Text = "Active-document snapshot failed. No editor content was changed.";
    }

    public void ShowTableError(string message)
    {
        ResetVisualTableContext();
        PrepareMetadataGrid();
        AddMetadataRow("Table error", message);
        _statusLabel.Text = "The visual table could not be produced. The editor content was not changed.";
    }

    public void ShowApplyConflict(CsvEditorApplyStatus status)
    {
        _statusLabel.Text = status switch
        {
            CsvEditorApplyStatus.DocumentIdentityChanged =>
                "Apply blocked: another Notepad++ document is active. Return to the original document or Revert All.",
            CsvEditorApplyStatus.CodePageChanged =>
                "Apply blocked: the editor code page changed after Edit mode started. Revert and reopen Edit mode.",
            CsvEditorApplyStatus.ContentChanged =>
                "Apply blocked: the editor buffer changed after Edit mode started. Revert and refresh before editing again.",
            CsvEditorApplyStatus.NoChanges =>
                "Nothing to apply: the edit session contains no pending cell changes.",
            _ => "Apply was not completed. No automatic overwrite was attempted."
        };
    }

    public void ShowApplyError()
    {
        _statusLabel.Text =
            "Apply failed. The edit session remains open; verify the editor buffer before retrying or reverting.";
    }

    public override void ToggleDarkMode(bool isDark)
    {
        if (isDark)
        {
            var theme = new DarkMode.DarkModeColors();
            BackColor = theme.SofterBackground;
            ForeColor = theme.Text;
            _topPanel.BackColor = theme.SofterBackground;
            _toolStrip.BackColor = theme.SofterBackground;
            _toolStrip.ForeColor = theme.Text;
            _viewToolStrip.BackColor = theme.SofterBackground;
            _viewToolStrip.ForeColor = theme.Text;
            _delimiterCombo.BackColor = theme.SofterBackground;
            _delimiterCombo.ForeColor = theme.Text;
            _headerCombo.BackColor = theme.SofterBackground;
            _headerCombo.ForeColor = theme.Text;
            _searchBox.BackColor = theme.SofterBackground;
            _searchBox.ForeColor = theme.Text;
            _searchColumnCombo.BackColor = theme.SofterBackground;
            _searchColumnCombo.ForeColor = theme.Text;
            _statusStrip.BackColor = theme.SofterBackground;
            _statusStrip.ForeColor = theme.Text;
            _tabControl.BackColor = theme.SofterBackground;
            _tablePage.BackColor = theme.SofterBackground;
            _tablePage.ForeColor = theme.Text;
            _diagnosticsPage.BackColor = theme.SofterBackground;
            _diagnosticsPage.ForeColor = theme.Text;
            ApplyGridTheme(
                _grid,
                theme.SofterBackground,
                theme.Text,
                enableVisualStyles: false);
            ApplyGridTheme(
                _diagnosticsGrid,
                theme.SofterBackground,
                theme.Text,
                enableVisualStyles: false);
        }
        else
        {
            BackColor = SystemColors.Control;
            ForeColor = SystemColors.ControlText;
            _topPanel.BackColor = SystemColors.Control;
            _toolStrip.BackColor = SystemColors.Control;
            _toolStrip.ForeColor = SystemColors.ControlText;
            _viewToolStrip.BackColor = SystemColors.Control;
            _viewToolStrip.ForeColor = SystemColors.ControlText;
            _delimiterCombo.BackColor = SystemColors.Window;
            _delimiterCombo.ForeColor = SystemColors.WindowText;
            _headerCombo.BackColor = SystemColors.Window;
            _headerCombo.ForeColor = SystemColors.WindowText;
            _searchBox.BackColor = SystemColors.Window;
            _searchBox.ForeColor = SystemColors.WindowText;
            _searchColumnCombo.BackColor = SystemColors.Window;
            _searchColumnCombo.ForeColor = SystemColors.WindowText;
            _statusStrip.BackColor = SystemColors.Control;
            _statusStrip.ForeColor = SystemColors.ControlText;
            _tabControl.BackColor = SystemColors.Control;
            _tablePage.BackColor = SystemColors.Control;
            _tablePage.ForeColor = SystemColors.ControlText;
            _diagnosticsPage.BackColor = SystemColors.Control;
            _diagnosticsPage.ForeColor = SystemColors.ControlText;
            ApplyGridTheme(
                _grid,
                SystemColors.Window,
                SystemColors.WindowText,
                enableVisualStyles: true);
            ApplyGridTheme(
                _diagnosticsGrid,
                SystemColors.Window,
                SystemColors.WindowText,
                enableVisualStyles: true);
        }

        Invalidate(invalidateChildren: true);
    }

    protected override void AttachEventHandlers()
    {
        base.AttachEventHandlers();
        _grid.Focus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _searchTimer.Stop();
            _searchTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private static DataGridView CreateReadOnlyGrid(bool showRowHeaders)
    {
        var grid = new DataGridView
        {
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToOrderColumns = false,
            AllowUserToResizeColumns = true,
            AllowUserToResizeRows = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            Dock = DockStyle.Fill,
            EditMode = DataGridViewEditMode.EditProgrammatically,
            MultiSelect = true,
            ReadOnly = true,
            RowHeadersVisible = showRowHeaders,
            RowHeadersWidth = 72,
            SelectionMode = DataGridViewSelectionMode.CellSelect
        };
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        grid.RowTemplate.Height = 22;
        return grid;
    }

    private void OnDisplayOptionChanged(object? sender, EventArgs e)
    {
        if (_editMode)
        {
            return;
        }

        _sortColumnIndex = null;
        _sortDirection = CsvTableSortDirection.None;
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSearchTextChanged(object? sender, EventArgs e)
    {
        if (_updatingViewControls || _projection is null || _editMode)
        {
            return;
        }

        _clearSearchButton.Enabled =
            _searchBox.Text.Length > 0 || _sortColumnIndex is not null;
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void OnSearchColumnChanged(object? sender, EventArgs e)
    {
        if (_updatingViewControls || _projection is null || _editMode)
        {
            return;
        }

        ApplyCurrentView();
    }

    private void OnSearchTimerTick(object? sender, EventArgs e)
    {
        _searchTimer.Stop();
        if (!_editMode)
        {
            ApplyCurrentView();
        }
    }

    private void OnTableColumnHeaderMouseClick(
        object? sender,
        DataGridViewCellMouseEventArgs e)
    {
        if (_projection is null || e.ColumnIndex < 0 || _editMode)
        {
            return;
        }

        if (_sortColumnIndex != e.ColumnIndex)
        {
            _sortColumnIndex = e.ColumnIndex;
            _sortDirection = CsvTableSortDirection.Ascending;
        }
        else
        {
            _sortDirection = _sortDirection switch
            {
                CsvTableSortDirection.Ascending => CsvTableSortDirection.Descending,
                CsvTableSortDirection.Descending => CsvTableSortDirection.None,
                _ => CsvTableSortDirection.Ascending
            };

            if (_sortDirection == CsvTableSortDirection.None)
            {
                _sortColumnIndex = null;
            }
        }

        _clearSearchButton.Enabled =
            _searchBox.Text.Length > 0 || _sortColumnIndex is not null;
        ApplyCurrentView();
    }

    private void OnGridCellValueChanged(
        object? sender,
        DataGridViewCellEventArgs e)
    {
        if (!_editMode ||
            _suppressGridChanges ||
            _editSession is null ||
            e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            e.RowIndex >= _grid.Rows.Count ||
            e.ColumnIndex >= _grid.Columns.Count ||
            _grid.Rows[e.RowIndex].Tag is not int sourceRecordIndex)
        {
            return;
        }

        var value = Convert.ToString(
                _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value,
                CultureInfo.InvariantCulture) ?? string.Empty;
        _editSession.SetCellValue(sourceRecordIndex, e.ColumnIndex, value);
        UpdateDirtyIndicators();
    }

    private void ToggleEditMode()
    {
        if (_editMode)
        {
            if (_editSession?.IsDirty == true)
            {
                _statusLabel.Text =
                    "Edit mode contains pending changes. Use Apply or Revert All before leaving Edit mode.";
                return;
            }

            LeaveEditMode();
            return;
        }

        EnterEditMode();
    }

    private void EnterEditMode()
    {
        if (_editSession is null || _projection is null)
        {
            _statusLabel.Text =
                "Edit mode is unavailable for the current table. Resolve parser errors or display limits first.";
            return;
        }

        ClearViewStateWithoutRendering();
        _editMode = true;
        UpdateControlAvailability();
        ApplyCurrentView();
        _grid.Focus();
    }

    private void LeaveEditMode()
    {
        _editMode = false;
        UpdateControlAvailability();
        ApplyCurrentView();
    }

    private void RequestApply()
    {
        if (!_editMode || _editSession is null)
        {
            return;
        }

        if (!CommitPendingEdit())
        {
            _statusLabel.Text =
                "The active cell edit could not be committed. Correct the value before applying.";
            return;
        }

        if (!_editSession.IsDirty)
        {
            ShowApplyConflict(CsvEditorApplyStatus.NoChanges);
            UpdateDirtyIndicators();
            return;
        }

        ApplyRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RevertAllEdits()
    {
        if (!_editMode || _editSession is null)
        {
            return;
        }

        _grid.CancelEdit();
        _editSession.RevertAll();
        ApplyCurrentView();
        _statusLabel.Text =
            "All pending grid edits were reverted. The Notepad++ editor buffer was not changed.";
    }

    private void ClearViewOptions()
    {
        if (_editMode)
        {
            return;
        }

        ClearViewStateWithoutRendering();
        ApplyCurrentView();
    }

    private void ClearViewStateWithoutRendering()
    {
        _searchTimer.Stop();
        _updatingViewControls = true;
        try
        {
            _searchBox.Clear();
            if (_searchColumnCombo.Items.Count > 0)
            {
                _searchColumnCombo.SelectedIndex = 0;
            }
        }
        finally
        {
            _updatingViewControls = false;
        }

        _sortColumnIndex = null;
        _sortDirection = CsvTableSortDirection.None;
        _clearSearchButton.Enabled = false;
    }

    private void PopulateTableColumns(CsvTableProjection projection)
    {
        var fillWeights = CsvColumnFillWeightCalculator.Calculate(projection);
        foreach (var column in projection.Columns)
        {
            _grid.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = $"CsvColumn{column.Index}",
                    HeaderText = column.Name,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    FillWeight = fillWeights[column.Index],
                    MinimumWidth = TableColumnMinimumWidth,
                    SortMode = DataGridViewColumnSortMode.Programmatic
                });
        }
    }

    private void PopulateSearchColumns(CsvTableProjection projection)
    {
        _updatingViewControls = true;
        try
        {
            _searchColumnCombo.Items.Clear();
            _searchColumnCombo.Items.Add("All columns");
            foreach (var column in projection.Columns)
            {
                _searchColumnCombo.Items.Add(column.Name);
            }

            _searchColumnCombo.SelectedIndex = 0;
        }
        finally
        {
            _updatingViewControls = false;
        }
    }

    private void TryCreateEditSession(
        ActiveDocumentSnapshot snapshot,
        CsvParseResult parseResult,
        CsvTableProjection projection)
    {
        try
        {
            _editSession = CsvEditSession.Create(snapshot, parseResult, projection);
            _editButton.ToolTipText =
                "Enter explicit cell-editing mode. Apply writes only to the editor buffer.";
        }
        catch (InvalidOperationException exception)
        {
            _editSession = null;
            _editButton.ToolTipText = exception.Message;
        }
    }

    private void ApplyCurrentView()
    {
        if (_projection is null || _snapshot is null || _parseResult is null)
        {
            return;
        }

        int? searchColumnIndex = !_editMode && _searchColumnCombo.SelectedIndex > 0
            ? _searchColumnCombo.SelectedIndex - 1
            : null;
        var view = CsvTableViewBuilder.Build(
            _projection,
            new CsvTableViewOptions
            {
                SearchText = _editMode ? string.Empty : _searchBox.Text,
                SearchColumnIndex = searchColumnIndex,
                SortColumnIndex = _editMode ? null : _sortColumnIndex,
                SortDirection = _editMode
                    ? CsvTableSortDirection.None
                    : _sortDirection
            });
        _lastViewResult = view;

        _suppressGridChanges = true;
        _grid.SuspendLayout();
        try
        {
            _grid.Rows.Clear();
            foreach (var row in view.Rows)
            {
                var values = CreateDisplayedValues(row);
                var gridRowIndex = _grid.Rows.Add(values);
                var gridRow = _grid.Rows[gridRowIndex];
                gridRow.Tag = row.SourceRecordIndex;
                SetRowHeader(gridRow, row.SourceRecordIndex);
            }

            UpdateSortGlyphs();
        }
        finally
        {
            _grid.ResumeLayout(performLayout: true);
            _suppressGridChanges = false;
        }

        UpdateDirtyIndicators();
        UpdateStatus(view);
    }

    private object[] CreateDisplayedValues(CsvTableRow row)
    {
        if (!_editMode || _editSession is null)
        {
            return row.Values.Select(static value => (object)value).ToArray();
        }

        var values = new object[_projection?.ColumnCount ?? row.Values.Count];
        for (var columnIndex = 0; columnIndex < values.Length; columnIndex++)
        {
            values[columnIndex] = _editSession.GetValue(
                row.SourceRecordIndex,
                columnIndex);
        }

        return values;
    }

    private void SetRowHeader(DataGridViewRow gridRow, int sourceRecordIndex)
    {
        var isDirty = IsRecordDirty(sourceRecordIndex);
        gridRow.HeaderCell.Value =
            (sourceRecordIndex + 1).ToString(CultureInfo.InvariantCulture) +
            (isDirty ? " *" : string.Empty);
    }

    private bool IsRecordDirty(int sourceRecordIndex)
    {
        if (_editSession is null)
        {
            return false;
        }

        for (var columnIndex = 0; columnIndex < _editSession.ColumnCount; columnIndex++)
        {
            if (_editSession.IsCellDirty(sourceRecordIndex, columnIndex))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateDirtyIndicators()
    {
        var changedCells = _editSession?.ChangedCellCount ?? 0;
        var changedRecords = _editSession?.ChangedRecordCount ?? 0;
        _dirtyLabel.Text = changedCells == 0
            ? "0 changes"
            : $"{FormatNumber(changedCells)} cells / {FormatNumber(changedRecords)} rows";

        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is int sourceRecordIndex)
            {
                SetRowHeader(row, sourceRecordIndex);
            }
        }

        _applyButton.Enabled = _editMode && changedCells > 0;
        _revertAllButton.Enabled = _editMode && changedCells > 0;

        if (_lastViewResult is not null)
        {
            UpdateStatus(_lastViewResult);
        }
    }

    private void UpdateControlAvailability()
    {
        var hasTable = _projection is not null;
        var canEdit = _editSession is not null &&
                      _projection is not null &&
                      _projection.DisplayedRowCount > 0;

        _refreshButton.Enabled = !_editMode;
        _delimiterCombo.Enabled = !_editMode;
        _headerCombo.Enabled = !_editMode;
        _searchBox.Enabled = hasTable && !_editMode;
        _searchColumnCombo.Enabled = hasTable && !_editMode;
        _clearSearchButton.Enabled = hasTable &&
                                     !_editMode &&
                                     (_searchBox.Text.Length > 0 ||
                                      _sortColumnIndex is not null);
        _editButton.Enabled = canEdit;
        _editButton.Text = _editMode ? "Exit Edit" : "Edit";
        _editButton.Checked = _editMode;
        _applyButton.Enabled = _editMode && (_editSession?.IsDirty ?? false);
        _revertAllButton.Enabled = _applyButton.Enabled;
        _grid.ReadOnly = !_editMode;
        _grid.EditMode = _editMode
            ? DataGridViewEditMode.EditOnKeystrokeOrF2
            : DataGridViewEditMode.EditProgrammatically;
    }

    private void UpdateSortGlyphs()
    {
        foreach (DataGridViewColumn column in _grid.Columns)
        {
            column.HeaderCell.SortGlyphDirection = SortOrder.None;
        }

        if (_editMode ||
            _sortColumnIndex is null ||
            _sortDirection == CsvTableSortDirection.None)
        {
            return;
        }

        _grid.Columns[_sortColumnIndex.Value].HeaderCell.SortGlyphDirection =
            _sortDirection == CsvTableSortDirection.Ascending
                ? SortOrder.Ascending
                : SortOrder.Descending;
    }

    private void UpdateStatus(CsvTableViewResult view)
    {
        if (_projection is null || _snapshot is null || _parseResult is null)
        {
            return;
        }

        var allDiagnostics = GetAllDiagnostics(_detectionResult, _parseResult);
        var errorCount = allDiagnostics.Count(
            static diagnostic => diagnostic.Severity == CsvDiagnosticSeverity.Error);
        var warningCount = allDiagnostics.Count(
            static diagnostic => diagnostic.Severity == CsvDiagnosticSeverity.Warning);

        var rowDescription = view.IsFiltered
            ? $"{FormatNumber(view.VisibleRowCount)} matching of " +
              $"{FormatNumber(_projection.DisplayedRowCount)} displayed rows"
            : _projection.IsRowLimited
                ? $"showing {FormatNumber(_projection.DisplayedRowCount)} of " +
                  $"{FormatNumber(_projection.TotalDataRecordCount)} rows"
                : $"{FormatNumber(_projection.DisplayedRowCount)} rows";
        var delimiterSource = _delimiterWasAutomatic
            ? $"automatic {_detectionResult?.Confidence.ToString().ToLowerInvariant() ?? "unknown"} confidence"
            : "manual selection";
        var headerDescription = SelectedHeaderMode == CsvHeaderMode.FirstRecord
            ? "first row as header"
            : "no header row";
        var diagnosticDescription = errorCount == 0 && warningCount == 0
            ? "no parser diagnostics"
            : $"{errorCount} errors, {warningCount} warnings";
        var sortDescription = view.IsSorted && view.SortColumnIndex is not null
            ? $" — sorted by {_projection.Columns[view.SortColumnIndex.Value].Name} " +
              view.SortDirection.ToString().ToLowerInvariant()
            : string.Empty;
        var editDescription = _editMode
            ? $" — EDIT MODE: {FormatNumber(_editSession?.ChangedCellCount ?? 0)} changed cells in " +
              $"{FormatNumber(_editSession?.ChangedRecordCount ?? 0)} rows"
            : string.Empty;

        _statusLabel.Text =
            $"{_snapshot.DisplayName} — {rowDescription} × " +
            $"{FormatNumber(_projection.ColumnCount)} columns — " +
            $"{_parseResult.Dialect.DelimiterDisplayName}, {delimiterSource} — " +
            $"{headerDescription} — {diagnosticDescription}{sortDescription}{editDescription}.";
    }

    private void PopulateDiagnostics(IEnumerable<CsvDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        var copiedDiagnostics = diagnostics.ToArray();
        _diagnosticsGrid.Rows.Clear();
        foreach (var diagnostic in copiedDiagnostics)
        {
            _diagnosticsGrid.Rows.Add(
                diagnostic.Severity.ToString(),
                diagnostic.Code,
                diagnostic.RecordIndex is null
                    ? string.Empty
                    : (diagnostic.RecordIndex.Value + 1).ToString(CultureInfo.InvariantCulture),
                diagnostic.CharacterOffset.ToString(CultureInfo.InvariantCulture),
                diagnostic.Message);
        }

        var label = $"Diagnostics ({FormatNumber(copiedDiagnostics.Length)})";
        _diagnosticsPage.Text = label;
        _diagnosticsButton.Text = label;
        _diagnosticsGrid.ClearSelection();
    }

    private void ConfigureDiagnosticsGrid()
    {
        _diagnosticsGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Severity",
                HeaderText = "Severity",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 90,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        _diagnosticsGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Code",
                HeaderText = "Code",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 80,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        _diagnosticsGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Record",
                HeaderText = "Record",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 80,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        _diagnosticsGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Character",
                HeaderText = "Character",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 90,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        _diagnosticsGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Message",
                HeaderText = "Message",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 260,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
    }

    private void PrepareMetadataGrid()
    {
        _grid.Rows.Clear();
        _grid.Columns.Clear();
        _grid.ReadOnly = true;
        _grid.EditMode = DataGridViewEditMode.EditProgrammatically;
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
        _tabControl.SelectedTab = _tablePage;
    }

    private void PrepareTableGrid()
    {
        _grid.Rows.Clear();
        _grid.Columns.Clear();
        _grid.RowHeadersVisible = true;
        _grid.MultiSelect = true;
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grid.ReadOnly = !_editMode;
    }

    private void ResetVisualTableContext()
    {
        _searchTimer.Stop();
        ResetEditState(clearSession: true);
        _snapshot = null;
        _parseResult = null;
        _projection = null;
        _detectionResult = null;
        _lastViewResult = null;
        _delimiterWasAutomatic = false;
        _sortColumnIndex = null;
        _sortDirection = CsvTableSortDirection.None;

        _updatingViewControls = true;
        try
        {
            _searchBox.Clear();
            _searchColumnCombo.Items.Clear();
            _searchColumnCombo.Items.Add("All columns");
            _searchColumnCombo.SelectedIndex = 0;
        }
        finally
        {
            _updatingViewControls = false;
        }

        PopulateDiagnostics(Enumerable.Empty<CsvDiagnostic>());
        UpdateControlAvailability();
    }

    private void ResetEditState(bool clearSession)
    {
        _editMode = false;
        if (clearSession)
        {
            _editSession = null;
        }

        _dirtyLabel.Text = "0 changes";
        _applyButton.Enabled = false;
        _revertAllButton.Enabled = false;
        _editButton.Checked = false;
        _editButton.Text = "Edit";
        _grid.ReadOnly = true;
        _grid.EditMode = DataGridViewEditMode.EditProgrammatically;
    }

    private void AddMetadataRow(string property, string value)
    {
        _grid.Rows.Add(property, value);
    }

    private static IReadOnlyList<CsvDiagnostic> GetAllDiagnostics(
        CsvDialectDetectionResult? detectionResult,
        CsvParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        return (detectionResult?.Diagnostics ?? Array.Empty<CsvDiagnostic>())
            .Concat(parseResult.Diagnostics)
            .ToArray();
    }

    private static void ApplyGridTheme(
        DataGridView grid,
        Color background,
        Color foreground,
        bool enableVisualStyles)
    {
        grid.BackgroundColor = background;
        grid.DefaultCellStyle.BackColor = background;
        grid.DefaultCellStyle.ForeColor = foreground;
        grid.ColumnHeadersDefaultCellStyle.BackColor = background;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = foreground;
        grid.RowHeadersDefaultCellStyle.BackColor = background;
        grid.RowHeadersDefaultCellStyle.ForeColor = foreground;
        grid.EnableHeadersVisualStyles = enableVisualStyles;
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
