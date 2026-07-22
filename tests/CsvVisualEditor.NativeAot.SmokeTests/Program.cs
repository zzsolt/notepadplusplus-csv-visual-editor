namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor.Core;
using System.Globalization;

internal static class Program
{
    private const string Sample =
        "EmailAddress,UserName,Password\r\n" +
        "muller.bela@example.invalid,bmuller@example.invalid,TEMP-password\r\n";

    [STAThread]
    private static int Main()
    {
        try
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            RunCase("automatic", delimiterOverride: null);
            RunCase("manual comma", delimiterOverride: ',');
            Console.WriteLine("All Native AOT CSV table runtime smoke tests passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"{exception.GetType().FullName}: {exception.Message}");
            Console.Error.WriteLine(exception.StackTrace);
            return 1;
        }
    }

    private static void RunCase(string caseName, char? delimiterOverride)
    {
        var result = CsvTableBuilder.Build(
            Sample,
            new CsvTableBuildOptions
            {
                DelimiterOverride = delimiterOverride,
                HeaderMode = CsvHeaderMode.FirstRecord,
                MaximumRows = 10_000,
                MaximumColumns = 512,
                MaximumCells = 250_000
            });

        if (result.Status != CsvTableBuildStatus.Ready ||
            result.ParseResult is null ||
            result.Projection is null)
        {
            throw new InvalidOperationException(
                $"The {caseName} table build did not return a complete Ready result.");
        }

        using var grid = CreateGrid();
        Bind(grid, result.Projection);

        Require(grid.Columns.Count == 3, $"{caseName}: expected 3 columns.");
        Require(grid.Rows.Count == 1, $"{caseName}: expected 1 data row.");
        Require(
            string.Equals(
                Convert.ToString(grid.Columns[0].HeaderText, CultureInfo.InvariantCulture),
                "EmailAddress",
                StringComparison.Ordinal),
            $"{caseName}: first header mismatch.");
        Require(
            string.Equals(
                Convert.ToString(grid.Rows[0].Cells[1].Value, CultureInfo.InvariantCulture),
                "bmuller@example.invalid",
                StringComparison.Ordinal),
            $"{caseName}: second cell mismatch.");
        Require(grid.ReadOnly, $"{caseName}: grid must remain read-only.");
    }

    private static DataGridView CreateGrid() =>
        new()
        {
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToOrderColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
            EditMode = DataGridViewEditMode.EditProgrammatically,
            ReadOnly = true,
            RowHeadersVisible = true,
            SelectionMode = DataGridViewSelectionMode.CellSelect
        };

    private static void Bind(DataGridView grid, CsvTableProjection projection)
    {
        foreach (var column in projection.Columns)
        {
            grid.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = $"CsvColumn{column.Index}",
                    HeaderText = column.Name,
                    MinimumWidth = 70,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    Width = 160
                });
        }

        foreach (var row in projection.Rows)
        {
            var values = new object[row.Values.Count];
            for (var index = 0; index < row.Values.Count; index++)
            {
                values[index] = row.Values[index];
            }

            var rowIndex = grid.Rows.Add(values);
            grid.Rows[rowIndex].HeaderCell.Value =
                (row.SourceRecordIndex + 1).ToString(CultureInfo.InvariantCulture);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
