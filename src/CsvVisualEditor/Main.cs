namespace CsvVisualEditor;

using Npp.DotNet.Plugin;
using System.Runtime.InteropServices;

partial class Main : IDotNetPlugin
{
    private const string PluginDisplayName = "CSV Visual Editor";
    private const string PluginAssemblyName = "CsvVisualEditor";
    private const int DialogCommandIndex = 0;

    private static readonly IDotNetPlugin Instance;
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
            return;
        }

        if (_gridForm.Visible)
        {
            _gridForm.HideDockingForm();
        }
        else
        {
            _gridForm.ShowDockingForm();
        }
    }

    private void RefreshTable()
    {
        EnsureDialogVisible();
        _gridForm?.ShowBootstrapState();
    }

    private void EnsureDialogVisible()
    {
        if (_gridForm is null || !_gridForm.Visible)
        {
            ToggleDialog();
        }
    }

    private void OnRefreshRequested(object? sender, EventArgs e)
    {
        _gridForm?.ShowBootstrapState();
    }

    private static void ShowAboutDialog()
    {
        MessageBox.Show(
            "CSV Visual Editor 0.1.0\n\n" +
            "A graphical, spreadsheet-like CSV editor for Notepad++.\n" +
            "This bootstrap version validates the dockable WinForms plugin shell.",
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
