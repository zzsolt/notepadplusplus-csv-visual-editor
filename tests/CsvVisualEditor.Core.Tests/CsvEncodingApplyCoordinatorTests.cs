namespace CsvVisualEditor.Core.Tests;

using CsvVisualEditor.Core;
using System.Text;
using Xunit;

public sealed class CsvEncodingApplyCoordinatorTests
{
    private const string Hungarian =
        "\u00c1rv\u00edzt\u0171r\u0151 t\u00fck\u00f6rf\u00far\u00f3g\u00e9p";

    [Fact]
    public void Execute_ReadyLegacyCodePage_DefaultPolicyBlocksBeforeEditor()
    {
        var context = CreateContext(1250);
        context.Session.SetCellValue(1, 1, Hungarian);
        var target = new RecordingTarget();

        var result = CsvEditorApplyCoordinator.Execute(
            context.Session,
            context.Snapshot,
            target);

        Assert.Equal(CsvEditorApplyStatus.EncodingWriteNotEnabled, result.Status);
        Assert.Equal(1250, result.EncodingPreflight?.CodePage);
        Assert.Empty(target.Calls);
    }

    [Fact]
    public void Execute_EnabledWindows1250UnrepresentableText_BlocksBeforeEditor()
    {
        var context = CreateContext(1250);
        context.Session.SetCellValue(1, 1, "\u6771\u4eac");
        var target = new RecordingTarget();
        var policy = CsvEncodingApplyPolicy.Create([65001, 1250]);

        var result = CsvEditorApplyCoordinator.Execute(
            context.Session,
            context.Snapshot,
            target,
            policy);

        Assert.Equal(CsvEditorApplyStatus.TextNotRepresentable, result.Status);
        Assert.Equal(
            CsvEncodingApplyPreflightStatus.TextNotRepresentable,
            result.EncodingPreflight?.Status);
        Assert.Null(result.ReplacementSha256);
        Assert.Empty(target.Calls);
    }

    [Fact]
    public void Execute_EnabledWindows1250RepresentableText_UsesSingleUndoPath()
    {
        var context = CreateContext(1250);
        context.Session.SetCellValue(1, 1, Hungarian);
        var target = new RecordingTarget();
        var policy = CsvEncodingApplyPolicy.Create([1250]);

        var result = CsvEditorApplyCoordinator.Execute(
            context.Session,
            context.Snapshot,
            target,
            policy);

        Assert.Equal(CsvEditorApplyStatus.Applied, result.Status);
        Assert.True(result.EncodingPreflight?.IsReady == true);
        Assert.Equal(
            ["BeginUndoAction", "ReplaceWholeDocument", "SetSelection", "EndUndoAction"],
            target.Calls);
    }

    [Fact]
    public void Execute_UnsupportedCodePage_BlocksBeforeEditor()
    {
        var context = CreateContext(932);
        context.Session.SetCellValue(1, 1, "3");
        var target = new RecordingTarget();

        var result = CsvEditorApplyCoordinator.Execute(
            context.Session,
            context.Snapshot,
            target);

        Assert.Equal(CsvEditorApplyStatus.UnsupportedCodePage, result.Status);
        Assert.Empty(target.Calls);
    }

    [Fact]
    public void Execute_NoChangesLegacyCodePage_RemainsNoChanges()
    {
        var context = CreateContext(1250);
        var target = new RecordingTarget();

        var result = CsvEditorApplyCoordinator.Execute(
            context.Session,
            context.Snapshot,
            target);

        Assert.Equal(CsvEditorApplyStatus.NoChanges, result.Status);
        Assert.Null(result.EncodingPreflight);
        Assert.Empty(target.Calls);
    }

    private static BuildContext CreateContext(int codePage)
    {
        const string text = "A,B\n1,2";
        var snapshot = ActiveDocumentSnapshot.Create(
            @"C:\Synthetic\encoding.csv",
            text,
            Encoding.UTF8.GetByteCount(text),
            codePage,
            caretPosition: 3,
            anchorPosition: 1,
            isModified: false,
            new DateTimeOffset(2026, 7, 29, 10, 0, 0, TimeSpan.Zero));
        var parseResult = CsvParser.Parse(
            text,
            CsvDialect.Create(',', headerMode: CsvHeaderMode.FirstRecord));
        var projection = CsvTableProjector.Create(
            parseResult,
            new CsvTableProjectionOptions
            {
                HeaderMode = CsvHeaderMode.FirstRecord,
                MaximumRows = 100,
                MaximumColumns = 20,
                MaximumCells = 2_000
            });
        return new BuildContext(
            snapshot,
            CsvEditSession.Create(snapshot, parseResult, projection));
    }

    private sealed record BuildContext(
        ActiveDocumentSnapshot Snapshot,
        CsvEditSession Session);

    private sealed class RecordingTarget : IEditorReplacementTarget
    {
        public List<string> Calls { get; } = [];

        public void BeginUndoAction() => Calls.Add("BeginUndoAction");

        public long ReplaceWholeDocument(string text)
        {
            Calls.Add("ReplaceWholeDocument");
            return Encoding.UTF8.GetByteCount(text);
        }

        public void SetSelection(long anchorPosition, long caretPosition) =>
            Calls.Add("SetSelection");

        public void EndUndoAction() => Calls.Add("EndUndoAction");
    }
}
