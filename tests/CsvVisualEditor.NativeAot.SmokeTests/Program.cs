namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor.Core;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static class Program
{
    private const string Sample =
        "EmailAddress,UserName,Password\r\n" +
        "muller.bela@example.invalid,bmuller@example.invalid,TEMP-password\r\n";

    [UnmanagedCallersOnly(
        EntryPoint = "RunCsvVisualTableSmoke",
        CallConvs = [typeof(CallConvCdecl)])]
    public static int RunCsvVisualTableSmoke()
    {
        var result = 99;
        var thread = new Thread(() => result = RunOnStaThread());
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return result;
    }

    private static int RunOnStaThread()
    {
        try
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            RunCase("automatic", delimiterOverride: null);
            RunCase("manual comma", delimiterOverride: ',');
            WriteDiagnostic("All Native AOT CSV table runtime smoke tests passed.");
            return 0;
        }
        catch (Exception exception)
        {
            WriteDiagnostic(
                $"{exception.GetType().FullName}: {exception.Message}{Environment.NewLine}" +
                exception.StackTrace);
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
        grid.PerformLayout();

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
        Require(
            grid.Columns.Cast<DataGridViewColumn>().All(
                static column => column.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill),
            $"{caseName}: every table column must use Fill sizing.");
        Require(
            grid.Columns.Cast<DataGridViewColumn>().All(
                static column => column.MinimumWidth == 90),
            $"{caseName}: every table column must retain the readable minimum width.");
        Require(
            grid.Columns[0].FillWeight > grid.Columns[2].FillWeight,
            $"{caseName}: the longer e-mail column should receive more relative width than Password.");
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
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            Size = new Size(760, 300)
        };

    private static void Bind(DataGridView grid, CsvTableProjection projection)
    {
        var fillWeights = CsvColumnFillWeightCalculator.Calculate(projection);

        foreach (var column in projection.Columns)
        {
            grid.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = $"CsvColumn{column.Index}",
                    HeaderText = column.Name,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    FillWeight = fillWeights[column.Index],
                    MinimumWidth = 90,
                    SortMode = DataGridViewColumnSortMode.NotSortable
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

    private static void WriteDiagnostic(string message)
    {
        var path = Environment.GetEnvironmentVariable("CSV_VISUAL_EDITOR_SMOKE_LOG");
        if (!string.IsNullOrWhiteSpace(path))
        {
            File.WriteAllText(path, message);
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
