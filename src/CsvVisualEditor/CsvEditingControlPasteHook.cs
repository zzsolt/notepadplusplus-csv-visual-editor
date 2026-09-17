namespace CsvVisualEditor;

/// <summary>
/// Subclasses the active DataGridView editing-control window. Besides clipboard
/// routing, it answers the dialog manager's Space-key query so Notepad++ treats
/// Space as text input for the active cell instead of a modeless-dialog command.
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
        if (message.Msg == WmGetDlgCode &&
            (Keys)message.WParam.ToInt32() == Keys.Space)
        {
            // IsDialogMessage asks the focused editor whether it wants this exact
            // key before dispatching it. The modeless Notepad++ host otherwise
            // consumes Space here, so no WM_CHAR ever reaches the cell editor.
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

    public void Dispose()
    {
        Detach();
        GC.SuppressFinalize(this);
    }
}
