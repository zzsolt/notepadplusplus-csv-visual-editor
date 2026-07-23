namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using Npp.DotNet.Plugin;
using System.Runtime.InteropServices;

partial class Main : IDotNetPlugin
{
    private const string PluginDisplayName = "CSV Visual Editor";
    private const string PluginAssemblyName = "CsvVisualEditor";
    private const string DeveloperName = "Zolnai Zsolt";
    private const string DeveloperEmail = "zzsolt@gmail.com";
    private const int DialogCommandIndex = 0;
    private const int Utf8CodePage = 65001;
    private const int MaximumDisplayedRows = 10_000;
    private const int MaximumDisplayedColumns = 512;
    private const int MaximumDisplayedCells = 250_000;

    private static readonly IDotNetPlugin Instance;
    private readonly IActiveDocumentReader _activeDocumentReader =
        new NotepadActiveDocumentReader();
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

        if (_gridForm.EditSession.Baseline.CodePage != Utf8CodePage ||
            currentSnapshot.CodePage != Utf8CodePage)
        {
            MessageBox.Show(
                "Apply is currently supported only for UTF-8 editor buffers " +
                "(Scintilla code page 65001). No editor content was changed. " +
                "Use Revert All, convert the document to UTF-8 in Notepad++, " +
                "then reopen Edit mode.",
                PluginDisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        CsvEditorApplyResult result;
        try
        {
            result = CsvEditorApplyCoordinator.Execute(
                _gridForm.RowEditModel,
                currentSnapshot,
                new NotepadEditorReplacementTarget());
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

        _gridForm.ShowApplyConflict(result.Status);
    }

    private void LoadActiveDocumentTable()
    {
        if (_gridForm is null)
        {
            return;
        }

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

        try
        {
            var buildResult = CsvTableBuilder.Build(
                snapshot.Text,
                new CsvTableBuildOptions
                {
                    DelimiterOverride = _gridForm.SelectedDelimiterOverride,
                    HeaderMode = _gridForm.SelectedHeaderMode,
                    MaximumRows = MaximumDisplayedRows,
                    MaximumColumns = MaximumDisplayedColumns,
                    MaximumCells = MaximumDisplayedCells
                });

            switch (buildResult.Status)
            {
                case CsvTableBuildStatus.Empty:
                    _gridForm.ShowEmptyDocument(snapshot);
                    return;

                case CsvTableBuildStatus.DelimiterSelectionRequired
                    when buildResult.DetectionResult is not null:
                    _gridForm.ShowDelimiterSelectionRequired(
                        snapshot,
                        buildResult.DetectionResult);
                    return;

                case CsvTableBuildStatus.Ready
                    when buildResult.ParseResult is not null &&
                         buildResult.Projection is not null:
                    _gridForm.ShowVisualTable(
                        snapshot,
                        buildResult.ParseResult,
                        buildResult.Projection,
                        buildResult.DetectionResult,
                        buildResult.DelimiterWasAutomatic);
                    return;

                default:
                    throw new InvalidOperationException(
                        "The table builder returned an incomplete result.");
            }
        }
        catch (InvalidOperationException exception)
        {
            _gridForm.ShowTableError(exception.Message);
        }
        catch (Exception)
        {
            _gridForm.ShowTableError(
                "The editor buffer could not be converted into a visual table. " +
                "Choose an explicit delimiter or refresh after correcting the document.");
        }
    }

    private static void ShowAboutDialog()
    {
        MessageBox.Show(
            "CSV Visual Editor 0.8.0-alpha\n\n" +
            "A graphical, spreadsheet-like CSV editor for Notepad++.\n" +
            "Edit mode supports deterministic cell editing plus Add Row and Delete Row. " +
            "Fresh-buffer conflict checks and one Scintilla undo transaction protect Apply. " +
            "Apply currently supports UTF-8 editor buffers only and modifies only the " +
            "active Notepad++ editor buffer; saving to disk remains a normal Notepad++ action.\n\n" +
            $"Developer: {DeveloperName}\n" +
            $"Contact: {DeveloperEmail}",
            $"About {PluginDisplayName}",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void PluginCleanUp()
    {
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
