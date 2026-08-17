namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

internal static class NativeClipboardMessageRoutingSmoke
{
    private const int WmKeyDown = 0x0100;
    private const int WmCut = 0x0300;
    private const int WmCopy = 0x0301;
    private const int WmPaste = 0x0302;

    [ModuleInitializer]
    internal static void Run()
    {
        RequireCommand(WmCopy, IntPtr.Zero, Keys.None, Keys.C, "WM_COPY");
        RequireCommand(WmCut, IntPtr.Zero, Keys.None, Keys.X, "WM_CUT");
        RequireCommand(WmPaste, IntPtr.Zero, Keys.None, Keys.V, "WM_PASTE");
        RequireCommand(
            WmKeyDown,
            new IntPtr((int)Keys.V),
            Keys.Control,
            Keys.V,
            "raw Ctrl+V");

        Require(
            !CsvDataGridView.TryResolveNativeClipboardCommand(
                WmKeyDown,
                new IntPtr((int)Keys.V),
                Keys.None,
                out _),
            "Native AOT raw V without Ctrl must not become a grid paste command.");
        Require(
            !CsvDataGridView.TryResolveNativeClipboardCommand(
                WmKeyDown,
                new IntPtr((int)Keys.V),
                Keys.Control | Keys.Alt,
                out _),
            "Native AOT Alt+Ctrl+V must not become a grid paste command.");

        using var editingHook = new CsvEditingControlPasteHook();
    }

    private static void RequireCommand(
        int messageId,
        IntPtr wParam,
        Keys modifiers,
        Keys expectedKey,
        string description)
    {
        Require(
            CsvDataGridView.TryResolveNativeClipboardCommand(
                messageId,
                wParam,
                modifiers,
                out var keyData),
            $"Native AOT {description} was not recognized as a clipboard command.");
        Require(
            (keyData & Keys.KeyCode) == expectedKey,
            $"Native AOT {description} resolved to the wrong clipboard command.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
