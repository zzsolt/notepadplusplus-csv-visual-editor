namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;

/// <summary>
/// Stable address of one editable CSV cell. The row identity is the source record index
/// used by <see cref="CsvEditSession"/> and the column identity is the physical CSV column.
/// Presentation columns and DataGridView object identity are deliberately excluded.
/// </summary>
public readonly record struct CsvCellAddress(int SourceRecordIndex, int ColumnIndex);

public enum CsvClipboardPasteStatus
{
    Ready,
    EmptyTarget,
    ShapeMismatch,
    TargetOutsideSession
}

public sealed record CsvClipboardCellEdit(
    CsvCellAddress Address,
    string Value);

/// <summary>
/// Fully prevalidated immutable paste operation. Applying a ready plan cannot discover a
/// new address or shape error halfway through mutation.
/// </summary>
public sealed class CsvClipboardPastePlan
{
    private CsvClipboardPastePlan(
        CsvClipboardPasteStatus status,
        IReadOnlyList<CsvClipboardCellEdit> edits)
    {
        Status = status;
        Edits = new ReadOnlyCollection<CsvClipboardCellEdit>(edits.ToArray());
    }

    public CsvClipboardPasteStatus Status { get; }

    public bool IsReady => Status == CsvClipboardPasteStatus.Ready;

    public ReadOnlyCollection<CsvClipboardCellEdit> Edits { get; }

    public static CsvClipboardPastePlan Create(
        CsvEditSession session,
        IReadOnlyList<int> orderedSourceRecordIndexes,
        int startRowOffset,
        int startColumnIndex,
        int targetRowCount,
        int targetColumnCount,
        CsvClipboardMatrix matrix)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(orderedSourceRecordIndexes);
        ArgumentNullException.ThrowIfNull(matrix);

        if (targetRowCount <= 0 || targetColumnCount <= 0)
        {
            return new CsvClipboardPastePlan(CsvClipboardPasteStatus.EmptyTarget, []);
        }

        var broadcast = matrix.IsSingleCell;
        if (!broadcast &&
            (matrix.RowCount != targetRowCount || matrix.ColumnCount != targetColumnCount))
        {
            return new CsvClipboardPastePlan(CsvClipboardPasteStatus.ShapeMismatch, []);
        }

        if (startRowOffset < 0 ||
            startColumnIndex < 0 ||
            startRowOffset + targetRowCount > orderedSourceRecordIndexes.Count ||
            startColumnIndex + targetColumnCount > session.ColumnCount)
        {
            return new CsvClipboardPastePlan(CsvClipboardPasteStatus.TargetOutsideSession, []);
        }

        var edits = new List<CsvClipboardCellEdit>(targetRowCount * targetColumnCount);
        try
        {
            for (var rowOffset = 0; rowOffset < targetRowCount; rowOffset++)
            {
                var sourceRecordIndex = orderedSourceRecordIndexes[startRowOffset + rowOffset];
                for (var columnOffset = 0; columnOffset < targetColumnCount; columnOffset++)
                {
                    var columnIndex = startColumnIndex + columnOffset;

                    // Force validation of every stable address before creating a Ready plan.
                    _ = session.GetValue(sourceRecordIndex, columnIndex);

                    edits.Add(new CsvClipboardCellEdit(
                        new CsvCellAddress(sourceRecordIndex, columnIndex),
                        broadcast ? matrix[0, 0] : matrix[rowOffset, columnOffset]));
                }
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            return new CsvClipboardPastePlan(CsvClipboardPasteStatus.TargetOutsideSession, []);
        }

        return new CsvClipboardPastePlan(CsvClipboardPasteStatus.Ready, edits);
    }

    /// <summary>
    /// Applies an already validated plan to the pending edit model. No Scintilla or disk
    /// operation occurs here. Returns the number of values that actually changed.
    /// </summary>
    public int Apply(CsvEditSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!IsReady)
        {
            throw new InvalidOperationException("Only a ready clipboard paste plan can be applied.");
        }

        // Revalidate before the first mutation so a plan cannot be applied to a different
        // session shape and fail after a partial update.
        foreach (var edit in Edits)
        {
            _ = session.GetValue(edit.Address.SourceRecordIndex, edit.Address.ColumnIndex);
        }

        var changed = 0;
        foreach (var edit in Edits)
        {
            if (session.SetCellValue(
                    edit.Address.SourceRecordIndex,
                    edit.Address.ColumnIndex,
                    edit.Value))
            {
                changed++;
            }
        }

        return changed;
    }
}
