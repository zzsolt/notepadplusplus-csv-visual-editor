namespace CsvVisualEditor;

/// <summary>
/// Subclasses the active DataGridView editing-control window and intercepts the
/// actual WM_PASTE message. This remains reliable when Notepad++ owns the outer
/// native message loop and when the WinForms TextBox consumes Ctrl+V before a
/// managed KeyDown event can be raised.
/// </summary>
internal sealed class CsvEditingControlPasteHook : NativeWindow, IDisposable
{
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
        if (message.Msg == WmPaste && _tryHandlePaste?.Invoke() == true)
        {
            return;
        }

        base.WndProc(ref message);
    }

    public void Dispose()
    {
        Detach();
        GC.SuppressFinalize(this);
    }
}
