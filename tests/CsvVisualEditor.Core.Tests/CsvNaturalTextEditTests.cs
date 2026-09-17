namespace CsvVisualEditor.Core.Tests;

using Xunit;

public sealed class CsvNaturalTextEditTests
{
    [Theory]
    [InlineData("")]
    [InlineData("  a b\\c  ")]
    [InlineData("a\nb\rc\r\nd")]
    [InlineData("\r\n\r\n\n\r")]
    [InlineData("literal\\n\t\u00b7\u00e1\U0001f642")]
    public void DisplayWithoutEditsKeepsExactValue(string raw)
    {
        Assert.True(CsvNaturalTextEdit.TryApply(raw, CsvNaturalTextEdit.ToDisplay(raw), "\r\n", out var actual));
        Assert.Equal(raw, actual);
    }

    [Theory]
    [InlineData("a\nb\rc\r\nd", "X\r\na\r\nb\r\nc\r\nd", "X\r\na\nb\rc\r\nd")]
    [InlineData("a\nb\rc\r\nd", "ab\r\nc\r\nd", "ab\rc\r\nd")]
    [InlineData("a\nb\rc\r\nd", "a\r\nb\r\nX\r\nc\r\nd", "a\nb\rX\r\nc\r\nd")]
    [InlineData("a\nb\rc\r\nd", "a\r\nbc\r\nd", "a\nbc\r\nd")]
    [InlineData("a\nb\rc\r\nd", "a\r\nb\r\nc\r\nd\r\nx", "a\nb\rc\r\nd\r\nx")]
    [InlineData("a\nb\rc\r\nd", "a\r\nb\r\nZ\r\nd", "a\nb\rZ\r\nd")]
    [InlineData("  a b\\c  ", " x y\\z ", " x y\\z ")]
    [InlineData("a\nb", "", "")]
    public void EditsDoNotReassignUntouchedLineEndings(string before, string display, string expected)
    {
        Assert.True(CsvNaturalTextEdit.TryApply(before, display, "\r\n", out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ASecondEditUsesTheLatestExactValue()
    {
        Assert.True(CsvNaturalTextEdit.TryApply("a\nb\rc", "X\r\na\r\nb\r\nc", "\r\n", out var first));
        Assert.True(CsvNaturalTextEdit.TryApply(first, "X\r\na\r\nB\r\nc", "\r\n", out var second));
        Assert.Equal("X\r\na\nB\rc", second);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\r\n")]
    public void NewBreaksUseTheExplicitConvention(string ending)
    {
        Assert.True(CsvNaturalTextEdit.TryApply("ab", "a\r\nb", ending, out var actual));
        Assert.Equal("a" + ending + "b", actual);
        Assert.Equal(ending, CsvNaturalTextEdit.PreferredLineEnding(actual));
    }

    [Fact]
    public void ExplicitMixedInputIsPreserved()
    {
        const string raw = "a\nb\rc\r\nd";
        Assert.True(CsvNaturalTextEdit.TryApply("", raw, "\r\n", out var actual));
        Assert.Equal(raw, actual);
    }

    [Fact]
    public void LimitsNeverTruncate()
    {
        var maximum = new string('a', CsvCellTextCodec.MaximumValueLength);
        Assert.True(CsvNaturalTextEdit.TryApply("", maximum, "\r\n", out var actual));
        Assert.Equal(maximum, actual);
        Assert.False(CsvNaturalTextEdit.TryApply(maximum, maximum + "b", "\r\n", out actual));
        Assert.Empty(actual);
        Assert.Equal("\r\n", CsvNaturalTextEdit.PreferredLineEnding("no lines"));
    }
}
