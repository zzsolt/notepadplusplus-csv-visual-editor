namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using System.Text;
using Xunit;

public sealed class CsvRowEditModelTests
{
    [Fact]
    public void Create_HeaderMode_ExcludesHeaderAndRetainsSourceIdentities()
    {
        var context = CreateContext("Name,Age\nAlice,30\nBob,40");
        var model = CsvRowEditModel.Create(context.Session, context.Projection);

        var rows = model.GetVisibleRows();

        Assert.Equal(2, model.VisibleRowCount);
        Assert.Equal([1, 2], rows.Select(static row => row.SourceRecordIndex));
        Assert.All(rows, static row => Assert.False(row.IsInserted));
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void Create_NoHeaderMode_IncludesFirstSourceRecord()
    {
        var context = CreateContext(
            "Alice,30\nBob,40",
            headerMode: CsvHeaderMode.NoHeader);
        var model = CsvRowEditModel.Create(context.Session, context.Projection);

        Assert.Equal(
            [0, 1],
            model.GetVisibleRows().Select(static row => row.SourceRecordIndex));
    }

    [Fact]
    public void AppendRow_AssignsUniqueNegativeIdentityAndPadsValues()
    {
        var context = CreateContext("Name,Age,City\nAlice,30,Budapest");
        var model = CsvRowEditModel.Create(context.Session, context.Projection);

        var firstInserted = model.AppendRow(["Bob", "40"]);
        var secondInserted = model.AppendRow();
        var rows = model.GetVisibleRows();

        Assert.True(firstInserted.IsInserted);
        Assert.True(secondInserted.IsInserted);
        Assert.NotEqual(firstInserted, secondInserted);
        Assert.Equal(3, model.VisibleRowCount);
        Assert.Equal(2, model.InsertedRowCount);
        Assert.Equal(2, model.ChangedRowCount);
        Assert.True(model.IsDirty);
        Assert.Equal(["Bob", "40", ""], rows[1].Values);
        Assert.Equal(["", "", ""], rows[2].Values);
    }

    [Fact]
    public void InsertBeforeAndAfter_PreserveStableSourceOrder()
    {
        var context = CreateContext("Name\nAlpha\nGamma");
        var model = CsvRowEditModel.Create(context.Session, context.Projection);
        var sourceRows = model.GetVisibleRows();

        var beforeGamma = model.InsertRowBefore(sourceRows[1].Id, ["Beta"]);
        var afterGamma = model.InsertRowAfter(sourceRows[1].Id, ["Delta"]);
        var rows = model.GetVisibleRows();

        Assert.Equal(
            [sourceRows[0].Id, beforeGamma, sourceRows[1].Id, afterGamma],
            rows.Select(static row => row.Id));
        Assert.Equal(
            ["Alpha", "Beta", "Gamma", "Delta"],
            rows.Select(static row => row.Values[0]));
        Assert.Equal(1, rows[0].SourceRecordIndex);
        Assert.Equal(2, rows[2].SourceRecordIndex);
    }

    [Fact]
    public void DeleteAndRestoreSourceRow_UpdateVisibilityAndCounters()
    {
        var context = CreateContext("Name\nAlpha\nBeta");
        var model = CsvRowEditModel.Create(context.Session, context.Projection);
        var betaId = model.GetVisibleRows()[1].Id;

        Assert.True(model.DeleteRow(betaId));
        Assert.False(model.DeleteRow(betaId));
        Assert.Equal(1, model.VisibleRowCount);
        Assert.Equal(1, model.DeletedRowCount);
        Assert.True(model.GetRow(betaId).IsDeleted);

        Assert.True(model.RestoreDeletedRow(betaId));
        Assert.False(model.RestoreDeletedRow(betaId));
        Assert.Equal(2, model.VisibleRowCount);
        Assert.Equal(0, model.DeletedRowCount);
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void DeleteInsertedRow_CancelsInsertionAndRemovesIdentity()
    {
        var context = CreateContext("Name\nAlpha");
        var model = CsvRowEditModel.Create(context.Session, context.Projection);
        var insertedId = model.AppendRow(["Beta"]);

        Assert.True(model.DeleteRow(insertedId));
        Assert.False(model.IsDirty);
        Assert.Equal(0, model.InsertedRowCount);
        Assert.Equal(1, model.VisibleRowCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => model.GetRow(insertedId));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            model.RestoreDeletedRow(insertedId));
    }

    [Fact]
    public void RevertAll_RemovesInsertionsRestoresDeletionsAndSourceOrder()
    {
        var context = CreateContext("Name\nAlpha\nBeta\nGamma");
        var model = CsvRowEditModel.Create(context.Session, context.Projection);
        var baseline = model.GetVisibleRows();

        model.InsertRowAfter(baseline[0].Id, ["Inserted"]);
        model.DeleteRow(baseline[1].Id);

        Assert.True(model.IsDirty);
        Assert.True(model.RevertAll());
        Assert.False(model.RevertAll());
        Assert.False(model.IsDirty);
        Assert.Equal(0, model.InsertedRowCount);
        Assert.Equal(0, model.DeletedRowCount);
        Assert.Equal(
            baseline.Select(static row => row.Id),
            model.GetVisibleRows().Select(static row => row.Id));
    }

    [Fact]
    public void DeletedRow_CannotBeUsedAsInsertionAnchor()
    {
        var context = CreateContext("Name\nAlpha\nBeta");
        var model = CsvRowEditModel.Create(context.Session, context.Projection);
        var betaId = model.GetVisibleRows()[1].Id;
        model.DeleteRow(betaId);

        Assert.Throws<InvalidOperationException>(() =>
            model.InsertRowBefore(betaId, ["Before"]));
        Assert.Throws<InvalidOperationException>(() =>
            model.InsertRowAfter(betaId, ["After"]));
    }

    [Fact]
    public void InsertedValues_AreCopiedAndCannotExceedColumnCount()
    {
        var context = CreateContext("A,B\n1,2");
        var model = CsvRowEditModel.Create(context.Session, context.Projection);
        string[] values = ["x", "y"];

        var insertedId = model.AppendRow(values);
        values[0] = "external mutation";

        Assert.Equal("x", model.GetRow(insertedId).Values[0]);
        Assert.Throws<ArgumentException>(() =>
            model.AppendRow(["1", "2", "3"]));
    }

    [Fact]
    public void Create_RowLimitedProjectionIsRejected()
    {
        const string source = "A\n1\n2\n3";
        var snapshot = CreateSnapshot(source);
        var dialect = CsvDialect.Create(',', headerMode: CsvHeaderMode.FirstRecord);
        var parseResult = CsvParser.Parse(source, dialect);
        var projection = CsvTableProjector.Create(
            parseResult,
            new CsvTableProjectionOptions
            {
                HeaderMode = CsvHeaderMode.FirstRecord,
                MaximumRows = 1,
                MaximumColumns = 512,
                MaximumCells = 250_000
            });
        var session = CsvEditSession.Create(snapshot, parseResult, projection);

        Assert.True(projection.IsRowLimited);
        Assert.Throws<InvalidOperationException>(() =>
            CsvRowEditModel.Create(session, projection));
    }

    private static BuildContext CreateContext(
        string text,
        CsvHeaderMode headerMode = CsvHeaderMode.FirstRecord)
    {
        var snapshot = CreateSnapshot(text);
        var dialect = CsvDialect.Create(',', headerMode: headerMode);
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

        return new BuildContext(session, projection);
    }

    private static ActiveDocumentSnapshot CreateSnapshot(string text)
    {
        return ActiveDocumentSnapshot.Create(
            @"C:\Synthetic\rows.csv",
            text,
            Encoding.UTF8.GetByteCount(text),
            codePage: 65001,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            new DateTimeOffset(2026, 7, 22, 12, 45, 0, TimeSpan.Zero));
    }

    private sealed record BuildContext(
        CsvEditSession Session,
        CsvTableProjection Projection);
}
