namespace CsvVisualEditor;

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
            "Open Visual Table",
            ToggleDialog,
            new ShortcutKey(ctrl: false, alt: true, shift: false, Keys.F10));
        Utils.SetCommand("Refresh Table", RefreshTable);
        Utils.SetCommand("Filter and Sort", () => OpenDataTool(summary: false));
        Utils.SetCommand("Column Summary", () => OpenDataTool(summary: true));
        Utils.MakeSeparator();
        Utils.SetCommand("About", ShowAboutDialog);
    }

    public void OnBeNotified(ScNotification notification)
    {
        if (notification.Header.HwndFrom != PluginData.NppData.NppHandle)
        {
            return;
        }

        switch ((NppMsg)notification.Header.Code)
        {
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
                "Refresh is disabled while Edit mode is active. " +
                "Apply or Revert All pending changes first.",
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
                    "The CSV changes were applied successfully, but the previous " +
                    "caret or selection could not be restored. The document remains " +
                    "fully undoable as one action.",
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
            ? "the current editor code page"
            : $"Scintilla code page {codePage.Value}";
        var message = result.Status switch
        {
            CsvEditorApplyStatus.EncodingWriteNotEnabled =>
                $"Apply is not yet enabled for {codePageText}. No editor content was changed. " +
                "Host writes are enabled only for UTF-8 (65001) and Windows-1250 (1250), after lossless validation. " +
                "Use Revert All or convert the document to UTF-8 in Notepad++, then reopen Edit mode.",
            CsvEditorApplyStatus.UnsupportedCodePage =>
                $"Apply is blocked because {codePageText} has no explicit supported encoding profile. " +
                "No editor content was changed.",
            CsvEditorApplyStatus.TextNotRepresentable =>
                $"Apply is blocked because the pending replacement cannot be represented exactly in {codePageText}. " +
                "No replacement character was used and no editor content was changed.",
            CsvEditorApplyStatus.EncodingRoundTripMismatch =>
                $"Apply is blocked because strict encoding validation for {codePageText} did not round-trip exactly. " +
                "No editor content was changed.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(result),
                result.Status,
                "The Apply result is not an encoding-blocked status.")
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
            _gridForm.ShowSnapshotError(exception.Message);
            return;
        }
        catch (Exception)
        {
            _gridForm.ShowSnapshotError(
                "The active Notepad++ document could not be read. " +
                "No editor content was changed.");
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
            errorMessage = exception.Message;
        }
        catch (Exception)
        {
            errorMessage =
                "The editor buffer could not be converted into a visual table. " +
                "Choose an explicit delimiter or refresh after correcting the document.";
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
                        "The background table build completed without a result."));
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
                    "The table builder returned an incomplete result.");
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

        if (PluginData.PluginNamePtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(PluginData.PluginNamePtr);
            PluginData.PluginNamePtr = IntPtr.Zero;
        }
    }
}

