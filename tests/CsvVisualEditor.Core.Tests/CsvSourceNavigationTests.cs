namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using System.Text;
using Xunit;

public sealed class CsvSourceNavigationTests
{
    [Fact]
    public void Create_Utf8Cell_MapsCharacterSpanToScintillaBytePositions()
    {
        const string text = "Név,City\r\nÁrvíz,Budapest\r\n";
        var snapshot = CreateSnapshot(text, CsvEncodingProfiles.Utf8CodePage);
        var parse = CsvParser.Parse(text, CsvDialect.Create(','));
        var address = new CsvSourceNavigationAddress(sourceRecordIndex: 1, columnIndex: 1);

        var plan = CsvSourceNavigationPlanner.Create(snapshot, snapshot, parse, address);

        Assert.True(plan.IsReady);
        var span = Assert.IsType<CsvSourceSpan>(plan.CharacterSpan);
        Assert.Equal(parse.Records[1].Cells[1].SourceSpan, span);
        var utf8 = new UTF8Encoding(false, true);
        Assert.Equal(
            utf8.GetByteCount(text.AsSpan(0, span.Start)),
            plan.AnchorBytePosition);
        Assert.Equal(
            utf8.GetByteCount(text.AsSpan(0, span.End)),
            plan.CaretBytePosition);
        Assert.True(plan.AnchorBytePosition > span.Start);
    }

    [Fact]
    public void Create_Windows1250HungarianCell_UsesSingleBytePositions()
    {
        const string text = "Név,Város\r\nÁrvíztűrő,Budapest\r\n";
        var snapshot = CreateSnapshot(text, CsvEncodingProfiles.Windows1250CodePage);
        var parse = CsvParser.Parse(text, CsvDialect.Create(','));
        var plan = CsvSourceNavigationPlanner.Create(
            snapshot,
            snapshot,
            parse,
            new CsvSourceNavigationAddress(1, 0));

        Assert.True(plan.IsReady);
        var span = Assert.IsType<CsvSourceSpan>(plan.CharacterSpan);
        Assert.Equal(span.Start, plan.AnchorBytePosition);
        Assert.Equal(span.End, plan.CaretBytePosition);
    }

    [Fact]
    public void Create_RecordTarget_SelectsRawLogicalRecordWithoutSeparator()
    {
        const string text = "A,B\r\n1,2\r\n3,4\r\n";
        var snapshot = CreateSnapshot(text, CsvEncodingProfiles.Utf8CodePage);
        var parse = CsvParser.Parse(text, CsvDialect.Create(','));
        var plan = CsvSourceNavigationPlanner.Create(
            snapshot,
            snapshot,
            parse,
            new CsvSourceNavigationAddress(2, columnIndex: null));

        Assert.True(plan.IsReady);
        var span = Assert.IsType<CsvSourceSpan>(plan.CharacterSpan);
        Assert.Equal("3,4", text.Substring(span.Start, span.Length));
    }

    [Fact]
    public void Create_QuotedMultilineCell_SelectsCompleteRawField()
    {
        const string text = "A,B\r\n1,\"two\r\nlines\"\r\n";
        var snapshot = CreateSnapshot(text, CsvEncodingProfiles.Utf8CodePage);
        var parse = CsvParser.Parse(text, CsvDialect.Create(','));
        var plan = CsvSourceNavigationPlanner.Create(
            snapshot,
            snapshot,
            parse,
            new CsvSourceNavigationAddress(1, 1));

        Assert.True(plan.IsReady);
        var span = Assert.IsType<CsvSourceSpan>(plan.CharacterSpan);
        Assert.Equal("\"two\r\nlines\"", text.Substring(span.Start, span.Length));
    }

    [Fact]
    public void Create_MissingProjectedField_BlocksWithoutInventingSourceSpan()
    {
        const string text = "A,B,C\n1,2\n";
        var snapshot = CreateSnapshot(text, CsvEncodingProfiles.Utf8CodePage);
        var parse = CsvParser.Parse(text, CsvDialect.Create(','));
        var plan = CsvSourceNavigationPlanner.Create(
            snapshot,
            snapshot,
            parse,
            new CsvSourceNavigationAddress(1, 2));

        Assert.Equal(CsvSourceNavigationStatus.SourceColumnUnavailable, plan.Status);
        Assert.False(plan.IsReady);
        Assert.Null(plan.CharacterSpan);
    }

    [Fact]
    public void Create_UnknownRecord_BlocksSafely()
    {
        const string text = "A,B\n1,2\n";
        var snapshot = CreateSnapshot(text, CsvEncodingProfiles.Utf8CodePage);
        var parse = CsvParser.Parse(text, CsvDialect.Create(','));
        var plan = CsvSourceNavigationPlanner.Create(
            snapshot,
            snapshot,
            parse,
            new CsvSourceNavigationAddress(99, 0));

        Assert.Equal(CsvSourceNavigationStatus.SourceRecordUnavailable, plan.Status);
        Assert.False(plan.IsReady);
    }

    [Fact]
    public void Create_DifferentDocument_BlocksBeforePositionMapping()
    {
        const string text = "A,B\n1,2\n";
        var source = CreateSnapshot(text, CsvEncodingProfiles.Utf8CodePage, @"C:\Data\one.csv");
        var current = CreateSnapshot(text, CsvEncodingProfiles.Utf8CodePage, @"C:\Data\two.csv");
        var parse = CsvParser.Parse(text, CsvDialect.Create(','));

        var plan = CsvSourceNavigationPlanner.Create(
            source,
            current,
            parse,
            new CsvSourceNavigationAddress(1, 0));

        Assert.Equal(CsvSourceNavigationStatus.DocumentIdentityChanged, plan.Status);
    }

    [Fact]
    public void Create_ChangedCodePage_BlocksBeforeContentNavigation()
    {
        const string text = "A,B\n1,2\n";
        var source = CreateSnapshot(text, CsvEncodingProfiles.Utf8CodePage);
        var current = CreateSnapshot(text, CsvEncodingProfiles.Windows1250CodePage);
        var parse = CsvParser.Parse(text, CsvDialect.Create(','));

        var plan = CsvSourceNavigationPlanner.Create(
            source,
            current,
            parse,
            new CsvSourceNavigationAddress(1, 0));

        Assert.Equal(CsvSourceNavigationStatus.CodePageChanged, plan.Status);
    }

    [Fact]
    public void Create_ChangedContent_BlocksStaleSourceOffsets()
    {
        const string sourceText = "A,B\n1,2\n";
        const string currentText = "A,B\n10,2\n";
        var source = CreateSnapshot(sourceText, CsvEncodingProfiles.Utf8CodePage);
        var current = CreateSnapshot(currentText, CsvEncodingProfiles.Utf8CodePage);
        var parse = CsvParser.Parse(sourceText, CsvDialect.Create(','));

        var plan = CsvSourceNavigationPlanner.Create(
            source,
            current,
            parse,
            new CsvSourceNavigationAddress(1, 0));

        Assert.Equal(CsvSourceNavigationStatus.ContentChanged, plan.Status);
    }

    [Fact]
    public void Create_UnsupportedCodePage_FailsClosed()
    {
        const string text = "A,B\n1,2\n";
        var snapshot = ActiveDocumentSnapshot.Create(
            @"C:\Data\sample.csv",
            text,
            editorByteLength: text.Length,
            codePage: 0,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            DateTimeOffset.UtcNow);
        var parse = CsvParser.Parse(text, CsvDialect.Create(','));

        var plan = CsvSourceNavigationPlanner.Create(
            snapshot,
            snapshot,
            parse,
            new CsvSourceNavigationAddress(1, 0));

        Assert.Equal(CsvSourceNavigationStatus.UnsupportedCodePage, plan.Status);
    }

    [Fact]
    public void Create_EditorByteLengthMismatch_FailsClosed()
    {
        const string text = "A,B\nÁ,2\n";
        var correctLength = Encoding.UTF8.GetByteCount(text);
        var snapshot = ActiveDocumentSnapshot.Create(
            @"C:\Data\sample.csv",
            text,
            editorByteLength: correctLength + 1,
            codePage: CsvEncodingProfiles.Utf8CodePage,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            DateTimeOffset.UtcNow);
        var parse = CsvParser.Parse(text, CsvDialect.Create(','));

        var plan = CsvSourceNavigationPlanner.Create(
            snapshot,
            snapshot,
            parse,
            new CsvSourceNavigationAddress(1, 0));

        Assert.Equal(CsvSourceNavigationStatus.EditorByteLengthMismatch, plan.Status);
    }

    [Fact]
    public void Mapper_OffsetInsideSurrogatePair_IsRejected()
    {
        const string text = "A😀B";

        Assert.False(CsvScintillaPositionMapper.TryMapCharacterOffset(
            text,
            characterOffset: 2,
            CsvEncodingProfiles.Utf8CodePage,
            out _));
        Assert.True(CsvScintillaPositionMapper.TryMapCharacterOffset(
            text,
            characterOffset: 3,
            CsvEncodingProfiles.Utf8CodePage,
            out var bytePosition));
        Assert.Equal(5, bytePosition);
    }

    private static ActiveDocumentSnapshot CreateSnapshot(
        string text,
        int codePage,
        string path = @"C:\Data\sample.csv")
    {
        var byteLength = GetByteLength(text, codePage);
        return ActiveDocumentSnapshot.Create(
            path,
            text,
            byteLength,
            codePage,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            new DateTimeOffset(2026, 8, 19, 7, 45, 0, TimeSpan.Zero));
    }

    private static int GetByteLength(string text, int codePage)
    {
        if (codePage == CsvEncodingProfiles.Utf8CodePage)
        {
            return new UTF8Encoding(false, true).GetByteCount(text);
        }

        var encoding = CodePagesEncodingProvider.Instance.GetEncoding(codePage) ??
            throw new InvalidOperationException($"Encoding {codePage} unavailable in test runtime.");
        return encoding.GetByteCount(text);
    }
}
