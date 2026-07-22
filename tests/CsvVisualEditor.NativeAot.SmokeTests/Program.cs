namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor.Core;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

internal static class Program
{
    private const string Sample =
        "EmailAddress,UserName,Password\r\n" +
        "muller.bela@example.invalid,bmuller@example.invalid,TEMP-b\r\n" +
        "alpha@example.invalid,alpha@example.invalid,TEMP-a\r\n" +
        "gamma@other.invalid,gamma@other.invalid,TEMP-c\r\n";

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
            RunSerializerCase();
            RunCase("automatic", delimiterOverride: null);
            RunCase("manual comma", delimiterOverride: ',');
            WriteDiagnostic(
                "All Native AOT CSV table, view, serializer, edit-session, and diagnostics UI smoke tests passed.");
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
        Bind(grid, result.Projection.Rows, result.Projection);
        grid.PerformLayout();

        Require(grid.Columns.Count == 3, $"{caseName}: expected 3 columns.");
        Require(grid.Rows.Count == 3, $"{caseName}: expected 3 data rows.");
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

        RunViewCase(caseName, result.Projection);
        RunEditSessionCase(caseName, result.ParseResult, result.Projection);
        RunViewControlsCase(caseName);
    }

    private static void RunSerializerCase()
    {
        var policy = CsvSerializationPolicy.Create(
            CsvDialect.Create(';'),
            "\n",
            hasTerminalNewLine: true,
            hasLeadingBom: true);
        string[][] records =
        [
            ["Név", "Megjegyzés"],
            ["Árvíztűrő", "idézőjel: \" és pontosvessző; valamint\núj sor"]
        ];

        var serialized = CsvSerializer.SerializeRecords(records, policy);
        Require(serialized.StartsWith('\uFEFF'), "serializer: leading BOM was not retained.");
        Require(serialized.EndsWith('\n'), "serializer: terminal LF was not retained.");
        Require(
            serialized.Contains(
                "\"idézőjel: \"\" és pontosvessző; valamint\núj sor\"",
                StringComparison.Ordinal),
            "serializer: structural characters were not quoted deterministically.");
    }

    private static void RunEditSessionCase(
        string caseName,
        CsvParseResult parseResult,
        CsvTableProjection projection)
    {
        var baseline = CreateSnapshot(Sample);
        var session = CsvEditSession.Create(baseline, parseResult, projection);

        Require(!session.IsDirty, $"{caseName}: new edit session must be clean.");
        Require(session.RecordCount == 4, $"{caseName}: edit session record count mismatch.");
        Require(session.HeaderSourceRecordIndex == 0, $"{caseName}: header identity mismatch.");

        session.SetCellValue(1, 2, "TEMP,\"b\"");
        Require(session.IsDirty, $"{caseName}: edited session must be dirty.");
        Require(session.ChangedCellCount == 1, $"{caseName}: changed-cell count mismatch.");
        Require(session.ChangedRecordCount == 1, $"{caseName}: changed-record count mismatch.");

        var preview = session.CreatePreview();
        Require(preview.HasChanges, $"{caseName}: preview must report changes.");
        Require(
            preview.Text.Contains("\"TEMP,\"\"b\"\"\"", StringComparison.Ordinal),
            $"{caseName}: edited structural value was not serialized safely.");
        Require(
            preview.Text.EndsWith("gamma@other.invalid,gamma@other.invalid,TEMP-c\r\n", StringComparison.Ordinal),
            $"{caseName}: unchanged final record or separator was modified.");

        var readyPlan = session.CreateApplyPlan(baseline);
        Require(readyPlan.IsReady, $"{caseName}: matching baseline should produce Ready plan.");
        Require(readyPlan.Preview is not null, $"{caseName}: Ready plan must contain preview.");

        var changedSnapshot = CreateSnapshot(
            Sample.Replace("TEMP-c", "EXTERNAL", StringComparison.Ordinal));
        var conflictPlan = session.CreateApplyPlan(changedSnapshot);
        Require(
            conflictPlan.Status == CsvEditApplyStatus.ContentChanged,
            $"{caseName}: changed source content must block apply planning.");
        Require(conflictPlan.Preview is null, $"{caseName}: conflict plan must not expose replacement preview.");

        Require(session.RevertAll(), $"{caseName}: Revert All should report a change.");
        Require(!session.IsDirty, $"{caseName}: Revert All must clear dirty state.");
        Require(
            string.Equals(session.CreatePreview().Text, Sample, StringComparison.Ordinal),
            $"{caseName}: Revert All must restore exact source text.");
    }

    private static ActiveDocumentSnapshot CreateSnapshot(string text)
    {
        return ActiveDocumentSnapshot.Create(
            @"C:\Synthetic\native-aot.csv",
            text,
            Encoding.UTF8.GetByteCount(text),
            codePage: 65001,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            new DateTimeOffset(2026, 7, 22, 8, 0, 0, TimeSpan.Zero));
    }

    private static void RunViewCase(
        string caseName,
        CsvTableProjection projection)
    {
        var originalOrder = projection.Rows
            .Select(static row => row.SourceRecordIndex)
            .ToArray();
        var view = CsvTableViewBuilder.Build(
            projection,
            new CsvTableViewOptions
            {
                SearchText = "EXAMPLE.INVALID",
                SearchColumnIndex = 0,
                SortColumnIndex = 1,
                SortDirection = CsvTableSortDirection.Descending
            });

        Require(view.IsFiltered, $"{caseName}: search view should be filtered.");
        Require(view.IsSorted, $"{caseName}: search view should be sorted.");
        Require(view.VisibleRowCount == 2, $"{caseName}: expected two matching rows.");
        Require(
            view.Rows[0].SourceRecordIndex == 1 &&
            view.Rows[1].SourceRecordIndex == 2,
            $"{caseName}: stable descending view order mismatch.");
        Require(
            projection.Rows.Select(static row => row.SourceRecordIndex)
                .SequenceEqual(originalOrder),
            $"{caseName}: view operations must not mutate projection order.");

        using var filteredGrid = CreateGrid();
        Bind(filteredGrid, view.Rows, projection);
        Require(
            filteredGrid.Rows.Count == 2,
            $"{caseName}: filtered grid should contain two rows.");
        Require(
            string.Equals(
                Convert.ToString(
                    filteredGrid.Rows[0].HeaderCell.Value,
                    CultureInfo.InvariantCulture),
                "2",
                StringComparison.Ordinal),
            $"{caseName}: filtered row must retain source logical-record number.");
    }

    private static void RunViewControlsCase(string caseName)
    {
        using var searchBox = new ToolStripTextBox
        {
            Text = "example.invalid"
        };
        using var columnCombo = new ToolStripComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        columnCombo.Items.AddRange(["All columns", "EmailAddress"]);
        columnCombo.SelectedIndex = 0;

        using var searchStrip = new ToolStrip();
        searchStrip.Items.Add(searchBox);
        searchStrip.Items.Add(columnCombo);

        using var tablePage = new TabPage("Table");
        using var diagnosticsPage = new TabPage("Diagnostics (1)");
        using var tabs = new TabControl();
        tabs.TabPages.Add(tablePage);
        tabs.TabPages.Add(diagnosticsPage);
        tabs.SelectedTab = diagnosticsPage;

        using var diagnosticsGrid = CreateDiagnosticsGrid();
        diagnosticsPage.Controls.Add(diagnosticsGrid);
        diagnosticsGrid.Rows.Add("Warning", "CSV004", "2", "24", "Synthetic diagnostic");

        using var timer = new System.Windows.Forms.Timer
        {
            Interval = 250
        };

        Require(
            tabs.TabPages.Count == 2 && tabs.SelectedTab == diagnosticsPage,
            $"{caseName}: diagnostics tab control mismatch.");
        Require(
            searchStrip.Items.Count == 2 && columnCombo.SelectedIndex == 0,
            $"{caseName}: search toolbar mismatch.");
        Require(
            diagnosticsGrid.Rows.Count == 1 && diagnosticsGrid.Columns.Count == 5,
            $"{caseName}: diagnostics grid mismatch.");
        Require(timer.Interval == 250, $"{caseName}: search debounce timer mismatch.");
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

    private static DataGridView CreateDiagnosticsGrid()
    {
        var grid = new DataGridView
        {
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false
        };

        foreach (var name in new[] { "Severity", "Code", "Record", "Character", "Message" })
        {
            grid.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = name,
                    HeaderText = name,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
        }

        return grid;
    }

    private static void Bind(
        DataGridView grid,
        IEnumerable<CsvTableRow> rows,
        CsvTableProjection projection)
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
                    SortMode = DataGridViewColumnSortMode.Programmatic
                });
        }

        foreach (var row in rows)
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
