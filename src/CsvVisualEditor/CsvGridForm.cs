namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using Npp.DotNet.Plugin;
using Npp.DotNet.Plugin.Winforms;
using Npp.DotNet.Plugin.Winforms.Classes;
using System.Globalization;

internal sealed partial class CsvGridForm : DockingForm
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
    private readonly ToolStripButton _addRowButton;
    private readonly ToolStripButton _deleteRowButton;
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
    private CsvRowEditModel? _rowEditModel;
    private CsvTableViewResult? _lastViewResult;
    private readonly CsvEditingControlPasteHook _editingControlPasteHook = new();
    private IReadOnlyList<CsvTableRow> _virtualReadOnlyRows = Array.Empty<CsvTableRow>();
    private bool _usingVirtualReadOnlyRows;
    private bool _delimiterWasAutomatic;
    private bool _updatingViewControls;
    private bool _suppressGridChanges;
    private bool _editMode;
    private int? _sortColumnIndex;
    private CsvTableSortDirection _sortDirection = CsvTableSortDirection.None;

    public CsvGridForm(int dialogId, string pluginModuleName, Icon formIcon)
        : base(dialogId, pluginModuleName, FormTitle, null, formIcon, InitialDockPosition)
    {
        _refreshButton = CreateTextButton(
            "Refresh",
            "Read and render the current active Notepad++ editor buffer");
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

        _editButton = CreateTextButton(
            "Edit",
            "Enter explicit cell and row editing mode");
        _editButton.Enabled = false;
        _editButton.Click += (_, _) => ToggleEditMode();

        _addRowButton = CreateTextButton(
            "Add Row",
            "Insert a new row after the selected row, or append when no row is selected");
        _addRowButton.Enabled = false;
        _addRowButton.Click += (_, _) => AddRow();

        _deleteRowButton = CreateTextButton(
            "Delete Row",
            "Delete the selected stable data row or rows from the pending edit session");
        _deleteRowButton.Enabled = false;
        _deleteRowButton.Click += (_, _) => DeleteCurrentRow();

        _applyButton = CreateTextButton(
            "Apply",
            "Apply all cell and row edits to the active editor as one undoable action");
        _applyButton.Enabled = false;
        _applyButton.Click += (_, _) => RequestApply();

        _revertAllButton = CreateTextButton(
            "Revert All",
            "Discard every pending cell insertion and deletion change");
        _revertAllButton.Enabled = false;
        _revertAllButton.Click += (_, _) => RevertAllEdits();

        _dirtyLabel = new ToolStripLabel("0 changes")
        {
            ToolTipText = "Pending cell and structural row changes"
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
        _toolStrip.Items.Add(_addRowButton);
        _toolStrip.Items.Add(_deleteRowButton);
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

        _clearSearchButton = CreateTextButton(
            "Clear",
            "Clear the current search filter and view-only sort");
        _clearSearchButton.Enabled = false;
        _clearSearchButton.Click += (_, _) => ClearViewOptions();

        _diagnosticsButton = CreateTextButton(
            "Diagnostics (0)",
            "Show detailed parser and delimiter diagnostics");

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
        _grid.CellValueNeeded += OnGridCellValueNeeded;
        _grid.CellToolTipTextNeeded += OnGridCellToolTipTextNeeded;
        _grid.EditingControlShowing += OnGridEditingControlShowing;
        _grid.SelectionChanged += (_, _) => UpdateControlAvailability();
        if (_grid is CsvDataGridView csvGrid)
        {
            csvGrid.ClipboardCommandHandler = keyData =>
                CsvGridClipboardController.TryHandleGridCommand(csvGrid, this, keyData);
        }

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

    public CsvRowEditModel? RowEditModel => _rowEditModel;

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

    public void ShowLoadingDocument(ActiveDocumentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        ResetVisualTableContext();
        PrepareMetadataGrid();
        AddMetadataRow("Document", snapshot.DisplayName);
        AddMetadataRow("Status", "Parsing and preparing a bounded visual table in the background…");
        AddMetadataRow(
            "Snapshot",
            $"{FormatNumber(snapshot.CharacterCount)} characters / {FormatNumber(snapshot.EditorByteLength)} editor bytes");
        _tabControl.SelectedTab = _tablePage;
        _statusLabel.Text =
            $"{snapshot.DisplayName} — loading CSV data in the background; Notepad++ remains responsive.";
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
        ConfigureLazyEditCapability();
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
                "Nothing to apply: the edit session contains no pending changes.",
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

        CsvGridRowHeaderBehavior.RefreshPresentationLayout(_grid);
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
            _editingControlPasteHook.Dispose();
            _searchTimer.Stop();
            _searchTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private static ToolStripButton CreateTextButton(string text, string toolTipText) =>
        new(text)
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ToolTipText = toolTipText
        };

    private static DataGridView CreateReadOnlyGrid(bool showRowHeaders)
    {
        var grid = new CsvDataGridView
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
            SelectionMode = DataGridViewSelectionMode.CellSelect
        };
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        grid.RowTemplate.Height = 22;
        return grid;
    }

    private void OnGridCellValueNeeded(
        object? sender,
        DataGridViewCellValueEventArgs e)
    {
        if (!_usingVirtualReadOnlyRows ||
            e.RowIndex < 0 ||
            e.RowIndex >= _virtualReadOnlyRows.Count ||
            e.ColumnIndex < 0 ||
            e.ColumnIndex >= _grid.Columns.Count)
        {
            return;
        }

        var row = _virtualReadOnlyRows[e.RowIndex];
        if (e.ColumnIndex < row.Values.Count)
        {
            e.Value = row.Values[e.ColumnIndex];
            return;
        }

        if (CsvGridRowPresentation.IsRowIndicatorColumn(_grid.Columns[e.ColumnIndex]))
        {
            e.Value = (row.SourceRecordIndex + 1).ToString(CultureInfo.InvariantCulture);
        }
    }

    private void OnGridCellToolTipTextNeeded(
        object? sender,
        DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (!_usingVirtualReadOnlyRows ||
            e.RowIndex < 0 ||
            e.RowIndex >= _virtualReadOnlyRows.Count ||
            e.ColumnIndex < 0 ||
            e.ColumnIndex >= _grid.Columns.Count ||
            !CsvGridRowPresentation.IsRowIndicatorColumn(_grid.Columns[e.ColumnIndex]))
        {
            return;
        }

        var logicalRecordNumber = _virtualReadOnlyRows[e.RowIndex].SourceRecordIndex + 1;
        e.ToolTipText =
            $"Source logical record {logicalRecordNumber.ToString(CultureInfo.CurrentCulture)}";
    }

    private void OnGridEditingControlShowing(
        object? sender,
        DataGridViewEditingControlShowingEventArgs e)
    {
        _editingControlPasteHook.Attach(e.Control, TryHandleEditingControlPasteMessage);
    }

    private bool TryHandleEditingControlPasteMessage()
    {
        if (!CsvGridClipboardController.TryHandleEditingControlPaste(
                _grid,
                this,
                out var clipboardText))
        {
            return false;
        }

        if (clipboardText.Length == 0)
        {
            return true;
        }

        try
        {
            BeginInvoke((Action)(() =>
            {
                if (!IsDisposed && !Disposing)
                {
                    CsvGridClipboardController.TryPasteText(
                        _grid,
                        this,
                        clipboardText);
                }
            }));
        }
        catch (InvalidOperationException)
        {
            // The dock may be closing while WM_PASTE is unwinding.
        }

        return true;
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
        if (_projection is null ||
            e.ColumnIndex < 0 ||
            e.ColumnIndex >= _grid.Columns.Count ||
            _editMode ||
            CsvGridRowHeaderBehavior.IsPresentationColumn(_grid.Columns[e.ColumnIndex]))
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
            _rowEditModel is null ||
            e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            e.RowIndex >= _grid.Rows.Count ||
            e.ColumnIndex >= _grid.Columns.Count ||
            CsvGridRowHeaderBehavior.IsPresentationColumn(_grid.Columns[e.ColumnIndex]) ||
            _grid.Rows[e.RowIndex].Tag is not CsvEditRowId rowId)
        {
            return;
        }

        var value = Convert.ToString(
                _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value,
                CultureInfo.InvariantCulture) ?? string.Empty;
        _rowEditModel.SetCellValue(rowId, e.ColumnIndex, value);
        UpdateDirtyIndicators();
    }

    private void ToggleEditMode()
    {
        if (_editMode)
        {
            if (_rowEditModel?.IsDirty == true)
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
        if (_projection is null || !EnsureEditSession())
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

    private void AddRow()
    {
        if (!_editMode || _rowEditModel is null)
        {
            return;
        }

        if (!CommitPendingEdit())
        {
            _statusLabel.Text =
                "The active cell edit could not be committed. Correct the value before adding a row.";
            return;
        }

        CsvEditRowId insertedId;
        if (_grid.CurrentRow?.Tag is CsvEditRowId anchorId)
        {
            insertedId = _rowEditModel.InsertRowAfter(anchorId);
        }
        else
        {
            insertedId = _rowEditModel.AppendRow();
        }

        ApplyCurrentView();
        SelectRow(insertedId, beginEdit: true);
        _statusLabel.Text =
            "A new row was added to the pending edit session. Apply writes it to the editor buffer; Revert All discards it.";
    }

    private void DeleteCurrentRow()
    {
        if (!_editMode || _rowEditModel is null)
        {
            return;
        }

        if (!CommitPendingEdit())
        {
            _statusLabel.Text =
                "The active cell edit could not be committed. Correct the value before deleting rows.";
            return;
        }

        var targets = CsvGridSelectionSnapshot.Capture(_grid);
        if (targets.Count == 0)
        {
            return;
        }

        var preferredDisplayIndex = targets.Min(static target => target.DisplayIndex);
        CsvBatchDeleteResult result;
        try
        {
            result = _rowEditModel.DeleteRows(
                targets.Select(static target => target.Id));
        }
        catch (ArgumentOutOfRangeException)
        {
            _statusLabel.Text =
                "The selected row set is no longer valid for this edit session. No rows were deleted.";
            return;
        }

        if (!result.HasChanges)
        {
            UpdateDirtyIndicators();
            _statusLabel.Text = "The selected rows were already deleted. No additional change was made.";
            return;
        }

        ApplyCurrentView();
        SelectRowByDisplayIndex(preferredDisplayIndex);
        _statusLabel.Text = FormatBatchDeleteStatus(result);
    }

    private void RequestApply()
    {
        if (!_editMode || _rowEditModel is null)
        {
            return;
        }

        if (!CommitPendingEdit())
        {
            _statusLabel.Text =
                "The active cell edit could not be committed. Correct the value before applying.";
            return;
        }

        if (!_rowEditModel.IsDirty)
        {
            ShowApplyConflict(CsvEditorApplyStatus.NoChanges);
            UpdateDirtyIndicators();
            return;
        }

        ApplyRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RevertAllEdits()
    {
        if (!_editMode || _rowEditModel is null)
        {
            return;
        }

        _grid.CancelEdit();
        _rowEditModel.RevertAll();
        ApplyCurrentView();
        _statusLabel.Text =
            "All pending cell and row edits were reverted. The Notepad++ editor buffer was not changed.";
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

        CsvGridRowHeaderBehavior.SynchronizeTablePresentation(_grid);
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

    private void ConfigureLazyEditCapability()
    {
        _editSession = null;
        _rowEditModel = null;
        _editButton.ToolTipText = CanStartEditMode()
            ? "Enter explicit cell and row editing mode. The full edit model is created only when requested."
            : GetEditUnavailableReason();
    }

    private bool CanStartEditMode()
    {
        return _snapshot is not null &&
               _parseResult is not null &&
               _projection is not null &&
               _projection.ColumnCount > 0 &&
               !_parseResult.HasErrors &&
               !_projection.IsRowLimited &&
               _projection.DisplayedRowCount == _projection.TotalDataRecordCount;
    }

    private string GetEditUnavailableReason()
    {
        if (_parseResult?.HasErrors == true)
        {
            return "Editing cannot start while the parsed CSV contains errors.";
        }

        if (_projection?.IsRowLimited == true ||
            (_projection is not null &&
             _projection.DisplayedRowCount != _projection.TotalDataRecordCount))
        {
            return "Editing cannot start from a row-limited visual projection.";
        }

        return "Editing is unavailable for the current table.";
    }

    private bool EnsureEditSession()
    {
        if (_editSession is not null && _rowEditModel is not null)
        {
            return true;
        }

        if (!CanStartEditMode() ||
            _snapshot is null ||
            _parseResult is null ||
            _projection is null)
        {
            _editButton.ToolTipText = GetEditUnavailableReason();
            return false;
        }

        _statusLabel.Text = "Preparing the complete edit session…";
        try
        {
            _editSession = CsvEditSession.Create(
                _snapshot,
                _parseResult,
                _projection);
            _rowEditModel = CsvRowEditModel.Create(
                _snapshot,
                _parseResult,
                _editSession,
                _projection);
            _editButton.ToolTipText =
                "Enter explicit cell and row editing mode. Apply writes only to the editor buffer.";
            return true;
        }
        catch (InvalidOperationException exception)
        {
            _editSession = null;
            _rowEditModel = null;
            _editButton.ToolTipText = exception.Message;
            _statusLabel.Text = exception.Message;
            return false;
        }
    }

    private void ApplyCurrentView()
    {
        if (_projection is null || _snapshot is null || _parseResult is null)
        {
            return;
        }

        if (_editMode && _rowEditModel is not null)
        {
            RenderStructuralRows(_rowEditModel.GetVisibleRows());
            UpdateDirtyIndicators();
            return;
        }

        int? searchColumnIndex = _searchColumnCombo.SelectedIndex > 0
            ? _searchColumnCombo.SelectedIndex - 1
            : null;
        var view = CsvTableViewBuilder.Build(
            _projection,
            new CsvTableViewOptions
            {
                SearchText = _searchBox.Text,
                SearchColumnIndex = searchColumnIndex,
                SortColumnIndex = _sortColumnIndex,
                SortDirection = _sortDirection
            });
        _lastViewResult = view;
        RenderViewRows(view.Rows);
        UpdateSortGlyphs();
        UpdateDirtyIndicators();
    }

    private void RenderViewRows(IReadOnlyList<CsvTableRow> rows)
    {
        var useVirtualRows = CsvGridRenderingPolicy.ShouldUseVirtualReadOnlyRows(
            rows.Count,
            _projection?.ColumnCount ?? 0);

        _suppressGridChanges = true;
        _grid.SuspendLayout();
        try
        {
            ResetRenderedRows(useVirtualRows);
            CsvGridRowPresentation.ResetSizingLabel(
                _grid,
                GetMaximumReadOnlyIndicatorLabel(rows));

            if (useVirtualRows)
            {
                _virtualReadOnlyRows = rows;
                _usingVirtualReadOnlyRows = true;
                _grid.RowCount = rows.Count;
            }
            else
            {
                var indicatorColumnIndex = GetRowIndicatorColumnIndex();
                var gridRows = new DataGridViewRow[rows.Count];
                for (var index = 0; index < rows.Count; index++)
                {
                    var sourceRow = rows[index];
                    var gridRow = new DataGridViewRow();
                    gridRow.CreateCells(_grid);
                    for (var columnIndex = 0; columnIndex < sourceRow.Values.Count; columnIndex++)
                    {
                        gridRow.Cells[columnIndex].Value = sourceRow.Values[columnIndex];
                    }

                    gridRow.Tag = sourceRow.SourceRecordIndex;
                    var logicalRecordNumber = sourceRow.SourceRecordIndex + 1;
                    CsvGridRowPresentation.SetDetachedRowIndicator(
                        gridRow,
                        indicatorColumnIndex,
                        logicalRecordNumber.ToString(CultureInfo.InvariantCulture),
                        $"Source logical record {logicalRecordNumber.ToString(CultureInfo.CurrentCulture)}");
                    gridRows[index] = gridRow;
                }

                if (gridRows.Length > 0)
                {
                    _grid.Rows.AddRange(gridRows);
                }
            }
        }
        finally
        {
            _grid.ResumeLayout(performLayout: true);
            _suppressGridChanges = false;
        }

        CsvGridRowHeaderBehavior.RefreshPresentationLayout(_grid);
    }

    private void RenderStructuralRows(IEnumerable<CsvEditRowSnapshot> rows)
    {
        var snapshots = rows as IReadOnlyList<CsvEditRowSnapshot> ?? rows.ToArray();
        _suppressGridChanges = true;
        _grid.SuspendLayout();
        try
        {
            ResetRenderedRows(useVirtualRows: false);
            var indicatorColumnIndex = GetRowIndicatorColumnIndex();
            CsvGridRowPresentation.ResetSizingLabel(
                _grid,
                GetMaximumStructuralIndicatorLabel(snapshots));

            var gridRows = new DataGridViewRow[snapshots.Count];
            for (var index = 0; index < snapshots.Count; index++)
            {
                var snapshot = snapshots[index];
                var gridRow = new DataGridViewRow();
                gridRow.CreateCells(_grid);
                for (var columnIndex = 0; columnIndex < snapshot.Values.Count; columnIndex++)
                {
                    gridRow.Cells[columnIndex].Value = snapshot.Values[columnIndex];
                }

                gridRow.Tag = snapshot.Id;
                var indicator = GetStructuralRowIndicator(snapshot);
                CsvGridRowPresentation.SetDetachedRowIndicator(
                    gridRow,
                    indicatorColumnIndex,
                    indicator.Label,
                    indicator.ToolTipText);
                gridRows[index] = gridRow;
            }

            if (gridRows.Length > 0)
            {
                _grid.Rows.AddRange(gridRows);
            }

            UpdateSortGlyphs();
        }
        finally
        {
            _grid.ResumeLayout(performLayout: true);
            _suppressGridChanges = false;
        }

        CsvGridRowHeaderBehavior.RefreshPresentationLayout(_grid);
    }

    private void SetStructuralRowIndicator(
        DataGridViewRow gridRow,
        CsvEditRowSnapshot row)
    {
        var indicator = GetStructuralRowIndicator(row);
        CsvGridRowPresentation.SetRowIndicator(
            gridRow,
            indicator.Label,
            indicator.ToolTipText);
    }

    private (string Label, string ToolTipText) GetStructuralRowIndicator(
        CsvEditRowSnapshot row)
    {
        if (row.IsInserted)
        {
            var insertedNumber = Math.Abs(row.Id.Value);
            return (
                $"new:{insertedNumber.ToString(CultureInfo.InvariantCulture)} *",
                $"Pending inserted row {insertedNumber.ToString(CultureInfo.CurrentCulture)}; not yet applied");
        }

        var sourceRecordIndex = row.SourceRecordIndex ??
            throw new InvalidOperationException("A source row did not expose its source record index.");
        var logicalRecordNumber = sourceRecordIndex + 1;
        var isDirty = IsSourceRecordDirty(sourceRecordIndex);
        return (
            logicalRecordNumber.ToString(CultureInfo.InvariantCulture) +
                (isDirty ? " *" : string.Empty),
            isDirty
                ? $"Source logical record {logicalRecordNumber.ToString(CultureInfo.CurrentCulture)}; modified in the pending edit session"
                : $"Source logical record {logicalRecordNumber.ToString(CultureInfo.CurrentCulture)}");
    }

    private void ResetRenderedRows(bool useVirtualRows)
    {
        if (_grid.VirtualMode)
        {
            _grid.RowCount = 0;
        }
        else
        {
            _grid.Rows.Clear();
        }

        _virtualReadOnlyRows = Array.Empty<CsvTableRow>();
        _usingVirtualReadOnlyRows = false;
        _grid.VirtualMode = useVirtualRows;
    }

    private int GetRowIndicatorColumnIndex()
    {
        var column = _grid.Columns[CsvGridRowPresentation.RowIndicatorColumnName] ??
            throw new InvalidOperationException("The row-indicator column is not configured.");
        return column.Index;
    }

    private static string GetMaximumReadOnlyIndicatorLabel(IReadOnlyList<CsvTableRow> rows)
    {
        if (rows.Count == 0)
        {
            return "#";
        }

        var maximumSourceRecordIndex = rows.Max(static row => row.SourceRecordIndex);
        return (maximumSourceRecordIndex + 1).ToString(CultureInfo.InvariantCulture);
    }

    private string GetMaximumStructuralIndicatorLabel(IReadOnlyList<CsvEditRowSnapshot> rows)
    {
        var maximum = "#";
        foreach (var row in rows)
        {
            var label = GetStructuralRowIndicator(row).Label;
            if (label.Length > maximum.Length)
            {
                maximum = label;
            }
        }

        return maximum;
    }

    private bool IsSourceRecordDirty(int sourceRecordIndex)
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

    private void SelectRow(CsvEditRowId id, bool beginEdit)
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is CsvEditRowId candidate && candidate == id)
            {
                if (_grid.Columns.Count > 0)
                {
                    _grid.CurrentCell = row.Cells[0];
                    if (beginEdit)
                    {
                        _grid.BeginEdit(selectAll: true);
                    }
                }

                return;
            }
        }
    }

    private void SelectRowByDisplayIndex(int preferredIndex)
    {
        if (_grid.Rows.Count == 0 || _grid.Columns.Count == 0)
        {
            return;
        }

        var selectedIndex = Math.Clamp(preferredIndex, 0, _grid.Rows.Count - 1);
        _grid.CurrentCell = _grid.Rows[selectedIndex].Cells[0];
    }

    private void UpdateDirtyIndicators()
    {
        var changedCells = _rowEditModel?.ChangedCellCount ?? 0;
        var changedRows = _rowEditModel?.ChangedRowCount ?? 0;
        var insertedRows = _rowEditModel?.InsertedRowCount ?? 0;
        var deletedRows = _rowEditModel?.DeletedRowCount ?? 0;
        var isDirty = _rowEditModel?.IsDirty ?? false;

        _dirtyLabel.Text = !isDirty
            ? "0 changes"
            : $"{FormatNumber(changedCells)} cells / {FormatNumber(changedRows)} rows " +
              $"(+{FormatNumber(insertedRows)} / -{FormatNumber(deletedRows)})";

        if (_editMode && _rowEditModel is not null)
        {
            var snapshots = _rowEditModel.GetVisibleRows().ToDictionary(static row => row.Id);
            foreach (DataGridViewRow gridRow in _grid.Rows)
            {
                if (gridRow.Tag is CsvEditRowId rowId && snapshots.TryGetValue(rowId, out var snapshot))
                {
                    SetStructuralRowIndicator(gridRow, snapshot);
                }
            }

            CsvGridRowHeaderBehavior.RefreshPresentationLayout(_grid);
        }

        UpdateControlAvailability();
        UpdateStatus();
    }

    private void UpdateControlAvailability()
    {
        var hasTable = _projection is not null;
        var canEdit = CanStartEditMode();
        var isDirty = _rowEditModel?.IsDirty ?? false;
        var deletionTargets = _editMode
            ? CsvGridSelectionSnapshot.Capture(_grid)
            : Array.Empty<CsvGridSelectedRow>();
        var deleteTargetCount = deletionTargets.Count;

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
        _addRowButton.Enabled = _editMode && canEdit;
        _deleteRowButton.Enabled = deleteTargetCount > 0;
        _deleteRowButton.Text = deleteTargetCount > 1
            ? $"Delete Rows ({FormatNumber(deleteTargetCount)})"
            : "Delete Row";
        _deleteRowButton.ToolTipText = deleteTargetCount > 1
            ? "Delete every selected stable data row from the pending edit session"
            : "Delete the selected stable data row from the pending edit session";
        _applyButton.Enabled = _editMode && isDirty;
        _revertAllButton.Enabled = _editMode && isDirty;
        _grid.MultiSelect = hasTable;
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

    private void UpdateStatus()
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

        string rowDescription;
        if (_editMode && _rowEditModel is not null)
        {
            rowDescription = $"{FormatNumber(_rowEditModel.VisibleRowCount)} pending rows";
        }
        else if (_lastViewResult?.IsFiltered == true)
        {
            rowDescription =
                $"{FormatNumber(_lastViewResult.VisibleRowCount)} matching of " +
                $"{FormatNumber(_projection.DisplayedRowCount)} displayed rows";
        }
        else if (_projection.IsRowLimited)
        {
            rowDescription =
                $"showing {FormatNumber(_projection.DisplayedRowCount)} of " +
                $"{FormatNumber(_projection.TotalDataRecordCount)} rows";
        }
        else
        {
            rowDescription = $"{FormatNumber(_projection.DisplayedRowCount)} rows";
        }

        var delimiterSource = _delimiterWasAutomatic
            ? $"automatic {_detectionResult?.Confidence.ToString().ToLowerInvariant() ?? "unknown"} confidence"
            : "manual selection";
        var headerDescription = SelectedHeaderMode == CsvHeaderMode.FirstRecord
            ? "first row as header"
            : "no header row";
        var diagnosticDescription = errorCount == 0 && warningCount == 0
            ? "no parser diagnostics"
            : $"{errorCount} errors, {warningCount} warnings";
        var sortDescription = !_editMode &&
                              _lastViewResult?.IsSorted == true &&
                              _lastViewResult.SortColumnIndex is not null
            ? $" — sorted by {_projection.Columns[_lastViewResult.SortColumnIndex.Value].Name} " +
              _lastViewResult.SortDirection.ToString().ToLowerInvariant()
            : string.Empty;
        var editDescription = _editMode && _rowEditModel is not null
            ? $" — EDIT MODE: {FormatNumber(_rowEditModel.ChangedCellCount)} cells, " +
              $"{FormatNumber(_rowEditModel.ChangedRowCount)} rows, " +
              $"+{FormatNumber(_rowEditModel.InsertedRowCount)} / " +
              $"-{FormatNumber(_rowEditModel.DeletedRowCount)}"
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
        AddDiagnosticsColumn(
            "Severity",
            DataGridViewAutoSizeColumnMode.AllCells,
            minimumWidth: 90);
        AddDiagnosticsColumn(
            "Code",
            DataGridViewAutoSizeColumnMode.AllCells,
            minimumWidth: 80);
        AddDiagnosticsColumn(
            "Record",
            DataGridViewAutoSizeColumnMode.AllCells,
            minimumWidth: 80);
        AddDiagnosticsColumn(
            "Character",
            DataGridViewAutoSizeColumnMode.AllCells,
            minimumWidth: 90);
        AddDiagnosticsColumn(
            "Message",
            DataGridViewAutoSizeColumnMode.Fill,
            minimumWidth: 260);
    }

    private void AddDiagnosticsColumn(
        string name,
        DataGridViewAutoSizeColumnMode autoSizeMode,
        int minimumWidth)
    {
        _diagnosticsGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = name,
                AutoSizeMode = autoSizeMode,
                MinimumWidth = minimumWidth,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
    }

    private void PrepareMetadataGrid()
    {
        ResetRenderedRows(useVirtualRows: false);
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
        ResetRenderedRows(useVirtualRows: false);
        _grid.Columns.Clear();
        _grid.RowHeadersVisible = true;
        _grid.MultiSelect = true;
        _grid.SelectionMode = DataGridViewSelectionMode.RowHeaderSelect;
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
            _rowEditModel = null;
        }

        _dirtyLabel.Text = "0 changes";
        _addRowButton.Enabled = false;
        _deleteRowButton.Enabled = false;
        _deleteRowButton.Text = "Delete Row";
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

    private static string FormatBatchDeleteStatus(CsvBatchDeleteResult result)
    {
        if (result.DeletedSourceRowCount > 0 &&
            result.CancelledInsertedRowCount > 0)
        {
            return $"{FormatNumber(result.DeletedSourceRowCount)} source rows are marked for deletion and " +
                   $"{FormatNumber(result.CancelledInsertedRowCount)} inserted rows were removed from the pending session.";
        }

        if (result.DeletedSourceRowCount > 0)
        {
            return result.DeletedSourceRowCount == 1
                ? "The source row is marked for deletion. Apply writes the deletion; Revert All restores it."
                : $"{FormatNumber(result.DeletedSourceRowCount)} source rows are marked for deletion. " +
                  "Apply writes the batch; Revert All restores every row.";
        }

        return result.CancelledInsertedRowCount == 1
            ? "The newly inserted row was removed from the pending session."
            : $"{FormatNumber(result.CancelledInsertedRowCount)} newly inserted rows were removed from the pending session.";
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
