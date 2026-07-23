namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor.Core;
using System.Runtime.CompilerServices;
using System.Text;

internal static class StructuralRowNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        const string source = "A,B\r\n1,2\n3,4\r\n";
        const string expectedPreview = "A,B\r\n1,\"x,y\"\n5,6\r\n";
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
        var rows = model.GetVisibleRows();

        model.SetCellValue(rows[0].Id, 1, "x,y");
        model.DeleteRow(rows[1].Id);
        model.AppendRow(["5", "6"]);
        var preview = model.CreatePreview();

        Require(
            string.Equals(preview.Text, expectedPreview, StringComparison.Ordinal),
            "Native AOT structural preview text mismatch.");
        Require(preview.ChangedCellCount == 1, "Native AOT structural changed-cell count mismatch.");
        Require(preview.ChangedRowCount == 3, "Native AOT structural changed-row count mismatch.");
        Require(preview.InsertedRowCount == 1, "Native AOT structural inserted-row count mismatch.");
        Require(preview.DeletedRowCount == 1, "Native AOT structural deleted-row count mismatch.");
        Require(preview.HasChanges, "Native AOT structural preview should report changes.");

        var target = new RecordingReplacementTarget(
            Encoding.UTF8.GetByteCount(expectedPreview));
        var applyResult = CsvEditorApplyCoordinator.Execute(
            model,
            snapshot,
            target);
        Require(
            applyResult.Status == CsvEditorApplyStatus.Applied,
            "Native AOT structural coordinator did not report Applied.");
        Require(applyResult.SelectionRestored, "Native AOT structural selection was not restored.");
        Require(applyResult.ChangedCellCount == 1, "Native AOT Apply changed-cell count mismatch.");
        Require(applyResult.ChangedRecordCount == 3, "Native AOT Apply changed-row count mismatch.");
        Require(applyResult.InsertedRowCount == 1, "Native AOT Apply inserted-row count mismatch.");
        Require(applyResult.DeletedRowCount == 1, "Native AOT Apply deleted-row count mismatch.");
        Require(
            string.Equals(target.ReplacementText, expectedPreview, StringComparison.Ordinal),
            "Native AOT structural coordinator replacement text mismatch.");
        Require(
            target.Calls.SequenceEqual(
            [
                "BeginUndoAction",
                "ReplaceWholeDocument",
                "SetSelection:0:0",
                "EndUndoAction"
            ]),
            "Native AOT structural coordinator call order mismatch.");

        var conflictSnapshot = CreateSnapshot(
            source.Replace("3,4", "external", StringComparison.Ordinal));
        var conflictTarget = new RecordingReplacementTarget(0);
        var conflictResult = CsvEditorApplyCoordinator.Execute(
            model,
            conflictSnapshot,
            conflictTarget);
        Require(
            conflictResult.Status == CsvEditorApplyStatus.ContentChanged,
            "Native AOT structural content conflict was not retained.");
        Require(
            conflictTarget.Calls.Count == 0,
            "Native AOT structural conflict must not call the editor target.");

        Require(model.RevertAll(), "Native AOT structural Revert All should report a change.");
        Require(!model.IsDirty, "Native AOT structural Revert All should clear dirty state.");
        Require(
            string.Equals(model.CreatePreview().Text, source, StringComparison.Ordinal),
            "Native AOT structural Revert All should restore exact source text.");
    }

    private static ActiveDocumentSnapshot CreateSnapshot(string text)
    {
        return ActiveDocumentSnapshot.Create(
            @"C:\Synthetic\native-structural.csv",
            text,
            Encoding.UTF8.GetByteCount(text),
            codePage: 65001,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            new DateTimeOffset(2026, 7, 23, 8, 0, 0, TimeSpan.Zero));
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

        public string? ReplacementText { get; private set; }

        public void BeginUndoAction()
        {
            Calls.Add("BeginUndoAction");
        }

        public long ReplaceWholeDocument(string text)
        {
            Calls.Add("ReplaceWholeDocument");
            ReplacementText = text;
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
