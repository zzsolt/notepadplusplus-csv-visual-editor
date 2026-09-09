namespace CsvVisualEditor;

using CsvVisualEditor.Localization;

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
    private readonly TextBox _searchBox;
    private readonly ComboBox _searchColumnCombo;
    private readonly ToolStripButton _clearSearchButton;
    private readonly CsvSearchBar _searchBar = new();
    private CsvCellSearchIndex? _searchResults;
    private readonly Label _noMatchesLabel = new()
    {
        Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Visible = false,
        Text = L10n.Get(TextKey.Table_NoMatchingRowsTryAnotherSearchTextOr), UseMnemonic = false
    };
    private readonly Label _diagnosticsEmpty = new()
    {
        Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
        Text = L10n.Get(TextKey.Table_NoDiagnosticsToDisplay), UseMnemonic = false
    };
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
            L10n.Get(TextKey.Common_Refresh),
            L10n.Get(TextKey.Table_ReadAndRenderTheCurrentActiveNotepadEditor));
        _refreshButton.Name = "CsvRefreshButton";
        _refreshButton.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);

        _delimiterCombo = new ToolStripComboBox
        {
            AutoSize = false,
            DropDownStyle = ComboBoxStyle.DropDownList,
            ToolTipText = L10n.Get(TextKey.Table_UseReliableAutomaticDetectionOrChooseADelimiter),
            Width = 128
        };
        _delimiterCombo.Items.AddRange([
            L10n.Get(TextKey.Table_AutoDetect),
            L10n.Get(TextKey.Table_Comma),
            L10n.Get(TextKey.Table_Semicolon),
            L10n.Get(TextKey.Table_Tab)
        ]);
        _delimiterCombo.SelectedIndex = DelimiterAutoIndex;
        _delimiterCombo.SelectedIndexChanged += OnDisplayOptionChanged;

        _headerCombo = new ToolStripComboBox
        {
            AutoSize = false,
            DropDownStyle = ComboBoxStyle.DropDownList,
            ToolTipText = L10n.Get(TextKey.Table_ChooseExplicitlyWhetherTheFirstLogicalRecordIs),
            Width = 150
        };
        _headerCombo.Items.AddRange([
            L10n.Get(TextKey.Table_FirstRowIsHeader),
            L10n.Get(TextKey.Table_NoHeaderRow)
        ]);
        _headerCombo.SelectedIndex = HeaderFirstRecordIndex;
        _headerCombo.SelectedIndexChanged += OnDisplayOptionChanged;

        _editButton = CreateTextButton(
            L10n.Get(TextKey.Common_Edit),
            L10n.Get(TextKey.Table_EnterExplicitCellAndRowEditingMode));
        _editButton.Name = "CsvEditButton";
        _editButton.Enabled = false;
        _editButton.Click += (_, _) => ToggleEditMode();

        _addRowButton = CreateTextButton(
            L10n.Get(TextKey.Table_AddRow),
            L10n.Get(TextKey.Table_InsertANewRowAfterTheSelectedRow));
        _addRowButton.Name = "CsvAddRowButton";
        _addRowButton.Enabled = false;
        _addRowButton.Click += (_, _) => AddRow();

        _deleteRowButton = CreateTextButton(
            L10n.Get(TextKey.Table_DeleteRow),
            L10n.Get(TextKey.Table_DeleteTheSelectedStableDataRowOrRows));
        _deleteRowButton.Name = "CsvDeleteRowButton";
        _deleteRowButton.Enabled = false;
        _deleteRowButton.Click += (_, _) => DeleteCurrentRow();

        _applyButton = CreateTextButton(
            L10n.Get(TextKey.Common_Apply),
            L10n.Get(TextKey.Table_ApplyAllCellAndRowEditsToThe));
        _applyButton.Name = "CsvApplyButton";
        _applyButton.Enabled = false;
        _applyButton.Click += (_, _) => RequestApply();

        _revertAllButton = CreateTextButton(
            L10n.Get(TextKey.Table_RevertAll),
            L10n.Get(TextKey.Table_DiscardEveryPendingCellInsertionAndDeletionChange));
        _revertAllButton.Name = "CsvRevertAllButton";
        _revertAllButton.Enabled = false;
        _revertAllButton.Click += (_, _) => RevertAllEdits();

        _dirtyLabel = new ToolStripLabel(L10n.Get(TextKey.Edit_NoChanges))
        {
            Name = "CsvDirtyLabel",
            ToolTipText = L10n.Get(TextKey.Table_PendingCellAndStructuralRowChanges)
        };

        _toolStrip = new ToolStrip
        {
            Name = "CsvCommandStrip",
            Dock = DockStyle.Fill,
            GripStyle = ToolStripGripStyle.Hidden
        };
        _toolStrip.Items.Add(_refreshButton);
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(new ToolStripLabel(L10n.Get(TextKey.Table_Delimiter)) { Name = "CsvDelimiterLabel" });
        _toolStrip.Items.Add(_delimiterCombo);
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(new ToolStripLabel(L10n.Get(TextKey.Table_Header)) { Name = "CsvHeaderLabel" });
        _toolStrip.Items.Add(_headerCombo);
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(_editButton);
        _toolStrip.Items.Add(_addRowButton);
        _toolStrip.Items.Add(_deleteRowButton);
        _toolStrip.Items.Add(_applyButton);
        _toolStrip.Items.Add(_revertAllButton);
        _toolStrip.Items.Add(_dirtyLabel);

        _searchBox = _searchBar.Query;
        _searchBox.Enabled = false;
        _searchBox.TextChanged += OnSearchTextChanged;
        _searchColumnCombo = _searchBar.Column;
        _searchColumnCombo.Enabled = false;
        _searchBar.NavigateRequested += NavigateSearch;
        _searchBar.ClearRequested += ClearSearch;

        _searchColumnCombo.Items.Add(L10n.Get(TextKey.Search_AllColumns));
        _searchColumnCombo.SelectedIndex = 0;
        _searchColumnCombo.SelectedIndexChanged += OnSearchColumnChanged;

        _clearSearchButton = CreateTextButton(
            L10n.Get(TextKey.Table_Clear),
            L10n.Get(TextKey.Table_ClearTheCurrentSearchFilterAndViewOnly));
        _clearSearchButton.Name = "CsvResetViewButton";
        _clearSearchButton.Enabled = false;
        _clearSearchButton.Click += (_, _) => ClearViewOptions();

        _diagnosticsButton = CreateTextButton(
            L10n.Get(TextKey.Diagnostics_InitialCount),
            L10n.Get(TextKey.Table_ShowDetailedParserAndDelimiterDiagnostics));
        _diagnosticsButton.Name = "CsvDiagnosticsButton";

        _viewToolStrip = new ToolStrip
        {
            Name = "CsvInterpretationStrip",
            Dock = DockStyle.Fill,
            GripStyle = ToolStripGripStyle.Hidden
        };
        _viewToolStrip.Items.Add(_clearSearchButton);
        _viewToolStrip.Items.Add(new ToolStripSeparator());
        _viewToolStrip.Items.Add(_diagnosticsButton);
        InstallDataTools();

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
        _topPanel.RowCount = 3;
        _topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _topPanel.Controls.Add(_searchBar, 0, 2);

        _grid = CreateReadOnlyGrid(showRowHeaders: true);
        _searchBar.ReturnToGridRequested += () => _grid.Focus();
        _grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
        _grid.ColumnHeaderMouseClick += OnTableColumnHeaderMouseClick;
        _grid.CellValueChanged += OnGridCellValueChanged;
        _grid.CellValueNeeded += OnGridCellValueNeeded;
        _grid.CellToolTipTextNeeded += OnGridCellToolTipTextNeeded;
        _grid.EditingControlShowing += OnGridEditingControlShowing;
        _grid.SelectionChanged += (_, _) => UpdateControlAvailability();
        _grid.CurrentCellChanged += (_, _) => UpdateSearchSummary();
        if (_grid is CsvDataGridView csvGrid)
        {
            csvGrid.SearchCommandHandler = TryHandleSearchKey;
            csvGrid.ClipboardCommandHandler = keyData =>
                CsvGridClipboardController.TryHandleGridCommand(csvGrid, this, keyData);
        }

        _diagnosticsGrid = CreateReadOnlyGrid(showRowHeaders: false);
        _diagnosticsGrid.MultiSelect = false;
        _diagnosticsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        ConfigureDiagnosticsGrid();

        _tablePage = new TabPage(L10n.Get(TextKey.Common_Table))
        {
            Padding = Padding.Empty
        };
        _tablePage.Controls.Add(_grid);
        _tablePage.Controls.Add(_noMatchesLabel);

        _diagnosticsPage = new TabPage(L10n.Get(TextKey.Diagnostics_InitialCount))
        {
            Padding = Padding.Empty
        };
        _diagnosticsPage.Controls.Add(_diagnosticsGrid);
        _diagnosticsPage.Controls.Add(_diagnosticsEmpty);

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
            TextAlign = ContentAlignment.MiddleLeft,
            AutoToolTip = true
        };
        _statusStrip = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            SizingGrip = false,
            ShowItemToolTips = true
        };
        _statusStrip.Items.Add(_statusLabel);
        _statusLabel.TextChanged += (_, _) => _statusLabel.ToolTipText = _statusLabel.Text;

        _searchTimer = new System.Windows.Forms.Timer
        {
            Interval = SearchDelayMilliseconds
        };
        _searchTimer.Tick += OnSearchTimerTick;

        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(740, 560);
        Controls.Add(_tabControl);
        Controls.Add(_statusStrip);
        Controls.Add(_topPanel);
        // The dock must be allowed to shrink; a 620px form minimum defeated
        // the search bar's narrow layout and displaced the source editor.
        MinimumSize = new Size(260, 240);
        Text = FormTitle;
        ResumeLayout(performLayout: true);

        AttachEventHandlers();
        ShowBootstrapState();
        CsvLocalizationAppearance.Apply(this);
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
        AddMetadataRow(L10n.Get(TextKey.Table_Status), L10n.Get(TextKey.Table_WaitingForTheActiveNotepadDocument));
        _statusLabel.Text = L10n.Get(TextKey.Table_PluginReadyReadingTheActiveEditorBuffer);
    }

    public void ShowLoadingDocument(ActiveDocumentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        ResetVisualTableContext();
        PrepareMetadataGrid();
        AddMetadataRow(L10n.Get(TextKey.Table_Document), snapshot.DisplayName);
        AddMetadataRow(L10n.Get(TextKey.Table_Status), L10n.Get(TextKey.Table_ParsingAndPreparingABoundedVisualTableIn));
        AddMetadataRow(
            L10n.Get(TextKey.Table_Snapshot),
            L10n.Format(TextKey.Table_CharactersEditorBytes, FormatNumber(snapshot.CharacterCount), FormatNumber(snapshot.EditorByteLength)));
        _tabControl.SelectedTab = _tablePage;
        _statusLabel.Text =
            L10n.Format(TextKey.Table_LoadingCSVDataInTheBackgroundNotepadRemains, snapshot.DisplayName);
    }

    public void ShowEmptyDocument(ActiveDocumentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        ResetVisualTableContext();
        PrepareTableGrid();
        _tabControl.SelectedTab = _tablePage;
        _statusLabel.Text =
            L10n.Format(TextKey.Table_EmptyEditorBufferNoCSVRecordsToDisplay, snapshot.DisplayName);
    }

    public void ShowDelimiterSelectionRequired(
        ActiveDocumentSnapshot snapshot,
        CsvDialectDetectionResult detectionResult)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(detectionResult);

        ResetVisualTableContext();
        PrepareMetadataGrid();
        AddMetadataRow(L10n.Get(TextKey.Table_Document), snapshot.DisplayName);
        AddMetadataRow(L10n.Get(TextKey.Table_AutomaticDetection), L10n.Get(TextKey.Table_NoReliableDelimiterCouldBeSelectedSafely));
        AddMetadataRow(L10n.Get(TextKey.Table_Confidence), CsvUiText.Confidence(detectionResult.Confidence));
        AddMetadataRow(
            L10n.Get(TextKey.Table_CandidateScores),
            string.Join(
                ", ",
                detectionResult.Candidates.Select(
                    static candidate =>
                        $"{DelimiterDisplayName(candidate.Delimiter)}={candidate.Score}")));
        AddMetadataRow(
            L10n.Get(TextKey.Table_Action),
            L10n.Get(TextKey.Table_ChooseCommaSemicolonOrTabFromTheDelimiter));
        PopulateDiagnostics(detectionResult.Diagnostics);
        _tabControl.SelectedTab = _tablePage;
        _statusLabel.Text =
            L10n.Format(TextKey.Table_AutomaticDelimiterDetectionIsNotReliableManualSelection, snapshot.DisplayName);
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
        _dataView = CsvDataViewDefinition.Empty;

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
        AddMetadataRow(L10n.Get(TextKey.Table_SnapshotError), message);
        _statusLabel.Text = L10n.Get(TextKey.Table_ActiveDocumentSnapshotFailedNoEditorContentWas);
    }

    public void ShowTableError(string message)
    {
        ResetVisualTableContext();
        PrepareMetadataGrid();
        AddMetadataRow(L10n.Get(TextKey.Table_TableError), message);
        _statusLabel.Text = L10n.Get(TextKey.Table_TheVisualTableCouldNotBeProducedThe);
    }

    public void ShowApplyConflict(CsvEditorApplyStatus status)
    {
        _statusLabel.Text = status switch
        {
            CsvEditorApplyStatus.DocumentIdentityChanged =>
                L10n.Get(TextKey.Table_ApplyBlockedAnotherNotepadDocumentIsActiveReturn),
            CsvEditorApplyStatus.CodePageChanged =>
                L10n.Get(TextKey.Table_ApplyBlockedTheEditorCodePageChangedAfter),
            CsvEditorApplyStatus.ContentChanged =>
                L10n.Get(TextKey.Table_ApplyBlockedTheEditorBufferChangedAfterEdit),
            CsvEditorApplyStatus.NoChanges =>
                L10n.Get(TextKey.Table_NothingToApplyTheEditSessionContainsNo),
            _ => L10n.Get(TextKey.Table_ApplyWasNotCompletedNoAutomaticOverwriteWas)
        };
    }

    public void ShowApplyError()
    {
        _statusLabel.Text =
            L10n.Get(TextKey.Table_ApplyFailedTheEditSessionRemainsOpenVerify);
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
        RefreshCommandAppearance();
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
            _commandSurface?.Dispose();
            _commandSurface = null;
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
            L10n.Format(TextKey.Table_SourceLogicalRecord, logicalRecordNumber.ToString(CultureInfo.CurrentCulture));
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
        _dataView = CsvDataViewDefinition.Empty;
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSearchTextChanged(object? sender, EventArgs e)
    {
        if (_updatingViewControls || _projection is null || _editMode)
        {
            return;
        }

        _clearSearchButton.Enabled =
            _searchBox.Text.Length > 0 || _sortColumnIndex is not null || _dataView.IsActive || _searchColumnCombo.SelectedIndex > 0;
        _searchBar.SetResults(-1, 0, _searchBox.TextLength > 0, pending: true);
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

        if (_dataView.SortKeys.Count > 0)
            _dataView = new CsvDataViewDefinition(_dataView.Filters, _dataView.Combination);

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
                    L10n.Get(TextKey.Table_EditModeContainsPendingChangesUseApplyOr);
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
                L10n.Get(TextKey.Table_EditModeIsUnavailableForTheCurrentTable);
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
                L10n.Get(TextKey.Table_TheActiveCellEditCouldNotBeCommitted);
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
            L10n.Get(TextKey.Table_ANewRowWasAddedToThePending);
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
                L10n.Get(TextKey.Table_TheActiveCellEditCouldNotBeCommitted2);
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
                L10n.Get(TextKey.Table_TheSelectedRowSetIsNoLongerValid);
            return;
        }

        if (!result.HasChanges)
        {
            UpdateDirtyIndicators();
            _statusLabel.Text = L10n.Get(TextKey.Table_TheSelectedRowsWereAlreadyDeletedNoAdditional);
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
                L10n.Get(TextKey.Table_TheActiveCellEditCouldNotBeCommitted3);
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
            L10n.Get(TextKey.Table_AllPendingCellAndRowEditsWereReverted);
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
        _dataView = CsvDataViewDefinition.Empty;
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
            _searchColumnCombo.Items.Add(L10n.Get(TextKey.Search_AllColumns));
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
            ? L10n.Get(TextKey.Table_EnterExplicitCellAndRowEditingModeThe)
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
            return L10n.Get(TextKey.Table_EditingCannotStartWhileTheParsedCSVContains);
        }

        if (_projection?.IsRowLimited == true ||
            (_projection is not null &&
             _projection.DisplayedRowCount != _projection.TotalDataRecordCount))
        {
            return L10n.Get(TextKey.Table_EditingCannotStartFromARowLimitedVisual);
        }

        return L10n.Get(TextKey.Table_EditingIsUnavailableForTheCurrentTable);
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

        _statusLabel.Text = L10n.Get(TextKey.Table_PreparingTheCompleteEditSession);
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
                L10n.Get(TextKey.Table_EnterExplicitCellAndRowEditingModeApply);
            return true;
        }
        catch (InvalidOperationException exception)
        {
            _editSession = null;
            _rowEditModel = null;
            _editButton.ToolTipText = CsvUiText.Exception(exception);
            _statusLabel.Text = CsvUiText.Exception(exception);
            return false;
        }
    }

    private void ApplyCurrentView()
    {
        _searchTimer.Stop();
        _searchResults = null;
        if (_grid is CsvDataGridView clearGrid) clearGrid.SetSearchResults(null);
        if (_projection is null || _snapshot is null || _parseResult is null)
        {
            return;
        }

        if (_editMode && _rowEditModel is not null)
        {
            RenderStructuralRows(_rowEditModel.GetVisibleRows());
            UpdateSearchSummary();
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
                SortDirection = _sortDirection,
                DataView = _dataView
            });
        _lastViewResult = view;
        RenderViewRows(view.Rows);
        _searchResults = CsvCellSearchIndex.Create(view);
        if (_grid is CsvDataGridView searchGrid) searchGrid.SetSearchResults(_searchResults);
        if (_searchResults.Count > 0) SelectSearchResult(0);
        else if (_grid.RowCount > 0 && (_grid.CurrentCell?.OwningColumn is not { } currentColumn ||
                 CsvGridRowHeaderBehavior.IsPresentationColumn(currentColumn)))
        {
            // DisplayIndex 0 is the # lane, not a CSV data cell. Keep initial
            // keyboard focus/selection on data, without changing row gestures.
            var first = _grid.Columns.Cast<DataGridViewColumn>()
                .FirstOrDefault(column => !CsvGridRowHeaderBehavior.IsPresentationColumn(column));
            if (first is not null)
            {
                _grid.ClearSelection();
                var firstCell = _grid.Rows[0].Cells[first.Index];
                _grid.CurrentCell = firstCell;
                firstCell.Selected = true;
            }
        }
        UpdateSearchSummary();
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
                        L10n.Format(TextKey.Table_SourceLogicalRecord, logicalRecordNumber.ToString(CultureInfo.CurrentCulture)));
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
                L10n.Format(TextKey.Rows_NewPendingIdentifier, insertedNumber.ToString(CultureInfo.InvariantCulture)),
                L10n.Format(TextKey.Table_PendingInsertedRowNotYetApplied, insertedNumber.ToString(CultureInfo.CurrentCulture)));
        }

        var sourceRecordIndex = row.SourceRecordIndex ??
            throw new InvalidOperationException(L10n.Get(TextKey.Table_ASourceRowDidNotExposeItsSource));
        var logicalRecordNumber = sourceRecordIndex + 1;
        var isDirty = IsSourceRecordDirty(sourceRecordIndex);
        return (
            logicalRecordNumber.ToString(CultureInfo.InvariantCulture) +
                (isDirty ? " *" : string.Empty),
            isDirty
                ? L10n.Format(TextKey.Table_SourceLogicalRecordModifiedInThePendingEdit, logicalRecordNumber.ToString(CultureInfo.CurrentCulture))
                : L10n.Format(TextKey.Table_SourceLogicalRecord, logicalRecordNumber.ToString(CultureInfo.CurrentCulture)));
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
            throw new InvalidOperationException(L10n.Get(TextKey.Table_TheRowIndicatorColumnIsNotConfigured));
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
            ? L10n.Get(TextKey.Edit_NoChanges)
            : L10n.Format(TextKey.Table_CellsRows, FormatNumber(changedCells), FormatNumber(changedRows), FormatNumber(insertedRows), FormatNumber(deletedRows));

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
        UpdateDataToolAvailability();
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
                                      _sortColumnIndex is not null ||
                                      _dataView.IsActive ||
                                      _searchColumnCombo.SelectedIndex > 0);
        _editButton.Enabled = canEdit;
        _editButton.Text = _editMode ? L10n.Get(TextKey.Common_ExitEdit) : L10n.Get(TextKey.Common_Edit);
        _editButton.Checked = _editMode;
        _addRowButton.Enabled = _editMode && canEdit;
        _deleteRowButton.Enabled = deleteTargetCount > 0;
        _deleteRowButton.Text = deleteTargetCount > 1
            ? L10n.Format(TextKey.Table_DeleteRows, FormatNumber(deleteTargetCount))
            : L10n.Get(TextKey.Table_DeleteRow);
        _deleteRowButton.ToolTipText = deleteTargetCount > 1
            ? L10n.Get(TextKey.Table_DeleteEverySelectedStableDataRowFromThe)
            : L10n.Get(TextKey.Table_DeleteTheSelectedStableDataRowFromThe);
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
            if (!CsvGridRowHeaderBehavior.IsPresentationColumn(column)) column.HeaderCell.ToolTipText = string.Empty;
        }
        if (_editMode) return;
        var keys = _dataView.SortKeys.Count > 0 ? _dataView.SortKeys.ToArray() :
            _sortColumnIndex.HasValue && _sortDirection != CsvTableSortDirection.None
                ? new[] { new CsvSortKey(_sortColumnIndex.Value, _sortDirection) } : Array.Empty<CsvSortKey>();
        for (var i = 0; i < keys.Length; i++)
        {
            var key = keys[i];
            _grid.Columns[key.ColumnIndex].HeaderCell.SortGlyphDirection =
                key.Direction == CsvTableSortDirection.Ascending ? SortOrder.Ascending : SortOrder.Descending;
            _grid.Columns[key.ColumnIndex].HeaderCell.ToolTipText = L10n.Format(TextKey.Table_SortLevelViewOnly, i + 1, CsvUiText.SortKind(key.Kind), CsvUiText.SortDirection(key.Direction));
        }
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
            rowDescription = L10n.Format(TextKey.Table_PendingRows, FormatNumber(_rowEditModel.VisibleRowCount));
        }
        else if (_lastViewResult?.IsFiltered == true)
        {
            rowDescription =
                L10n.Format(TextKey.Table_MatchingOfDisplayedRows, FormatNumber(_lastViewResult.VisibleRowCount), FormatNumber(_projection.DisplayedRowCount));
        }
        else if (_projection.IsRowLimited)
        {
            rowDescription =
                L10n.Format(TextKey.Table_ShowingOfRows, FormatNumber(_projection.DisplayedRowCount), FormatNumber(_projection.TotalDataRecordCount));
        }
        else
        {
            rowDescription = L10n.Format(TextKey.Table_Rows, FormatNumber(_projection.DisplayedRowCount));
        }

        var delimiterSource = _delimiterWasAutomatic
            ? L10n.Format(TextKey.Table_AutomaticConfidence, CsvUiText.Confidence(_detectionResult?.Confidence))
            : L10n.Get(TextKey.Table_ManualSelection);
        var headerDescription = SelectedHeaderMode == CsvHeaderMode.FirstRecord
            ? L10n.Get(TextKey.Table_FirstRowAsHeader)
            : L10n.Get(TextKey.Table_NoHeaderRow2);
        var diagnosticDescription = errorCount == 0 && warningCount == 0
            ? L10n.Get(TextKey.Table_NoParserDiagnostics)
            : L10n.Format(TextKey.Table_ErrorsWarnings, errorCount, warningCount);
        var sortDescription = !_editMode &&
                              _lastViewResult?.IsSorted == true &&
                              _lastViewResult.SortColumnIndex is not null
            ? L10n.Format(TextKey.Table_SortedBy, _projection.Columns[_lastViewResult.SortColumnIndex.Value].Name, CsvUiText.SortDirection(_lastViewResult.SortDirection))
            : string.Empty;
        var editDescription = _editMode && _rowEditModel is not null
            ? L10n.Format(TextKey.Table_EDITMODECellsRows, FormatNumber(_rowEditModel.ChangedCellCount), FormatNumber(_rowEditModel.ChangedRowCount), FormatNumber(_rowEditModel.InsertedRowCount), FormatNumber(_rowEditModel.DeletedRowCount))
            : string.Empty;

        var dataViewDescription = !_editMode && _dataView.IsActive
            ? L10n.Format(TextKey.Table_ColumnConditionsSortLevels, _dataView.Filters.Count, CsvUiText.Combination(_dataView.Combination), _dataView.SortKeys.Count) : string.Empty;
        var detailedStatus =
            L10n.Format(TextKey.Table_Columns, _snapshot.DisplayName, rowDescription, FormatNumber(_projection.ColumnCount), CsvUiText.Delimiter(_parseResult.Dialect.Delimiter), delimiterSource, headerDescription, diagnosticDescription, sortDescription, dataViewDescription, editDescription);
        _statusLabel.Text = L10n.Format(TextKey.Table_XColumns, _snapshot.DisplayName, rowDescription, _projection.ColumnCount) +
            (_editMode ? L10n.Get(TextKey.Table_EditMode) : $" - {diagnosticDescription}{dataViewDescription}");
        _statusLabel.ToolTipText = detailedStatus;
    }

    private void PopulateDiagnostics(IEnumerable<CsvDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        var copiedDiagnostics = diagnostics.ToArray();
        _diagnosticsGrid.Rows.Clear();
        foreach (var diagnostic in copiedDiagnostics)
        {
            _diagnosticsGrid.Rows.Add(
                CsvUiText.Severity(diagnostic.Severity),
                diagnostic.Code,
                diagnostic.RecordIndex is null
                    ? string.Empty
                    : (diagnostic.RecordIndex.Value + 1).ToString(CultureInfo.InvariantCulture),
                diagnostic.CharacterOffset.ToString(CultureInfo.InvariantCulture),
                CsvUiText.Diagnostic(diagnostic));
        }

        var label = L10n.Format(TextKey.Table_Diagnostics, FormatNumber(copiedDiagnostics.Length));
        _diagnosticsPage.Text = label;
        _diagnosticsButton.Text = label;
        _diagnosticsGrid.ClearSelection();
        _diagnosticsEmpty.Visible = copiedDiagnostics.Length == 0;
        if (_diagnosticsEmpty.Visible) _diagnosticsEmpty.BringToFront();
    }

    private void ConfigureDiagnosticsGrid()
    {
        AddDiagnosticsColumn(
            L10n.Get(TextKey.Table_Severity),
            DataGridViewAutoSizeColumnMode.AllCells,
            minimumWidth: 90);
        AddDiagnosticsColumn(
            L10n.Get(TextKey.Table_Code),
            DataGridViewAutoSizeColumnMode.AllCells,
            minimumWidth: 80);
        AddDiagnosticsColumn(
            L10n.Get(TextKey.Table_Record),
            DataGridViewAutoSizeColumnMode.AllCells,
            minimumWidth: 80);
        AddDiagnosticsColumn(
            L10n.Get(TextKey.Table_Character),
            DataGridViewAutoSizeColumnMode.AllCells,
            minimumWidth: 90);
        AddDiagnosticsColumn(
            L10n.Get(TextKey.Table_Message),
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
                Name = "CsvDiagnostic" + _diagnosticsGrid.Columns.Count.ToString(CultureInfo.InvariantCulture),
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
                HeaderText = L10n.Get(TextKey.Table_Property),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 120,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        _grid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Value",
                HeaderText = L10n.Get(TextKey.Common_Value),
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
        _searchResults = null;
        if (_grid is CsvDataGridView searchGrid) searchGrid.SetSearchResults(null);
        _noMatchesLabel.Visible = false;
        _searchBar.SetResults(-1, 0, false);
        ResetEditState(clearSession: true);
        _snapshot = null;
        _parseResult = null;
        _projection = null;
        _detectionResult = null;
        _lastViewResult = null;
        _delimiterWasAutomatic = false;
        _sortColumnIndex = null;
        _sortDirection = CsvTableSortDirection.None;
        _dataView = CsvDataViewDefinition.Empty;

        _updatingViewControls = true;
        try
        {
            _searchBox.Clear();
            _searchColumnCombo.Items.Clear();
            _searchColumnCombo.Items.Add(L10n.Get(TextKey.Search_AllColumns));
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

        _dirtyLabel.Text = L10n.Get(TextKey.Edit_NoChanges);
        _addRowButton.Enabled = false;
        _deleteRowButton.Enabled = false;
        _deleteRowButton.Text = L10n.Get(TextKey.Table_DeleteRow);
        _applyButton.Enabled = false;
        _revertAllButton.Enabled = false;
        _editButton.Checked = false;
        _editButton.Text = L10n.Get(TextKey.Common_Edit);
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
            return L10n.Format(TextKey.Table_SourceRowsAreMarkedForDeletionAndInserted, FormatNumber(result.DeletedSourceRowCount), FormatNumber(result.CancelledInsertedRowCount));
        }

        if (result.DeletedSourceRowCount > 0)
        {
            return result.DeletedSourceRowCount == 1
                ? L10n.Get(TextKey.Table_TheSourceRowIsMarkedForDeletionApply)
                : L10n.Format(TextKey.Table_SourceRowsAreMarkedForDeletionApplyWrites, FormatNumber(result.DeletedSourceRowCount));
        }

        return result.CancelledInsertedRowCount == 1
            ? L10n.Get(TextKey.Table_TheNewlyInsertedRowWasRemovedFromThe)
            : L10n.Format(TextKey.Table_NewlyInsertedRowsWereRemovedFromThePending, FormatNumber(result.CancelledInsertedRowCount));
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
        return value.ToString("N0", L10n.FormattingCulture);
    }

    private static string DelimiterDisplayName(char delimiter) => delimiter switch
    {
        ',' => L10n.Get(TextKey.Table_Comma2),
        ';' => L10n.Get(TextKey.Table_Semicolon2),
        '\t' => L10n.Get(TextKey.Table_Tab2),
        _ => $"U+{(int)delimiter:X4}"
    };
}
