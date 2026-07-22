namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using Xunit;

public sealed class CsvSerializerTests
{
    [Fact]
    public void Detect_IgnoresQuotedLineBreakAndFindsFirstRecordSeparator()
    {
        const string source = "\"line1\nline2\",Value\r\nx,y\r\n";

        var policy = CsvSerializationPolicy.Detect(
            source,
            CsvDialect.Create(','));

        Assert.Equal("\r\n", policy.NewLine);
        Assert.True(policy.HasTerminalNewLine);
        Assert.False(policy.HasLeadingBom);
    }

    [Fact]
    public void Detect_LeadingBomAndNoSeparator_UsesDeterministicCrLfDefault()
    {
        const string source = "\uFEFFA,B";

        var policy = CsvSerializationPolicy.Detect(
            source,
            CsvDialect.Create(','));

        Assert.Equal("\r\n", policy.NewLine);
        Assert.False(policy.HasTerminalNewLine);
        Assert.True(policy.HasLeadingBom);
    }

    [Fact]
    public void SerializeRecords_QuotesStructuralCharactersAndBoundaryWhitespace()
    {
        var policy = CsvSerializationPolicy.Create(
            CsvDialect.Create(','),
            "\n",
            hasTerminalNewLine: false);
        string[][] records =
        [
            ["plain", "a,b", "a\"b", " line ", "line\nbreak", ""]
        ];

        var text = CsvSerializer.SerializeRecords(records, policy);

        Assert.Equal(
            "plain,\"a,b\",\"a\"\"b\",\" line \",\"line\nbreak\",",
            text);
    }

    [Fact]
    public void SerializeRecords_SemicolonAndTerminalCrLf_ArePreserved()
    {
        var policy = CsvSerializationPolicy.Create(
            CsvDialect.Create(';'),
            "\r\n",
            hasTerminalNewLine: true);
        string[][] records =
        [
            ["A", "B"],
            ["1", "2;3"]
        ];

        var text = CsvSerializer.SerializeRecords(records, policy);

        Assert.Equal("A;B\r\n1;\"2;3\"\r\n", text);
    }

    [Fact]
    public void SerializeRecords_TabDelimiter_QuotesEmbeddedTab()
    {
        var policy = CsvSerializationPolicy.Create(
            CsvDialect.Create('\t'),
            "\n",
            hasTerminalNewLine: false);
        string[][] records =
        [
            ["A", "B"],
            ["1", "two\tparts"]
        ];

        var text = CsvSerializer.SerializeRecords(records, policy);

        Assert.Equal("A\tB\n1\t\"two\tparts\"", text);
    }

    [Fact]
    public void SerializeRecords_LeadingBomAndUnicode_ArePreserved()
    {
        var policy = CsvSerializationPolicy.Create(
            CsvDialect.Create(','),
            "\n",
            hasTerminalNewLine: false,
            hasLeadingBom: true);
        string[][] records =
        [
            ["Név", "Érték"],
            ["Árvíztűrő tükörfúrógép", "東京"]
        ];

        var text = CsvSerializer.SerializeRecords(records, policy);

        Assert.Equal(
            "\uFEFFNév,Érték\nÁrvíztűrő tükörfúrógép,東京",
            text);
    }

    [Fact]
    public void SerializeRecord_EmptyAndTrailingFields_RemainRepresented()
    {
        var policy = CsvSerializationPolicy.Create(
            CsvDialect.Create(','),
            "\r\n",
            hasTerminalNewLine: false);

        var text = CsvSerializer.SerializeRecord(
            ["", "value", ""],
            fieldCount: 3,
            policy);

        Assert.Equal(",value,", text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\r\n\r\n")]
    [InlineData("x")]
    public void Create_InvalidRecordSeparator_Throws(string newLine)
    {
        Assert.Throws<ArgumentException>(() =>
            CsvSerializationPolicy.Create(
                CsvDialect.Create(','),
                newLine,
                hasTerminalNewLine: false));
    }

    [Fact]
    public void SerializeRecord_FieldCountBeyondValues_Throws()
    {
        var policy = CsvSerializationPolicy.Create(
            CsvDialect.Create(','),
            "\n",
            hasTerminalNewLine: false);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CsvSerializer.SerializeRecord(["A"], 2, policy));
    }
}
