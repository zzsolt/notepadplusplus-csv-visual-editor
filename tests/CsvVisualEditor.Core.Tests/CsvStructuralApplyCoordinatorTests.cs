namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using System.Text;
using Xunit;

public sealed class CsvStructuralApplyCoordinatorTests
{
    [Fact]
    public void Execute_NoStructuralChanges_DoesNotCallEditor()
    {
        var context = CreateContext("A,B\n1,2\n3,4");
        var target = new RecordingTarget();

        var result = CsvEditorApplyCoordinator.Execute(
            context.Model,
            context.Snapshot,
            target);

        Assert.Equal(CsvEditorApplyStatus.NoChanges, result.Status);
        Assert.False(result.WasApplied);
        Assert.Equal(0, result.ChangedCellCount);
        Assert.Equal(0, result.ChangedRecordCount);
        Assert.Equal(0, result.InsertedRowCount);
        Assert.Equal(0, result.DeletedRowCount);
        Assert.Null(result.ReplacementSha256);
        Assert.Empty(target.Calls);
    }

    [Theory]
    [InlineData(ConflictKind.Document, CsvEditorApplyStatus.DocumentIdentityChanged)]
    [InlineData(ConflictKind.CodePage, CsvEditorApplyStatus.CodePageChanged)]
    [InlineData(ConflictKind.Content, CsvEditorApplyStatus.ContentChanged)]
    public void Execute_StructuralConflict_DoesNotCallEditor(
        ConflictKind conflictKind,
        CsvEditorApplyStatus expectedStatus)
    {
        var context = CreateContext("A,B\n1,2\n3,4");
        context.Model.AppendRow(["5", "6"]);
        var currentSnapshot = conflictKind switch
        {
            ConflictKind.Document => CreateSnapshot(
                context.Snapshot.Text,
                documentPath: @"C:\Data\other.csv"),
            ConflictKind.CodePage => CreateSnapshot(
                context.Snapshot.Text,
                codePage: 1250),
            ConflictKind.Content => CreateSnapshot("A,B\n1,external\n3,4"),
            _ => throw new ArgumentOutOfRangeException(nameof(conflictKind))
        };
        var target = new RecordingTarget();

        var plan = context.Model.CreateApplyPlan(currentSnapshot);
        var result = CsvEditorApplyCoordinator.Execute(
            context.Model,
            currentSnapshot,
            target);

        Assert.Equal(
            expectedStatus switch
            {
                CsvEditorApplyStatus.DocumentIdentityChanged =>
                    CsvEditApplyStatus.DocumentIdentityChanged,
                CsvEditorApplyStatus.CodePageChanged => CsvEditApplyStatus.CodePageChanged,
                CsvEditorApplyStatus.ContentChanged => CsvEditApplyStatus.ContentChanged,
                _ => throw new ArgumentOutOfRangeException(nameof(expectedStatus))
            },
            plan.Status);
        Assert.False(plan.IsReady);
        Assert.Null(plan.ReplacementText);
        Assert.Null(plan.ReplacementSha256);
        Assert.Equal(expectedStatus, result.Status);
        Assert.False(result.WasApplied);
        Assert.False(result.SelectionRestored);
        Assert.Equal(0, result.ChangedCellCount);
        Assert.Equal(1, result.ChangedRecordCount);
        Assert.Equal(1, result.InsertedRowCount);
        Assert.Equal(0, result.DeletedRowCount);
        Assert.Null(result.ReplacementSha256);
        Assert.Empty(target.Calls);
    }

    [Fact]
    public void Execute_ReadyStructuralPlan_UsesSingleUndoReplacementPath()
    {
        var context = CreateContext(
            "A,B\r\n1,2\n3,4\r\n",
            caretPosition: 99,
            anchorPosition: 3);
        var rows = context.Model.GetVisibleRows();
        context.Model.SetCellValue(rows[0].Id, 1, "x,y");
        context.Model.DeleteRow(rows[1].Id);
        context.Model.AppendRow(["5", "6"]);
        var expectedText = "A,B\r\n1,\"x,y\"\n5,6\r\n";
        var target = new RecordingTarget
        {
            ReplacementByteLength = Encoding.UTF8.GetByteCount(expectedText)
        };

        var plan = context.Model.CreateApplyPlan(context.Snapshot);
        var result = CsvEditorApplyCoordinator.Execute(
            context.Model,
            context.Snapshot,
            target);

        Assert.True(plan.IsReady);
        Assert.Equal(expectedText, plan.ReplacementText);
        Assert.NotNull(plan.ReplacementSha256);
        Assert.Equal(CsvEditorApplyStatus.Applied, result.Status);
        Assert.True(result.WasApplied);
        Assert.True(result.SelectionRestored);
        Assert.Equal(1, result.ChangedCellCount);
        Assert.Equal(3, result.ChangedRecordCount);
        Assert.Equal(1, result.InsertedRowCount);
        Assert.Equal(1, result.DeletedRowCount);
        Assert.Equal(plan.ReplacementSha256, result.ReplacementSha256);
        Assert.Equal(
            [
                "BeginUndoAction",
                $"ReplaceWholeDocument:{expectedText}",
                $"SetSelection:3:{Encoding.UTF8.GetByteCount(expectedText)}",
                "EndUndoAction"
            ],
            target.Calls);
    }

    [Fact]
    public void Execute_StructuralSelectionFailure_StillReportsAppliedAndEndsUndo()
    {
        var context = CreateContext("A,B\n1,2");
        context.Model.AppendRow(["3", "4"]);
        var target = new RecordingTarget
        {
            ReplacementByteLength = 13,
            SelectionException = new InvalidOperationException("synthetic selection failure")
        };

        var result = CsvEditorApplyCoordinator.Execute(
            context.Model,
            context.Snapshot,
            target);

        Assert.True(result.WasApplied);
        Assert.False(result.SelectionRestored);
        Assert.Equal(
            [
                "BeginUndoAction",
                "ReplaceWholeDocument:A,B\n1,2\n3,4",
                "SetSelection:3:7",
                "EndUndoAction"
            ],
            target.Calls);
    }

    [Fact]
    public void Execute_StructuralReplaceFailure_StillEndsUndo()
    {
        var context = CreateContext("A,B\n1,2");
        context.Model.AppendRow(["3", "4"]);
        var target = new RecordingTarget
        {
            ReplaceException = new InvalidOperationException("synthetic structural replace failure")
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CsvEditorApplyCoordinator.Execute(
                context.Model,
                context.Snapshot,
                target));

        Assert.Equal("synthetic structural replace failure", exception.Message);
        Assert.Equal(
            [
                "BeginUndoAction",
                "ReplaceWholeDocument:A,B\n1,2\n3,4",
                "EndUndoAction"
            ],
            target.Calls);
    }

    [Fact]
    public void Create_ModelRejectsDifferentDocumentIdentityCodePageAndDialect()
    {
        const string source = "A,B;C\n1,2;3";
        var original = CreateContext(source);
        var differentDocumentSnapshot = CreateSnapshot(
            source,
            documentPath: @"C:\Data\other.csv");
        var differentCodePageSnapshot = CreateSnapshot(source, codePage: 1250);
        var semicolonDialect = CsvDialect.Create(
            ';',
            headerMode: CsvHeaderMode.FirstRecord);
        var semicolonParseResult = CsvParser.Parse(source, semicolonDialect);
        var semicolonProjection = CsvTableProjector.Create(
            semicolonParseResult,
            CreateProjectionOptions());

        Assert.Throws<InvalidOperationException>(() =>
            CsvRowEditModel.Create(
                differentDocumentSnapshot,
                original.ParseResult,
                original.Session,
                original.Projection));
        Assert.Throws<InvalidOperationException>(() =>
            CsvRowEditModel.Create(
                differentCodePageSnapshot,
                original.ParseResult,
                original.Session,
                original.Projection));
        Assert.Throws<InvalidOperationException>(() =>
            CsvRowEditModel.Create(
                original.Snapshot,
                semicolonParseResult,
                original.Session,
                semicolonProjection));
    }

    private static BuildContext CreateContext(
        string text,
        string documentPath = @"C:\Data\sample.csv",
        int codePage = 65001,
        long caretPosition = 7,
        long anchorPosition = 3)
    {
        var snapshot = CreateSnapshot(
            text,
            documentPath,
            codePage,
            caretPosition,
            anchorPosition);
        var dialect = CsvDialect.Create(
            ',',
            headerMode: CsvHeaderMode.FirstRecord);
        var parseResult = CsvParser.Parse(text, dialect);
        var projection = CsvTableProjector.Create(
            parseResult,
            CreateProjectionOptions());
        var session = CsvEditSession.Create(snapshot, parseResult, projection);
        var model = CsvRowEditModel.Create(
            snapshot,
            parseResult,
            session,
            projection);

        return new BuildContext(
            snapshot,
            parseResult,
            projection,
            session,
            model);
    }

    private static CsvTableProjectionOptions CreateProjectionOptions() =>
        new()
        {
            HeaderMode = CsvHeaderMode.FirstRecord,
            MaximumRows = 10_000,
            MaximumColumns = 512,
            MaximumCells = 250_000
        };

    private static ActiveDocumentSnapshot CreateSnapshot(
        string text,
        string documentPath = @"C:\Data\sample.csv",
        int codePage = 65001,
        long caretPosition = 7,
        long anchorPosition = 3)
    {
        return ActiveDocumentSnapshot.Create(
            documentPath,
            text,
            Encoding.UTF8.GetByteCount(text),
            codePage,
            caretPosition,
            anchorPosition,
            isModified: false,
            new DateTimeOffset(2026, 7, 23, 9, 0, 0, TimeSpan.Zero));
    }

    private sealed record BuildContext(
        ActiveDocumentSnapshot Snapshot,
        CsvParseResult ParseResult,
        CsvTableProjection Projection,
        CsvEditSession Session,
        CsvRowEditModel Model);

    public enum ConflictKind
    {
        Document,
        CodePage,
        Content
    }

    private sealed class RecordingTarget : IEditorReplacementTarget
    {
        public List<string> Calls { get; } = [];

        public long ReplacementByteLength { get; set; } = 13;

        public Exception? ReplaceException { get; set; }

        public Exception? SelectionException { get; set; }

        public void BeginUndoAction()
        {
            Calls.Add("BeginUndoAction");
        }

        public long ReplaceWholeDocument(string text)
        {
            Calls.Add($"ReplaceWholeDocument:{text}");
            if (ReplaceException is not null)
            {
                throw ReplaceException;
            }

            return ReplacementByteLength;
        }

        public void SetSelection(long anchorPosition, long caretPosition)
        {
            Calls.Add($"SetSelection:{anchorPosition}:{caretPosition}");
            if (SelectionException is not null)
            {
                throw SelectionException;
            }
        }

        public void EndUndoAction()
        {
            Calls.Add("EndUndoAction");
        }
    }
}
