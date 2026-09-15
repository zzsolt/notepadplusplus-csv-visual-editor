namespace CsvVisualEditor.Core.Tests;

using System.Text;
using Xunit;

public sealed class CsvCellDetailsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("  value  ")]
    [InlineData("x\r\ny\nz\rnext\tlast")]
    [InlineData("C:\\new\\test.csv")]
    [InlineData("literal\\n and \u00b7 and \u00a0 and \u202e")]
    [InlineData("\u00e1rv\u00edzt\u0171r\u0151 \u4e2d\u6587 \u0627\u0644\u0639\u0631\u0628\u064a\u0629")]
    [InlineData("\U0001f642\ud800\udfff")]
    [InlineData("\0\u0001\u0085\u2028\ufeff")]
    public void ExactNotationRoundTripsEveryCharacter(string value)
    {
        Assert.True(CsvCellTextCodec.TryDecode(CsvCellTextCodec.Encode(value), out var actual, out _));
        Assert.Equal(value, actual);
    }

    [Fact]
    public void AllUtf16CodeUnitsRoundTripIncludingIsolatedSurrogates()
    {
        var source = new string(Enumerable.Range(0, 65_536).Select(i => (char)i).ToArray());
        Assert.True(CsvCellTextCodec.TryDecode(CsvCellTextCodec.Encode(source), out var actual, out _));
        Assert.Equal(source, actual);
    }

    [Fact]
    public void SpacesMarkersAndTypedLiteralEscapesAreUnambiguous()
    {
        Assert.Equal("\u00b7\u00b7x\u00b7", CsvCellTextCodec.Encode("  x "));
        Assert.Equal("\\u00B7", CsvCellTextCodec.Encode("\u00b7"));
        Assert.Equal("\\\\n", CsvCellTextCodec.Encode("\\n"));
        Assert.True(CsvCellTextCodec.TryDecode(" a\u00b7\\t\\r\\n\\u00b7\\\\", out var actual, out _));
        Assert.Equal(" a \t\r\n\u00b7\\", actual);
    }

    [Theory]
    [InlineData("\\", 0)]
    [InlineData("a\\x", 1)]
    [InlineData("\\u", 0)]
    [InlineData("x\\u123", 1)]
    [InlineData("x\\u12G4", 1)]
    [InlineData("valid\\nthen\\q", 11)]
    public void MalformedNotationFailsWithoutAPartialValue(string input, int expected)
    {
        Assert.False(CsvCellTextCodec.TryDecode(input, out var value, out var offset));
        Assert.Empty(value);
        Assert.Equal(expected, offset);
    }

    [Fact]
    public void LengthLimitsAreExactAndNeverTruncate()
    {
        var exact = new string('x', CsvCellTextCodec.MaximumValueLength);
        Assert.True(CsvCellTextCodec.TryDecode(exact, out var result, out _));
        Assert.Equal(exact, result);
        Assert.False(CsvCellTextCodec.TryDecode(exact + "x", out result, out var offset));
        Assert.Empty(result);
        Assert.Equal(-1, offset);
        Assert.Throws<ArgumentOutOfRangeException>(() => CsvCellTextCodec.Encode(exact + "x"));
        var escaped = CsvCellTextCodec.Encode(new string('\0', CsvCellTextCodec.MaximumValueLength));
        Assert.Equal(CsvCellTextCodec.MaximumEditorLength, escaped.Length);
        Assert.True(CsvCellTextCodec.TryDecode(escaped, out result, out _));
        Assert.Equal(CsvCellTextCodec.MaximumValueLength, result.Length);
    }

    [Fact]
    public void CountsDistinguishMixedLineEndingsAndEmptyFromWhitespace()
    {
        Assert.Equal(new CsvCellTextMetrics(0, 0, 0, 0, 0, 0), CsvCellTextCodec.Measure(""));
        Assert.Equal(new CsvCellTextMetrics(10, 2, 1, 1, 1, 2), CsvCellTextCodec.Measure("  \t\r\n\n\rX\rY"));
    }

    [Fact]
    public void PreviewDoesNotEditAndAcceptOnlyUpdatesPendingModel()
    {
        var model = Model();
        var before = model.CreatePreview().Text;
        var id = model.GetVisibleRows()[0].Id;
        var change = CsvCellValueChange.Create(model, new(id, 0), "\"a,b\"\r\nx\nend\r");
        Assert.Equal(before, model.CreatePreview().Text);
        Assert.False(model.IsDirty);
        Assert.True(change.Apply(model));
        Assert.Equal(change.After, model.GetRow(id).Values[0]);
        Assert.Equal("untouched", model.GetRow(id).Values[1]);
        var parsed = CsvParser.Parse(model.CreatePreview().Text, CsvDialect.Create(','));
        Assert.Equal(change.After, parsed.Records[1].Cells[0].Value);
        Assert.True(model.RevertAll());
        Assert.Equal(before, model.CreatePreview().Text);
    }

    [Fact]
    public void NoChangePreservesOriginalQuotingAndMixedRecordSeparators()
    {
        var model = Model();
        var source = model.CreatePreview().Text;
        var row = model.GetVisibleRows()[0];
        Assert.False(CsvCellValueChange.Create(model, new(row.Id, 0), row.Values[0]).Apply(model));
        Assert.False(model.IsDirty);
        Assert.Equal(source, model.CreatePreview().Text);
    }

    [Fact]
    public void EmptyValueIsARealAcceptedChange()
    {
        var model = Model();
        var id = model.GetVisibleRows()[0].Id;
        Assert.True(CsvCellValueChange.Create(model, new(id, 0), "").Apply(model));
        Assert.Equal("", model.GetRow(id).Values[0]);
        Assert.True(model.IsDirty);
    }

    [Fact]
    public void StaleCellCannotOverwriteANewerEdit()
    {
        var model = Model(); var id = model.GetVisibleRows()[0].Id;
        var change = CsvCellValueChange.Create(model, new(id, 0), "new");
        model.SetCellValue(id, 0, "external");
        Assert.Throws<InvalidOperationException>(() => change.Apply(model));
        Assert.Equal("external", model.GetRow(id).Values[0]);
    }

    [Fact]
    public void AnotherModelWithIdenticalTextIsRejected()
    {
        var model = Model(); var other = Model();
        var change = CsvCellValueChange.Create(model, new(model.GetVisibleRows()[0].Id, 0), "new");
        Assert.Throws<InvalidOperationException>(() => change.Apply(other));
        Assert.False(other.IsDirty);
    }

    [Fact]
    public void DeletedTargetIsRejectedAndInsertedRowKeepsItsStableId()
    {
        var model = Model(); var id = model.GetVisibleRows()[0].Id;
        var change = CsvCellValueChange.Create(model, new(id, 0), "new");
        model.DeleteRow(id);
        Assert.Throws<InvalidOperationException>(() => change.Apply(model));
        var inserted = model.AppendRow(["first", "second"]);
        Assert.True(CsvCellValueChange.Create(model, new(inserted, 1), "\t\n").Apply(model));
        Assert.Equal(inserted, model.GetRow(inserted).Id);
        Assert.Equal("first", model.GetRow(inserted).Values[0]);
        Assert.Equal("\t\n", model.GetRow(inserted).Values[1]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void PresentationOrInvalidColumnCannotBeEdited(int column)
    {
        var model = Model();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CsvCellValueChange.Create(model, new(model.GetVisibleRows()[0].Id, column), "x"));
        Assert.False(model.IsDirty);
    }

    private static CsvRowEditModel Model()
    {
        const string text = "Value,Note\r\n\"  original  \",untouched\nlast,end\r\n";
        var snapshot = ActiveDocumentSnapshot.Create("cell-details.csv", text,
            Encoding.UTF8.GetByteCount(text), 65001, 0, 0, false, DateTimeOffset.UnixEpoch);
        var parse = CsvParser.Parse(text, CsvDialect.Create(',', headerMode: CsvHeaderMode.FirstRecord));
        var projection = CsvTableProjector.Create(parse, new CsvTableProjectionOptions { HeaderMode = CsvHeaderMode.FirstRecord });
        return CsvRowEditModel.Create(snapshot, parse, CsvEditSession.Create(snapshot, parse, projection), projection);
    }
}
