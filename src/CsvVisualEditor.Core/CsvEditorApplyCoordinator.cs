namespace CsvVisualEditor.Core;

/// <summary>
/// Minimal host contract for replacing an editor buffer as one undoable action.
/// </summary>
public interface IEditorReplacementTarget
{
    void BeginUndoAction();

    long ReplaceWholeDocument(string text);

    void SetSelection(long anchorPosition, long caretPosition);

    void EndUndoAction();
}

public enum CsvEditorApplyStatus
{
    NoChanges,
    Applied,
    DocumentIdentityChanged,
    CodePageChanged,
    ContentChanged
}

public sealed record CsvEditorApplyResult
{
    private CsvEditorApplyResult(
        CsvEditorApplyStatus status,
        int changedCellCount,
        int changedRecordCount,
        string? replacementSha256)
    {
        Status = status;
        ChangedCellCount = changedCellCount;
        ChangedRecordCount = changedRecordCount;
        ReplacementSha256 = replacementSha256;
    }

    public CsvEditorApplyStatus Status { get; }

    public int ChangedCellCount { get; }

    public int ChangedRecordCount { get; }

    public string? ReplacementSha256 { get; }

    public bool WasApplied => Status == CsvEditorApplyStatus.Applied;

    internal static CsvEditorApplyResult FromPlan(CsvEditApplyPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var status = plan.Status switch
        {
            CsvEditApplyStatus.NoChanges => CsvEditorApplyStatus.NoChanges,
            CsvEditApplyStatus.DocumentIdentityChanged =>
                CsvEditorApplyStatus.DocumentIdentityChanged,
            CsvEditApplyStatus.CodePageChanged => CsvEditorApplyStatus.CodePageChanged,
            CsvEditApplyStatus.ContentChanged => CsvEditorApplyStatus.ContentChanged,
            CsvEditApplyStatus.Ready => throw new ArgumentException(
                "A Ready plan must be executed instead of converted directly.",
                nameof(plan)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(plan),
                plan.Status,
                "Unknown edit apply status.")
        };

        return new CsvEditorApplyResult(
            status,
            plan.ChangedCellCount,
            changedRecordCount: 0,
            replacementSha256: null);
    }

    internal static CsvEditorApplyResult Applied(CsvEditPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        return new CsvEditorApplyResult(
            CsvEditorApplyStatus.Applied,
            preview.ChangedCellCount,
            preview.ChangedRecordCount,
            preview.ContentSha256);
    }
}

/// <summary>
/// Executes a conflict-checked edit plan against a host replacement target.
/// The host is never called for NoChanges or conflict states.
/// </summary>
public static class CsvEditorApplyCoordinator
{
    public static CsvEditorApplyResult Execute(
        CsvEditSession session,
        ActiveDocumentSnapshot currentSnapshot,
        IEditorReplacementTarget target)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(currentSnapshot);
        ArgumentNullException.ThrowIfNull(target);

        var plan = session.CreateApplyPlan(currentSnapshot);
        if (!plan.IsReady)
        {
            return CsvEditorApplyResult.FromPlan(plan);
        }

        var preview = plan.Preview ?? throw new InvalidOperationException(
            "The Ready apply plan did not contain a replacement preview.");

        target.BeginUndoAction();
        try
        {
            var newByteLength = target.ReplaceWholeDocument(preview.Text);
            if (newByteLength < 0)
            {
                throw new InvalidOperationException(
                    "The editor replacement returned an invalid document length.");
            }

            target.SetSelection(
                ClampPosition(currentSnapshot.AnchorPosition, newByteLength),
                ClampPosition(currentSnapshot.CaretPosition, newByteLength));
        }
        finally
        {
            target.EndUndoAction();
        }

        return CsvEditorApplyResult.Applied(preview);
    }

    private static long ClampPosition(long position, long documentByteLength)
    {
        if (position <= 0)
        {
            return 0;
        }

        return Math.Min(position, documentByteLength);
    }
}
