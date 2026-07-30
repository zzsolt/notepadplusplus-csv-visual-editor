namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor.Core;
using System.Runtime.CompilerServices;
using System.Text;

internal static class ClipboardNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        const string source = "A,B,C\n1,2,3\n4,5,6";
        var snapshot = ActiveDocumentSnapshot.Create(
            @"C:\Synthetic\native-clipboard.csv",
            source,
            Encoding.UTF8.GetByteCount(source),
            codePage: 65001,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            new DateTimeOffset(2026, 7, 30, 7, 30, 0, TimeSpan.Zero));
        var dialect = CsvDialect.Create(',', headerMode: CsvHeaderMode.FirstRecord);
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
        var model = CsvRowEditModel.Create(snapshot, parseResult, session, projection);
        var rowIds = model.GetVisibleRows().Select(static row => row.Id).ToArray();

        var matrix = CsvClipboardMatrix.Parse("x\ty\r\nu\tv\r\n");
        Require(matrix.RowCount == 2, "Native AOT clipboard row count mismatch.");
        Require(matrix.ColumnCount == 2, "Native AOT clipboard column count mismatch.");
        Require(
            string.Equals(matrix.ToTabSeparatedText(), "x\ty\r\nu\tv", StringComparison.Ordinal),
            "Native AOT clipboard canonical text mismatch.");

        var exactPlan = CsvClipboardPastePlan.Create(
            model,
            rowIds,
            startRowOffset: 0,
            startColumnIndex: 1,
            targetRowCount: 2,
            targetColumnCount: 2,
            matrix);
        Require(exactPlan.IsReady, "Native AOT exact clipboard plan was not ready.");
        Require(exactPlan.Apply(model) == 4, "Native AOT exact clipboard changed-cell count mismatch.");
        Require(
            string.Equals(model.CreatePreview().Text, "A,B,C\n1,x,y\n4,u,v", StringComparison.Ordinal),
            "Native AOT exact clipboard preview mismatch.");

        Require(model.RevertAll(), "Native AOT clipboard Revert All should report a change.");
        var broadcastPlan = CsvClipboardPastePlan.Create(
            model,
            rowIds,
            startRowOffset: 0,
            startColumnIndex: 0,
            targetRowCount: 2,
            targetColumnCount: 2,
            CsvClipboardMatrix.Parse("same"));
        Require(broadcastPlan.IsReady, "Native AOT broadcast clipboard plan was not ready.");
        Require(broadcastPlan.Apply(model) == 4, "Native AOT broadcast changed-cell count mismatch.");
        Require(
            string.Equals(model.CreatePreview().Text, "A,B,C\nsame,same,3\nsame,same,6", StringComparison.Ordinal),
            "Native AOT broadcast clipboard preview mismatch.");

        var overflowPlan = CsvClipboardPastePlan.Create(
            model,
            rowIds,
            startRowOffset: 1,
            startColumnIndex: 2,
            targetRowCount: 2,
            targetColumnCount: 2,
            CsvClipboardMatrix.Parse("a\tb\nc\td"));
        Require(
            overflowPlan.Status == CsvClipboardPasteStatus.TargetOutsideSession,
            "Native AOT clipboard overflow was not rejected.");
        Require(overflowPlan.Edits.Count == 0, "Native AOT rejected clipboard plan retained edits.");

        var raggedRejected = false;
        try
        {
            _ = CsvClipboardMatrix.Parse("a\tb\nc");
        }
        catch (FormatException)
        {
            raggedRejected = true;
        }

        Require(raggedRejected, "Native AOT ragged clipboard matrix was not rejected.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
