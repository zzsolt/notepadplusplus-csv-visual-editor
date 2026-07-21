namespace CsvVisualEditor.Core;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Immutable copy of the active Notepad++ editor buffer and the metadata needed
/// to detect whether later operations still refer to the same source content.
/// </summary>
public sealed record ActiveDocumentSnapshot
{
    private ActiveDocumentSnapshot(
        string documentPath,
        string displayName,
        string text,
        long editorByteLength,
        int codePage,
        long caretPosition,
        long anchorPosition,
        bool isModified,
        DateTimeOffset capturedAtUtc,
        string contentSha256)
    {
        DocumentPath = documentPath;
        DisplayName = displayName;
        Text = text;
        EditorByteLength = editorByteLength;
        CodePage = codePage;
        CaretPosition = caretPosition;
        AnchorPosition = anchorPosition;
        IsModified = isModified;
        CapturedAtUtc = capturedAtUtc;
        ContentSha256 = contentSha256;
    }

    public string DocumentPath { get; }

    public string DisplayName { get; }

    public string Text { get; }

    public int CharacterCount => Text.Length;

    /// <summary>
    /// Byte-oriented length reported by Scintilla for the active editor buffer.
    /// This can differ from <see cref="CharacterCount"/> for multibyte text.
    /// </summary>
    public long EditorByteLength { get; }

    public int CodePage { get; }

    public long CaretPosition { get; }

    public long AnchorPosition { get; }

    public long SelectionLength => Math.Abs(CaretPosition - AnchorPosition);

    public bool IsModified { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    /// <summary>
    /// Lowercase SHA-256 of the UTF-8 representation of <see cref="Text"/>.
    /// This is a stable content identity, not a Notepad++ buffer revision number.
    /// </summary>
    public string ContentSha256 { get; }

    public static ActiveDocumentSnapshot Create(
        string? documentPath,
        string text,
        long editorByteLength,
        int codePage,
        long caretPosition,
        long anchorPosition,
        bool isModified,
        DateTimeOffset capturedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (editorByteLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(editorByteLength),
                editorByteLength,
                "Editor byte length cannot be negative.");
        }

        if (caretPosition < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(caretPosition),
                caretPosition,
                "Caret position cannot be negative.");
        }

        if (anchorPosition < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(anchorPosition),
                anchorPosition,
                "Anchor position cannot be negative.");
        }

        var normalizedPath = documentPath?.Trim() ?? string.Empty;
        var displayName = GetDisplayName(normalizedPath);
        var contentHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(text)))
            .ToLowerInvariant();

        return new ActiveDocumentSnapshot(
            normalizedPath,
            displayName,
            text,
            editorByteLength,
            codePage,
            caretPosition,
            anchorPosition,
            isModified,
            capturedAtUtc.ToUniversalTime(),
            contentHash);
    }

    private static string GetDisplayName(string documentPath)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return "Untitled";
        }

        var fileName = Path.GetFileName(documentPath);
        return string.IsNullOrWhiteSpace(fileName) ? documentPath : fileName;
    }
}