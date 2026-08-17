namespace CsvVisualEditor;

/// <summary>
/// Subclasses the active DataGridView editing-control window and intercepts both
/// the actual WM_PASTE message and a raw Ctrl+V key message. This remains reliable
/// when Notepad++ owns the outer native message loop and different click states
/// cause the host or the WinForms editor to choose different paste paths.
/// </summary>
internal sealed class CsvEditingControlPasteHook : NativeWindow, IDisposable
{
    private const int WmKeyDown = 0x0100;
    private const int WmPaste = 0x0302;

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
