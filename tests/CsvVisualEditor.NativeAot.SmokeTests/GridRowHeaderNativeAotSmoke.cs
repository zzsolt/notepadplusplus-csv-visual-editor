namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

internal static class GridRowHeaderNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        using var form = new Form();
        using var tableGrid = new DataGridView
        {
            AllowUserToAddRows = false,
            RowHeadersVisible = true,
            SelectionMode = DataGridViewSelectionMode.CellSelect
        };
        using var diagnosticsGrid = new DataGridView
        {
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        tableGrid.Columns.Add("A", "A");
        tableGrid.Columns.Add("B", "B");
        tableGrid.Rows.Add("one", "two");
        form.Controls.Add(diagnosticsGrid);
        form.Controls.Add(tableGrid);
        form.CreateControl();
        tableGrid.CreateControl();

        Require(
            CsvGridRowHeaderBehavior.TryAttach(form),
            "Native AOT row-header behavior did not find the table grid.");
        Require(
            tableGrid.RowHeadersWidth == CsvGridRowHeaderBehavior.PreferredRowHeaderWidth,
            "Native AOT row-header width policy mismatch.");
        Require(
            tableGrid.SelectionMode == DataGridViewSelectionMode.RowHeaderSelect,
            "Native AOT table grid did not use RowHeaderSelect mode.");

        tableGrid.ClearSelection();
        Require(
            CsvGridRowHeaderBehavior.SelectWholeRow(tableGrid, rowIndex: 0),
            "Native AOT row-header selection did not select the row.");
        Require(
            tableGrid.Rows[0].Selected,
            "Native AOT row-header selection did not mark the complete row selected.");
        Require(
            tableGrid.SelectedCells.Count == tableGrid.Columns.Count,
            "Native AOT row-header selection did not select every row cell.");

        tableGrid.RowHeadersVisible = false;
        tableGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        tableGrid.RowHeadersVisible = true;
        tableGrid.Columns.Add("C", "C");
        Require(
            tableGrid.SelectionMode == DataGridViewSelectionMode.RowHeaderSelect,
            "Native AOT row-header mode was not restored after table reconstruction.");
        Require(
            tableGrid.RowHeadersWidth == CsvGridRowHeaderBehavior.PreferredRowHeaderWidth,
            "Native AOT row-header width was not restored after table reconstruction.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
