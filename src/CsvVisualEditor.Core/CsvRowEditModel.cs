namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

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
/// It tracks insertion/deletion and creates deterministic minimal-difference
/// previews, but it never writes to Notepad++ or disk.
/// </summary>
public sealed class CsvRowEditModel
{
    private readonly CsvEditSession _session;
    private readonly string _sourcePrefix;
    private readonly Dictionary<int, SourceRecordState> _sourceRecordsByIndex;
    private readonly string _originalTerminalSeparator;
    private readonly List<RowState> _rows;
    private readonly Dictionary<CsvEditRowId, RowState> _rowsById;
    private readonly CsvEditRowId[] _baselineOrder;
    private long _nextInsertedSequence = -1;

    private CsvRowEditModel(
        CsvEditSession session,
        string sourcePrefix,
        Dictionary<int, SourceRecordState> sourceRecordsByIndex,
        string originalTerminalSeparator,
        List<RowState> rows)
    {
        _session = session;
        _sourcePrefix = sourcePrefix;
        _sourceRecordsByIndex = sourceRecordsByIndex;
        _originalTerminalSeparator = originalTerminalSeparator;
        ColumnCount = session.ColumnCount;
        _rows = rows;
        _rowsById = rows.ToDictionary(static row => row.Id);
        _baselineOrder = rows.Select(static row => row.Id).ToArray();
    }

    public int ColumnCount { get; }

    public int VisibleRowCount => _rows.Count(static row => !row.IsDeleted);

    public int InsertedRowCount => _rows.Count(static row => row.Id.IsInserted);

    public int DeletedRowCount => _rows.Count(
        static row => !row.Id.IsInserted && row.IsDeleted);

    public int ChangedCellCount => _session.ChangedCellCount;

    public int ChangedRowCount
    {
        get
        {
            var changedRows = _session.ChangedRecordCount +
                InsertedRowCount +
                DeletedRowCount;

            foreach (var row in _rows)
            {
                if (!row.Id.IsInserted &&
                    row.IsDeleted &&
                    IsSourceRowCellDirty(row.Id))
                {
                    changedRows--;
                }
            }

            return changedRows;
        }
    }

    public bool IsDirty => ChangedRowCount > 0;

    public static CsvRowEditModel Create(
        ActiveDocumentSnapshot snapshot,
        CsvParseResult parseResult,
        CsvEditSession session,
        CsvTableProjection projection)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(projection);

        ValidateInputs(snapshot, parseResult, session, projection);

        var sourcePrefix = parseResult.Records.Count == 0
            ? snapshot.Text
            : snapshot.Text[..parseResult.Records[0].SourceSpan.Start];
        var sourceRecords = CreateSourceRecordStates(snapshot.Text, parseResult.Records);
        ValidateExactSourceReconstruction(snapshot.Text, sourcePrefix, sourceRecords.Values);

        var rows = projection.Rows
            .Select(static projectedRow => new RowState(
                CsvEditRowId.FromSourceRecord(projectedRow.SourceRecordIndex),
                insertedValues: null))
            .ToList();
        var terminalSeparator = parseResult.Records.Count == 0
            ? string.Empty
            : sourceRecords[parseResult.Records[^1].Index].SeparatorAfter;

        return new CsvRowEditModel(
            session,
            sourcePrefix,
            sourceRecords,
            terminalSeparator,
            rows);
    }

    public IReadOnlyList<CsvEditRowSnapshot> GetVisibleRows()
    {
        return _rows
            .Where(static row => !row.IsDeleted)
            .Select(ToSnapshot)
            .ToArray();
    }

    public IReadOnlyList<CsvEditRowSnapshot> GetAllRows()
    {
        return _rows
            .Select(ToSnapshot)
            .ToArray();
    }

    public CsvEditRowSnapshot GetRow(CsvEditRowId id)
    {
        return ToSnapshot(GetState(id));
    }

    public bool SetCellValue(
        CsvEditRowId id,
        int columnIndex,
        string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateColumnIndex(columnIndex);

        var row = GetState(id);
        if (row.IsDeleted)
        {
            throw new InvalidOperationException(
                "A deleted row cannot be edited until it is restored.");
        }

        if (!row.Id.IsInserted)
        {
            return _session.SetCellValue(
                row.Id.SourceRecordIndex!.Value,
                columnIndex,
                value);
        }

        if (string.Equals(
                row.InsertedValues![columnIndex],
                value,
                StringComparison.Ordinal))
        {
            return false;
        }

        row.InsertedValues[columnIndex] = value;
        return true;
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

        _session.RevertAll();
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

    public CsvEditPreview CreatePreview()
    {
        var outputRows = CreateOutputRows();
        if (outputRows.Count == 0)
        {
            return new CsvEditPreview(
                _sourcePrefix,
                ChangedCellCount,
                ChangedRowCount,
                InsertedRowCount,
                DeletedRowCount);
        }

        var builder = new StringBuilder();
        builder.Append(_sourcePrefix);
        for (var index = 0; index < outputRows.Count; index++)
        {
            var outputRow = outputRows[index];
            AppendOutputRow(builder, outputRow);

            if (index + 1 < outputRows.Count)
            {
                builder.Append(GetBoundarySeparator(outputRow));
            }
            else
            {
                builder.Append(_originalTerminalSeparator);
            }
        }

        return new CsvEditPreview(
            builder.ToString(),
            ChangedCellCount,
            ChangedRowCount,
            InsertedRowCount,
            DeletedRowCount);
    }

    private IReadOnlyList<OutputRow> CreateOutputRows()
    {
        var outputRows = new List<OutputRow>(VisibleRowCount + 1);
        if (_session.HeaderSourceRecordIndex is int headerSourceRecordIndex)
        {
            outputRows.Add(OutputRow.FromSource(headerSourceRecordIndex));
        }

        foreach (var row in _rows)
        {
            if (row.IsDeleted)
            {
                continue;
            }

            outputRows.Add(row.Id.IsInserted
                ? OutputRow.FromInserted(row.InsertedValues!)
                : OutputRow.FromSource(row.Id.SourceRecordIndex!.Value));
        }

        return outputRows;
    }

    private void AppendOutputRow(StringBuilder builder, OutputRow outputRow)
    {
        if (outputRow.SourceRecordIndex is int sourceRecordIndex)
        {
            var sourceRecord = _sourceRecordsByIndex[sourceRecordIndex];
            if (!IsSourceRowCellDirty(CsvEditRowId.FromSourceRecord(sourceRecordIndex)))
            {
                builder.Append(sourceRecord.RawRecordText);
                return;
            }

            var values = GetSourceValues(sourceRecordIndex);
            builder.Append(CsvSerializer.SerializeRecord(
                values,
                _session.GetOriginalFieldCount(sourceRecordIndex),
                _session.SerializationPolicy));
            return;
        }

        builder.Append(CsvSerializer.SerializeRecord(
            outputRow.InsertedValues!,
            ColumnCount,
            _session.SerializationPolicy));
    }

    private string GetBoundarySeparator(OutputRow leftRow)
    {
        if (leftRow.SourceRecordIndex is int sourceRecordIndex)
        {
            var originalSeparator = _sourceRecordsByIndex[sourceRecordIndex].SeparatorAfter;
            if (originalSeparator.Length > 0)
            {
                return originalSeparator;
            }
        }

        return _session.SerializationPolicy.NewLine;
    }

    private CsvEditRowSnapshot ToSnapshot(RowState row)
    {
        return new CsvEditRowSnapshot(
            row.Id,
            row.Id.IsInserted
                ? row.InsertedValues!
                : GetSourceValues(row.Id.SourceRecordIndex!.Value),
            row.IsDeleted);
    }

    private string[] GetSourceValues(int sourceRecordIndex)
    {
        var values = new string[ColumnCount];
        for (var columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
        {
            values[columnIndex] = _session.GetValue(sourceRecordIndex, columnIndex);
        }

        return values;
    }

    private bool IsSourceRowCellDirty(CsvEditRowId id)
    {
        var sourceRecordIndex = id.SourceRecordIndex ??
            throw new ArgumentException(
                "A source-row dirty check requires a source row ID.",
                nameof(id));

        for (var columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
        {
            if (_session.IsCellDirty(sourceRecordIndex, columnIndex))
            {
                return true;
            }
        }

        return false;
    }

    private CsvEditRowId InsertAt(
        int index,
        IReadOnlyList<string>? values)
    {
        if (ColumnCount <= 0)
        {
            throw new InvalidOperationException(
                "A row cannot be inserted when the editable CSV has no columns.");
        }

        var id = CsvEditRowId.FromInsertedSequence(_nextInsertedSequence--);
        var row = new RowState(id, NormalizeValues(values));
        _rows.Insert(index, row);
        _rowsById.Add(id, row);
        return id;
    }

    private string[] NormalizeValues(IReadOnlyList<string>? values)
    {
        if (values is not null && values.Count > ColumnCount)
        {
            throw new ArgumentException(
                "An inserted row cannot contain more values than the editable table has columns.",
                nameof(values));
        }

        var normalized = new string[ColumnCount];
        Array.Fill(normalized, string.Empty);
        if (values is null)
        {
            return normalized;
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

    private void ValidateColumnIndex(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= ColumnCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(columnIndex),
                columnIndex,
                "The column index must identify an editable CSV column.");
        }
    }

    private static Dictionary<int, SourceRecordState> CreateSourceRecordStates(
        string snapshotText,
        IReadOnlyList<CsvRecord> sourceRecords)
    {
        var states = new Dictionary<int, SourceRecordState>(sourceRecords.Count);
        for (var index = 0; index < sourceRecords.Count; index++)
        {
            var sourceRecord = sourceRecords[index];
            var nextRecordStart = index + 1 < sourceRecords.Count
                ? sourceRecords[index + 1].SourceSpan.Start
                : snapshotText.Length;

            if (sourceRecord.SourceSpan.End > nextRecordStart ||
                nextRecordStart > snapshotText.Length)
            {
                throw new InvalidOperationException(
                    "Parser source spans do not form a valid ordered view of the editor snapshot.");
            }

            states.Add(
                sourceRecord.Index,
                new SourceRecordState(
                    sourceRecord.Index,
                    snapshotText.Substring(
                        sourceRecord.SourceSpan.Start,
                        sourceRecord.SourceSpan.Length),
                    snapshotText.Substring(
                        sourceRecord.SourceSpan.End,
                        nextRecordStart - sourceRecord.SourceSpan.End)));
        }

        return states;
    }

    private static void ValidateExactSourceReconstruction(
        string originalText,
        string sourcePrefix,
        IEnumerable<SourceRecordState> records)
    {
        var builder = new StringBuilder(originalText.Length);
        builder.Append(sourcePrefix);
        foreach (var record in records.OrderBy(static record => record.SourceRecordIndex))
        {
            builder.Append(record.RawRecordText);
            builder.Append(record.SeparatorAfter);
        }

        if (!string.Equals(originalText, builder.ToString(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Parser source spans do not reconstruct the active editor snapshot exactly.");
        }
    }

    private static void ValidateInputs(
        ActiveDocumentSnapshot snapshot,
        CsvParseResult parseResult,
        CsvEditSession session,
        CsvTableProjection projection)
    {
        CsvParseResultVerifier.EnsureMatchesSnapshot(snapshot.Text, parseResult);

        if (!string.Equals(
                snapshot.ContentSha256,
                session.Baseline.ContentSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The structural row model and edit session must use the same editor snapshot.");
        }

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
        public RowState(CsvEditRowId id, string[]? insertedValues)
        {
            Id = id;
            InsertedValues = insertedValues;
        }

        public CsvEditRowId Id { get; }

        public string[]? InsertedValues { get; }

        public bool IsDeleted { get; set; }
    }

    private sealed record SourceRecordState(
        int SourceRecordIndex,
        string RawRecordText,
        string SeparatorAfter);

    private sealed record OutputRow(
        int? SourceRecordIndex,
        IReadOnlyList<string>? InsertedValues)
    {
        public static OutputRow FromSource(int sourceRecordIndex)
        {
            return new OutputRow(sourceRecordIndex, InsertedValues: null);
        }

        public static OutputRow FromInserted(IReadOnlyList<string> values)
        {
            return new OutputRow(SourceRecordIndex: null, values);
        }
    }
}
