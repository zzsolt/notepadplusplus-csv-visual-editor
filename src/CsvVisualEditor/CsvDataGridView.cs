namespace CsvVisualEditor;

/// <summary>
/// DataGridView command-key seam that works inside the native Notepad++ host.
/// Application-level WinForms message filters are not reliable when Notepad++ owns
/// the outer message loop, so clipboard commands are also intercepted directly by
/// the focused grid control.
/// </summary>
internal sealed class CsvDataGridView : DataGridView
{
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

    private static bool IsClipboardCommand(Keys keyData)
    {
        var modifiers = keyData & Keys.Modifiers;
        if ((modifiers & Keys.Control) != Keys.Control ||
            (modifiers & Keys.Alt) == Keys.Alt)
        {
            return false;
        }

        var keyCode = keyData & Keys.KeyCode;
        return keyCode is Keys.C or Keys.V;
    }
}
