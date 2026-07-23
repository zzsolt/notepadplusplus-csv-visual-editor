namespace CsvVisualEditor.Core;

/// <summary>
/// Immutable outcome of one prevalidated batch row-deletion request.
/// TargetRowCount is the number of unique stable IDs supplied by the caller;
/// already-deleted source rows are valid targets but are not counted again as
/// affected rows.
/// </summary>
public sealed record CsvBatchDeleteResult
{
    internal CsvBatchDeleteResult(
        int targetRowCount,
        int deletedSourceRowCount,
        int cancelledInsertedRowCount,
        int remainingVisibleRowCount)
    {
        if (targetRowCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetRowCount));
        }

        if (deletedSourceRowCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deletedSourceRowCount));
        }

        if (cancelledInsertedRowCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cancelledInsertedRowCount));
        }

        if (remainingVisibleRowCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingVisibleRowCount));
        }

        if (deletedSourceRowCount + cancelledInsertedRowCount > targetRowCount)
        {
            throw new ArgumentException(
                "The affected row count cannot exceed the unique target row count.");
        }

        TargetRowCount = targetRowCount;
        DeletedSourceRowCount = deletedSourceRowCount;
        CancelledInsertedRowCount = cancelledInsertedRowCount;
        RemainingVisibleRowCount = remainingVisibleRowCount;
    }

    public int TargetRowCount { get; }

    public int DeletedSourceRowCount { get; }

    public int CancelledInsertedRowCount { get; }

    public int AffectedRowCount => DeletedSourceRowCount + CancelledInsertedRowCount;

    public int RemainingVisibleRowCount { get; }

    public bool HasChanges => AffectedRowCount > 0;
}

/// <summary>
/// Atomic, host-independent batch operations over a structural row model.
/// The complete stable-ID set is normalized and validated before the first
/// mutation. UI row objects, indexes, and selection enumeration order never
/// become model identity.
/// </summary>
public static class CsvRowEditModelBatchOperations
{
    public static CsvBatchDeleteResult DeleteRows(
        this CsvRowEditModel model,
        IEnumerable<CsvEditRowId> rowIds)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(rowIds);

        var uniqueIds = new HashSet<CsvEditRowId>();
        foreach (var rowId in rowIds)
        {
            uniqueIds.Add(rowId);
        }

        if (uniqueIds.Count == 0)
        {
            return new CsvBatchDeleteResult(
                targetRowCount: 0,
                deletedSourceRowCount: 0,
                cancelledInsertedRowCount: 0,
                model.VisibleRowCount);
        }

        // GetAllRows is an immutable snapshot in structural source order. It
        // includes already-deleted source rows and every live inserted row.
        var allRows = model.GetAllRows();
        var knownIds = allRows
            .Select(static row => row.Id)
            .ToHashSet();

        foreach (var rowId in uniqueIds)
        {
            if (!knownIds.Contains(rowId))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rowIds),
                    rowId,
                    "Every batch-delete row ID must belong to the current structural edit model.");
            }
        }

        var deletedSourceRows = 0;
        var cancelledInsertedRows = 0;

        // Execute in model order, never in caller enumeration order. All IDs
        // have already been validated, so no expected validation failure can
        // leave a partially applied batch.
        foreach (var row in allRows)
        {
            if (!uniqueIds.Contains(row.Id))
            {
                continue;
            }

            if (row.IsInserted)
            {
                if (!model.DeleteRow(row.Id))
                {
                    throw new InvalidOperationException(
                        "A validated inserted row could not be cancelled during batch deletion.");
                }

                cancelledInsertedRows++;
                continue;
            }

            if (row.IsDeleted)
            {
                continue;
            }

            if (!model.DeleteRow(row.Id))
            {
                throw new InvalidOperationException(
                    "A validated source row could not be marked for deletion during batch deletion.");
            }

            deletedSourceRows++;
        }

        return new CsvBatchDeleteResult(
            uniqueIds.Count,
            deletedSourceRows,
            cancelledInsertedRows,
            model.VisibleRowCount);
    }
}
