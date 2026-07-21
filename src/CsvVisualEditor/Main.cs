namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using Npp.DotNet.Plugin;
using System.Runtime.InteropServices;

partial class Main : IDotNetPlugin
{
    private const string PluginDisplayName = "CSV Visual Editor";
    private const string PluginAssemblyName = "CsvVisualEditor";
    private const int DialogCommandIndex = 0;

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
            LoadActiveDocumentSnapshot();
            return;
        }

        if (_gridForm.Visible)
        {
            _gridForm.HideDockingForm();
        }
        else
        {
            _gridForm.ShowDockingForm();
            LoadActiveDocumentSnapshot();
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

        LoadActiveDocumentSnapshot();
    }

    private void OnRefreshRequested(object? sender, EventArgs e)
    {
        LoadActiveDocumentSnapshot();
    }

    private void LoadActiveDocumentSnapshot()
    {
        if (_gridForm is null)
        {
            return;
        }

        try
        {
            var snapshot = _activeDocumentReader.ReadActiveDocument();
            _gridForm.ShowDocumentSnapshot(snapshot);
        }
        catch (InvalidOperationException exception)
        {
            _gridForm.ShowSnapshotError(exception.Message);
        }
        catch (Exception)
        {
            _gridForm.ShowSnapshotError(
                "The active Notepad++ document could not be read. " +
                "No editor content was changed.");
        }
    }

    private static void ShowAboutDialog()
    {
        MessageBox.Show(
            "CSV Visual Editor 0.3.0-alpha\n\n" +
            "A graphical, spreadsheet-like CSV editor for Notepad++.\n" +
            "This version includes host-independent CSV dialect detection and " +
            "record-aware parsing. Grid population and editing are not enabled yet.",
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
