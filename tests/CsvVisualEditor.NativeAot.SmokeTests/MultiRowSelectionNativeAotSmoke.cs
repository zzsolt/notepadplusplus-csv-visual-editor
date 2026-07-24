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
            @"C:\Synthetic\native-explicit-selector.csv",
            source,
            Encoding.UTF8.GetByteCount(source),
            codePage: 65001,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            new DateTimeOffset(2026, 7, 24, 8, 0, 0, TimeSpan.Zero));
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
            "Native AOT explicit row selector did not attach to the table grid.");

        grid.Columns.Add("Name", "Name");
        Require(
            CsvGridRowHeaderBehavior.HasSelectorColumn(grid),
            "Native AOT explicit selector column was not added in editable mode.");
        Require(
            grid.Columns[grid.Columns.Count - 1].Name ==
                CsvGridRowHeaderBehavior.SelectorColumnName,
            "Native AOT selector column must be appended after CSV data columns.");

        foreach (var row in sourceRows)
        {
            var index = grid.Rows.Add(row.Values[0]);
            grid.Rows[index].Tag = row.Id;
        }

        Require(
            CsvGridRowHeaderBehavior.ToggleSelectorForTesting(grid, rowIndex: 0),
            "Native AOT selector toggle failed for Alpha.");
        Require(
            CsvGridRowHeaderBehavior.ToggleSelectorForTesting(grid, rowIndex: 2),
            "Native AOT selector toggle failed for Gamma.");
        Require(
            CsvGridRowHeaderBehavior.ToggleSelectorForTesting(grid, rowIndex: 3),
            "Native AOT selector toggle failed for Delta.");

        // The marked stable IDs remain authoritative even when WinForms visual
        // selection is cleared or represented differently by the docked host.
        grid.ClearSelection();
        var selected = CsvGridSelectionSnapshot.Capture(grid);
        Require(selected.Count == 3, "Native AOT explicit selector snapshot count mismatch.");
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

        // Toggling a checked mark off removes only that stable target.
        CsvGridRowHeaderBehavior.ResetManagedSelectionForCurrentCell(grid);
        CsvGridRowHeaderBehavior.ToggleSelectorForTesting(grid, rowIndex: 1);
        CsvGridRowHeaderBehavior.ToggleSelectorForTesting(grid, rowIndex: 3);
        CsvGridRowHeaderBehavior.ToggleSelectorForTesting(grid, rowIndex: 1);
        var oneMarked = CsvGridSelectionSnapshot.Capture(grid);
        Require(oneMarked.Count == 1, "Native AOT selector toggle-off count mismatch.");
        Require(oneMarked[0].Id == sourceRows[3].Id, "Native AOT selector toggle-off identity mismatch.");

        // With no marked checkboxes the accepted single-current-row fallback is
        // retained for the ordinary Delete Row command.
        CsvGridRowHeaderBehavior.ResetManagedSelectionForCurrentCell(grid);
        grid.CurrentCell = grid.Rows[2].Cells[0];
        grid.ClearSelection();
        var fallback = CsvGridSelectionSnapshot.Capture(grid);
        Require(fallback.Count == 1, "Native AOT current-row fallback count mismatch.");
        Require(fallback[0].Id == sourceRows[2].Id, "Native AOT current-row fallback identity mismatch.");
        Require(fallback[0].DisplayIndex == 2, "Native AOT current-row fallback display index mismatch.");

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
