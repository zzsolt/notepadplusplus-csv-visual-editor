namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor.Core;
using System.Runtime.CompilerServices;
using System.Text;

internal static class TransformNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        const string source = "A,B\r\n  árvíz  , x \r\n";
        var snapshot = Snapshot(source);
        var parse = CsvParser.Parse(source, CsvDialect.Create(',', headerMode: CsvHeaderMode.FirstRecord));
        var projection = CsvTableProjector.Create(parse, new CsvTableProjectionOptions { HeaderMode = CsvHeaderMode.FirstRecord });
        var model = CsvRowEditModel.Create(snapshot, parse, CsvEditSession.Create(snapshot, parse, projection), projection);
        var id = model.GetVisibleRows()[0].Id;
        var plan = CsvCellTransformPlan.Create(model, [new(id, 0), new(id, 1)], new(CsvCellTransformKind.Trim));
        Require(!model.IsDirty && plan.Changes.Count == 2, "Transform preview must not mutate pending edits.");
        Require(plan.Apply(model) == 2, "Native AOT transform change count mismatch.");
        Require(model.CreatePreview().Text == "A,B\r\nárvíz,x\r\n", "Native AOT transform serialization mismatch.");

        var target = new Target();
        var result = CsvEditorApplyCoordinator.Execute(model, snapshot, target);
        Require(result.Status == CsvEditorApplyStatus.Applied, "Transformed model Apply failed.");
        Require(target.Calls.SequenceEqual(new[] { "begin", "replace", "selection", "end" }), "Transform Apply must use one undo action.");
        var conflictTarget = new Target();
        CsvEditorApplyCoordinator.Execute(model, Snapshot(source + "external"), conflictTarget);
        Require(conflictTarget.Calls.Count == 0, "Transform conflict must make zero host calls.");

        var stale = CsvCellTransformPlan.Create(model, [new(id, 0), new(id, 1)], new(CsvCellTransformKind.Uppercase));
        model.SetCellValue(id, 1, "changed");
        var blocked = false;
        try { stale.Apply(model); }
        catch (InvalidOperationException) { blocked = true; }
        Require(blocked && model.GetRow(id).Values[0] == "árvíz", "Stale transform must have no partial effects.");
        model.RevertAll();
        Require(!model.IsDirty && model.CreatePreview().Text == source, "Transform Revert All must be exact.");

        // Construct the actual dialog in Native AOT, including its preview grid and events.
        using var dialog = new CsvTransformDialog((_, transform) =>
            CsvCellTransformPlan.Create(model, [new(id, 0)], transform), preview => preview.Apply(model));
        Require(dialog.Controls.Count > 0 && dialog.AcceptButton is not null && dialog.CancelButton is not null,
            "Native AOT transform dialog construction failed.");
    }

    private static ActiveDocumentSnapshot Snapshot(string source) => ActiveDocumentSnapshot.Create(
        "synthetic-native-transform.csv", source, Encoding.UTF8.GetByteCount(source), 65001, 0, 0, false, DateTimeOffset.UnixEpoch);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Target : IEditorReplacementTarget
    {
        internal List<string> Calls { get; } = [];
        public void BeginUndoAction() => Calls.Add("begin");
        public long ReplaceWholeDocument(string text) { Calls.Add("replace"); return Encoding.UTF8.GetByteCount(text); }
        public void SetSelection(long anchorPosition, long caretPosition) => Calls.Add("selection");
        public void EndUndoAction() => Calls.Add("end");
    }
}
