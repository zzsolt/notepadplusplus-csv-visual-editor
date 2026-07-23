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
        var snapshot = ActiveDocumentSnapshot.Create(
            @"C:\Synthetic\native-structural.csv",
            source,
            Encoding.UTF8.GetByteCount(source),
            codePage: 65001,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            new DateTimeOffset(2026, 7, 23, 8, 0, 0, TimeSpan.Zero));
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
            string.Equals(
                preview.Text,
                "A,B\r\n1,\"x,y\"\n5,6\r\n",
                StringComparison.Ordinal),
            "Native AOT structural preview text mismatch.");
        Require(preview.ChangedCellCount == 1, "Native AOT structural changed-cell count mismatch.");
        Require(preview.ChangedRowCount == 3, "Native AOT structural changed-row count mismatch.");
        Require(preview.InsertedRowCount == 1, "Native AOT structural inserted-row count mismatch.");
        Require(preview.DeletedRowCount == 1, "Native AOT structural deleted-row count mismatch.");
        Require(preview.HasChanges, "Native AOT structural preview should report changes.");

        Require(model.RevertAll(), "Native AOT structural Revert All should report a change.");
        Require(!model.IsDirty, "Native AOT structural Revert All should clear dirty state.");
        Require(
            string.Equals(model.CreatePreview().Text, source, StringComparison.Ordinal),
            "Native AOT structural Revert All should restore exact source text.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
