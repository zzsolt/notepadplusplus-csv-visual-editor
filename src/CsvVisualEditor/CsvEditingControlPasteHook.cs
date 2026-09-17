namespace CsvVisualEditor;

/// <summary>
/// Subclasses the active DataGridView editing-control window and intercepts both
/// the actual WM_PASTE message and a raw Ctrl+V key message. It also repairs the
/// Space key on the transient text editor: Notepad++/modeless-dialog key handling
/// can suppress Space before DataGridView sees it, while the editor itself still
/// raises KeyDown. Handling that control directly keeps ordinary typing reliable.
/// </summary>
internal sealed class CsvEditingControlPasteHook : NativeWindow, IDisposable
{
    private const int WmKeyDown = 0x0100;
    private const int WmPaste = 0x0302;

    private Func<bool>? _tryHandlePaste;
    private Control? _editingControl;

    internal void Attach(Control editingControl, Func<bool> tryHandlePaste)
    {
        ArgumentNullException.ThrowIfNull(editingControl);
        ArgumentNullException.ThrowIfNull(tryHandlePaste);

        Detach();
        _editingControl = editingControl;
        _editingControl.KeyDown += OnEditingControlKeyDown;
        _tryHandlePaste = tryHandlePaste;
        AssignHandle(editingControl.Handle);
    }

    internal void Detach()
    {
        if (_editingControl is not null)
        {
            _editingControl.KeyDown -= OnEditingControlKeyDown;
            _editingControl = null;
        }
        _tryHandlePaste = null;
        if (Handle != IntPtr.Zero)
        {
            ReleaseHandle();
        }
    }

    private static void OnEditingControlKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBoxBase editor ||
            e.KeyCode != Keys.Space ||
            e.Modifiers is not (Keys.None or Keys.Shift))
        {
            return;
        }

        // The modeless Notepad++ keyboard seam can mark Space as suppressed before
        // this handler runs. Event subscribers still run, so insert exactly one
        // ordinary space at the native editor selection and consume the key here.
        editor.SelectedText = " ";
        e.Handled = true;
        e.SuppressKeyPress = true;
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
