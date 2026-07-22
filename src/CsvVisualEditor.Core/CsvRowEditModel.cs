namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;
using System.Globalization;

/// <summary>
/// Stable identity for one editable CSV data row. Source rows retain their
/// parser record index; inserted rows receive negative session-local IDs.
/// </summary>
public readonly record struct CsvEditRowId
{
    private CsvEditRowId(long value)
    {
        Value = value;
    }

    public long Value { get; }

    public bool IsInserted => Value < 0;

    public int? SourceRecordIndex => Value >= 0 && Value <= int.MaxValue
        ? (int)Value
        : null;

    internal static CsvEditRowId FromSourceRecord(int sourceRecordIndex)
    {
        if (sourceRecordIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRecordIndex));
        }

        return new CsvEditRowId(sourceRecordIndex);
    }

    internal static CsvEditRowId FromInsertedSequence(long sequence)
    {
        if (sequence >= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        return new CsvEditRowId(sequence);
    }

    public override string ToString()
    {
        return IsInserted
            ? $"new:{Math.Abs(Value).ToString(CultureInfo.InvariantCulture)}"
            : $"source:{Value.ToString(CultureInfo.InvariantCulture)}";
    }
}

/// <summary>
/// Immutable public view of one structural row state.
/// </summary>
public sealed record CsvEditRowSnapshot
{
    internal CsvEditRowSnapshot(
        CsvEditRowId id,
        IReadOnlyList<string> values,
        bool isDeleted)
    {
        Id = id;
        Values = Array.AsReadOnly(values.ToArray());
        IsDeleted = isDeleted;
    }

    public CsvEditRowId Id { get; }

    public int? SourceRecordIndex => Id.SourceRecordIndex;

    public bool IsInserted => Id.IsInserted;

    public bool IsDeleted { get; }

    public ReadOnlyCollection<string> Values { get; }
}

/// <summary>
/// Host-independent structural row editor layered over a CsvEditSession.
/// It tracks insertion/deletion and stable row identity, but it does not
/// serialize, write to Notepad++, or modify the wrapped cell-edit session.
/// </summary>
public sealed class CsvRowEditModel
{
    private readonly List<RowState> _rows;
    private readonly Dictionary<CsvEditRowId, RowState> _rowsById;
    private readonly CsvEditRowId[] _baselineOrder;
    private long _nextInsertedSequence = -1;

    private CsvRowEditModel(int columnCount, List<RowState> rows)
    {
        ColumnCount = columnCount;
        _rows = rows;
        _rowsById = rows.ToDictionary(static row => row.Id);
        _baselineOrder = rows.Select(static row => row.Id).ToArray();
    }

    public int ColumnCount { get; }

    public int VisibleRowCount => _rows.Count(static row => !row.IsDeleted);

    public int InsertedRowCount => _rows.Count(static row => row.Id.IsInserted);

    public int DeletedRowCount => _rows.Count(
        static row => !row.Id.IsInserted && row.IsDeleted);

    public int ChangedRowCount => InsertedRowCount + DeletedRowCount;

    public bool IsDirty => ChangedRowCount > 0;

    public static CsvRowEditModel Create(
        CsvEditSession session,
        CsvTableProjection projection)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(projection);

        ValidateProjection(session, projection);

        var rows = new List<RowState>(projection.Rows.Count);
        foreach (var projectedRow in projection.Rows)
        {
            var values = new string[session.ColumnCount];
            for (var columnIndex = 0;
                 columnIndex < session.ColumnCount;
                 columnIndex++)
            {
                values[columnIndex] = session.GetValue(
                    projectedRow.SourceRecordIndex,
                    columnIndex);
            }

            rows.Add(new RowState(
                CsvEditRowId.FromSourceRecord(projectedRow.SourceRecordIndex),
                values));
        }

        return new CsvRowEditModel(session.ColumnCount, rows);
    }

    public IReadOnlyList<CsvEditRowSnapshot> GetVisibleRows()
    {
        return _rows
            .Where(static row => !row.IsDeleted)
            .Select(static row => row.ToSnapshot())
            .ToArray();
    }

    public IReadOnlyList<CsvEditRowSnapshot> GetAllRows()
    {
        return _rows
            .Select(static row => row.ToSnapshot())
            .ToArray();
    }

    public CsvEditRowSnapshot GetRow(CsvEditRowId id)
    {
        return GetState(id).ToSnapshot();
    }

    public CsvEditRowId AppendRow(IReadOnlyList<string>? values = null)
    {
        return InsertAt(_rows.Count, values);
    }

    public CsvEditRowId InsertRowBefore(
        CsvEditRowId anchorId,
        IReadOnlyList<string>? values = null)
    {
        var anchor = GetVisibleAnchor(anchorId);
        return InsertAt(_rows.IndexOf(anchor), values);
    }

    public CsvEditRowId InsertRowAfter(
        CsvEditRowId anchorId,
        IReadOnlyList<string>? values = null)
    {
        var anchor = GetVisibleAnchor(anchorId);
        return InsertAt(_rows.IndexOf(anchor) + 1, values);
    }

    public bool DeleteRow(CsvEditRowId id)
    {
        var row = GetState(id);
        if (row.Id.IsInserted)
        {
            _rows.Remove(row);
            _rowsById.Remove(row.Id);
            return true;
        }

        if (row.IsDeleted)
        {
            return false;
        }

        row.IsDeleted = true;
        return true;
    }

    public bool RestoreDeletedRow(CsvEditRowId id)
    {
        var row = GetState(id);
        if (row.Id.IsInserted)
        {
            throw new InvalidOperationException(
                "An inserted row that was deleted cancels its insertion and cannot be restored by ID.");
        }

        if (!row.IsDeleted)
        {
            return false;
        }

        row.IsDeleted = false;
        return true;
    }

    public bool RevertAll()
    {
        if (!IsDirty)
        {
            return false;
        }

        _rows.RemoveAll(static row => row.Id.IsInserted);
        _rowsById.Clear();

        var sourceRowsById = _rows.ToDictionary(static row => row.Id);
        _rows.Clear();
        foreach (var id in _baselineOrder)
        {
            var row = sourceRowsById[id];
            row.IsDeleted = false;
            _rows.Add(row);
            _rowsById.Add(id, row);
        }

        return true;
    }

    private CsvEditRowId InsertAt(
        int index,
        IReadOnlyList<string>? values)
    {
        var id = CsvEditRowId.FromInsertedSequence(_nextInsertedSequence--);
        var row = new RowState(id, NormalizeValues(values));
        _rows.Insert(index, row);
        _rowsById.Add(id, row);
        return id;
    }

    private string[] NormalizeValues(IReadOnlyList<string>? values)
    {
        var normalized = new string[ColumnCount];
        Array.Fill(normalized, string.Empty);

        if (values is null)
        {
            return normalized;
        }

        if (values.Count > ColumnCount)
        {
            throw new ArgumentException(
                "An inserted row cannot contain more values than the editable table has columns.",
                nameof(values));
        }

        for (var index = 0; index < values.Count; index++)
        {
            normalized[index] = values[index] ?? throw new ArgumentException(
                "Inserted row values cannot contain null entries.",
                nameof(values));
        }

        return normalized;
    }

    private RowState GetVisibleAnchor(CsvEditRowId id)
    {
        var row = GetState(id);
        if (row.IsDeleted)
        {
            throw new InvalidOperationException(
                "A deleted row cannot be used as an insertion anchor.");
        }

        return row;
    }

    private RowState GetState(CsvEditRowId id)
    {
        if (!_rowsById.TryGetValue(id, out var row))
        {
            throw new ArgumentOutOfRangeException(
                nameof(id),
                id,
                "The row ID is not part of this structural edit model.");
        }

        return row;
    }

    private static void ValidateProjection(
        CsvEditSession session,
        CsvTableProjection projection)
    {
        if (projection.ColumnCount != session.ColumnCount)
        {
            throw new InvalidOperationException(
                "The structural row model requires the same complete column set as the edit session.");
        }

        if (projection.IsRowLimited ||
            projection.DisplayedRowCount != projection.TotalDataRecordCount)
        {
            throw new InvalidOperationException(
                "Structural row editing cannot start from a row-limited projection.");
        }

        var expectedSourceIndexes = session.SourceRecordIndexes
            .Where(index => index != session.HeaderSourceRecordIndex);
        var projectedSourceIndexes = projection.Rows
            .Select(static row => row.SourceRecordIndex);
        if (!expectedSourceIndexes.SequenceEqual(projectedSourceIndexes))
        {
            throw new InvalidOperationException(
                "The structural row model requires complete source-order data rows.");
        }
    }

    private sealed class RowState
    {
        public RowState(CsvEditRowId id, string[] values)
        {
            Id = id;
            Values = values;
        }

        public CsvEditRowId Id { get; }

        public string[] Values { get; }

        public bool IsDeleted { get; set; }

        public CsvEditRowSnapshot ToSnapshot()
        {
            return new CsvEditRowSnapshot(Id, Values, IsDeleted);
        }
    }
}
