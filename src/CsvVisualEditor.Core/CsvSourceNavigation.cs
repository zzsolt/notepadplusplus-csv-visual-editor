namespace CsvVisualEditor.Core;

using System.Text;

/// <summary>
/// Stable source address for navigation. Record identity is the parser logical-record
/// index; column identity is the physical CSV field index. A null column navigates to
/// the complete logical record.
/// </summary>
public readonly record struct CsvSourceNavigationAddress
{
    public CsvSourceNavigationAddress(int sourceRecordIndex, int? columnIndex)
    {
        if (sourceRecordIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRecordIndex));
        }

        if (columnIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columnIndex));
        }

        SourceRecordIndex = sourceRecordIndex;
        ColumnIndex = columnIndex;
    }

    public int SourceRecordIndex { get; }

    public int? ColumnIndex { get; }
}

public enum CsvSourceNavigationStatus
{
    Ready,
    DocumentIdentityChanged,
    CodePageChanged,
    ContentChanged,
    SourceRecordUnavailable,
    SourceColumnUnavailable,
    UnsupportedCodePage,
    PositionEncodingFailed,
    EditorByteLengthMismatch
}

/// <summary>
/// Host-independent plan for selecting one source record/cell in Scintilla.
/// Character offsets come from parser source spans; byte positions are derived using
/// the exact current editor code page because Scintilla positions are byte-oriented.
/// </summary>
public sealed record CsvSourceNavigationPlan
{
    private CsvSourceNavigationPlan(
        CsvSourceNavigationStatus status,
        CsvSourceNavigationAddress address,
        CsvSourceSpan? characterSpan,
        long anchorBytePosition,
        long caretBytePosition)
    {
        if (anchorBytePosition < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(anchorBytePosition));
        }

        if (caretBytePosition < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(caretBytePosition));
        }

        if (status == CsvSourceNavigationStatus.Ready && characterSpan is null)
        {
            throw new ArgumentException(
                "A ready source-navigation plan requires a source character span.",
                nameof(characterSpan));
        }

        if (status != CsvSourceNavigationStatus.Ready &&
            (characterSpan is not null || anchorBytePosition != 0 || caretBytePosition != 0))
        {
            throw new ArgumentException(
                "A blocked source-navigation plan cannot contain source positions.",
                nameof(characterSpan));
        }

        Status = status;
        Address = address;
        CharacterSpan = characterSpan;
        AnchorBytePosition = anchorBytePosition;
        CaretBytePosition = caretBytePosition;
    }

    public CsvSourceNavigationStatus Status { get; }

    public CsvSourceNavigationAddress Address { get; }

    public CsvSourceSpan? CharacterSpan { get; }

    public long AnchorBytePosition { get; }

    public long CaretBytePosition { get; }

    public bool IsReady => Status == CsvSourceNavigationStatus.Ready;

    internal static CsvSourceNavigationPlan Ready(
        CsvSourceNavigationAddress address,
        CsvSourceSpan characterSpan,
        long anchorBytePosition,
        long caretBytePosition) =>
        new(
            CsvSourceNavigationStatus.Ready,
            address,
            characterSpan,
            anchorBytePosition,
            caretBytePosition);

    internal static CsvSourceNavigationPlan Blocked(
        CsvSourceNavigationAddress address,
        CsvSourceNavigationStatus status)
    {
        if (status == CsvSourceNavigationStatus.Ready)
        {
            throw new ArgumentException(
                "A blocked source-navigation plan cannot have Ready status.",
                nameof(status));
        }

        return new CsvSourceNavigationPlan(
            status,
            address,
            characterSpan: null,
            anchorBytePosition: 0,
            caretBytePosition: 0);
    }
}

/// <summary>
/// Converts decoded UTF-16 character offsets into byte positions used by Scintilla.
/// Only explicitly recognized code pages are accepted and encoding is always strict.
/// </summary>
public static class CsvScintillaPositionMapper
{
    public static bool TryMapCharacterOffset(
        string text,
        int characterOffset,
        int codePage,
        out long bytePosition)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (characterOffset < 0 || characterOffset > text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(characterOffset));
        }

        bytePosition = 0;
        if (!CsvEncodingProfiles.TryGet(codePage, out var profile) || profile is null)
        {
            return false;
        }

        try
        {
            var encoding = profile.CreateStrictEncoding();
            bytePosition = encoding.GetByteCount(text.AsSpan(0, characterOffset));
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    internal static bool TryGetStrictDocumentByteLength(
        string text,
        int codePage,
        out long byteLength)
    {
        ArgumentNullException.ThrowIfNull(text);

        byteLength = 0;
        if (!CsvEncodingProfiles.TryGet(codePage, out var profile) || profile is null)
        {
            return false;
        }

        try
        {
            Encoding encoding = profile.CreateStrictEncoding();
            byteLength = encoding.GetByteCount(text);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}

/// <summary>
/// Creates one conflict-safe source-navigation plan from the parsed snapshot and a
/// freshly read active editor snapshot. The host must not move the caret when the plan
/// is blocked.
/// </summary>
public static class CsvSourceNavigationPlanner
{
    public static CsvSourceNavigationPlan Create(
        ActiveDocumentSnapshot sourceSnapshot,
        ActiveDocumentSnapshot currentSnapshot,
        CsvParseResult parseResult,
        CsvSourceNavigationAddress address)
    {
        ArgumentNullException.ThrowIfNull(sourceSnapshot);
        ArgumentNullException.ThrowIfNull(currentSnapshot);
        ArgumentNullException.ThrowIfNull(parseResult);

        var baseline = CsvEditSessionBaseline.Create(sourceSnapshot);
        if (!baseline.IsSameDocument(currentSnapshot))
        {
            return CsvSourceNavigationPlan.Blocked(
                address,
                CsvSourceNavigationStatus.DocumentIdentityChanged);
        }

        if (sourceSnapshot.CodePage != currentSnapshot.CodePage)
        {
            return CsvSourceNavigationPlan.Blocked(
                address,
                CsvSourceNavigationStatus.CodePageChanged);
        }

        if (!string.Equals(
                sourceSnapshot.ContentSha256,
                currentSnapshot.ContentSha256,
                StringComparison.Ordinal))
        {
            return CsvSourceNavigationPlan.Blocked(
                address,
                CsvSourceNavigationStatus.ContentChanged);
        }

        CsvParseResultVerifier.EnsureMatchesSnapshot(sourceSnapshot.Text, parseResult);

        var record = ResolveRecord(parseResult, address.SourceRecordIndex);
        if (record is null)
        {
            return CsvSourceNavigationPlan.Blocked(
                address,
                CsvSourceNavigationStatus.SourceRecordUnavailable);
        }

        CsvSourceSpan span;
        if (address.ColumnIndex is int columnIndex)
        {
            if (columnIndex >= record.Cells.Count)
            {
                return CsvSourceNavigationPlan.Blocked(
                    address,
                    CsvSourceNavigationStatus.SourceColumnUnavailable);
            }

            span = record.Cells[columnIndex].SourceSpan;
        }
        else
        {
            span = record.SourceSpan;
        }

        if (!CsvEncodingProfiles.TryGet(currentSnapshot.CodePage, out _))
        {
            return CsvSourceNavigationPlan.Blocked(
                address,
                CsvSourceNavigationStatus.UnsupportedCodePage);
        }

        if (!CsvScintillaPositionMapper.TryGetStrictDocumentByteLength(
                currentSnapshot.Text,
                currentSnapshot.CodePage,
                out var computedByteLength))
        {
            return CsvSourceNavigationPlan.Blocked(
                address,
                CsvSourceNavigationStatus.PositionEncodingFailed);
        }

        if (computedByteLength != currentSnapshot.EditorByteLength)
        {
            return CsvSourceNavigationPlan.Blocked(
                address,
                CsvSourceNavigationStatus.EditorByteLengthMismatch);
        }

        if (!CsvScintillaPositionMapper.TryMapCharacterOffset(
                currentSnapshot.Text,
                span.Start,
                currentSnapshot.CodePage,
                out var startBytePosition) ||
            !CsvScintillaPositionMapper.TryMapCharacterOffset(
                currentSnapshot.Text,
                span.End,
                currentSnapshot.CodePage,
                out var endBytePosition))
        {
            return CsvSourceNavigationPlan.Blocked(
                address,
                CsvSourceNavigationStatus.PositionEncodingFailed);
        }

        if (endBytePosition < startBytePosition ||
            endBytePosition > currentSnapshot.EditorByteLength)
        {
            return CsvSourceNavigationPlan.Blocked(
                address,
                CsvSourceNavigationStatus.PositionEncodingFailed);
        }

        return CsvSourceNavigationPlan.Ready(
            address,
            span,
            startBytePosition,
            endBytePosition);
    }

    private static CsvRecord? ResolveRecord(
        CsvParseResult parseResult,
        int sourceRecordIndex)
    {
        if (sourceRecordIndex < parseResult.Records.Count)
        {
            var indexed = parseResult.Records[sourceRecordIndex];
            if (indexed.Index == sourceRecordIndex)
            {
                return indexed;
            }
        }

        return parseResult.Records.FirstOrDefault(
            record => record.Index == sourceRecordIndex);
    }
}
