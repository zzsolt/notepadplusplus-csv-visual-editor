namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;

/// <summary>
/// Stable address of one editable CSV cell. Row identity is a CsvEditRowId and column
/// identity is the physical CSV column index. Presentation objects and display indexes
/// are deliberately excluded.
/// </summary>
public readonly record struct CsvCellAddress(CsvEditRowId RowId, int ColumnIndex);

public enum CsvClipboardPasteStatus
{
    Ready,
    EmptyTarget,
    ShapeMismatch,
    TargetOutsideSession
}

public sealed record CsvClipboardCellEdit(CsvCellAddress Address, string Value);

/// <summary>
/// Fully prevalidated immutable paste operation. The plan targets stable row IDs and
/// physical CSV columns and can therefore survive presentation-only column ordering.
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
        CsvRowEditModel model,
        IReadOnlyList<CsvEditRowId> orderedRowIds,
        int startRowOffset,
        int startColumnIndex,
        int targetRowCount,
        int targetColumnCount,
        CsvClipboardMatrix matrix)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(orderedRowIds);
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
            startRowOffset + targetRowCount > orderedRowIds.Count ||
            startColumnIndex + targetColumnCount > model.ColumnCount)
        {
            return new CsvClipboardPastePlan(CsvClipboardPasteStatus.TargetOutsideSession, []);
        }

        var edits = new List<CsvClipboardCellEdit>(targetRowCount * targetColumnCount);
        try
        {
            for (var rowOffset = 0; rowOffset < targetRowCount; rowOffset++)
            {
                var rowId = orderedRowIds[startRowOffset + rowOffset];
                var row = model.GetRow(rowId);
                if (row.IsDeleted)
                {
                    return new CsvClipboardPastePlan(CsvClipboardPasteStatus.TargetOutsideSession, []);
                }

                for (var columnOffset = 0; columnOffset < targetColumnCount; columnOffset++)
                {
                    var columnIndex = startColumnIndex + columnOffset;
                    _ = row.Values[columnIndex];
                    edits.Add(new CsvClipboardCellEdit(
                        new CsvCellAddress(rowId, columnIndex),
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
    /// Applies the plan atomically to the pending row-edit model. If an unexpected model
    /// error occurs, already changed cells are restored before the exception is rethrown.
    /// No Scintilla or disk operation occurs here.
    /// </summary>
    public int Apply(CsvRowEditModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!IsReady)
        {
            throw new InvalidOperationException("Only a ready clipboard paste plan can be applied.");
        }

        var originals = new string[Edits.Count];
        for (var index = 0; index < Edits.Count; index++)
        {
            var edit = Edits[index];
            var row = model.GetRow(edit.Address.RowId);
            if (row.IsDeleted)
            {
                throw new InvalidOperationException("A clipboard target row is no longer editable.");
            }

            originals[index] = row.Values[edit.Address.ColumnIndex];
        }

        var changed = 0;
        var appliedCount = 0;
        try
        {
            for (; appliedCount < Edits.Count; appliedCount++)
            {
                var edit = Edits[appliedCount];
                if (model.SetCellValue(edit.Address.RowId, edit.Address.ColumnIndex, edit.Value))
                {
                    changed++;
                }
            }
        }
        catch
        {
            for (var index = appliedCount - 1; index >= 0; index--)
            {
                var edit = Edits[index];
                model.SetCellValue(edit.Address.RowId, edit.Address.ColumnIndex, originals[index]);
            }

            throw;
        }

        return changed;
    }
}
