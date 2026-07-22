namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using Npp.DotNet.Plugin;

/// <summary>
/// Thin Notepad++/Scintilla adapter for one-transaction whole-document replacement.
/// Conflict checks and replacement planning remain in the host-independent core.
/// </summary>
internal sealed class NotepadEditorReplacementTarget : IEditorReplacementTarget
{
    private readonly IScintillaGateway _editor;

    public NotepadEditorReplacementTarget()
        : this(PluginData.Editor)
    {
    }

    internal NotepadEditorReplacementTarget(IScintillaGateway editor)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
    }

    public void BeginUndoAction()
    {
        _editor.BeginUndoAction();
    }

    public long ReplaceWholeDocument(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _editor.TargetWholeDocument();
        return _editor.ReplaceTarget(text);
    }

    public void SetSelection(long anchorPosition, long caretPosition)
    {
        _editor.SetSel(anchorPosition, caretPosition);
    }

    public void EndUndoAction()
    {
        _editor.EndUndoAction();
    }
}
