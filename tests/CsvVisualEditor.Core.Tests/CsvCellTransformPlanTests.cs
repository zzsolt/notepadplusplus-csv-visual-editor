namespace CsvVisualEditor.Core.Tests;

using System.Globalization;
using System.Text;
using Xunit;

public sealed class CsvCellTransformPlanTests
{
    [Theory]
    [InlineData(CsvCellTransformKind.Trim, "  a b \t", "a b")]
    [InlineData(CsvCellTransformKind.Trim, "\u00a0árvíz\u00a0", "árvíz")]
    [InlineData(CsvCellTransformKind.Trim, "\r\n\t ", "")]
    [InlineData(CsvCellTransformKind.Uppercase, "árvíztűrő", "ÁRVÍZTŰRŐ")]
    [InlineData(CsvCellTransformKind.Lowercase, "ÁRVÍZTŰRŐ", "árvíztűrő")]
    [InlineData(CsvCellTransformKind.Uppercase, "", "")]
    public void OperationsProduceExpectedValues(CsvCellTransformKind kind, string before, string after)
    {
        var model = Model();
        var id = model.AppendRow([before, "untouched"]);
        var plan = CsvCellTransformPlan.Create(model, [new(id, 0)], new(kind));
        Assert.Equal(before, model.GetRow(id).Values[0]);
        Assert.Equal(before == after ? 0 : 1, plan.Apply(model));
        Assert.Equal(after, model.GetRow(id).Values[0]);
        Assert.Equal("untouched", model.GetRow(id).Values[1]);
    }

    [Fact]
    public void PreviewDoesNotMutateAndApplyIsPendingOnly()
    {
        var model = Model();
        var plan = Plan(model, new(CsvCellTransformKind.Trim));
        Assert.False(model.IsDirty);
        Assert.Equal(4, plan.TargetCellCount);
        Assert.Equal(2, plan.Changes.Count);
        Assert.Equal(2, plan.ChangedRowCount);
        Assert.Equal(2, plan.Apply(model));
        Assert.Equal("Name,Value\r\nalpha,one\nbeta,two\r\n", model.CreatePreview().Text);
    }

    [Fact]
    public void RevertAllRestoresExactOriginalIncludingMixedSeparators()
    {
        var model = Model();
        var original = model.CreatePreview().Text;
        Plan(model, new(CsvCellTransformKind.Uppercase)).Apply(model);
        Assert.True(model.RevertAll());
        Assert.Equal(original, model.CreatePreview().Text);
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void DuplicateAddressesTransformOnlyOnce()
    {
        var model = Model();
        var address = new CsvCellAddress(model.GetVisibleRows()[0].Id, 0);
        var plan = CsvCellTransformPlan.Create(model, [address, address], new(CsvCellTransformKind.ReplaceText, "a", "aa"));
        Assert.Single(plan.Changes);
        Assert.Equal(1, plan.TargetCellCount);
        plan.Apply(model);
        Assert.Equal("  aalphaa  ", model.GetRow(address.RowId).Values[0]);
    }

    [Theory]
    [InlineData(true, "xAA")]
    [InlineData(false, "xxx")]
    public void LiteralReplaceHonorsMatchCase(bool matchCase, string expected)
    {
        var model = Model("A,B\naAA,z");
        var id = model.GetVisibleRows()[0].Id;
        CsvCellTransformPlan.Create(model, [new(id, 0)], new(CsvCellTransformKind.ReplaceText, "a", "x", matchCase)).Apply(model);
        Assert.Equal(expected, model.GetRow(id).Values[0]);
    }

    [Fact]
    public void ReplaceIsLiteralAndReplacementIsNotRecursivelyReplaced()
    {
        var model = Model("A,B\na.a.*,b");
        var id = model.GetVisibleRows()[0].Id;
        CsvCellTransformPlan.Create(model, [new(id, 0)], new(CsvCellTransformKind.ReplaceText, ".", "..$")).Apply(model);
        Assert.Equal("a..$a..$*", model.GetRow(id).Values[0]);
    }

    [Fact]
    public void EmptyReplacementDeletesMatches()
    {
        var model = Model("A,B\nbanana,x");
        Plan(model, new(CsvCellTransformKind.ReplaceText, "an", "")).Apply(model);
        Assert.Equal("A,B\nba,x", model.CreatePreview().Text);
    }

    [Fact]
    public void EmptyFindIsRejectedWithoutMutation()
    {
        var model = Model();
        Assert.Throws<ArgumentException>(() => Plan(model, new(CsvCellTransformKind.ReplaceText)));
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void CasingIsIndependentOfCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var model = Model("A,B\ni,I");
            Plan(model, new(CsvCellTransformKind.Uppercase)).Apply(model);
            Assert.Equal("A,B\nI,I", model.CreatePreview().Text);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void EmptyTargetsAndNoOpsStayClean()
    {
        var model = Model();
        var empty = CsvCellTransformPlan.Create(model, [], new(CsvCellTransformKind.Trim));
        Assert.Equal(0, empty.Apply(model));
        var noOp = Plan(model, new(CsvCellTransformKind.ReplaceText, "absent", "x"));
        Assert.Empty(noOp.Changes);
        Assert.Equal(0, noOp.ChangedRowCount);
        Assert.Equal(0, noOp.Apply(model));
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void ChangedLateTargetRejectsEntirePreview()
    {
        var model = Model();
        var plan = Plan(model, new(CsvCellTransformKind.Trim));
        var rows = model.GetVisibleRows();
        model.SetCellValue(rows[1].Id, 1, "external pending edit");
        Assert.Throws<InvalidOperationException>(() => plan.Apply(model));
        Assert.Equal("  alpha  ", model.GetRow(rows[0].Id).Values[0]);
        Assert.Equal("external pending edit", model.GetRow(rows[1].Id).Values[1]);
        Assert.Equal(1, model.ChangedCellCount);
    }

    [Fact]
    public void PreviouslyUnchangedTargetAlsoInvalidatesPreview()
    {
        var model = Model();
        var plan = Plan(model, new(CsvCellTransformKind.ReplaceText, "alpha", "ALPHA"));
        model.SetCellValue(model.GetVisibleRows()[1].Id, 1, "alpha");
        Assert.Throws<InvalidOperationException>(() => plan.Apply(model));
        Assert.Equal("  alpha  ", model.GetVisibleRows()[0].Values[0]);
    }

    [Fact]
    public void UnrelatedPendingEditIsPreserved()
    {
        var model = Model();
        var rows = model.GetVisibleRows();
        var plan = CsvCellTransformPlan.Create(model, [new(rows[0].Id, 0)], new(CsvCellTransformKind.Trim));
        model.SetCellValue(rows[1].Id, 1, "keep");
        plan.Apply(model);
        Assert.Equal("keep", model.GetRow(rows[1].Id).Values[1]);
        Assert.Equal(2, model.ChangedCellCount);
    }

    [Fact]
    public void EquivalentButDifferentModelIsRejected()
    {
        var model = Model();
        var other = Model();
        var plan = Plan(model, new(CsvCellTransformKind.Trim));
        Assert.Throws<InvalidOperationException>(() => plan.Apply(other));
        Assert.False(model.IsDirty);
        Assert.False(other.IsDirty);
    }

    [Fact]
    public void DeletedTargetRejectsPreviewWithoutPartialEdits()
    {
        var model = Model();
        var plan = Plan(model, new(CsvCellTransformKind.Trim));
        model.DeleteRow(model.GetVisibleRows()[1].Id);
        Assert.Throws<InvalidOperationException>(() => plan.Apply(model));
        Assert.Equal(0, model.ChangedCellCount);
    }

    [Fact]
    public void CancelledInsertedTargetRejectsPreviewWithoutPartialEdits()
    {
        var model = Model();
        var id = model.AppendRow([" x ", " y "]);
        var plan = Plan(model, new(CsvCellTransformKind.Trim));
        model.DeleteRow(id);
        Assert.Throws<ArgumentOutOfRangeException>(() => plan.Apply(model));
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void InsertedRowsKeepTheirIdentityAndAreRevertible()
    {
        var model = Model();
        var id = model.AppendRow([" x ", " y "]);
        Plan(model, new(CsvCellTransformKind.Trim)).Apply(model);
        Assert.True(model.GetRow(id).IsInserted);
        Assert.Equal(new[] { "x", "y" }, model.GetRow(id).Values);
        model.RevertAll();
        Assert.Equal(0, model.InsertedRowCount);
        Assert.False(model.IsDirty);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void InvalidPhysicalColumnRejectsPlan(int column)
    {
        var model = Model();
        var id = model.GetVisibleRows()[0].Id;
        Assert.Throws<InvalidOperationException>(() => CsvCellTransformPlan.Create(model,
            [new(id, 0), new(id, column)], new(CsvCellTransformKind.Trim)));
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void DeletedRowCannotBePreviewed()
    {
        var model = Model();
        var id = model.GetVisibleRows()[0].Id;
        model.DeleteRow(id);
        Assert.Throws<InvalidOperationException>(() => CsvCellTransformPlan.Create(model,
            [new(id, 0)], new(CsvCellTransformKind.Trim)));
        Assert.Equal(0, model.ChangedCellCount);
    }

    [Fact]
    public void QuoteDelimiterAndNewLineReplacementUsesExistingSerializer()
    {
        var model = Model("A,B\nx,untouched\r\n");
        var id = model.GetVisibleRows()[0].Id;
        CsvCellTransformPlan.Create(model, [new(id, 0)], new(CsvCellTransformKind.ReplaceText, "x", "a,\"b\"\nc")).Apply(model);
        Assert.Equal("A,B\n\"a,\"\"b\"\"\nc\",untouched\r\n", model.CreatePreview().Text);
    }

    [Fact]
    public void PreviewUsesPendingValuesAndCanReturnModelToCleanState()
    {
        var model = Model("A,B\nx,y");
        var id = model.GetVisibleRows()[0].Id;
        model.SetCellValue(id, 0, " x ");
        var plan = Plan(model, new(CsvCellTransformKind.Trim));
        Assert.Equal(" x ", Assert.Single(plan.Changes).Before);
        plan.Apply(model);
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void OversizedReplacementIsRejectedBeforeMutation()
    {
        var model = Model("A,B\nxx,y");
        Assert.Throws<InvalidOperationException>(() => Plan(model,
            new(CsvCellTransformKind.ReplaceText, "x", new string('z', CsvCellTransformPlan.MaximumResultCharacters))));
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void TextBudgetAccumulatesAcrossCells()
    {
        var model = Model("A,B\nx,x");
        Assert.Throws<InvalidOperationException>(() => Plan(model,
            new(CsvCellTransformKind.ReplaceText, "x", new string('z', CsvCellTransformPlan.MaximumResultCharacters / 2 + 1))));
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void TrimCanReduceOversizedInput()
    {
        var model = Model();
        var id = model.AppendRow([new string(' ', CsvCellTransformPlan.MaximumResultCharacters + 1), ""]);
        var plan = CsvCellTransformPlan.Create(model, [new(id, 0)], new(CsvCellTransformKind.Trim));
        plan.Apply(model);
        Assert.Equal("", model.GetRow(id).Values[0]);
    }

    [Fact]
    public void PlanCannotBeAppliedTwiceAfterChangingValues()
    {
        var model = Model();
        var plan = Plan(model, new(CsvCellTransformKind.Trim));
        plan.Apply(model);
        var preview = model.CreatePreview().Text;
        Assert.Throws<InvalidOperationException>(() => plan.Apply(model));
        Assert.Equal(preview, model.CreatePreview().Text);
    }

    private static CsvCellTransformPlan Plan(CsvRowEditModel model, CsvCellTransform transform) =>
        CsvCellTransformPlan.Create(model, model.GetVisibleRows().SelectMany(row =>
            Enumerable.Range(0, model.ColumnCount).Select(column => new CsvCellAddress(row.Id, column))), transform);

    private static CsvRowEditModel Model(string source = "Name,Value\r\n  alpha  ,one\nbeta, two \r\n")
    {
        var snapshot = ActiveDocumentSnapshot.Create("synthetic-transform.csv", source,
            Encoding.UTF8.GetByteCount(source), 65001, 0, 0, false, DateTimeOffset.UnixEpoch);
        var parse = CsvParser.Parse(source, CsvDialect.Create(',', headerMode: CsvHeaderMode.FirstRecord));
        var projection = CsvTableProjector.Create(parse, new CsvTableProjectionOptions
        {
            HeaderMode = CsvHeaderMode.FirstRecord, MaximumRows = 10_000,
            MaximumColumns = 512, MaximumCells = 250_000
        });
        return CsvRowEditModel.Create(snapshot, parse, CsvEditSession.Create(snapshot, parse, projection), projection);
    }
}
