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
    private const int MaximumDisplayedRows = 10_000;
    private const int MaximumDisplayedColumns = 512;

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
            _gridForm = new CsvGridForm(
                DialogCommandIndex,
                $"{PluginAssemblyName}.dll",
                SystemIcons.Application);
            _gridForm.RefreshRequested += OnRefreshRequested;
            LoadActiveDocumentTable();
            return;
        }

        if (_gridForm.Visible)
        {
            _gridForm.HideDockingForm();
        }
        else
        {
            _gridForm.ShowDockingForm();
            LoadActiveDocumentTable();
        }
    }

    private void RefreshTable()
    {
        if (_gridForm is null)
        {
            ToggleDialog();
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
        LoadActiveDocumentTable();
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

        if (snapshot.Text.Length == 0)
        {
            _gridForm.ShowEmptyDocument(snapshot);
            return;
        }

        try
        {
            var delimiterOverride = _gridForm.SelectedDelimiterOverride;
            var headerMode = _gridForm.SelectedHeaderMode;
            CsvDialectDetectionResult? detectionResult = null;
            CsvDialect dialect;
            var delimiterWasAutomatic = delimiterOverride is null;

            if (delimiterWasAutomatic)
            {
                detectionResult = CsvDialectDetector.Detect(snapshot.Text);
                if (!detectionResult.IsReliable || detectionResult.SuggestedDialect is null)
                {
                    _gridForm.ShowDelimiterSelectionRequired(snapshot, detectionResult);
                    return;
                }

                dialect = CsvDialect.Create(
                    detectionResult.SuggestedDialect.Delimiter,
                    headerMode: headerMode);
            }
            else
            {
                dialect = CsvDialect.Create(
                    delimiterOverride.Value,
                    headerMode: headerMode);
            }

            var parseResult = CsvParser.Parse(snapshot.Text, dialect);
            var projection = CsvTableProjector.Create(
                parseResult,
                new CsvTableProjectionOptions
                {
                    HeaderMode = headerMode,
                    MaximumRows = MaximumDisplayedRows,
                    MaximumColumns = MaximumDisplayedColumns
                });

            _gridForm.ShowVisualTable(
                snapshot,
                parseResult,
                projection,
                detectionResult,
                delimiterWasAutomatic);
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
            "CSV Visual Editor 0.4.0-alpha\n\n" +
            "A graphical, spreadsheet-like CSV viewer for Notepad++.\n" +
            "This version displays parsed CSV rows in a read-only grid with explicit " +
            "delimiter and header controls. Editing is not enabled yet.\n\n" +
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