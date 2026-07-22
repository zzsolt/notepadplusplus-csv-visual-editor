namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using System.Text;
using Xunit;

public sealed class CsvEditSessionTests
{
    [Fact]
    public void Create_UnchangedPreview_ReturnsOriginalTextExactly()
    {
        const string source =
            "\uFEFF\"Name\",Note\r\n" +
            "Alice,\"line1\nline2\"\n" +
            "Bob,\"x,y\"\r";
        var context = CreateContext(source);

        var session = CsvEditSession.Create(
            context.Snapshot,
            context.ParseResult,
            context.Projection);
        var preview = session.CreatePreview();

        Assert.False(session.IsDirty);
        Assert.False(preview.HasChanges);
        Assert.Equal(source, preview.Text);
        Assert.Equal(context.Snapshot.ContentSha256, preview.ContentSha256);
    }

    [Fact]
    public void SetCellValue_TracksDirtyStateAndRevertCellClearsIt()
    {
        var context = CreateContext("A,B\n1,2");
        var session = CreateSession(context);

        Assert.True(session.SetCellValue(1, 1, "3"));
        Assert.False(session.SetCellValue(1, 1, "3"));
        Assert.True(session.IsDirty);
        Assert.True(session.IsCellDirty(1, 1));
        Assert.Equal(1, session.ChangedCellCount);
        Assert.Equal(1, session.ChangedRecordCount);
        Assert.Equal("2", session.GetOriginalValue(1, 1));
        Assert.Equal("3", session.GetValue(1, 1));

        Assert.True(session.RevertCell(1, 1));
        Assert.False(session.IsDirty);
        Assert.False(session.IsCellDirty(1, 1));
        Assert.Equal(0, session.ChangedCellCount);
        Assert.Equal(0, session.ChangedRecordCount);
        Assert.Equal("A,B\n1,2", session.CreatePreview().Text);
    }

    [Fact]
    public void RevertAll_RestoresEveryCellAndCounter()
    {
        var context = CreateContext("A,B\n1,2\n3,4");
        var session = CreateSession(context);

        session.SetCellValue(1, 0, "one");
        session.SetCellValue(1, 1, "two");
        session.SetCellValue(2, 0, "three");

        Assert.Equal(3, session.ChangedCellCount);
        Assert.Equal(2, session.ChangedRecordCount);
        Assert.True(session.RevertAll());
        Assert.False(session.RevertAll());
        Assert.False(session.IsDirty);
        Assert.Equal("A,B\n1,2\n3,4", session.CreatePreview().Text);
    }

    [Fact]
    public void CreatePreview_RewritesOnlyChangedRecordAndPreservesMixedSeparators()
    {
        const string source =
            "Name,Note\r\n" +
            "Alice,plain\n" +
            "Bob,\"already,quoted\"\r\n";
        var session = CreateSession(CreateContext(source));

        session.SetCellValue(1, 1, "x,y");
        var preview = session.CreatePreview();

        Assert.Equal(
            "Name,Note\r\nAlice,\"x,y\"\nBob,\"already,quoted\"\r\n",
            preview.Text);
        Assert.Equal(1, preview.ChangedCellCount);
        Assert.Equal(1, preview.ChangedRecordCount);
    }

    [Fact]
    public void CreatePreview_PaddedCellExtendsOnlyTheChangedRecord()
    {
        const string source = "A,B\n1\n2,3,4";
        var session = CreateSession(CreateContext(source));

        Assert.Equal(1, session.GetOriginalFieldCount(1));
        session.SetCellValue(1, 1, "x");

        Assert.Equal("A,B\n1,x\n2,3,4", session.CreatePreview().Text);
    }

    [Fact]
    public void CreatePreview_ThirdPaddedCellCreatesIntermediateEmptyField()
    {
        const string source = "A,B\n1\n2,3,4";
        var session = CreateSession(CreateContext(source));

        session.SetCellValue(1, 2, "z");

        Assert.Equal("A,B\n1,,z\n2,3,4", session.CreatePreview().Text);
    }

    [Fact]
    public void CreatePreview_ClearedTrailingFieldPreservesOriginalFieldCount()
    {
        var session = CreateSession(CreateContext("A,B,C\n1,2,3"));

        session.SetCellValue(1, 2, string.Empty);

        Assert.Equal("A,B,C\n1,2,", session.CreatePreview().Text);
    }

    [Fact]
    public void RevertingPaddedExtension_RestoresExactOriginalSource()
    {
        const string source = "A,B\n1\n2,3,4";
        var session = CreateSession(CreateContext(source));

        session.SetCellValue(1, 2, "z");
        session.RevertCell(1, 2);

        Assert.False(session.IsDirty);
        Assert.Equal(source, session.CreatePreview().Text);
    }

    [Fact]
    public void CreatePreview_HeaderRecordCanBeEditedWithoutChangingDataRows()
    {
        var session = CreateSession(CreateContext("Name,Age\nAlice,30"));

        session.SetCellValue(0, 0, "Full Name");

        Assert.Equal("Full Name,Age\nAlice,30", session.CreatePreview().Text);
    }

    [Fact]
    public void CreatePreview_UnicodeValueIsPreserved()
    {
        var session = CreateSession(CreateContext("Name,City\nAnna,Budapest"));

        session.SetCellValue(1, 1, "東京 🗼");

        Assert.Equal("Name,City\nAnna,東京 🗼", session.CreatePreview().Text);
    }

    [Fact]
    public void Create_ParserErrorsAreRejected()
    {
        const string source = "A,B\n\"unterminated";
        var snapshot = CreateSnapshot(source);
        var dialect = CsvDialect.Create(',', headerMode: CsvHeaderMode.FirstRecord);
        var parseResult = CsvParser.Parse(source, dialect);
        var projection = CsvTableProjector.Create(
            parseResult,
            new CsvTableProjectionOptions
            {
                HeaderMode = CsvHeaderMode.FirstRecord
            });

        Assert.True(parseResult.HasErrors);
        Assert.Throws<InvalidOperationException>(() =>
            CsvEditSession.Create(snapshot, parseResult, projection));
    }

    [Fact]
    public void Create_RowLimitedProjectionIsRejected()
    {
        const string source = "A,B\n1,2\n3,4\n5,6";
        var snapshot = CreateSnapshot(source);
        var dialect = CsvDialect.Create(',', headerMode: CsvHeaderMode.FirstRecord);
        var parseResult = CsvParser.Parse(source, dialect);
        var projection = CsvTableProjector.Create(
            parseResult,
            new CsvTableProjectionOptions
            {
                HeaderMode = CsvHeaderMode.FirstRecord,
                MaximumRows = 1
            });

        Assert.True(projection.IsRowLimited);
        Assert.Throws<InvalidOperationException>(() =>
            CsvEditSession.Create(snapshot, parseResult, projection));
    }

    [Fact]
    public void Create_ParseResultFromDifferentSnapshotIsRejected()
    {
        const string parsedSource = "A,B\n1,2";
        const string snapshotSource = "A,B\n9,9";
        var dialect = CsvDialect.Create(',', headerMode: CsvHeaderMode.FirstRecord);
        var parseResult = CsvParser.Parse(parsedSource, dialect);
        var projection = CsvTableProjector.Create(
            parseResult,
            new CsvTableProjectionOptions
            {
                HeaderMode = CsvHeaderMode.FirstRecord
            });

        Assert.Throws<InvalidOperationException>(() =>
            CsvEditSession.Create(
                CreateSnapshot(snapshotSource),
                parseResult,
                projection));
    }

    [Fact]
    public void CreateApplyPlan_NoChanges_ReturnsNoChanges()
    {
        var context = CreateContext("A,B\n1,2");
        var session = CreateSession(context);

        var plan = session.CreateApplyPlan(context.Snapshot);

        Assert.Equal(CsvEditApplyStatus.NoChanges, plan.Status);
        Assert.False(plan.IsReady);
        Assert.Null(plan.Preview);
        Assert.Equal(0, plan.ChangedCellCount);
    }

    [Fact]
    public void CreateApplyPlan_MatchingBaseline_ReturnsReadyPreview()
    {
        var context = CreateContext("A,B\n1,2");
        var session = CreateSession(context);
        session.SetCellValue(1, 1, "3");

        var plan = session.CreateApplyPlan(context.Snapshot);

        Assert.Equal(CsvEditApplyStatus.Ready, plan.Status);
        Assert.True(plan.IsReady);
        Assert.NotNull(plan.Preview);
        Assert.Equal("A,B\n1,3", plan.Preview.Text);
        Assert.Equal(1, plan.ChangedCellCount);
    }

    [Fact]
    public void CreateApplyPlan_PathCaseDifference_IsTheSameDocument()
    {
        var context = CreateContext(
            "A,B\n1,2",
            documentPath: @"C:\Data\Sample.csv");
        var session = CreateSession(context);
        session.SetCellValue(1, 1, "3");
        var current = CreateSnapshot(
            context.Snapshot.Text,
            documentPath: @"c:\data\sample.csv");

        var plan = session.CreateApplyPlan(current);

        Assert.Equal(CsvEditApplyStatus.Ready, plan.Status);
    }

    [Fact]
    public void CreateApplyPlan_DifferentDocument_IsBlocked()
    {
        var context = CreateContext("A,B\n1,2");
        var session = CreateSession(context);
        session.SetCellValue(1, 1, "3");
        var current = CreateSnapshot(
            context.Snapshot.Text,
            documentPath: @"C:\Data\other.csv");

        var plan = session.CreateApplyPlan(current);

        Assert.Equal(CsvEditApplyStatus.DocumentIdentityChanged, plan.Status);
        Assert.Null(plan.Preview);
    }

    [Fact]
    public void CreateApplyPlan_CodePageChange_IsBlocked()
    {
        var context = CreateContext("A,B\n1,2");
        var session = CreateSession(context);
        session.SetCellValue(1, 1, "3");
        var current = CreateSnapshot(
            context.Snapshot.Text,
            codePage: 1250);

        var plan = session.CreateApplyPlan(current);

        Assert.Equal(CsvEditApplyStatus.CodePageChanged, plan.Status);
        Assert.Null(plan.Preview);
    }

    [Fact]
    public void CreateApplyPlan_ContentChange_IsBlocked()
    {
        var context = CreateContext("A,B\n1,2");
        var session = CreateSession(context);
        session.SetCellValue(1, 1, "3");
        var current = CreateSnapshot("A,B\n1,external");

        var plan = session.CreateApplyPlan(current);

        Assert.Equal(CsvEditApplyStatus.ContentChanged, plan.Status);
        Assert.Null(plan.Preview);
    }

    [Fact]
    public void InvalidRecordAndColumnIndexes_AreRejected()
    {
        var session = CreateSession(CreateContext("A,B\n1,2"));

        Assert.Throws<ArgumentOutOfRangeException>(() => session.GetValue(99, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.GetValue(1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetCellValue(1, 2, "x"));
    }

    private static CsvEditSession CreateSession(BuildContext context)
    {
        return CsvEditSession.Create(
            context.Snapshot,
            context.ParseResult,
            context.Projection);
    }

    private static BuildContext CreateContext(
        string text,
        char delimiter = ',',
        CsvHeaderMode headerMode = CsvHeaderMode.FirstRecord,
        string documentPath = @"C:\Data\sample.csv")
    {
        var snapshot = CreateSnapshot(text, documentPath);
        var dialect = CsvDialect.Create(
            delimiter,
            headerMode: headerMode);
        var parseResult = CsvParser.Parse(text, dialect);
        var projection = CsvTableProjector.Create(
            parseResult,
            new CsvTableProjectionOptions
            {
                HeaderMode = headerMode,
                MaximumRows = 10_000,
                MaximumColumns = 512,
                MaximumCells = 250_000
            });

        return new BuildContext(snapshot, parseResult, projection);
    }

    private static ActiveDocumentSnapshot CreateSnapshot(
        string text,
        string documentPath = @"C:\Data\sample.csv",
        int codePage = 65001)
    {
        return ActiveDocumentSnapshot.Create(
            documentPath,
            text,
            Encoding.UTF8.GetByteCount(text),
            codePage,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            new DateTimeOffset(2026, 7, 22, 8, 0, 0, TimeSpan.Zero));
    }

    private sealed record BuildContext(
        ActiveDocumentSnapshot Snapshot,
        CsvParseResult ParseResult,
        CsvTableProjection Projection);
}
