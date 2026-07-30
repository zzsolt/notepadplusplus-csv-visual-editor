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
    public void Parse_RaggedRows_AreRejectedInsteadOfSilentlyPadded()
    {
        var exception = Assert.Throws<FormatException>(() =>
            CsvClipboardMatrix.Parse("A\tB\nC"));
        Assert.Contains("rectangular", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_EmptyText_IsOneEmptyCell()
    {
        var matrix = CsvClipboardMatrix.Parse(string.Empty);
        Assert.True(matrix.IsSingleCell);
        Assert.Equal(string.Empty, matrix[0, 0]);
    }

    [Theory]
    [InlineData("A\0B")]
    [InlineData("A\u0001B")]
    [InlineData("A\u0007B")]
    public void Parse_UnsupportedControlCharacter_IsRejected(string text)
    {
        Assert.Throws<FormatException>(() => CsvClipboardMatrix.Parse(text));
    }

    [Fact]
    public void Parse_TooManyColumns_IsRejected()
    {
        var text = string.Join('\t', Enumerable.Repeat("x", 513));
        Assert.Throws<FormatException>(() => CsvClipboardMatrix.Parse(text));
    }

    [Fact]
    public void CreatePlan_ExactRectangle_MapsStableRowsAndPhysicalColumns()
    {
        var model = CreateModel("A,B,C\n1,2,3\n4,5,6");
        var rows = VisibleIds(model);
        var plan = CsvClipboardPastePlan.Create(model, rows, 0, 1, 2, 2, CsvClipboardMatrix.Parse("x\ty\nu\tv"));

        Assert.True(plan.IsReady);
        Assert.Collection(
            plan.Edits,
            edit => Assert.Equal(new CsvCellAddress(rows[0], 1), edit.Address),
            edit => Assert.Equal(new CsvCellAddress(rows[0], 2), edit.Address),
            edit => Assert.Equal(new CsvCellAddress(rows[1], 1), edit.Address),
            edit => Assert.Equal(new CsvCellAddress(rows[1], 2), edit.Address));
        Assert.Equal(4, plan.Apply(model));
        Assert.Equal("A,B,C\n1,x,y\n4,u,v", model.CreatePreview().Text);
    }

    [Fact]
    public void CreatePlan_SingleCell_BroadcastsAcrossTargetRectangle()
    {
        var model = CreateModel("A,B\n1,2\n3,4");
        var plan = CsvClipboardPastePlan.Create(model, VisibleIds(model), 0, 0, 2, 2, CsvClipboardMatrix.Parse("same"));
        Assert.True(plan.IsReady);
        Assert.Equal(4, plan.Apply(model));
        Assert.Equal("A,B\nsame,same\nsame,same", model.CreatePreview().Text);
    }

    [Fact]
    public void CreatePlan_ShapeMismatch_ReturnsNoEditsAndDoesNotMutate()
    {
        var model = CreateModel("A,B\n1,2\n3,4");
        var plan = CsvClipboardPastePlan.Create(model, VisibleIds(model), 0, 0, 2, 2, CsvClipboardMatrix.Parse("x\ty"));
        Assert.Equal(CsvClipboardPasteStatus.ShapeMismatch, plan.Status);
        Assert.Empty(plan.Edits);
        Assert.False(model.IsDirty);
        Assert.Throws<InvalidOperationException>(() => plan.Apply(model));
    }

    [Fact]
    public void CreatePlan_TargetOutsideSession_ReturnsNoEdits()
    {
        var model = CreateModel("A,B\n1,2");
        var plan = CsvClipboardPastePlan.Create(model, VisibleIds(model), 0, 1, 1, 2, CsvClipboardMatrix.Parse("x\ty"));
        Assert.Equal(CsvClipboardPasteStatus.TargetOutsideSession, plan.Status);
        Assert.Empty(plan.Edits);
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void Apply_RevalidationAgainstDifferentModel_PreventsPartialMutation()
    {
        var sourceModel = CreateModel("A,B\n1,2\n3,4");
        var plan = CsvClipboardPastePlan.Create(sourceModel, VisibleIds(sourceModel), 0, 0, 2, 2, CsvClipboardMatrix.Parse("x\ty\nu\tv"));
        var differentModel = CreateModel("A\n1");
        Assert.ThrowsAny<Exception>(() => plan.Apply(differentModel));
        Assert.False(differentModel.IsDirty);
    }

    [Fact]
    public void Apply_UnchangedValues_CountsOnlyActualChanges()
    {
        var model = CreateModel("A,B\n1,2");
        var plan = CsvClipboardPastePlan.Create(model, VisibleIds(model), 0, 0, 1, 2, CsvClipboardMatrix.Parse("1\tx"));
        Assert.Equal(1, plan.Apply(model));
        Assert.Equal(1, model.ChangedCellCount);
        Assert.Equal("A,B\n1,x", model.CreatePreview().Text);
    }

    [Fact]
    public void CreatePlan_InsertedRowsUseStableRowIds()
    {
        var model = CreateModel("A,B\n1,2");
        var inserted = model.AppendRow(["old", "value"]);
        var plan = CsvClipboardPastePlan.Create(model, VisibleIds(model), 1, 0, 1, 2, CsvClipboardMatrix.Parse("new\trow"));
        Assert.True(plan.IsReady);
        Assert.All(plan.Edits, edit => Assert.Equal(inserted, edit.Address.RowId));
        Assert.Equal(2, plan.Apply(model));
        Assert.Equal(["new", "row"], model.GetRow(inserted).Values);
    }

    private static CsvEditRowId[] VisibleIds(CsvRowEditModel model) =>
        model.GetVisibleRows().Select(static row => row.Id).ToArray();

    private static CsvRowEditModel CreateModel(string text)
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
        var session = CsvEditSession.Create(snapshot, parseResult, projection);
        return CsvRowEditModel.Create(snapshot, parseResult, session, projection);
    }
}
