namespace CsvVisualEditor.Core.Tests;

using System.Globalization;
using CsvVisualEditor.Core;
using Xunit;

public sealed class DataExplorationTests
{
    [Theory]
    [InlineData("0", "0")]
    [InlineData("-0.000", "0")]
    [InlineData("+0012.5000", "12.5")]
    [InlineData(" .25 ", "0.25")]
    [InlineData("-12.", "-12")]
    [InlineData("\t-1.5\r\n", "-1.5")]
    [InlineData("79228162514264337593543950335", "79228162514264337593543950335")]
    [InlineData("-79228162514264337593543950335", "-79228162514264337593543950335")]
    [InlineData("0.0000000000000000000000000001", "0.0000000000000000000000000001")]
    [InlineData("79228162514264337593543950335.000000", "79228162514264337593543950335")]
    [InlineData("1.2345678901234567890123456789", "1.2345678901234567890123456789")]
    [InlineData("000000000000000000000000000000000000001", "1")]
    public void ExactNumbers_AcceptOnlyLosslessValues(string text, string expected)
    {
        Assert.True(CsvNumericValue.TryParse(text, out var number));
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), number);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("+")]
    [InlineData("-.")]
    [InlineData("1,000")]
    [InlineData("1,5")]
    [InlineData("1e3")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("$12")]
    [InlineData("12-")]
    [InlineData("1 2")]
    [InlineData("1.2.3")]
    [InlineData("\u0661")]
    [InlineData("79228162514264337593543950336")]
    [InlineData("79228162514264337593543950334.9")]
    [InlineData("0.00000000000000000000000000001")]
    [InlineData("1.23456789012345678901234567891")]
    public void ExactNumbers_RejectInvalidAndRoundedValues(string? text)
    {
        Assert.False(CsvNumericValue.TryParse(text, out var number));
        Assert.Equal(0m, number);
    }

    [Theory]
    [InlineData(CsvFilterOperator.Contains, "bc", "AbCd", true)]
    [InlineData(CsvFilterOperator.DoesNotContain, "bc", "AbCd", false)]
    [InlineData(CsvFilterOperator.Equals, "ABC", "abc", true)]
    [InlineData(CsvFilterOperator.DoesNotEqual, "ABC", "abc", false)]
    [InlineData(CsvFilterOperator.StartsWith, "ab", "AbCd", true)]
    [InlineData(CsvFilterOperator.EndsWith, "cd", "AbCd", true)]
    [InlineData(CsvFilterOperator.Contains, "  ", "x  y", true)]
    [InlineData(CsvFilterOperator.Contains, "  ", "x y", false)]
    [InlineData(CsvFilterOperator.Equals, " ", "", false)]
    [InlineData(CsvFilterOperator.Equals, " ", " ", true)]
    [InlineData(CsvFilterOperator.Contains, "", "anything", true)]
    [InlineData(CsvFilterOperator.IsEmpty, "ignored", "", true)]
    [InlineData(CsvFilterOperator.IsEmpty, "", " ", false)]
    [InlineData(CsvFilterOperator.IsNotEmpty, "", " ", true)]
    [InlineData(CsvFilterOperator.IsWhitespace, "", " \t\n\u00a0", true)]
    [InlineData(CsvFilterOperator.IsWhitespace, "", "", false)]
    [InlineData(CsvFilterOperator.IsWhitespace, "", " a ", false)]
    [InlineData(CsvFilterOperator.IsNotWhitespace, "", "", true)]
    [InlineData(CsvFilterOperator.IsNotWhitespace, "", "\t", false)]
    [InlineData(CsvFilterOperator.NumberEquals, "1.00", "+01", true)]
    [InlineData(CsvFilterOperator.GreaterThan, "2", "10", true)]
    [InlineData(CsvFilterOperator.GreaterThan, "2", "2", false)]
    [InlineData(CsvFilterOperator.GreaterThanOrEqual, "2", "2.0", true)]
    [InlineData(CsvFilterOperator.LessThan, "-1", "-2", true)]
    [InlineData(CsvFilterOperator.LessThanOrEqual, "2", "2", true)]
    [InlineData(CsvFilterOperator.LessThan, "2", "garbage", false)]
    [InlineData(CsvFilterOperator.LessThan, "2", "", false)]
    public void Predicates_UseExplicitSemantics(CsvFilterOperator op, string operand, string value, bool matches)
    {
        var view = View(Table([value]), new CsvColumnFilter(0, op, operand));
        Assert.Equal(matches ? 1 : 0, view.VisibleRowCount);
        Assert.True(view.IsFiltered);
    }

    [Theory]
    [InlineData(CsvFilterOperator.Contains)]
    [InlineData(CsvFilterOperator.Equals)]
    [InlineData(CsvFilterOperator.StartsWith)]
    [InlineData(CsvFilterOperator.EndsWith)]
    public void CaseSensitivity_IsOptIn(CsvFilterOperator op)
    {
        var projection = Table(["Alpha"]);
        Assert.Single(View(projection, new CsvColumnFilter(0, op, "alpha")).Rows);
        Assert.Empty(View(projection, new CsvColumnFilter(0, op, "alpha", matchCase: true)).Rows);
    }

    [Fact]
    public void AllAny_ComposeWithQuickSearchAndKeepSourceIdentity()
    {
        var projection = Table(["alpha", "10"], ["beta", "2"], ["alpha", "1"], ["gamma", "20"]);
        var filters = new[] { new CsvColumnFilter(0, CsvFilterOperator.Contains, "alpha"), new CsvColumnFilter(1, CsvFilterOperator.GreaterThan, "5") };
        var all = CsvTableViewBuilder.Build(projection, new() { DataView = new(filters) });
        var any = CsvTableViewBuilder.Build(projection, new() { DataView = new(filters, CsvFilterCombination.Any) });
        var quick = CsvTableViewBuilder.Build(projection, new() { SearchText = " ALPHA ", SearchColumnIndex = 0, DataView = new(filters, CsvFilterCombination.Any) });
        Assert.Equal([1], all.Rows.Select(r => r.SourceRecordIndex));
        Assert.Equal([1, 3, 4], any.Rows.Select(r => r.SourceRecordIndex));
        Assert.Equal([1, 3], quick.Rows.Select(r => r.SourceRecordIndex));
        Assert.Same(projection.Rows[2], quick.Rows[1]);
        Assert.Equal(4, quick.TotalRowCount);
        Assert.Equal([1, 2, 3, 4], projection.Rows.Select(r => r.SourceRecordIndex));
        Assert.False(CsvTableViewBuilder.Build(projection, new() { DataView = new(combination: CsvFilterCombination.Any) }).IsFiltered);
    }

    [Fact]
    public void MultiSort_UsesNumericOrderInvalidLastAndStableTies()
    {
        var p = Table(["a", "10"], ["B", "2"], ["A", "2"], ["a", "2.0"], ["a", "oops"], ["a", ""]);
        var ascending = CsvTableViewBuilder.Build(p, new() { DataView = new(sortKeys: [new(0, CsvTableSortDirection.Ascending), new(1, CsvTableSortDirection.Ascending, CsvSortKind.Number)]) });
        var descending = CsvTableViewBuilder.Build(p, new() { DataView = new(sortKeys: [new(1, CsvTableSortDirection.Descending, CsvSortKind.Number), new(0, CsvTableSortDirection.Ascending)]) });
        Assert.Equal([3, 4, 1, 5, 6, 2], ascending.Rows.Select(r => r.SourceRecordIndex));
        Assert.Equal([1, 3, 4, 2, 5, 6], descending.Rows.Select(r => r.SourceRecordIndex));
        Assert.True(ascending.IsSorted);
        Assert.False(ascending.IsFiltered);
        Assert.Equal(0, ascending.SortColumnIndex);
        Assert.Same(p.Rows[2], ascending.Rows[0]);
    }

    [Fact]
    public void ThirdSortLevel_IsAppliedOnlyToTies()
    {
        var p = Table(["a", "2", "z"], ["a", "2.0", "b"], ["a", "10", "a"], ["b", "1", "a"]);
        var result = CsvTableViewBuilder.Build(p, new() { DataView = new(sortKeys: [new(0, CsvTableSortDirection.Ascending), new(1, CsvTableSortDirection.Ascending, CsvSortKind.Number), new(2, CsvTableSortDirection.Ascending)]) });
        Assert.Equal([2, 1, 3, 4], result.Rows.Select(r => r.SourceRecordIndex));
    }

    [Fact]
    public void Options_DefensivelyCopyAndBoundEnumerations()
    {
        var filters = new List<CsvColumnFilter> { new(0, CsvFilterOperator.Contains, "alpha") };
        var keys = new List<CsvSortKey> { new(0, CsvTableSortDirection.Ascending) };
        var d = new CsvDataViewDefinition(filters, sortKeys: keys);
        filters.Clear(); keys.Clear();
        Assert.Single(d.Filters); Assert.Single(d.SortKeys);
        Assert.Throws<NotSupportedException>(() => ((IList<CsvColumnFilter>)d.Filters).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<CsvSortKey>)d.SortKeys).Clear());
        Assert.Throws<ArgumentException>(() => new CsvDataViewDefinition(Enumerable.Repeat(new CsvColumnFilter(0, CsvFilterOperator.IsEmpty), 9)));
        Assert.Throws<ArgumentException>(() => new CsvDataViewDefinition(sortKeys: Enumerable.Range(0, 4).Select(i => new CsvSortKey(i, CsvTableSortDirection.Ascending))));
        Assert.Throws<ArgumentException>(() => new CsvDataViewDefinition([null!]));
        Assert.Throws<ArgumentException>(() => new CsvDataViewDefinition(sortKeys: [null!]));
    }

    [Fact]
    public void InvalidDefinitions_FailBeforeScan()
    {
        var p = Table(["alpha"]);
        Assert.Throws<ArgumentException>(() => new CsvDataViewDefinition(sortKeys: [new(0, CsvTableSortDirection.Ascending), new(0, CsvTableSortDirection.Descending)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CsvColumnFilter(-1, CsvFilterOperator.Contains));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CsvColumnFilter(0, (CsvFilterOperator)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CsvSortKey(0, CsvTableSortDirection.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CsvSortKey(0, CsvTableSortDirection.Ascending, (CsvSortKind)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CsvDataViewDefinition(combination: (CsvFilterCombination)99));
        Assert.Throws<ArgumentException>(() => new CsvColumnFilter(0, CsvFilterOperator.Contains, new string('x', 4097)));
        Assert.Throws<ArgumentException>(() => new CsvColumnFilter(0, CsvFilterOperator.NumberEquals, "1,5"));
        Assert.Throws<ArgumentException>(() => View(p, new CsvColumnFilter(1, CsvFilterOperator.IsEmpty)));
        Assert.Throws<ArgumentException>(() => CsvTableViewBuilder.Build(p, new() { DataView = new(sortKeys: [new(1, CsvTableSortDirection.Ascending)]) }));
        Assert.Throws<ArgumentException>(() => CsvTableViewBuilder.Build(p, new() { SortColumnIndex = 0, SortDirection = CsvTableSortDirection.Ascending, DataView = new(sortKeys: [new(0, CsvTableSortDirection.Ascending)]) }));
        Assert.Throws<ArgumentNullException>(() => new CsvColumnFilter(0, CsvFilterOperator.Contains, null!));
        Assert.Throws<ArgumentNullException>(() => CsvTableViewBuilder.Build(p, new() { DataView = null! }));
    }

    [Fact]
    public void Profile_IsExactAndUsesOnlyCurrentView()
    {
        var p = Table([""], [" "], ["A"], ["a"], ["A"], ["2"], ["2.0"], ["-1"], [" "]);
        var profile = CsvColumnProfile.Build(CsvTableViewBuilder.Build(p), 0, p.ColumnCount);
        Assert.Equal(9, profile.RowCount); Assert.Equal(1, profile.EmptyCount); Assert.Equal(2, profile.WhitespaceOnlyCount);
        Assert.Equal(7, profile.DistinctCount); Assert.Equal(2, profile.DuplicateOccurrenceCount); Assert.Equal(2, profile.RepeatedValueCount);
        Assert.Equal(3, profile.NumericCount); Assert.Equal(6, profile.NonNumericCount);
        Assert.Equal(-1m, profile.Minimum); Assert.Equal(2m, profile.Maximum); Assert.Equal(3, profile.MaximumLength);
        Assert.Equal(" ", profile.MostFrequent[0].Value); Assert.Equal(2, profile.MostFrequent[0].Count);
        var filtered = View(p, new(0, CsvFilterOperator.NumberEquals, "2"));
        var numeric = CsvColumnProfile.Build(filtered, 0, 1, 1);
        Assert.Equal(2, numeric.RowCount); Assert.Equal(2, numeric.DistinctCount); Assert.Single(numeric.MostFrequent);
        Assert.Equal(2m, numeric.Minimum); Assert.Equal(2m, numeric.Maximum);
        Assert.Throws<NotSupportedException>(() => ((IList<CsvValueFrequency>)numeric.MostFrequent).Clear());
    }

    [Fact]
    public void EmptyViewsAndProjectionLimits_AreNotWholeDocumentClaims()
    {
        var columns = new[] { new CsvTableColumn(0, "Value") };
        var p = new CsvTableProjection(columns, [new CsvTableRow(5, ["alpha"], new CsvSourceSpan(1, 5))], 100, 0, true);
        var view = View(p, new(0, CsvFilterOperator.Equals, "not present"));
        Assert.Equal(1, view.TotalRowCount); Assert.Empty(view.Rows);
        var profile = CsvColumnProfile.Build(view, 0, 1);
        Assert.Equal(0, profile.RowCount); Assert.Null(profile.Minimum); Assert.Null(profile.Maximum); Assert.Empty(profile.MostFrequent);
        Assert.Throws<ArgumentOutOfRangeException>(() => CsvColumnProfile.Build(view, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CsvColumnProfile.Build(view, 0, 1, 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => CsvColumnProfile.Build(view, 0, 1, -1));
        Assert.Throws<ArgumentNullException>(() => CsvColumnProfile.Build(null!, 0, 1));
    }

    [Fact]
    public void FullValues_AreFilteredBeyondPaintTruncation()
    {
        var p = Table([new string('x', 2000) + "Needle"], ["literal.*[regex]"]);
        Assert.Single(View(p, new(0, CsvFilterOperator.EndsWith, "Needle")).Rows);
        Assert.Single(View(p, new(0, CsvFilterOperator.Contains, ".*[regex]")).Rows);
        Assert.Empty(View(p, new(0, CsvFilterOperator.Contains, "^literal")).Rows);
    }

    private static CsvTableViewResult View(CsvTableProjection p, CsvColumnFilter filter) =>
        CsvTableViewBuilder.Build(p, new() { DataView = new([filter]) });

    private static CsvTableProjection Table(params string[][] values) => new(
        Enumerable.Range(0, values[0].Length).Select(i => new CsvTableColumn(i, "Column " + (i + 1))),
        values.Select((row, i) => new CsvTableRow(i + 1, row, new CsvSourceSpan(i * 10, row.Sum(v => v.Length)))),
        values.Length, 0, false);
}
