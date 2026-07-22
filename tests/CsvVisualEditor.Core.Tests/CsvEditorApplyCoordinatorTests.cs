namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using System.Text;
using Xunit;

public sealed class CsvEditorApplyCoordinatorTests
{
    [Fact]
    public void Execute_NoChanges_DoesNotCallEditor()
    {
        var context = CreateContext("A,B\n1,2");
        var target = new RecordingTarget();

        var result = CsvEditorApplyCoordinator.Execute(
            context.Session,
            context.Snapshot,
            target);

        Assert.Equal(CsvEditorApplyStatus.NoChanges, result.Status);
        Assert.False(result.WasApplied);
        Assert.Empty(target.Calls);
    }

    [Theory]
    [InlineData(ConflictKind.Document, CsvEditorApplyStatus.DocumentIdentityChanged)]
    [InlineData(ConflictKind.CodePage, CsvEditorApplyStatus.CodePageChanged)]
    [InlineData(ConflictKind.Content, CsvEditorApplyStatus.ContentChanged)]
    public void Execute_Conflict_DoesNotCallEditor(
        ConflictKind conflictKind,
        CsvEditorApplyStatus expectedStatus)
    {
        var context = CreateContext("A,B\n1,2");
        context.Session.SetCellValue(1, 1, "3");
        var currentSnapshot = conflictKind switch
        {
            ConflictKind.Document => CreateSnapshot(
                context.Snapshot.Text,
                documentPath: @"C:\Data\other.csv"),
            ConflictKind.CodePage => CreateSnapshot(
                context.Snapshot.Text,
                codePage: 1250),
            ConflictKind.Content => CreateSnapshot("A,B\n1,external"),
            _ => throw new ArgumentOutOfRangeException(nameof(conflictKind))
        };
        var target = new RecordingTarget();

        var result = CsvEditorApplyCoordinator.Execute(
            context.Session,
            currentSnapshot,
            target);

        Assert.Equal(expectedStatus, result.Status);
        Assert.False(result.WasApplied);
        Assert.Equal(1, result.ChangedCellCount);
        Assert.Null(result.ReplacementSha256);
        Assert.Empty(target.Calls);
    }

    [Fact]
    public void Execute_ReadyPlan_CallsEditorInSingleUndoOrder()
    {
        var context = CreateContext("A,B\n1,2");
        context.Session.SetCellValue(1, 1, "three");
        var target = new RecordingTarget
        {
            ReplacementByteLength = 17
        };

        var result = CsvEditorApplyCoordinator.Execute(
            context.Session,
            context.Snapshot,
            target);

        Assert.Equal(CsvEditorApplyStatus.Applied, result.Status);
        Assert.True(result.WasApplied);
        Assert.Equal(1, result.ChangedCellCount);
        Assert.Equal(1, result.ChangedRecordCount);
        Assert.NotNull(result.ReplacementSha256);
        Assert.Equal(
            [
                "BeginUndoAction",
                "ReplaceWholeDocument:A,B\n1,three",
                "SetSelection:3:7",
                "EndUndoAction"
            ],
            target.Calls);
    }

    [Fact]
    public void Execute_SelectionIsClampedToReplacementByteLength()
    {
        var context = CreateContext(
            "A,B\n1,2",
            caretPosition: 99,
            anchorPosition: 88);
        context.Session.SetCellValue(1, 1, string.Empty);
        var target = new RecordingTarget
        {
            ReplacementByteLength = 5
        };

        _ = CsvEditorApplyCoordinator.Execute(
            context.Session,
            context.Snapshot,
            target);

        Assert.Contains("SetSelection:5:5", target.Calls);
    }

    [Fact]
    public void Execute_NegativeReplacementLength_ThrowsAndEndsUndoAction()
    {
        var context = CreateDirtyContext();
        var target = new RecordingTarget
        {
            ReplacementByteLength = -1
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CsvEditorApplyCoordinator.Execute(
                context.Session,
                context.Snapshot,
                target));

        Assert.Contains("invalid document length", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            [
                "BeginUndoAction",
                "ReplaceWholeDocument:A,B\n1,3",
                "EndUndoAction"
            ],
            target.Calls);
    }

    [Fact]
    public void Execute_ReplaceFailure_StillEndsUndoAction()
    {
        var context = CreateDirtyContext();
        var target = new RecordingTarget
        {
            ReplaceException = new InvalidOperationException("synthetic replace failure")
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CsvEditorApplyCoordinator.Execute(
                context.Session,
                context.Snapshot,
                target));

        Assert.Equal("synthetic replace failure", exception.Message);
        Assert.Equal(
            [
                "BeginUndoAction",
                "ReplaceWholeDocument:A,B\n1,3",
                "EndUndoAction"
            ],
            target.Calls);
    }

    [Fact]
    public void Execute_SelectionFailure_StillEndsUndoAction()
    {
        var context = CreateDirtyContext();
        var target = new RecordingTarget
        {
            ReplacementByteLength = 9,
            SelectionException = new InvalidOperationException("synthetic selection failure")
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CsvEditorApplyCoordinator.Execute(
                context.Session,
                context.Snapshot,
                target));

        Assert.Equal("synthetic selection failure", exception.Message);
        Assert.Equal(
            [
                "BeginUndoAction",
                "ReplaceWholeDocument:A,B\n1,3",
                "SetSelection:3:7",
                "EndUndoAction"
            ],
            target.Calls);
    }

    [Fact]
    public void Execute_BeginFailure_DoesNotAttemptEndUndoAction()
    {
        var context = CreateDirtyContext();
        var target = new RecordingTarget
        {
            BeginException = new InvalidOperationException("synthetic begin failure")
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CsvEditorApplyCoordinator.Execute(
                context.Session,
                context.Snapshot,
                target));

        Assert.Equal("synthetic begin failure", exception.Message);
        Assert.Equal(["BeginUndoAction"], target.Calls);
    }

    private static BuildContext CreateDirtyContext()
    {
        var context = CreateContext("A,B\n1,2");
        context.Session.SetCellValue(1, 1, "3");
        return context;
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
            new CsvTableProjectionOptions
            {
                HeaderMode = CsvHeaderMode.FirstRecord,
                MaximumRows = 10_000,
                MaximumColumns = 512,
                MaximumCells = 250_000
            });
        var session = CsvEditSession.Create(snapshot, parseResult, projection);

        return new BuildContext(snapshot, session);
    }

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
            new DateTimeOffset(2026, 7, 22, 8, 0, 0, TimeSpan.Zero));
    }

    private sealed record BuildContext(
        ActiveDocumentSnapshot Snapshot,
        CsvEditSession Session);

    public enum ConflictKind
    {
        Document,
        CodePage,
        Content
    }

    private sealed class RecordingTarget : IEditorReplacementTarget
    {
        public List<string> Calls { get; } = [];

        public long ReplacementByteLength { get; set; } = 9;

        public Exception? BeginException { get; set; }

        public Exception? ReplaceException { get; set; }

        public Exception? SelectionException { get; set; }

        public void BeginUndoAction()
        {
            Calls.Add("BeginUndoAction");
            if (BeginException is not null)
            {
                throw BeginException;
            }
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
