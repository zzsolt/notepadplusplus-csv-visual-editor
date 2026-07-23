namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor.Core;
using System.Runtime.CompilerServices;
using System.Text;

internal static class BatchDeleteNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        const string source = "A,B\r\n1,2\n3,4\r5,6\r\n";
        const string expectedPreview = "A,B\r\n1,2\nx,y\r\n5,6\r\n";
        var snapshot = CreateSnapshot(source);
        var dialect = CsvDialect.Create(
            ',',
            headerMode: CsvHeaderMode.FirstRecord);
        var parseResult = CsvParser.Parse(source, dialect);
        var projection = CsvTableProjector.Create(
            parseResult,
            new CsvTableProjectionOptions
            {
                HeaderMode = CsvHeaderMode.FirstRecord,
                MaximumRows = 10_000,
                MaximumColumns = 512,
                MaximumCells = 250_000
            });
        var session = CsvEditSession.Create(snapshot, parseResult, projection);
        var model = CsvRowEditModel.Create(
            snapshot,
            parseResult,
            session,
            projection);
        var sourceRows = model.GetVisibleRows();
        var survivingInserted = model.InsertRowAfter(sourceRows[0].Id, ["x", "y"]);
        var cancelledInserted = model.AppendRow(["cancel", "me"]);

        var deleteResult = model.DeleteRows([
            cancelledInserted,
            sourceRows[1].Id,
            sourceRows[1].Id]);

        Require(deleteResult.TargetRowCount == 2, "Native AOT batch target normalization mismatch.");
        Require(deleteResult.DeletedSourceRowCount == 1, "Native AOT batch source-delete count mismatch.");
        Require(deleteResult.CancelledInsertedRowCount == 1, "Native AOT batch insertion-cancel count mismatch.");
        Require(deleteResult.AffectedRowCount == 2, "Native AOT batch affected-row count mismatch.");
        Require(deleteResult.RemainingVisibleRowCount == 3, "Native AOT batch remaining-row count mismatch.");
        Require(model.InsertedRowCount == 1, "Native AOT surviving insertion count mismatch.");
        Require(model.DeletedRowCount == 1, "Native AOT deleted source count mismatch.");
        Require(model.GetRow(survivingInserted).IsInserted, "Native AOT surviving inserted identity was lost.");

        var preview = model.CreatePreview();
        Require(
            string.Equals(preview.Text, expectedPreview, StringComparison.Ordinal),
            "Native AOT batch structural preview mismatch.");

        var target = new RecordingReplacementTarget(
            Encoding.UTF8.GetByteCount(expectedPreview));
        var applyResult = CsvEditorApplyCoordinator.Execute(
            model,
            snapshot,
            target);
        Require(
            applyResult.Status == CsvEditorApplyStatus.Applied,
            "Native AOT batch Apply did not report Applied.");
        Require(
            target.Calls.SequenceEqual(
            [
                "BeginUndoAction",
                "ReplaceWholeDocument",
                "SetSelection:0:0",
                "EndUndoAction"
            ]),
            "Native AOT batch Apply call order mismatch.");

        var conflictSnapshot = CreateSnapshot(
            source.Replace("3,4", "external", StringComparison.Ordinal));
        var conflictTarget = new RecordingReplacementTarget(0);
        var conflictResult = CsvEditorApplyCoordinator.Execute(
            model,
            conflictSnapshot,
            conflictTarget);
        Require(
            conflictResult.Status == CsvEditorApplyStatus.ContentChanged,
            "Native AOT batch content conflict was not retained.");
        Require(
            conflictTarget.Calls.Count == 0,
            "Native AOT batch conflict must not call the editor target.");

        Require(model.RevertAll(), "Native AOT batch Revert All should report a change.");
        Require(!model.IsDirty, "Native AOT batch Revert All should clear dirty state.");
        Require(
            string.Equals(model.CreatePreview().Text, source, StringComparison.Ordinal),
            "Native AOT batch Revert All should restore exact source text.");
    }

    private static ActiveDocumentSnapshot CreateSnapshot(string text)
    {
        return ActiveDocumentSnapshot.Create(
            @"C:\Synthetic\native-batch-delete.csv",
            text,
            Encoding.UTF8.GetByteCount(text),
            codePage: 65001,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            new DateTimeOffset(2026, 7, 23, 12, 45, 0, TimeSpan.Zero));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class RecordingReplacementTarget : IEditorReplacementTarget
    {
        private readonly long _replacementByteLength;

        public RecordingReplacementTarget(long replacementByteLength)
        {
            _replacementByteLength = replacementByteLength;
        }

        public List<string> Calls { get; } = [];

        public void BeginUndoAction()
        {
            Calls.Add("BeginUndoAction");
        }

        public long ReplaceWholeDocument(string text)
        {
            Calls.Add("ReplaceWholeDocument");
            return _replacementByteLength;
        }

        public void SetSelection(long anchorPosition, long caretPosition)
        {
            Calls.Add($"SetSelection:{anchorPosition}:{caretPosition}");
        }

        public void EndUndoAction()
        {
            Calls.Add("EndUndoAction");
        }
    }
}
