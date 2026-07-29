namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using System.Text;
using Xunit;

public sealed class CsvClipboardMatrixTests
{
    [Fact]
    public void Parse_TabAndMixedLineEndings_CreatesRectangle()
    {
        var matrix = CsvClipboardMatrix.Parse("A\tB\r\nC\tD\nE\tF");

        Assert.Equal(3, matrix.RowCount);
        Assert.Equal(2, matrix.ColumnCount);
        Assert.Equal("D", matrix[1, 1]);
        Assert.Equal("A\tB\r\nC\tD\r\nE\tF", matrix.ToTabSeparatedText());
    }

    [Fact]
    public void Parse_TerminalNewline_DoesNotCreateExtraRow()
    {
        var matrix = CsvClipboardMatrix.Parse("A\tB\r\nC\tD\r\n");

        Assert.Equal(2, matrix.RowCount);
        Assert.Equal(2, matrix.ColumnCount);
    }

    [Fact]
    public void Parse_RaggedRows_PadsMissingCellsWithEmptyStrings()
    {
        var matrix = CsvClipboardMatrix.Parse("A\tB\nC");

        Assert.Equal(2, matrix.ColumnCount);
        Assert.Equal(string.Empty, matrix[1, 1]);
        Assert.Equal("A\tB\r\nC\t", matrix.ToTabSeparatedText());
    }

    [Fact]
    public void Parse_EmptyText_IsOneEmptyCell()
    {
        var matrix = CsvClipboardMatrix.Parse(string.Empty);

        Assert.True(matrix.IsSingleCell);
        Assert.Equal(string.Empty, matrix[0, 0]);
    }

    [Fact]
    public void CreatePlan_ExactRectangle_MapsStableRowsAndPhysicalColumns()
    {
        var session = CreateSession("A,B,C\n1,2,3\n4,5,6");
        var matrix = CsvClipboardMatrix.Parse("x\ty\nu\tv");

        var plan = CsvClipboardPastePlan.Create(
            session,
            [1, 2],
            startRowOffset: 0,
            startColumnIndex: 1,
            targetRowCount: 2,
            targetColumnCount: 2,
            matrix);

        Assert.True(plan.IsReady);
        Assert.Collection(
            plan.Edits,
            edit => Assert.Equal(new CsvCellAddress(1, 1), edit.Address),
            edit => Assert.Equal(new CsvCellAddress(1, 2), edit.Address),
            edit => Assert.Equal(new CsvCellAddress(2, 1), edit.Address),
            edit => Assert.Equal(new CsvCellAddress(2, 2), edit.Address));

        Assert.Equal(4, plan.Apply(session));
        Assert.Equal("A,B,C\n1,x,y\n4,u,v", session.CreatePreview().Text);
    }

    [Fact]
    public void CreatePlan_SingleCell_BroadcastsAcrossTargetRectangle()
    {
        var session = CreateSession("A,B\n1,2\n3,4");
        var matrix = CsvClipboardMatrix.Parse("same");

        var plan = CsvClipboardPastePlan.Create(
            session,
            [1, 2],
            startRowOffset: 0,
            startColumnIndex: 0,
            targetRowCount: 2,
            targetColumnCount: 2,
            matrix);

        Assert.True(plan.IsReady);
        Assert.Equal(4, plan.Apply(session));
        Assert.Equal("A,B\nsame,same\nsame,same", session.CreatePreview().Text);
    }

    [Fact]
    public void CreatePlan_ShapeMismatch_ReturnsNoEditsAndDoesNotMutate()
    {
        var session = CreateSession("A,B\n1,2\n3,4");
        var matrix = CsvClipboardMatrix.Parse("x\ty");

        var plan = CsvClipboardPastePlan.Create(
            session,
            [1, 2],
            startRowOffset: 0,
            startColumnIndex: 0,
            targetRowCount: 2,
            targetColumnCount: 2,
            matrix);

        Assert.Equal(CsvClipboardPasteStatus.ShapeMismatch, plan.Status);
        Assert.Empty(plan.Edits);
        Assert.False(session.IsDirty);
        Assert.Throws<InvalidOperationException>(() => plan.Apply(session));
    }

    [Fact]
    public void CreatePlan_TargetOutsideSession_ReturnsNoEdits()
    {
        var session = CreateSession("A,B\n1,2");

        var plan = CsvClipboardPastePlan.Create(
            session,
            [1],
            startRowOffset: 0,
            startColumnIndex: 1,
            targetRowCount: 1,
            targetColumnCount: 2,
            CsvClipboardMatrix.Parse("x\ty"));

        Assert.Equal(CsvClipboardPasteStatus.TargetOutsideSession, plan.Status);
        Assert.Empty(plan.Edits);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void Apply_RevalidationAgainstDifferentSession_PreventsPartialMutation()
    {
        var sourceSession = CreateSession("A,B\n1,2\n3,4");
        var plan = CsvClipboardPastePlan.Create(
            sourceSession,
            [1, 2],
            0,
            0,
            2,
            2,
            CsvClipboardMatrix.Parse("x\ty\nu\tv"));
        var differentSession = CreateSession("A\n1");

        Assert.Throws<ArgumentOutOfRangeException>(() => plan.Apply(differentSession));
        Assert.False(differentSession.IsDirty);
    }

    [Fact]
    public void Apply_UnchangedValues_CountsOnlyActualChanges()
    {
        var session = CreateSession("A,B\n1,2");
        var plan = CsvClipboardPastePlan.Create(
            session,
            [1],
            0,
            0,
            1,
            2,
            CsvClipboardMatrix.Parse("1\tx"));

        Assert.Equal(1, plan.Apply(session));
        Assert.Equal(1, session.ChangedCellCount);
        Assert.Equal("A,B\n1,x", session.CreatePreview().Text);
    }

    private static CsvEditSession CreateSession(string text)
    {
        var snapshot = ActiveDocumentSnapshot.Create(
            @"C:\Data\sample.csv",
            text,
            Encoding.UTF8.GetByteCount(text),
            codePage: 65001,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero));
        var dialect = CsvDialect.Create(',', headerMode: CsvHeaderMode.FirstRecord);
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

        return CsvEditSession.Create(snapshot, parseResult, projection);
    }
}
