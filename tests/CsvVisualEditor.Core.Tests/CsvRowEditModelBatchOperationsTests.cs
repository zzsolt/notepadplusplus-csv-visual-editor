namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using System.Text;
using Xunit;

public sealed class CsvRowEditModelBatchOperationsTests
{
    [Fact]
    public void DeleteRows_NullIsRejectedAndEmptyRequestIsNoOp()
    {
        var context = CreateContext("Name\nAlpha\nBeta");
        var model = CreateModel(context);

        Assert.Throws<ArgumentNullException>(() =>
            CsvRowEditModelBatchOperations.DeleteRows(model, null!));

        var result = model.DeleteRows([]);

        Assert.False(result.HasChanges);
        Assert.Equal(0, result.TargetRowCount);
        Assert.Equal(0, result.AffectedRowCount);
        Assert.Equal(2, result.RemainingVisibleRowCount);
        Assert.False(model.IsDirty);
        Assert.Equal(context.Source, model.CreatePreview().Text);
    }

    [Fact]
    public void DeleteRows_DuplicatesAndEnumerationOrderDoNotAffectOutput()
    {
        const string source = "Name\nAlpha\nBeta\nGamma\nDelta";
        var firstModel = CreateModel(CreateContext(source));
        var secondModel = CreateModel(CreateContext(source));
        var firstRows = firstModel.GetVisibleRows();
        var secondRows = secondModel.GetVisibleRows();

        var firstResult = firstModel.DeleteRows([
            firstRows[3].Id,
            firstRows[0].Id,
            firstRows[3].Id]);
        var secondResult = secondModel.DeleteRows([
            secondRows[0].Id,
            secondRows[3].Id]);

        Assert.Equal(2, firstResult.TargetRowCount);
        Assert.Equal(2, firstResult.DeletedSourceRowCount);
        Assert.Equal(0, firstResult.CancelledInsertedRowCount);
        Assert.Equal(firstResult, secondResult);
        Assert.Equal("Name\nBeta\nGamma", firstModel.CreatePreview().Text);
        Assert.Equal(firstModel.CreatePreview().Text, secondModel.CreatePreview().Text);
    }

    [Fact]
    public void DeleteRows_MixedSourceAndInsertedRowsIsAtomicAndDeterministic()
    {
        var model = CreateModel(CreateContext("Name\nAlpha\nBeta\nGamma"));
        var sourceRows = model.GetVisibleRows();
        var insertedAfterAlpha = model.InsertRowAfter(sourceRows[0].Id, ["Inserted-1"]);
        var insertedAtEnd = model.AppendRow(["Inserted-2"]);

        var result = model.DeleteRows([
            insertedAtEnd,
            sourceRows[1].Id,
            insertedAfterAlpha]);

        Assert.True(result.HasChanges);
        Assert.Equal(3, result.TargetRowCount);
        Assert.Equal(1, result.DeletedSourceRowCount);
        Assert.Equal(2, result.CancelledInsertedRowCount);
        Assert.Equal(3, result.AffectedRowCount);
        Assert.Equal(2, result.RemainingVisibleRowCount);
        Assert.Equal(0, model.InsertedRowCount);
        Assert.Equal(1, model.DeletedRowCount);
        Assert.Equal("Name\nAlpha\nGamma", model.CreatePreview().Text);
    }

    [Fact]
    public void DeleteRows_InvalidIdentityLeavesModelUnchanged()
    {
        const string source = "Name\nAlpha\nBeta";
        var model = CreateModel(CreateContext(source));
        var validId = model.GetVisibleRows()[0].Id;
        var otherModel = CreateModel(CreateContext("Name\nAlpha\nBeta\nGamma"));
        var unknownId = otherModel.GetVisibleRows()[2].Id;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            model.DeleteRows([validId, unknownId]));

        Assert.False(model.IsDirty);
        Assert.Equal(2, model.VisibleRowCount);
        Assert.Equal(0, model.DeletedRowCount);
        Assert.Equal(source, model.CreatePreview().Text);
        Assert.False(model.GetRow(validId).IsDeleted);
    }

    [Fact]
    public void DeleteRows_AlreadyDeletedSourceRowIsIdempotentWithinBatch()
    {
        var model = CreateModel(CreateContext("Name\nAlpha\nBeta"));
        var rows = model.GetVisibleRows();
        Assert.True(model.DeleteRow(rows[0].Id));

        var result = model.DeleteRows([rows[0].Id, rows[1].Id]);

        Assert.Equal(2, result.TargetRowCount);
        Assert.Equal(1, result.DeletedSourceRowCount);
        Assert.Equal(0, result.CancelledInsertedRowCount);
        Assert.Equal(1, result.AffectedRowCount);
        Assert.Equal(0, result.RemainingVisibleRowCount);
        Assert.Equal(2, model.DeletedRowCount);
        Assert.Equal("Name", model.CreatePreview().Text);
    }

    [Fact]
    public void DeleteRows_AllHeaderlessRowsRetainsOnlyLeadingBom()
    {
        var model = CreateModel(CreateContext(
            "\uFEFFAlpha\nBeta\nGamma\n",
            headerMode: CsvHeaderMode.NoHeader));

        var result = model.DeleteRows(
            model.GetVisibleRows().Select(static row => row.Id));

        Assert.Equal(3, result.DeletedSourceRowCount);
        Assert.Equal(0, result.RemainingVisibleRowCount);
        Assert.Equal("\uFEFF", model.CreatePreview().Text);
    }

    [Fact]
    public void DeleteRows_RevertAllRestoresExactSourceAndBaselineOrder()
    {
        const string source = "Name\r\nAlpha\nBeta\rGamma\r\n";
        var model = CreateModel(CreateContext(source));
        var baselineRows = model.GetVisibleRows();
        var inserted = model.InsertRowAfter(baselineRows[0].Id, ["Inserted"]);

        var result = model.DeleteRows([
            baselineRows[0].Id,
            baselineRows[2].Id,
            inserted]);

        Assert.Equal(2, result.DeletedSourceRowCount);
        Assert.Equal(1, result.CancelledInsertedRowCount);
        Assert.True(model.IsDirty);
        Assert.True(model.RevertAll());
        Assert.False(model.IsDirty);
        Assert.Equal(source, model.CreatePreview().Text);
        Assert.Equal(
            baselineRows.Select(static row => row.Id),
            model.GetVisibleRows().Select(static row => row.Id));
    }

    [Fact]
    public void DeleteRows_MixedSeparatorsAndTerminalNewLineRemainStable()
    {
        var model = CreateModel(CreateContext("Name\r\nAlpha\nBeta\rGamma\r\n"));
        var rows = model.GetVisibleRows();

        var result = model.DeleteRows([rows[2].Id, rows[0].Id]);

        Assert.Equal(2, result.DeletedSourceRowCount);
        Assert.Equal("Name\r\nBeta\r\n", model.CreatePreview().Text);
    }

    [Fact]
    public void DeleteRows_InsertedOnlyBatchReturnsModelToCleanState()
    {
        const string source = "Name\nAlpha";
        var model = CreateModel(CreateContext(source));
        var first = model.AppendRow(["Beta"]);
        var second = model.AppendRow(["Gamma"]);

        var result = model.DeleteRows([second, first]);

        Assert.Equal(2, result.CancelledInsertedRowCount);
        Assert.Equal(0, result.DeletedSourceRowCount);
        Assert.Equal(1, result.RemainingVisibleRowCount);
        Assert.Equal(0, model.InsertedRowCount);
        Assert.False(model.IsDirty);
        Assert.Equal(source, model.CreatePreview().Text);
    }

    private static CsvRowEditModel CreateModel(BuildContext context)
    {
        return CsvRowEditModel.Create(
            context.Snapshot,
            context.ParseResult,
            context.Session,
            context.Projection);
    }

    private static BuildContext CreateContext(
        string text,
        char delimiter = ',',
        CsvHeaderMode headerMode = CsvHeaderMode.FirstRecord)
    {
        var snapshot = ActiveDocumentSnapshot.Create(
            @"C:\Synthetic\batch-rows.csv",
            text,
            Encoding.UTF8.GetByteCount(text),
            codePage: 65001,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            new DateTimeOffset(2026, 7, 23, 12, 30, 0, TimeSpan.Zero));
        var dialect = CsvDialect.Create(delimiter, headerMode: headerMode);
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
        var session = CsvEditSession.Create(snapshot, parseResult, projection);

        return new BuildContext(text, snapshot, parseResult, session, projection);
    }

    private sealed record BuildContext(
        string Source,
        ActiveDocumentSnapshot Snapshot,
        CsvParseResult ParseResult,
        CsvEditSession Session,
        CsvTableProjection Projection);
}
