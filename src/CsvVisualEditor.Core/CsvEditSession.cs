namespace CsvVisualEditor.Core;

using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Host-independent editing session. It tracks cell changes and can prepare a
/// conflict-checked replacement preview, but it never writes to Notepad++ or disk.
/// </summary>
public sealed class CsvEditSession
{
    private readonly string _originalText;
    private readonly string _sourcePrefix;
    private readonly EditableRecord[] _records;
    private readonly IReadOnlyDictionary<int, EditableRecord> _recordsBySourceIndex;

    private CsvEditSession(
        ActiveDocumentSnapshot snapshot,
        CsvParseResult parseResult,
        CsvTableProjection projection,
        string sourcePrefix,
        EditableRecord[] records)
    {
        _originalText = snapshot.Text;
        _sourcePrefix = sourcePrefix;
        _records = records;
        _recordsBySourceIndex = records.ToDictionary(
            static record => record.SourceRecordIndex);

        Baseline = CsvEditSessionBaseline.Create(snapshot);
        SerializationPolicy = CsvSerializationPolicy.Detect(
            snapshot.Text,
            parseResult.Dialect);
        HeaderSourceRecordIndex = projection.HeaderSourceRecordIndex;
        ColumnCount = projection.ColumnCount;
        SourceRecordIndexes = Array.AsReadOnly(
            records.Select(static record => record.SourceRecordIndex).ToArray());
    }

    public CsvEditSessionBaseline Baseline { get; }

    public CsvSerializationPolicy SerializationPolicy { get; }

    public int? HeaderSourceRecordIndex { get; }

    public int ColumnCount { get; }

    public ReadOnlyCollection<int> SourceRecordIndexes { get; }

    public int RecordCount => _records.Length;

    public int ChangedCellCount { get; private set; }

    public int ChangedRecordCount { get; private set; }

    public bool IsDirty => ChangedCellCount > 0;

    public static CsvEditSession Create(
        ActiveDocumentSnapshot snapshot,
        CsvParseResult parseResult,
        CsvTableProjection projection)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(projection);

        ValidateEditableProjection(snapshot, parseResult, projection);

        if (parseResult.Records.Count == 0)
        {
            return new CsvEditSession(
                snapshot,
                parseResult,
                projection,
                snapshot.Text,
                []);
        }

        var sourcePrefix = snapshot.Text[..parseResult.Records[0].SourceSpan.Start];
        var records = CreateEditableRecords(
            snapshot.Text,
            parseResult.Records,
            projection.ColumnCount);

        ValidateExactSourceReconstruction(snapshot.Text, sourcePrefix, records);

        return new CsvEditSession(
            snapshot,
            parseResult,
            projection,
            sourcePrefix,
            records);
    }

    public string GetValue(int sourceRecordIndex, int columnIndex)
    {
        var record = GetRecord(sourceRecordIndex);
        ValidateColumnIndex(columnIndex);
        return record.CurrentValues[columnIndex];
    }

    public string GetOriginalValue(int sourceRecordIndex, int columnIndex)
    {
        var record = GetRecord(sourceRecordIndex);
        ValidateColumnIndex(columnIndex);
        return record.OriginalValues[columnIndex];
    }

    public int GetOriginalFieldCount(int sourceRecordIndex)
    {
        return GetRecord(sourceRecordIndex).OriginalFieldCount;
    }

    public bool IsCellDirty(int sourceRecordIndex, int columnIndex)
    {
        var record = GetRecord(sourceRecordIndex);
        ValidateColumnIndex(columnIndex);
        return record.DirtyCells[columnIndex];
    }

    public bool SetCellValue(
        int sourceRecordIndex,
        int columnIndex,
        string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var record = GetRecord(sourceRecordIndex);
        ValidateColumnIndex(columnIndex);

        if (string.Equals(
                record.CurrentValues[columnIndex],
                value,
                StringComparison.Ordinal))
        {
            return false;
        }

        var recordWasDirty = record.DirtyCellCount > 0;
        var cellWasDirty = record.DirtyCells[columnIndex];

        record.CurrentValues[columnIndex] = value;
        var cellIsDirty = !string.Equals(
            record.OriginalValues[columnIndex],
            value,
            StringComparison.Ordinal);
        record.DirtyCells[columnIndex] = cellIsDirty;

        if (cellWasDirty != cellIsDirty)
        {
            record.DirtyCellCount += cellIsDirty ? 1 : -1;
            ChangedCellCount += cellIsDirty ? 1 : -1;
        }

        var recordIsDirty = record.DirtyCellCount > 0;
        if (recordWasDirty != recordIsDirty)
        {
            ChangedRecordCount += recordIsDirty ? 1 : -1;
        }

        return true;
    }

    public bool RevertCell(int sourceRecordIndex, int columnIndex)
    {
        return SetCellValue(
            sourceRecordIndex,
            columnIndex,
            GetOriginalValue(sourceRecordIndex, columnIndex));
    }

    public bool RevertAll()
    {
        if (!IsDirty)
        {
            return false;
        }

        foreach (var record in _records)
        {
            record.Reset();
        }

        ChangedCellCount = 0;
        ChangedRecordCount = 0;
        return true;
    }

    public CsvEditPreview CreatePreview()
    {
        if (!IsDirty)
        {
            return new CsvEditPreview(
                _originalText,
                changedCellCount: 0,
                changedRecordCount: 0);
        }

        var builder = new StringBuilder(_originalText.Length);
        builder.Append(_sourcePrefix);
        foreach (var record in _records)
        {
            if (record.DirtyCellCount == 0)
            {
                builder.Append(record.RawRecordText);
            }
            else
            {
                builder.Append(CsvSerializer.SerializeRecord(
                    record.CurrentValues,
                    record.GetSerializedFieldCount(),
                    SerializationPolicy));
            }

            builder.Append(record.SeparatorAfter);
        }

        return new CsvEditPreview(
            builder.ToString(),
            ChangedCellCount,
            ChangedRecordCount);
    }

    public CsvEditApplyPlan CreateApplyPlan(ActiveDocumentSnapshot currentSnapshot)
    {
        ArgumentNullException.ThrowIfNull(currentSnapshot);

        if (!IsDirty)
        {
            return CsvEditApplyPlan.Create(
                CsvEditApplyStatus.NoChanges,
                preview: null,
                changedCellCount: 0);
        }

        if (!Baseline.IsSameDocument(currentSnapshot))
        {
            return CsvEditApplyPlan.Create(
                CsvEditApplyStatus.DocumentIdentityChanged,
                preview: null,
                ChangedCellCount);
        }

        if (Baseline.CodePage != currentSnapshot.CodePage)
        {
            return CsvEditApplyPlan.Create(
                CsvEditApplyStatus.CodePageChanged,
                preview: null,
                ChangedCellCount);
        }

        if (!string.Equals(
                Baseline.ContentSha256,
                currentSnapshot.ContentSha256,
                StringComparison.Ordinal))
        {
            return CsvEditApplyPlan.Create(
                CsvEditApplyStatus.ContentChanged,
                preview: null,
                ChangedCellCount);
        }

        return CsvEditApplyPlan.Create(
            CsvEditApplyStatus.Ready,
            CreatePreview(),
            ChangedCellCount);
    }

    private static EditableRecord[] CreateEditableRecords(
        string snapshotText,
        IReadOnlyList<CsvRecord> sourceRecords,
        int columnCount)
    {
        var records = new EditableRecord[sourceRecords.Count];
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

            var originalValues = new string[columnCount];
            Array.Fill(originalValues, string.Empty);
            for (var fieldIndex = 0; fieldIndex < sourceRecord.Cells.Count; fieldIndex++)
            {
                originalValues[fieldIndex] = sourceRecord.Cells[fieldIndex].Value;
            }

            records[index] = new EditableRecord(
                sourceRecord.Index,
                sourceRecord.FieldCount,
                originalValues,
                snapshotText.Substring(
                    sourceRecord.SourceSpan.Start,
                    sourceRecord.SourceSpan.Length),
                snapshotText.Substring(
                    sourceRecord.SourceSpan.End,
                    nextRecordStart - sourceRecord.SourceSpan.End));
        }

        return records;
    }

    private static void ValidateEditableProjection(
        ActiveDocumentSnapshot snapshot,
        CsvParseResult parseResult,
        CsvTableProjection projection)
    {
        if (parseResult.HasErrors)
        {
            throw new InvalidOperationException(
                "Editing cannot start while the parsed CSV contains errors.");
        }

        CsvParseResultVerifier.EnsureMatchesSnapshot(snapshot.Text, parseResult);

        if (projection.IsRowLimited ||
            projection.DisplayedRowCount != projection.TotalDataRecordCount)
        {
            throw new InvalidOperationException(
                "Editing cannot start from a row-limited visual projection.");
        }

        var maximumFieldCount = parseResult.Records.Count == 0
            ? 0
            : parseResult.Records.Max(static record => record.FieldCount);
        if (projection.ColumnCount != maximumFieldCount)
        {
            throw new InvalidOperationException(
                "The visual projection does not contain every parsed CSV column.");
        }

        var headerCount = projection.HeaderSourceRecordIndex.HasValue ? 1 : 0;
        var expectedDataRecordCount = parseResult.Records.Count - headerCount;
        if (projection.TotalDataRecordCount != expectedDataRecordCount)
        {
            throw new InvalidOperationException(
                "The visual projection does not represent every parsed data record.");
        }

        var expectedSourceIndexes = parseResult.Records
            .Where(record => record.Index != projection.HeaderSourceRecordIndex)
            .Select(static record => record.Index);
        if (!expectedSourceIndexes.SequenceEqual(
                projection.Rows.Select(static row => row.SourceRecordIndex)))
        {
            throw new InvalidOperationException(
                "The visual projection record order differs from the parser source order.");
        }

        foreach (var record in parseResult.Records)
        {
            if (record.SourceSpan.End > snapshot.Text.Length)
            {
                throw new InvalidOperationException(
                    "Parser source spans exceed the active editor snapshot.");
            }
        }
    }

    private static void ValidateExactSourceReconstruction(
        string originalText,
        string sourcePrefix,
        IEnumerable<EditableRecord> records)
    {
        var builder = new StringBuilder(originalText.Length);
        builder.Append(sourcePrefix);
        foreach (var record in records)
        {
            builder.Append(record.RawRecordText);
            builder.Append(record.SeparatorAfter);
        }

        if (!string.Equals(
                originalText,
                builder.ToString(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Parser source spans do not reconstruct the active editor snapshot exactly.");
        }
    }

    private EditableRecord GetRecord(int sourceRecordIndex)
    {
        if (!_recordsBySourceIndex.TryGetValue(sourceRecordIndex, out var record))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceRecordIndex),
                sourceRecordIndex,
                "The source record is not part of this edit session.");
        }

        return record;
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

    private sealed class EditableRecord
    {
        public EditableRecord(
            int sourceRecordIndex,
            int originalFieldCount,
            string[] originalValues,
            string rawRecordText,
            string separatorAfter)
        {
            SourceRecordIndex = sourceRecordIndex;
            OriginalFieldCount = originalFieldCount;
            OriginalValues = originalValues;
            CurrentValues = (string[])originalValues.Clone();
            DirtyCells = new bool[originalValues.Length];
            RawRecordText = rawRecordText;
            SeparatorAfter = separatorAfter;
        }

        public int SourceRecordIndex { get; }

        public int OriginalFieldCount { get; }

        public string[] OriginalValues { get; }

        public string[] CurrentValues { get; }

        public bool[] DirtyCells { get; }

        public string RawRecordText { get; }

        public string SeparatorAfter { get; }

        public int DirtyCellCount { get; set; }

        public int GetSerializedFieldCount()
        {
            for (var index = CurrentValues.Length - 1;
                 index >= OriginalFieldCount;
                 index--)
            {
                if (CurrentValues[index].Length > 0)
                {
                    return index + 1;
                }
            }

            return OriginalFieldCount;
        }

        public void Reset()
        {
            Array.Copy(OriginalValues, CurrentValues, OriginalValues.Length);
            Array.Clear(DirtyCells);
            DirtyCellCount = 0;
        }
    }
}

public sealed record CsvEditSessionBaseline
{
    private CsvEditSessionBaseline(
        string documentPath,
        string displayName,
        string contentSha256,
        int codePage,
        DateTimeOffset capturedAtUtc)
    {
        DocumentPath = documentPath;
        DisplayName = displayName;
        ContentSha256 = contentSha256;
        CodePage = codePage;
        CapturedAtUtc = capturedAtUtc;
    }

    public string DocumentPath { get; }

    public string DisplayName { get; }

    public string ContentSha256 { get; }

    public int CodePage { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    internal static CsvEditSessionBaseline Create(ActiveDocumentSnapshot snapshot)
    {
        return new CsvEditSessionBaseline(
            snapshot.DocumentPath,
            snapshot.DisplayName,
            snapshot.ContentSha256,
            snapshot.CodePage,
            snapshot.CapturedAtUtc);
    }

    internal bool IsSameDocument(ActiveDocumentSnapshot snapshot)
    {
        if (DocumentPath.Length > 0 || snapshot.DocumentPath.Length > 0)
        {
            return string.Equals(
                DocumentPath,
                snapshot.DocumentPath,
                StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(
            DisplayName,
            snapshot.DisplayName,
            StringComparison.Ordinal);
    }
}

public sealed record CsvEditPreview
{
    public CsvEditPreview(
        string text,
        int changedCellCount,
        int changedRecordCount)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (changedCellCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(changedCellCount));
        }

        if (changedRecordCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(changedRecordCount));
        }

        Text = text;
        ChangedCellCount = changedCellCount;
        ChangedRecordCount = changedRecordCount;
        ContentSha256 = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(text)))
            .ToLowerInvariant();
    }

    public string Text { get; }

    public int ChangedCellCount { get; }

    public int ChangedRecordCount { get; }

    public string ContentSha256 { get; }

    public bool HasChanges => ChangedCellCount > 0;
}

public enum CsvEditApplyStatus
{
    NoChanges,
    Ready,
    DocumentIdentityChanged,
    CodePageChanged,
    ContentChanged
}

public sealed record CsvEditApplyPlan
{
    private CsvEditApplyPlan(
        CsvEditApplyStatus status,
        CsvEditPreview? preview,
        int changedCellCount)
    {
        Status = status;
        Preview = preview;
        ChangedCellCount = changedCellCount;
    }

    public CsvEditApplyStatus Status { get; }

    public CsvEditPreview? Preview { get; }

    public int ChangedCellCount { get; }

    public bool IsReady => Status == CsvEditApplyStatus.Ready;

    internal static CsvEditApplyPlan Create(
        CsvEditApplyStatus status,
        CsvEditPreview? preview,
        int changedCellCount)
    {
        if (changedCellCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(changedCellCount));
        }

        if (status == CsvEditApplyStatus.Ready && preview is null)
        {
            throw new ArgumentException(
                "A ready apply plan requires a serialized preview.",
                nameof(preview));
        }

        if (status != CsvEditApplyStatus.Ready && preview is not null)
        {
            throw new ArgumentException(
                "Only a ready apply plan may contain a serialized preview.",
                nameof(preview));
        }

        return new CsvEditApplyPlan(status, preview, changedCellCount);
    }
}
