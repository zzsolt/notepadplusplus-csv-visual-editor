namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor;
using CsvVisualEditor.Core;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;

internal static class GridRowHeaderNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        const string source = "A,B\none,two";
        var snapshot = ActiveDocumentSnapshot.Create(
            @"C:\Synthetic\native-row-header.csv",
            source,
            Encoding.UTF8.GetByteCount(source),
            codePage: 65001,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero));
        var dialect = CsvDialect.Create(',', headerMode: CsvHeaderMode.FirstRecord);
        var parseResult = CsvParser.Parse(source, dialect);
        var projection = CsvTableProjector.Create(
            parseResult,
            new CsvTableProjectionOptions
            {
                HeaderMode = CsvHeaderMode.FirstRecord,
                MaximumRows = 10,
                MaximumColumns = 10,
                MaximumCells = 100
            });
        var session = CsvEditSession.Create(snapshot, parseResult, projection);
        var model = CsvRowEditModel.Create(snapshot, parseResult, session, projection);
        var stableRow = model.GetVisibleRows().Single();

        using var form = new Form();
        using var tableGrid = new DataGridView
        {
            AllowUserToAddRows = false,
            ClipboardCopyMode =
                DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        using var diagnosticsGrid = new DataGridView
        {
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        form.Controls.Add(diagnosticsGrid);
        form.Controls.Add(tableGrid);
        form.CreateControl();
        tableGrid.CreateControl();

        Require(
            CsvGridRowHeaderBehavior.TryAttach(form),
            "Native AOT row-header behavior did not find the hidden table grid during bootstrap.");

        tableGrid.RowHeadersVisible = true;
        tableGrid.Columns.Add("A", "A");
        tableGrid.Columns.Add("B", "B");
        var rowIndex = tableGrid.Rows.Add("one", "two");
        tableGrid.Rows[rowIndex].Tag = stableRow.Id;
        Require(
            tableGrid.RowHeadersWidth == CsvGridRowHeaderBehavior.PreferredRowHeaderWidth,
            "Native AOT row-header width policy mismatch.");
        Require(
            !CsvGridRowHeaderBehavior.HasSelectorColumn(tableGrid),
            "Native AOT selector column must not appear while the table is read-only.");

        tableGrid.SelectionMode = DataGridViewSelectionMode.RowHeaderSelect;
        tableGrid.ClearSelection();
        Require(
            CsvGridRowHeaderBehavior.SelectWholeRow(tableGrid, rowIndex: 0),
            "Native AOT row-header selection did not select the row.");
        Require(
            tableGrid.Rows[0].Selected,
            "Native AOT row-header selection did not mark the complete row selected.");
        Require(
            tableGrid.SelectedCells.Count == tableGrid.Columns.Count,
            "Native AOT row-header selection did not select every CSV row cell.");
        Require(
            CsvGridSelectionSnapshot.Capture(tableGrid).Count == 1,
            "Native AOT row-header selection did not synchronize stable selection state.");

        tableGrid.RowHeadersVisible = false;
        tableGrid.RowHeadersVisible = true;
        tableGrid.Columns.Add("C", "C");
        Require(
            tableGrid.RowHeadersWidth == CsvGridRowHeaderBehavior.PreferredRowHeaderWidth,
            "Native AOT row-header width was not restored after table reconstruction.");
        Require(
            !CsvGridRowHeaderBehavior.HasSelectorColumn(tableGrid),
            "Native AOT reconstruction added a selector outside Edit mode.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
