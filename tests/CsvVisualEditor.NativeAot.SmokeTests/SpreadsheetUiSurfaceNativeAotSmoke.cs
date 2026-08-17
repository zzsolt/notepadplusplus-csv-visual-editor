namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

internal static class SpreadsheetUiSurfaceNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        using var tableGrid = new CsvDataGridView
        {
            RowHeadersVisible = true,
            SelectionMode = DataGridViewSelectionMode.RowHeaderSelect
        };
        Require(
            CsvDataGridView.IsPrimaryTableGridCandidate(tableGrid),
            "Spreadsheet UI discovery must accept the real RowHeaderSelect table grid.");

        tableGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        Require(
            CsvDataGridView.IsPrimaryTableGridCandidate(tableGrid),
            "Spreadsheet UI discovery must not depend on a specific selection mode.");

        using var diagnosticsGrid = new CsvDataGridView
        {
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect
        };
        Require(
            !CsvDataGridView.IsPrimaryTableGridCandidate(diagnosticsGrid),
            "Spreadsheet UI discovery must not attach to the diagnostics grid.");

        using var unrelatedGrid = new DataGridView
        {
            RowHeadersVisible = true
        };
        Require(
            !CsvDataGridView.IsPrimaryTableGridCandidate(unrelatedGrid),
            "Spreadsheet UI discovery must stay scoped to the plugin table grid type.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
