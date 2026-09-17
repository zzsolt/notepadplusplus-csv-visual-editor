namespace CsvVisualEditor;

using System.Runtime.InteropServices;

/// <summary>
/// Subclasses the active DataGridView editing-control window. Besides clipboard
/// routing, it answers the dialog manager's Space-key query so Notepad++ treats
/// Space as text input for the active cell instead of a modeless-dialog command.
/// This seam is intentionally limited to the transient in-cell editor.
/// </summary>
internal sealed class CsvEditingControlPasteHook : NativeWindow, IDisposable
{
    private const int WmGetDlgCode = 0x0087;
    private const int WmKeyDown = 0x0100;
    private const int WmPaste = 0x0302;
    private const int DlgcWantAllKeys = 0x0004;
    private const int DlgcWantChars = 0x0080;

    private Func<bool>? _tryHandlePaste;

    internal void Attach(Control editingControl, Func<bool> tryHandlePaste)
    {
        ArgumentNullException.ThrowIfNull(editingControl);
        ArgumentNullException.ThrowIfNull(tryHandlePaste);

        Detach();
        _tryHandlePaste = tryHandlePaste;
        AssignHandle(editingControl.Handle);
    }

    internal void Detach()
    {
        _tryHandlePaste = null;
        if (Handle != IntPtr.Zero)
        {
            ReleaseHandle();
        }
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmGetDlgCode && IsSpaceDialogQuery(message))
        {
            // IsDialogMessage can provide the queried virtual key either directly in
            // wParam or in the MSG pointed to by lParam. Advertise Space as text input
            // in both forms so the host cannot consume it before the editor gets WM_CHAR.
            base.WndProc(ref message);
            message.Result = (IntPtr)(message.Result.ToInt64() | DlgcWantAllKeys | DlgcWantChars);
            return;
        }

        if ((message.Msg == WmPaste || IsControlVKeyDown(message)) &&
            _tryHandlePaste?.Invoke() == true)
        {
            return;
        }

        base.WndProc(ref message);
    }

    private static bool IsSpaceDialogQuery(Message message)
    {
        if ((Keys)message.WParam.ToInt32() == Keys.Space) return true;
        if (message.LParam == IntPtr.Zero) return false;

        try
        {
            var queried = Marshal.PtrToStructure<DialogMessage>(message.LParam);
            return queried.Message == WmKeyDown &&
                   (Keys)queried.WParam.ToInt32() == Keys.Space;
        }
        catch (Exception exception) when (exception is ArgumentException or AccessViolationException)
        {
            return false;
        }
    }

    private static bool IsControlVKeyDown(Message message)
    {
        if (message.Msg != WmKeyDown ||
            (Keys)message.WParam.ToInt32() != Keys.V)
        {
            return false;
        }

        var modifiers = Control.ModifierKeys;
        return (modifiers & Keys.Control) == Keys.Control &&
               (modifiers & Keys.Alt) != Keys.Alt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DialogMessage
    {
        public IntPtr HWnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
    }

    public void Dispose()
    {
        Detach();
        GC.SuppressFinalize(this);
    }
}
