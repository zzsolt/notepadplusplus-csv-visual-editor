namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor.Core;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;

internal static class MultiRowSelectionNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        const string source = "Name\nAlpha\nBeta\nGamma";
        var snapshot = ActiveDocumentSnapshot.Create(
            @"C:\Synthetic\native-multi-selection.csv",
            source,
            Encoding.UTF8.GetByteCount(source),
            codePage: 65001,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            new DateTimeOffset(2026, 7, 23, 13, 0, 0, TimeSpan.Zero));
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

        using var grid = new DataGridView
        {
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            MultiSelect = true,
            RowHeadersVisible = true,
            SelectionMode = DataGridViewSelectionMode.RowHeaderSelect
        };
        grid.Columns.Add("Name", "Name");
        foreach (var row in sourceRows)
        {
            var index = grid.Rows.Add(row.Values[0]);
            grid.Rows[index].Tag = row.Id;
        }
        grid.CreateControl();

        grid.ClearSelection();
        grid.Rows[0].Selected = true;
        grid.Rows[2].Selected = true;
        grid.CurrentCell = grid.Rows[2].Cells[0];

        var selected = CsvGridSelectionSnapshot.Capture(grid);
        Require(selected.Count == 2, "Native AOT selected-row snapshot count mismatch.");
        Require(selected[0].Id == sourceRows[0].Id, "Native AOT selected-row order mismatch for first row.");
        Require(selected[1].Id == sourceRows[2].Id, "Native AOT selected-row order mismatch for last row.");
        Require(selected[0].DisplayIndex == 0, "Native AOT first selected display index mismatch.");
        Require(selected[1].DisplayIndex == 2, "Native AOT second selected display index mismatch.");

        var deleteResult = model.DeleteRows(selected.Select(static row => row.Id));
        Require(deleteResult.DeletedSourceRowCount == 2, "Native AOT selected batch delete count mismatch.");
        Require(deleteResult.RemainingVisibleRowCount == 1, "Native AOT selected batch remaining-row mismatch.");
        Require(
            string.Equals(model.CreatePreview().Text, "Name\nBeta", StringComparison.Ordinal),
            "Native AOT selected batch preview mismatch.");

        grid.ClearSelection();
        grid.CurrentCell = grid.Rows[1].Cells[0];
        var fallback = CsvGridSelectionSnapshot.Capture(grid);
        Require(fallback.Count == 1, "Native AOT current-row fallback count mismatch.");
        Require(fallback[0].Id == sourceRows[1].Id, "Native AOT current-row fallback identity mismatch.");
        Require(fallback[0].DisplayIndex == 1, "Native AOT current-row fallback display index mismatch.");

        Require(model.RevertAll(), "Native AOT selected batch Revert All should report a change.");
        Require(
            string.Equals(model.CreatePreview().Text, source, StringComparison.Ordinal),
            "Native AOT selected batch Revert All should restore exact source text.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
