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
        var model = CreateModel(context);

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
        var model = CreateModel(context);

        Assert.Equal(
            [0, 1],
            model.GetVisibleRows().Select(static row => row.SourceRecordIndex));
    }

    [Fact]
    public void CreatePreview_UnchangedSourceIsReconstructedExactly()
    {
        const string source = "\uFEFFA,B\r\n1,2\n3,4\r";
        var context = CreateContext(source);
        var model = CreateModel(context);

        var preview = model.CreatePreview();

        Assert.False(preview.HasChanges);
        Assert.Equal(source, preview.Text);
        Assert.Equal(context.Snapshot.ContentSha256, preview.ContentSha256);
        Assert.Equal(0, preview.ChangedCellCount);
        Assert.Equal(0, preview.ChangedRowCount);
        Assert.Equal(0, preview.InsertedRowCount);
        Assert.Equal(0, preview.DeletedRowCount);
    }

    [Fact]
    public void AppendRow_AssignsUniqueNegativeIdentityAndPadsValues()
    {
        var context = CreateContext("Name,Age,City\nAlice,30,Budapest");
        var model = CreateModel(context);

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
    public void AppendWithoutTerminalNewLine_AddsOneBoundaryAndNoTerminalNewLine()
    {
        var context = CreateContext("A,B\n1,2");
        var model = CreateModel(context);

        model.AppendRow(["3", "4"]);

        Assert.Equal("A,B\n1,2\n3,4", model.CreatePreview().Text);
    }

    [Fact]
    public void AppendWithTerminalNewLine_PreservesTerminalNewLine()
    {
        var context = CreateContext("A,B\r\n1,2\r\n");
        var model = CreateModel(context);

        model.AppendRow(["3", "4"]);

        Assert.Equal("A,B\r\n1,2\r\n3,4\r\n", model.CreatePreview().Text);
    }

    [Fact]
    public void HeaderOnlyAppendWithoutTerminalNewLine_UsesDetectedDefaultNewLine()
    {
        var context = CreateContext("A,B");
        var model = CreateModel(context);

        model.AppendRow(["1", "2"]);

        Assert.Equal("A,B\r\n1,2", model.CreatePreview().Text);
    }

    [Fact]
    public void HeaderOnlyAppendWithTerminalNewLine_PreservesTerminalState()
    {
        var context = CreateContext("A,B\n");
        var model = CreateModel(context);

        model.AppendRow(["1", "2"]);

        Assert.Equal("A,B\n1,2\n", model.CreatePreview().Text);
    }

    [Fact]
    public void InsertBeforeAndAfter_PreserveStableSourceOrder()
    {
        var context = CreateContext("Name\nAlpha\nGamma");
        var model = CreateModel(context);
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
    public void InsertInMixedSeparatorDocument_PreservesLeftSeparatorAndUsesPolicyAfterInsertedRow()
    {
        var context = CreateContext("Name\r\nAlpha\nOmega\r");
        var model = CreateModel(context);
        var alphaId = model.GetVisibleRows()[0].Id;

        model.InsertRowAfter(alphaId, ["Beta"]);

        Assert.Equal(
            "Name\r\nAlpha\nBeta\r\nOmega\r",
            model.CreatePreview().Text);
    }

    [Fact]
    public void InsertedFields_AreQuotedDeterministically()
    {
        var context = CreateContext("A,B\n1,2");
        var model = CreateModel(context);

        model.AppendRow([" x ", "a,b"]);

        Assert.Equal("A,B\n1,2\n\" x \",\"a,b\"", model.CreatePreview().Text);
    }

    [Fact]
    public void DeleteAndRestoreSourceRow_UpdateVisibilityAndCounters()
    {
        var context = CreateContext("Name\nAlpha\nBeta");
        var model = CreateModel(context);
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
    public void DeleteFirstDataRow_PreservesHeaderBoundarySeparator()
    {
        var context = CreateContext("Name\r\nAlpha\nBeta");
        var model = CreateModel(context);

        model.DeleteRow(model.GetVisibleRows()[0].Id);

        Assert.Equal("Name\r\nBeta", model.CreatePreview().Text);
    }

    [Fact]
    public void DeleteMiddleRow_PreservesSeparatorOfLeftSurvivingSourceRow()
    {
        var context = CreateContext("Name\r\nAlpha\nBeta\rOmega");
        var model = CreateModel(context);

        model.DeleteRow(model.GetVisibleRows()[1].Id);

        Assert.Equal("Name\r\nAlpha\nOmega", model.CreatePreview().Text);
    }

    [Fact]
    public void DeleteLastRowWithoutTerminalNewLine_DoesNotCreateTerminalNewLine()
    {
        var context = CreateContext("Name\nAlpha\nBeta");
        var model = CreateModel(context);

        model.DeleteRow(model.GetVisibleRows()[1].Id);

        Assert.Equal("Name\nAlpha", model.CreatePreview().Text);
    }

    [Fact]
    public void DeleteLastRowWithTerminalNewLine_PreservesTerminalNewLine()
    {
        var context = CreateContext("Name\r\nAlpha\nBeta\r\n");
        var model = CreateModel(context);

        model.DeleteRow(model.GetVisibleRows()[1].Id);

        Assert.Equal("Name\r\nAlpha\r\n", model.CreatePreview().Text);
    }

    [Theory]
    [InlineData("Name\nAlpha", "Name")]
    [InlineData("Name\nAlpha\n", "Name\n")]
    public void DeleteAllDataRows_PreservesHeaderAndOriginalTerminalState(
        string source,
        string expected)
    {
        var context = CreateContext(source);
        var model = CreateModel(context);

        foreach (var row in model.GetVisibleRows())
        {
            model.DeleteRow(row.Id);
        }

        Assert.Equal(expected, model.CreatePreview().Text);
    }

    [Fact]
    public void DeleteEveryHeaderlessRow_RetainsOnlyLeadingBom()
    {
        var context = CreateContext(
            "\uFEFFAlpha\nBeta\n",
            headerMode: CsvHeaderMode.NoHeader);
        var model = CreateModel(context);

        foreach (var row in model.GetVisibleRows())
        {
            model.DeleteRow(row.Id);
        }

        Assert.Equal("\uFEFF", model.CreatePreview().Text);
    }

    [Fact]
    public void DeleteInsertedRow_CancelsInsertionAndRemovesIdentity()
    {
        var context = CreateContext("Name\nAlpha");
        var model = CreateModel(context);
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
    public void SetCellValue_UsesLiveSessionValuesForSourceAndInsertedRows()
    {
        var context = CreateContext("A,B\n1,2");
        var model = CreateModel(context);
        var sourceId = model.GetVisibleRows()[0].Id;
        var insertedId = model.AppendRow(["3", "4"]);

        Assert.True(model.SetCellValue(sourceId, 1, "changed"));
        Assert.True(model.SetCellValue(insertedId, 0, "inserted"));
        Assert.False(model.SetCellValue(insertedId, 0, "inserted"));

        Assert.Equal("changed", model.GetRow(sourceId).Values[1]);
        Assert.Equal("inserted", model.GetRow(insertedId).Values[0]);
        Assert.Equal(1, model.ChangedCellCount);
        Assert.Equal(2, model.ChangedRowCount);
    }

    [Fact]
    public void CombinedCellInsertAndDelete_CreateOneMinimalDifferencePreview()
    {
        var context = CreateContext("A,B\n1,2\n3,4");
        var model = CreateModel(context);
        var rows = model.GetVisibleRows();

        model.SetCellValue(rows[0].Id, 1, "x,y");
        model.DeleteRow(rows[1].Id);
        model.AppendRow(["5", "6"]);
        var preview = model.CreatePreview();

        Assert.Equal("A,B\n1,\"x,y\"\n5,6", preview.Text);
        Assert.True(preview.HasChanges);
        Assert.Equal(1, preview.ChangedCellCount);
        Assert.Equal(3, preview.ChangedRowCount);
        Assert.Equal(1, preview.InsertedRowCount);
        Assert.Equal(1, preview.DeletedRowCount);
    }

    [Fact]
    public void EditedThenDeletedSourceRow_CountsAsOneChangedRow()
    {
        var context = CreateContext("A,B\n1,2");
        var model = CreateModel(context);
        var rowId = model.GetVisibleRows()[0].Id;

        model.SetCellValue(rowId, 1, "changed");
        model.DeleteRow(rowId);

        Assert.Equal(1, model.ChangedCellCount);
        Assert.Equal(1, model.ChangedRowCount);
        Assert.Equal("A,B", model.CreatePreview().Text);
    }

    [Fact]
    public void PaddedSourceCellExtension_IsPreservedInStructuralPreview()
    {
        var context = CreateContext("A,B\n1\n2,3,4");
        var model = CreateModel(context);
        var firstDataId = model.GetVisibleRows()[0].Id;

        model.SetCellValue(firstDataId, 2, "z");

        Assert.Equal("A,B\n1,,z\n2,3,4", model.CreatePreview().Text);
    }

    [Fact]
    public void RevertAll_RemovesInsertionsRestoresDeletionsCellsAndExactSource()
    {
        const string source = "Name\r\nAlpha\nBeta\rGamma";
        var context = CreateContext(source);
        var model = CreateModel(context);
        var baseline = model.GetVisibleRows();

        model.SetCellValue(baseline[0].Id, 0, "Changed");
        model.InsertRowAfter(baseline[0].Id, ["Inserted"]);
        model.DeleteRow(baseline[1].Id);

        Assert.True(model.IsDirty);
        Assert.True(model.RevertAll());
        Assert.False(model.RevertAll());
        Assert.False(model.IsDirty);
        Assert.Equal(0, model.InsertedRowCount);
        Assert.Equal(0, model.DeletedRowCount);
        Assert.Equal(0, model.ChangedCellCount);
        Assert.Equal(source, model.CreatePreview().Text);
        Assert.Equal(
            baseline.Select(static row => row.Id),
            model.GetVisibleRows().Select(static row => row.Id));
    }

    [Fact]
    public void DeletedRow_CannotBeEditedOrUsedAsInsertionAnchor()
    {
        var context = CreateContext("Name\nAlpha\nBeta");
        var model = CreateModel(context);
        var betaId = model.GetVisibleRows()[1].Id;
        model.DeleteRow(betaId);

        Assert.Throws<InvalidOperationException>(() =>
            model.SetCellValue(betaId, 0, "Changed"));
        Assert.Throws<InvalidOperationException>(() =>
            model.InsertRowBefore(betaId, ["Before"]));
        Assert.Throws<InvalidOperationException>(() =>
            model.InsertRowAfter(betaId, ["After"]));
    }

    [Fact]
    public void InsertedValues_AreCopiedAndCannotExceedColumnCount()
    {
        var context = CreateContext("A,B\n1,2");
        var model = CreateModel(context);
        string[] values = ["x", "y"];

        var insertedId = model.AppendRow(values);
        values[0] = "external mutation";

        Assert.Equal("x", model.GetRow(insertedId).Values[0]);
        Assert.Throws<ArgumentException>(() =>
            model.AppendRow(["1", "2", "3"]));
    }

    [Fact]
    public void EmptyCsvHasNoColumnsAndRejectsInsertion()
    {
        var context = CreateContext(
            string.Empty,
            headerMode: CsvHeaderMode.NoHeader);
        var model = CreateModel(context);

        Assert.Equal(0, model.ColumnCount);
        Assert.Throws<InvalidOperationException>(() => model.AppendRow());
    }

    [Fact]
    public void Create_RowLimitedProjectionIsRejected()
    {
        const string source = "A\n1\n2\n3";
        var snapshot = CreateSnapshot(source);
        var dialect = CsvDialect.Create(',', headerMode: CsvHeaderMode.FirstRecord);
        var parseResult = CsvParser.Parse(source, dialect);
        var fullProjection = CsvTableProjector.Create(
            parseResult,
            new CsvTableProjectionOptions
            {
                HeaderMode = CsvHeaderMode.FirstRecord,
                MaximumRows = 10_000,
                MaximumColumns = 512,
                MaximumCells = 250_000
            });
        var limitedProjection = CsvTableProjector.Create(
            parseResult,
            new CsvTableProjectionOptions
            {
                HeaderMode = CsvHeaderMode.FirstRecord,
                MaximumRows = 1,
                MaximumColumns = 512,
                MaximumCells = 250_000
            });
        var session = CsvEditSession.Create(snapshot, parseResult, fullProjection);

        Assert.True(limitedProjection.IsRowLimited);
        Assert.Throws<InvalidOperationException>(() =>
            CsvRowEditModel.Create(
                snapshot,
                parseResult,
                session,
                limitedProjection));
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
        var snapshot = CreateSnapshot(text);
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

        return new BuildContext(snapshot, parseResult, session, projection);
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
            new DateTimeOffset(2026, 7, 23, 8, 0, 0, TimeSpan.Zero));
    }

    private sealed record BuildContext(
        ActiveDocumentSnapshot Snapshot,
        CsvParseResult ParseResult,
        CsvEditSession Session,
        CsvTableProjection Projection);
}
