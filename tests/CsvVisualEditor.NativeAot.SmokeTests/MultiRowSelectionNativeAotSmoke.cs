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
            @"C:\Synthetic\native-synchronized-row-selection.csv",
            source,
            Encoding.UTF8.GetByteCount(source),
            codePage: 65001,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero));
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
            ReadOnly = false,
            RowHeadersVisible = true,
            SelectionMode = DataGridViewSelectionMode.CellSelect
        };
        form.Controls.Add(grid);
        form.CreateControl();
        grid.CreateControl();
        Require(
            CsvGridRowHeaderBehavior.TryAttach(form),
            "Native AOT synchronized row selection did not attach to the table grid.");

        grid.Columns.Add("Name", "Name");
        Require(
            CsvGridRowHeaderBehavior.HasSelectorColumn(grid),
            "Native AOT selector column was not added in editable mode.");
        Require(
            grid.Columns[grid.Columns.Count - 1].Name ==
                CsvGridRowHeaderBehavior.SelectorColumnName,
            "Native AOT selector column must be appended after CSV data columns.");

        foreach (var row in sourceRows)
        {
            var index = grid.Rows.Add(row.Values[0]);
            grid.Rows[index].Tag = row.Id;
        }

        // Checkbox-style selection must also highlight complete rows visually.
        Require(
            CsvGridRowHeaderBehavior.ToggleSelectorForTesting(grid, rowIndex: 0),
            "Native AOT selector toggle failed for Alpha.");
        Require(
            CsvGridRowHeaderBehavior.ToggleSelectorForTesting(grid, rowIndex: 2),
            "Native AOT selector toggle failed for Gamma.");
        Require(
            CsvGridRowHeaderBehavior.ToggleSelectorForTesting(grid, rowIndex: 3),
            "Native AOT selector toggle failed for Delta.");
        Require(grid.Rows[0].Selected, "Selector did not visually select Alpha's complete row.");
        Require(grid.Rows[2].Selected, "Selector did not visually select Gamma's complete row.");
        Require(grid.Rows[3].Selected, "Selector did not visually select Delta's complete row.");
        Require(!grid.Rows[1].Selected, "Selector unexpectedly selected Beta.");

        var selected = CsvGridSelectionSnapshot.Capture(grid);
        Require(selected.Count == 3, "Native AOT selector snapshot count mismatch.");
        Require(selected[0].Id == sourceRows[0].Id, "Native AOT selector first identity mismatch.");
        Require(selected[1].Id == sourceRows[2].Id, "Native AOT selector middle identity mismatch.");
        Require(selected[2].Id == sourceRows[3].Id, "Native AOT selector final identity mismatch.");

        var deleteResult = model.DeleteRows(selected.Select(static row => row.Id));
        Require(deleteResult.DeletedSourceRowCount == 3, "Native AOT explicit batch delete count mismatch.");
        Require(deleteResult.RemainingVisibleRowCount == 1, "Native AOT explicit batch remaining-row mismatch.");
        Require(
            string.Equals(model.CreatePreview().Text, "Name\nBeta", StringComparison.Ordinal),
            "Native AOT explicit batch preview mismatch.");
        Require(model.RevertAll(), "Native AOT explicit batch Revert All should report a change.");
        Require(
            string.Equals(model.CreatePreview().Text, source, StringComparison.Ordinal),
            "Native AOT explicit batch Revert All should restore exact source text.");

        // Plain and Ctrl row-header gestures must synchronize the same stable-ID
        // selector state and the complete-row visual selection.
        CsvGridRowHeaderBehavior.ClearManagedSelection(grid);
        Require(
            CsvGridRowHeaderBehavior.ApplyRowHeaderGestureForTesting(
                grid,
                rowIndex: 1,
                control: false,
                shift: false),
            "Native AOT plain row-header gesture failed for Beta.");
        Require(
            CsvGridRowHeaderBehavior.ApplyRowHeaderGestureForTesting(
                grid,
                rowIndex: 3,
                control: true,
                shift: false),
            "Native AOT Ctrl row-header gesture failed for Delta.");
        var ctrlSelection = CsvGridSelectionSnapshot.Capture(grid);
        Require(ctrlSelection.Count == 2, "Native AOT Ctrl row-header count mismatch.");
        Require(ctrlSelection[0].Id == sourceRows[1].Id, "Native AOT Ctrl first identity mismatch.");
        Require(ctrlSelection[1].Id == sourceRows[3].Id, "Native AOT Ctrl second identity mismatch.");
        Require(grid.Rows[1].Selected && grid.Rows[3].Selected,
            "Native AOT Ctrl row-header selection was not visually synchronized.");

        // Shift uses the stable anchor and visible structural order.
        CsvGridRowHeaderBehavior.ClearManagedSelection(grid);
        CsvGridRowHeaderBehavior.ApplyRowHeaderGestureForTesting(
            grid,
            rowIndex: 0,
            control: false,
            shift: false);
        CsvGridRowHeaderBehavior.ApplyRowHeaderGestureForTesting(
            grid,
            rowIndex: 2,
            control: false,
            shift: true);
        var range = CsvGridSelectionSnapshot.Capture(grid);
        Require(range.Count == 3, "Native AOT Shift row-header count mismatch.");
        Require(range[0].Id == sourceRows[0].Id, "Native AOT Shift first identity mismatch.");
        Require(range[1].Id == sourceRows[1].Id, "Native AOT Shift middle identity mismatch.");
        Require(range[2].Id == sourceRows[2].Id, "Native AOT Shift final identity mismatch.");
        Require(grid.Rows[0].Selected && grid.Rows[1].Selected && grid.Rows[2].Selected,
            "Native AOT Shift row-header selection was not visually synchronized.");

        // Ordinary cell context is deliberately not deletable. There is no
        // current-row fallback after explicit complete-row selection is cleared.
        CsvGridRowHeaderBehavior.ClearManagedSelection(grid);
        grid.CurrentCell = grid.Rows[2].Cells[0];
        grid.ClearSelection();
        grid.CurrentCell.Selected = true;
        Require(
            CsvGridSelectionSnapshot.Capture(grid).Count == 0,
            "Native AOT ordinary cell context incorrectly became a deletion target.");

        // Leaving editable mode removes the selector column and clears marks.
        grid.ReadOnly = true;
        Require(
            !CsvGridRowHeaderBehavior.HasSelectorColumn(grid),
            "Native AOT selector column was not removed outside editable mode.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
