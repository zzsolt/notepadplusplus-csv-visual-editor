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

/// <summary>
/// Host-neutral replacement plan shared by cell-only and structural edits.
/// A Ready plan contains one complete replacement buffer; conflict plans never do.
/// </summary>
public sealed record CsvEditorReplacementPlan
{
    private CsvEditorReplacementPlan(
        CsvEditApplyStatus status,
        string? replacementText,
        string? replacementSha256,
        int changedCellCount,
        int changedRecordCount,
        int insertedRowCount,
        int deletedRowCount)
    {
        if (changedCellCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(changedCellCount));
        }

        if (changedRecordCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(changedRecordCount));
        }

        if (insertedRowCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(insertedRowCount));
        }

        if (deletedRowCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deletedRowCount));
        }

        if (status == CsvEditApplyStatus.Ready)
        {
            ArgumentNullException.ThrowIfNull(replacementText);
            ArgumentNullException.ThrowIfNull(replacementSha256);
        }
        else if (replacementText is not null || replacementSha256 is not null)
        {
            throw new ArgumentException(
                "Only a Ready editor replacement plan may contain replacement content.",
                nameof(replacementText));
        }

        Status = status;
        ReplacementText = replacementText;
        ReplacementSha256 = replacementSha256;
        ChangedCellCount = changedCellCount;
        ChangedRecordCount = changedRecordCount;
        InsertedRowCount = insertedRowCount;
        DeletedRowCount = deletedRowCount;
    }

    public CsvEditApplyStatus Status { get; }

    public string? ReplacementText { get; }

    public string? ReplacementSha256 { get; }

    public int ChangedCellCount { get; }

    public int ChangedRecordCount { get; }

    public int InsertedRowCount { get; }

    public int DeletedRowCount { get; }

    public bool IsReady => Status == CsvEditApplyStatus.Ready;

    internal static CsvEditorReplacementPlan FromCellPlan(CsvEditApplyPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.IsReady)
        {
            return new CsvEditorReplacementPlan(
                plan.Status,
                replacementText: null,
                replacementSha256: null,
                plan.ChangedCellCount,
                changedRecordCount: 0,
                insertedRowCount: 0,
                deletedRowCount: 0);
        }

        var preview = plan.Preview ?? throw new InvalidOperationException(
            "The Ready cell-edit plan did not contain a replacement preview.");
        return new CsvEditorReplacementPlan(
            CsvEditApplyStatus.Ready,
            preview.Text,
            preview.ContentSha256,
            preview.ChangedCellCount,
            preview.ChangedRecordCount,
            insertedRowCount: 0,
            deletedRowCount: 0);
    }

    internal static CsvEditorReplacementPlan FromStructuralPreview(
        CsvEditApplyStatus status,
        CsvRowEditPreview? preview,
        int changedCellCount,
        int changedRecordCount,
        int insertedRowCount,
        int deletedRowCount)
    {
        if (status == CsvEditApplyStatus.Ready)
        {
            ArgumentNullException.ThrowIfNull(preview);
            return new CsvEditorReplacementPlan(
                status,
                preview.Text,
                preview.ContentSha256,
                preview.ChangedCellCount,
                preview.ChangedRowCount,
                preview.InsertedRowCount,
                preview.DeletedRowCount);
        }

        if (preview is not null)
        {
            throw new ArgumentException(
                "A non-Ready structural plan cannot expose a replacement preview.",
                nameof(preview));
        }

        return new CsvEditorReplacementPlan(
            status,
            replacementText: null,
            replacementSha256: null,
            changedCellCount,
            changedRecordCount,
            insertedRowCount,
            deletedRowCount);
    }
}

public sealed record CsvEditorApplyResult
{
    private CsvEditorApplyResult(
        CsvEditorApplyStatus status,
        int changedCellCount,
        int changedRecordCount,
        int insertedRowCount,
        int deletedRowCount,
        string? replacementSha256,
        bool selectionRestored)
    {
        Status = status;
        ChangedCellCount = changedCellCount;
        ChangedRecordCount = changedRecordCount;
        InsertedRowCount = insertedRowCount;
        DeletedRowCount = deletedRowCount;
        ReplacementSha256 = replacementSha256;
        SelectionRestored = selectionRestored;
    }

    public CsvEditorApplyStatus Status { get; }

    public int ChangedCellCount { get; }

    public int ChangedRecordCount { get; }

    public int InsertedRowCount { get; }

    public int DeletedRowCount { get; }

    public string? ReplacementSha256 { get; }

    public bool SelectionRestored { get; }

    public bool WasApplied => Status == CsvEditorApplyStatus.Applied;

    internal static CsvEditorApplyResult FromPlan(CsvEditorReplacementPlan plan)
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
                "Unknown editor replacement status.")
        };

        return new CsvEditorApplyResult(
            status,
            plan.ChangedCellCount,
            plan.ChangedRecordCount,
            plan.InsertedRowCount,
            plan.DeletedRowCount,
            replacementSha256: null,
            selectionRestored: false);
    }

    internal static CsvEditorApplyResult Applied(
        CsvEditorReplacementPlan plan,
        bool selectionRestored)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.IsReady ||
            plan.ReplacementText is null ||
            plan.ReplacementSha256 is null)
        {
            throw new ArgumentException(
                "An applied result requires a complete Ready replacement plan.",
                nameof(plan));
        }

        return new CsvEditorApplyResult(
            CsvEditorApplyStatus.Applied,
            plan.ChangedCellCount,
            plan.ChangedRecordCount,
            plan.InsertedRowCount,
            plan.DeletedRowCount,
            plan.ReplacementSha256,
            selectionRestored);
    }
}

/// <summary>
/// Executes conflict-checked cell or structural edits against one shared host
/// replacement path. The host is never called for NoChanges or conflict states.
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

        return ExecutePlan(
            CsvEditorReplacementPlan.FromCellPlan(
                session.CreateApplyPlan(currentSnapshot)),
            currentSnapshot,
            target);
    }

    public static CsvEditorApplyResult Execute(
        CsvRowEditModel rowModel,
        ActiveDocumentSnapshot currentSnapshot,
        IEditorReplacementTarget target)
    {
        ArgumentNullException.ThrowIfNull(rowModel);
        ArgumentNullException.ThrowIfNull(currentSnapshot);
        ArgumentNullException.ThrowIfNull(target);

        return ExecutePlan(
            rowModel.CreateApplyPlan(currentSnapshot),
            currentSnapshot,
            target);
    }

    private static CsvEditorApplyResult ExecutePlan(
        CsvEditorReplacementPlan plan,
        ActiveDocumentSnapshot currentSnapshot,
        IEditorReplacementTarget target)
    {
        if (!plan.IsReady)
        {
            return CsvEditorApplyResult.FromPlan(plan);
        }

        var replacementText = plan.ReplacementText ?? throw new InvalidOperationException(
            "The Ready replacement plan did not contain replacement text.");
        var selectionRestored = false;
        target.BeginUndoAction();
        try
        {
            var newByteLength = target.ReplaceWholeDocument(replacementText);
            if (newByteLength < 0)
            {
                throw new InvalidOperationException(
                    "The editor replacement returned an invalid document length.");
            }

            try
            {
                target.SetSelection(
                    ClampPosition(currentSnapshot.AnchorPosition, newByteLength),
                    ClampPosition(currentSnapshot.CaretPosition, newByteLength));
                selectionRestored = true;
            }
            catch (Exception)
            {
                // Selection restoration is non-critical after a successful atomic
                // replacement. The result records the failure without misreporting
                // the document replacement itself as unsuccessful.
                selectionRestored = false;
            }
        }
        finally
        {
            target.EndUndoAction();
        }

        return CsvEditorApplyResult.Applied(plan, selectionRestored);
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
