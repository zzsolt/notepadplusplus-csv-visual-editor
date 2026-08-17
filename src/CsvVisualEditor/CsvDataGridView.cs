namespace CsvVisualEditor;

using System.ComponentModel;

/// <summary>
/// DataGridView clipboard-command seam that works inside the native Notepad++ host.
/// Notepad++ can translate Ctrl+C/Ctrl+X/Ctrl+V into WM_COPY/WM_CUT/WM_PASTE before
/// WinForms command-key preprocessing runs, so both managed command keys and the
/// native clipboard messages are handled directly by the focused grid window.
/// </summary>
internal sealed class CsvDataGridView : DataGridView
{
    private const int WmKeyDown = 0x0100;
    private const int WmCut = 0x0300;
    private const int WmCopy = 0x0301;
    private const int WmPaste = 0x0302;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Func<Keys, bool>? ClipboardCommandHandler { get; set; }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (IsClipboardCommand(keyData) &&
            ClipboardCommandHandler?.Invoke(keyData) == true)
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void WndProc(ref Message message)
    {
        if (TryResolveNativeClipboardCommand(
                message.Msg,
                message.WParam,
                ModifierKeys,
                out var keyData) &&
            ClipboardCommandHandler?.Invoke(keyData) == true)
        {
            return;
        }

        base.WndProc(ref message);
    }

    internal static bool TryResolveNativeClipboardCommand(
        int messageId,
        IntPtr wParam,
        Keys modifierKeys,
        out Keys keyData)
    {
        keyData = Keys.None;
        switch (messageId)
        {
            case WmCopy:
                keyData = Keys.Control | Keys.C;
                return true;
            case WmCut:
                keyData = Keys.Control | Keys.X;
                return true;
            case WmPaste:
                keyData = Keys.Control | Keys.V;
                return true;
            case WmKeyDown:
                return TryResolveCommandKey(
                    (Keys)wParam.ToInt32(),
                    modifierKeys,
                    out keyData);
            default:
                return false;
        }
    }

    private static bool IsClipboardCommand(Keys keyData)
    {
        var modifiers = keyData & Keys.Modifiers;
        return TryResolveCommandKey(
            keyData & Keys.KeyCode,
            modifiers,
            out _);
    }

    private static bool TryResolveCommandKey(
        Keys keyCode,
        Keys modifierKeys,
        out Keys keyData)
    {
        keyData = Keys.None;
        var modifiers = modifierKeys & Keys.Modifiers;
        if ((modifiers & Keys.Control) != Keys.Control ||
            (modifiers & Keys.Alt) == Keys.Alt ||
            keyCode is not (Keys.C or Keys.X or Keys.V))
        {
            return false;
        }

        keyData = modifiers | keyCode;
        return true;
    }
}
