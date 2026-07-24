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
        const string source = "Name\nAlpha\nBeta\nGamma\nDelta";
        var snapshot = ActiveDocumentSnapshot.Create(
            @"C:\Synthetic\native-managed-selection.csv",
            source,
            Encoding.UTF8.GetByteCount(source),
            codePage: 65001,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            new DateTimeOffset(2026, 7, 24, 7, 0, 0, TimeSpan.Zero));
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

        using var form = new Form();
        using var grid = new DataGridView
        {
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ClipboardCopyMode =
                DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText,
            MultiSelect = true,
            RowHeadersVisible = true,
            SelectionMode = DataGridViewSelectionMode.CellSelect
        };
        form.Controls.Add(grid);
        form.CreateControl();
        grid.CreateControl();
        Require(
            CsvGridRowHeaderBehavior.TryAttach(form),
            "Native AOT managed row selection did not attach to the table grid.");

        grid.Columns.Add("Name", "Name");
        foreach (var row in sourceRows)
        {
            var index = grid.Rows.Add(row.Values[0]);
            grid.Rows[index].Tag = row.Id;
        }

        // The plugin-owned stable-ID state must preserve Ctrl selection even if
        // the host later clears or represents the visual DataGridView selection
        // differently.
        Require(
            CsvGridRowHeaderBehavior.ApplyRowHeaderGestureForTesting(
                grid,
                rowIndex: 0,
                control: false,
                shift: false),
            "Native AOT initial managed row selection failed.");
        Require(
            CsvGridRowHeaderBehavior.ApplyRowHeaderGestureForTesting(
                grid,
                rowIndex: 2,
                control: true,
                shift: false),
            "Native AOT Ctrl managed row selection failed for Gamma.");
        Require(
            CsvGridRowHeaderBehavior.ApplyRowHeaderGestureForTesting(
                grid,
                rowIndex: 3,
                control: true,
                shift: false),
            "Native AOT Ctrl managed row selection failed for Delta.");

        grid.ClearSelection();
        var selected = CsvGridSelectionSnapshot.Capture(grid);
        Require(selected.Count == 3, "Native AOT managed Ctrl snapshot count mismatch.");
        Require(selected[0].Id == sourceRows[0].Id, "Native AOT managed Ctrl first identity mismatch.");
        Require(selected[1].Id == sourceRows[2].Id, "Native AOT managed Ctrl middle identity mismatch.");
        Require(selected[2].Id == sourceRows[3].Id, "Native AOT managed Ctrl final identity mismatch.");

        var deleteResult = model.DeleteRows(selected.Select(static row => row.Id));
        Require(deleteResult.DeletedSourceRowCount == 3, "Native AOT managed batch delete count mismatch.");
        Require(deleteResult.RemainingVisibleRowCount == 1, "Native AOT managed batch remaining-row mismatch.");
        Require(
            string.Equals(model.CreatePreview().Text, "Name\nBeta", StringComparison.Ordinal),
            "Native AOT managed batch preview mismatch.");
        Require(model.RevertAll(), "Native AOT managed batch Revert All should report a change.");
        Require(
            string.Equals(model.CreatePreview().Text, source, StringComparison.Ordinal),
            "Native AOT managed batch Revert All should restore exact source text.");

        // Shift selection is computed from the plugin-owned stable anchor and
        // current visible structural order, not from a WinForms selection anchor.
        CsvGridRowHeaderBehavior.ResetManagedSelectionForCurrentCell(grid);
        Require(
            CsvGridRowHeaderBehavior.ApplyRowHeaderGestureForTesting(
                grid,
                rowIndex: 1,
                control: false,
                shift: false),
            "Native AOT Shift anchor selection failed.");
        Require(
            CsvGridRowHeaderBehavior.ApplyRowHeaderGestureForTesting(
                grid,
                rowIndex: 3,
                control: false,
                shift: true),
            "Native AOT Shift range selection failed.");
        var range = CsvGridSelectionSnapshot.Capture(grid);
        Require(range.Count == 3, "Native AOT managed Shift range count mismatch.");
        Require(range[0].Id == sourceRows[1].Id, "Native AOT Shift range first identity mismatch.");
        Require(range[1].Id == sourceRows[2].Id, "Native AOT Shift range middle identity mismatch.");
        Require(range[2].Id == sourceRows[3].Id, "Native AOT Shift range final identity mismatch.");

        // Ctrl can explicitly deselect the final selected row. That explicit
        // empty row-header context must not fall back and delete CurrentRow.
        CsvGridRowHeaderBehavior.ResetManagedSelectionForCurrentCell(grid);
        CsvGridRowHeaderBehavior.ApplyRowHeaderGestureForTesting(
            grid,
            rowIndex: 0,
            control: false,
            shift: false);
        CsvGridRowHeaderBehavior.ApplyRowHeaderGestureForTesting(
            grid,
            rowIndex: 0,
            control: true,
            shift: false);
        Require(
            CsvGridSelectionSnapshot.Capture(grid).Count == 0,
            "Native AOT explicit empty row-header selection incorrectly used current-row fallback.");

        // A normal cell focus deliberately resets managed row-header context and
        // restores the documented single current-row fallback.
        CsvGridRowHeaderBehavior.ResetManagedSelectionForCurrentCell(grid);
        grid.CurrentCell = grid.Rows[2].Cells[0];
        grid.ClearSelection();
        var fallback = CsvGridSelectionSnapshot.Capture(grid);
        Require(fallback.Count == 1, "Native AOT current-row fallback count mismatch.");
        Require(fallback[0].Id == sourceRows[2].Id, "Native AOT current-row fallback identity mismatch.");
        Require(fallback[0].DisplayIndex == 2, "Native AOT current-row fallback display index mismatch.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
