namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using Npp.DotNet.Plugin;

/// <summary>
/// Reads the current Notepad++ editor buffer through Scintilla. It intentionally
/// does not reopen the document path from disk, so unsaved editor changes are
/// included in the snapshot.
/// </summary>
internal sealed class NotepadActiveDocumentReader : IActiveDocumentReader
{
    internal const long MaximumSnapshotBytes = 64L * 1024L * 1024L;

    public ActiveDocumentSnapshot ReadActiveDocument()
    {
        var editor = PluginData.Editor;
        long editorByteLength = editor.GetTextLength();

        if (editorByteLength > MaximumSnapshotBytes)
        {
            throw new InvalidOperationException(
                $"The active document is {editorByteLength:N0} bytes. " +
                $"The current safe snapshot limit is {MaximumSnapshotBytes:N0} bytes.");
        }

        var text = editor.GetText();
        var documentPath = PluginData.Notepad.GetCurrentFilePath();
        long caretPosition = editor.GetCurrentPos();
        long anchorPosition = editor.GetAnchor();

        return ActiveDocumentSnapshot.Create(
            documentPath,
            text,
            editorByteLength,
            editor.GetCodePage(),
            caretPosition,
            anchorPosition,
            editor.GetModify(),
            DateTimeOffset.UtcNow);
    }
}