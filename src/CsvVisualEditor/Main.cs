namespace CsvVisualEditor;

using CsvVisualEditor.Localization;

using CsvVisualEditor.Core;
using Npp.DotNet.Plugin;
using System.Runtime.InteropServices;

partial class Main : IDotNetPlugin
{
    private const string PluginDisplayName = "CSV Visual Editor";
    private const string PluginAssemblyName = "CsvVisualEditor";
    private const int DialogCommandIndex = 0;
    private const int MaximumDisplayedRows = 10_000;
    private const int MaximumDisplayedColumns = 512;
    private const int MaximumDisplayedCells = 250_000;

    private static readonly IDotNetPlugin Instance;
    private readonly IActiveDocumentReader _activeDocumentReader =
        new NotepadActiveDocumentReader();
    private CancellationTokenSource? _loadCancellation;
    private int _loadGeneration;
    private CsvGridForm? _gridForm;

    static Main()
    {
        Instance = new Main();
        PluginData.PluginNamePtr = Marshal.StringToHGlobalUni($"{PluginDisplayName}\0");
    }

    public void OnSetInfo()
    {
        Utils.SetCommand(
            L10n.Get(TextKey.Native_OpenTable),
            ToggleDialog,
            new ShortcutKey(ctrl: false, alt: true, shift: false, Keys.F10));
        Utils.SetCommand(L10n.Get(TextKey.Native_RefreshTable), RefreshTable);
        Utils.SetCommand(L10n.Get(TextKey.Native_FilterSort), () => OpenDataTool(summary: false));
        Utils.SetCommand(L10n.Get(TextKey.Native_ColumnSummary), () => OpenDataTool(summary: true));
        Utils.MakeSeparator();
        Utils.SetCommand(L10n.Get(TextKey.Common_About), ShowAboutDialog);
        Utils.SetCommand(L10n.Get(TextKey.Cell_Title), OpenCellDetails);
    }

    public void OnBeNotified(ScNotification notification)
    {
        if (notification.Header.HwndFrom != PluginData.NppData.NppHandle)
        {
            return;
        }

        switch ((NppMsg)notification.Header.Code)
        {
            case NppMsg.NPPN_READY:
                InitializeInterfaceLanguage();
                break;

            case (NppMsg)1031: // NPPN_NATIVELANGCHANGED; apply the new language at next startup.
                if (_languageReady) LocalizeNativeCommands();
                break;

            case NppMsg.NPPN_TBMODIFICATION:
                PluginData.FuncItems.RefreshItems();
                break;

            case NppMsg.NPPN_DARKMODECHANGED:
                _gridForm?.ToggleDarkMode(PluginData.Notepad.IsDarkModeEnabled());
                break;

            case NppMsg.NPPN_SHUTDOWN:
                PluginCleanUp();
                break;
        }
    }

    public NativeBool OnMessageProc(uint msg, UIntPtr wParam, IntPtr lParam)
    {
        return Win32.TRUE;
    }

    private void ToggleDialog()
    {
        if (_gridForm is null)
        {
            var gridForm = new CsvGridForm(
                DialogCommandIndex,
                $"{PluginAssemblyName}.dll",
                SystemIcons.Application);
            _gridForm = gridForm;
            gridForm.RefreshRequested += OnRefreshRequested;
            gridForm.ApplyRequested += OnApplyRequested;
            CsvGridRowHeaderBehavior.TryAttach(gridForm);
            CsvGridClipboardToolbar.TryAttach(gridForm);
            LoadActiveDocumentTable();
            gridForm.BeginInvoke(
                (Action)(() => NotepadDockWidthAdjuster.TryExpandInitialRightDock(gridForm)));
            return;
        }

        if (_gridForm.Visible)
        {
            _gridForm.HideDockingForm();
        }
        else
        {
            _gridForm.ShowDockingForm();
            if (!_gridForm.IsEditMode)
            {
                LoadActiveDocumentTable();
            }
        }
    }

    private void OpenDataTool(bool summary)
    {
        if (_gridForm is null) { ToggleDialog(); return; }
        if (!_gridForm.Visible) _gridForm.ShowDockingForm();
        if (summary) _gridForm.ShowColumnSummary();
        else _gridForm.ShowDataViewDialog();
    }

    private void OpenCellDetails()
    {
        if (_gridForm is null) { ToggleDialog(); return; }
        if (!_gridForm.Visible) _gridForm.ShowDockingForm();
        _gridForm.ShowCellDetails();
    }

    private void RefreshTable()
    {
        if (_gridForm is null)
        {
            ToggleDialog();
            return;
        }

        if (_gridForm.IsEditMode)
        {
            MessageBox.Show(
                L10n.Get(TextKey.Host_RefreshIsDisabledWhileEditModeIsActive),
                PluginDisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!_gridForm.Visible)
        {
            _gridForm.ShowDockingForm();
        }

        LoadActiveDocumentTable();
    }

    private void OnRefreshRequested(object? sender, EventArgs e)
    {
        if (_gridForm?.IsEditMode != true)
        {
            LoadActiveDocumentTable();
        }
    }

    private void OnApplyRequested(object? sender, EventArgs e)
    {
        if (_gridForm is null ||
            !_gridForm.IsEditMode ||
            _gridForm.EditSession is null ||
            _gridForm.RowEditModel is null)
        {
            return;
        }

        if (!_gridForm.CommitPendingEdit())
        {
            _gridForm.ShowApplyError();
            return;
        }

        ActiveDocumentSnapshot currentSnapshot;
        try
        {
            currentSnapshot = _activeDocumentReader.ReadActiveDocument();
        }
        catch (Exception)
        {
            _gridForm.ShowApplyError();
            return;
        }

        CsvEditorApplyResult result;
        try
        {
            result = CsvHostApplyCoordinator.Execute(
                _gridForm.RowEditModel,
                currentSnapshot,
                static () => new NotepadEditorReplacementTarget());
        }
        catch (Exception)
        {
            _gridForm.ShowApplyError();
            return;
        }

        if (result.WasApplied)
        {
            var selectionRestored = result.SelectionRestored;
            LoadActiveDocumentTable();
            if (!selectionRestored)
            {
                MessageBox.Show(
                    L10n.Get(TextKey.Host_TheCSVChangesWereAppliedSuccessfullyButThe),
                    PluginDisplayName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            return;
        }

        if (IsEncodingApplyBlocked(result.Status))
        {
            ShowEncodingApplyBlocked(result);
            return;
        }

        _gridForm.ShowApplyConflict(result.Status);
    }

    private static bool IsEncodingApplyBlocked(CsvEditorApplyStatus status)
    {
        return status is
            CsvEditorApplyStatus.EncodingWriteNotEnabled or
            CsvEditorApplyStatus.UnsupportedCodePage or
            CsvEditorApplyStatus.TextNotRepresentable or
            CsvEditorApplyStatus.EncodingRoundTripMismatch;
    }

    private static void ShowEncodingApplyBlocked(CsvEditorApplyResult result)
    {
        var codePage = result.EncodingPreflight?.CodePage;
        var codePageText = codePage is null
            ? L10n.Get(TextKey.Host_TheCurrentEditorCodePage)
            : L10n.Format(TextKey.Host_ScintillaCodePage, codePage.Value);
        var message = result.Status switch
        {
            CsvEditorApplyStatus.EncodingWriteNotEnabled =>
                L10n.Format(TextKey.Host_ApplyIsNotYetEnabledForNoEditor, codePageText),
            CsvEditorApplyStatus.UnsupportedCodePage =>
                L10n.Format(TextKey.Host_ApplyIsBlockedBecauseHasNoExplicitSupported, codePageText),
            CsvEditorApplyStatus.TextNotRepresentable =>
                L10n.Format(TextKey.Host_ApplyIsBlockedBecauseThePendingReplacementCannot, codePageText),
            CsvEditorApplyStatus.EncodingRoundTripMismatch =>
                L10n.Format(TextKey.Host_ApplyIsBlockedBecauseStrictEncodingValidationFor, codePageText),
            _ => throw new ArgumentOutOfRangeException(
                nameof(result),
                result.Status,
                L10n.Get(TextKey.Host_TheApplyResultIsNotAnEncodingBlocked))
        };

        MessageBox.Show(
            message,
            PluginDisplayName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private void LoadActiveDocumentTable()
    {
        if (_gridForm is null)
        {
            return;
        }

        // Retire the preceding build BEFORE attempting the fresh snapshot. A failed
        // read must not let an older async build repopulate an error/another document.
        var generation = Interlocked.Increment(ref _loadGeneration);
        var cancellation = new CancellationTokenSource();
        var previousCancellation = Interlocked.Exchange(ref _loadCancellation, cancellation);
        previousCancellation?.Cancel();
        previousCancellation?.Dispose();
        CsvGridSourceNavigationController.ClearBaseline(_gridForm);

        ActiveDocumentSnapshot snapshot;
        try
        {
            snapshot = _activeDocumentReader.ReadActiveDocument();
        }
        catch (InvalidOperationException exception)
        {
            _gridForm.ShowSnapshotError(CsvUiText.Exception(exception));
            return;
        }
        catch (Exception)
        {
            _gridForm.ShowSnapshotError(
                L10n.Get(TextKey.Host_TheActiveNotepadDocumentCouldNotBeRead));
            return;
        }

        var gridForm = _gridForm;
        var options = new CsvTableBuildOptions
        {
            DelimiterOverride = gridForm.SelectedDelimiterOverride,
            HeaderMode = gridForm.SelectedHeaderMode,
            MaximumRows = MaximumDisplayedRows,
            MaximumColumns = MaximumDisplayedColumns,
            MaximumCells = MaximumDisplayedCells
        };
        gridForm.ShowLoadingDocument(snapshot);
        _ = BuildAndDisplayTableAsync(
            gridForm,
            snapshot,
            options,
            generation,
            cancellation.Token);
    }

    private async Task BuildAndDisplayTableAsync(
        CsvGridForm gridForm,
        ActiveDocumentSnapshot snapshot,
        CsvTableBuildOptions options,
        int generation,
        CancellationToken cancellationToken)
    {
        CsvTableBuildResult? buildResult = null;
        string? errorMessage = null;
        try
        {
            buildResult = await Task.Run(
                () => CsvTableBuilder.Build(snapshot.Text, options),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (InvalidOperationException exception)
        {
            errorMessage = CsvUiText.Exception(exception);
        }
        catch (Exception)
        {
            errorMessage =
                L10n.Get(TextKey.Host_TheEditorBufferCouldNotBeConvertedInto);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            gridForm.BeginInvoke((Action)(() =>
            {
                if (cancellationToken.IsCancellationRequested ||
                    generation != Volatile.Read(ref _loadGeneration) ||
                    _gridForm != gridForm ||
                    gridForm.IsDisposed ||
                    gridForm.Disposing)
                {
                    return;
                }

                if (errorMessage is not null)
                {
                    gridForm.ShowTableError(errorMessage);
                    return;
                }

                PresentTableBuildResult(
                    gridForm,
                    snapshot,
                    buildResult ??
                    throw new InvalidOperationException(
                        L10n.Get(TextKey.Host_TheBackgroundTableBuildCompletedWithoutAResult)));
            }));
        }
        catch (InvalidOperationException)
        {
            // The docking form was destroyed while the background build completed.
        }
    }

    private static void PresentTableBuildResult(
        CsvGridForm gridForm,
        ActiveDocumentSnapshot snapshot,
        CsvTableBuildResult buildResult)
    {
        switch (buildResult.Status)
        {
            case CsvTableBuildStatus.Empty:
                gridForm.ShowEmptyDocument(snapshot);
                return;

            case CsvTableBuildStatus.DelimiterSelectionRequired
                when buildResult.DetectionResult is not null:
                gridForm.ShowDelimiterSelectionRequired(
                    snapshot,
                    buildResult.DetectionResult);
                return;

            case CsvTableBuildStatus.Ready
                when buildResult.ParseResult is not null &&
                     buildResult.Projection is not null:
                gridForm.ShowVisualTable(
                    snapshot,
                    buildResult.ParseResult,
                    buildResult.Projection,
                    buildResult.DetectionResult,
                    buildResult.DelimiterWasAutomatic);
                CsvGridSourceNavigationController.SetBaseline(
                    gridForm,
                    snapshot,
                    buildResult.ParseResult);
                return;

            default:
                throw new InvalidOperationException(
                    L10n.Get(TextKey.Host_TheTableBuilderReturnedAnIncompleteResult));
        }
    }

    private static void ShowAboutDialog()
    {
        var dark = PluginData.Notepad.IsDarkModeEnabled();
        using var dialog = new CsvAboutDialog(
            dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control,
            dark ? Color.Gainsboro : SystemColors.ControlText);
        dialog.ShowDialog(new NotepadWindow(PluginData.NppData.NppHandle));
    }

    private sealed class NotepadWindow(IntPtr handle) : IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }

    private void PluginCleanUp()
    {
        Interlocked.Increment(ref _loadGeneration);
        var cancellation = Interlocked.Exchange(ref _loadCancellation, null);
        cancellation?.Cancel();
        cancellation?.Dispose();

        if (_gridForm is not null)
        {
            _gridForm.RefreshRequested -= OnRefreshRequested;
            _gridForm.ApplyRequested -= OnApplyRequested;
            _gridForm.Dispose();
            _gridForm = null;
        }

        PluginData.FuncItems.Dispose();

        // PluginNamePtr's setter owns the allocation and frees the previous pointer.
        // Assigning zero is sufficient; freeing it manually first would double-free
        // the unmanaged string during Notepad++ shutdown.
        PluginData.PluginNamePtr = IntPtr.Zero;
    }
}

