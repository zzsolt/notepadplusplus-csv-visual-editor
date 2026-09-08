namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor.Core;
using System.Runtime.CompilerServices;

internal static class HostApplyPolicyNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        Check(65001, "\u6771\u4eac \U0001F600", CsvEditorApplyStatus.Applied);
        Check(1250, "\u00c1rv\u00edzt\u0171r\u0151 t\u00fck\u00f6rf\u00far\u00f3g\u00e9p", CsvEditorApplyStatus.Applied);
        Check(1250, "\u6771\u4eac", CsvEditorApplyStatus.TextNotRepresentable);
        Check(1250, "\U0001F600", CsvEditorApplyStatus.TextNotRepresentable);
        Check(1252, "plain", CsvEditorApplyStatus.EncodingWriteNotEnabled);
        Check(20127, "plain", CsvEditorApplyStatus.EncodingWriteNotEnabled);
        Check(0, "plain", CsvEditorApplyStatus.UnsupportedCodePage);
        Check(1250, "plain", CsvEditorApplyStatus.ContentChanged, mutateSource: true);
        Console.WriteLine("Host Apply policy: UTF-8/Windows-1250 exact writes, rejection-before-native-target, conflicts and pending preservation PASS.");
    }

    private static void Check(int codePage, string replacement, CsvEditorApplyStatus expected, bool mutateSource = false)
    {
        const string source = "A,B\r\n1,old\r\n";
        var baseline = Snapshot(source, codePage);
        var parse = CsvParser.Parse(source, CsvDialect.Create(',', headerMode: CsvHeaderMode.FirstRecord));
        var projection = CsvTableProjector.Create(parse, new CsvTableProjectionOptions { HeaderMode = CsvHeaderMode.FirstRecord });
        var model = CsvRowEditModel.Create(baseline, parse, CsvEditSession.Create(baseline, parse, projection), projection);
        var row = model.GetVisibleRows()[0].Id;
        model.SetCellValue(row, 1, replacement);
        var target = new Target(codePage);
        var factoryCalls = 0;
        var result = CsvHostApplyCoordinator.Execute(model, mutateSource ? Snapshot(source + "2,new\r\n", codePage) : baseline,
            () => { factoryCalls++; return target; });
        Require(result.Status == expected, $"Host Apply policy returned {result.Status} instead of {expected} for code page {codePage}.");
        if (expected == CsvEditorApplyStatus.Applied)
        {
            Require(factoryCalls == 1 && target.Calls.SequenceEqual(["begin", "replace", "selection", "end"]),
                "Authorized Apply must use exactly one native target and one undo transaction.");
            Require(target.Text == "A,B\r\n1," + replacement + "\r\n", "Apply must preserve exact replacement text.");
        }
        else Require(factoryCalls == 0 && target.Calls.Count == 0, "Blocked Apply must not construct or touch the native editor target.");
        Require(model.GetVisibleRows()[0].Values[1] == replacement, "Preflight must never silently discard pending values.");
    }

    private static ActiveDocumentSnapshot Snapshot(string text, int codePage) => ActiveDocumentSnapshot.Create(
        "synthetic.csv", text, text.Length, codePage, 0, 0, true, DateTimeOffset.UnixEpoch);

    private sealed class Target(int codePage) : IEditorReplacementTarget
    {
        internal List<string> Calls { get; } = [];
        internal string? Text { get; private set; }
        public void BeginUndoAction() => Calls.Add("begin");
        public long ReplaceWholeDocument(string text)
        {
            Calls.Add("replace");
            Text = text;
            return CsvEncodingRepresentability.Evaluate(codePage, text).EncodedByteCount!.Value;
        }
        public void SetSelection(long anchorPosition, long caretPosition) => Calls.Add("selection");
        public void EndUndoAction() => Calls.Add("end");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
