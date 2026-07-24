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
            new DateTimeOffset(2026, 7, 24, 6, 30, 0, TimeSpan.Zero));
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

        // Reproduce the real-host failure: Ctrl/Shift row-header gestures can
        // remain visible as one selected cell per intended row, leaving
        // SelectedRows empty. The row-header behavior must promote that
        // transient state to complete rows before the stable-ID snapshot.
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grid.CurrentCell = grid.Rows[2].Cells[0];
        grid.ClearSelection();
        grid.Rows[0].Cells[0].Selected = true;
        grid.Rows[2].Cells[0].Selected = true;
        Require(grid.SelectedRows.Count == 0, "Native AOT host-symptom setup unexpectedly selected complete rows.");
        Require(
            CsvGridRowHeaderBehavior.PromoteModifiedSelectionToWholeRows(grid, clickedRowIndex: 2),
            "Native AOT host-symptom selection was not promoted to complete rows.");
        Require(
            grid.SelectionMode == DataGridViewSelectionMode.RowHeaderSelect,
            "Native AOT promoted selection did not restore RowHeaderSelect mode.");
        Require(grid.Rows[0].Selected, "Native AOT first intended row was not fully selected.");
        Require(grid.Rows[2].Selected, "Native AOT last intended row was not fully selected.");
        Require(grid.SelectedRows.Count == 2, "Native AOT promoted SelectedRows count mismatch.");

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

        // A normal cell focus with no complete-row selection uses the documented
        // current-row fallback.
        grid.CurrentCell = grid.Rows[1].Cells[0];
        grid.ClearSelection();
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
