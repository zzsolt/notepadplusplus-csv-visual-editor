namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor;
using CsvVisualEditor.Core;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;

internal static class GridRowHeaderNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        const string source = "A,B\none,two\nthree,four";
        var snapshot = ActiveDocumentSnapshot.Create(
            @"C:\Synthetic\native-row-header.csv",
            source,
            Encoding.UTF8.GetByteCount(source),
            codePage: 65001,
            caretPosition: 0,
            anchorPosition: 0,
            isModified: false,
            new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero));
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
        var stableRows = model.GetVisibleRows();

        using var form = new Form();
        using var tableGrid = new TestDataGridView
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
            "Native AOT row presentation did not find the hidden table grid during bootstrap.");

        tableGrid.RowHeadersVisible = true;
        tableGrid.Columns.Add("A", "A");
        tableGrid.Columns.Add("B", "B");
        CsvGridRowHeaderBehavior.SynchronizeTablePresentation(tableGrid);
        Require(
            tableGrid.Columns[0].Name == "A" && tableGrid.Columns[1].Name == "B",
            "Row presentation changed physical CSV column indexes.");
        Require(
            CsvGridRowPresentation.HasRowIndicatorColumn(tableGrid),
            "Native AOT row-indicator column was not created.");

        var indicatorColumn = tableGrid.Columns[CsvGridRowPresentation.RowIndicatorColumnName] ??
            throw new InvalidOperationException("Row-indicator column lookup failed.");
        Require(
            indicatorColumn.Index == 2 && indicatorColumn.DisplayIndex == 0,
            "Row indicator must be physically appended but visually placed before CSV columns.");
        Require(
            indicatorColumn.ReadOnly && indicatorColumn.Frozen,
            "Row indicator must remain read-only and visible while horizontally scrolling.");
        Require(
            indicatorColumn.DefaultCellStyle.Alignment ==
                DataGridViewContentAlignment.MiddleCenter,
            "Row-indicator labels are not centered in their dedicated column.");
        Require(
            tableGrid.RowHeadersWidthSizeMode ==
                DataGridViewRowHeadersWidthSizeMode.AutoSizeToDisplayedHeaders,
            "Native row-header width must be framework-measured from glyph/theme/DPI requirements.");
        Require(tableGrid.ShowEditingIcon, "Native current-row indication was disabled.");
        Require(!tableGrid.ShowRowErrors, "Unused native row-error capacity remains enabled.");

        for (var index = 0; index < stableRows.Count; index++)
        {
            var rowIndex = tableGrid.Rows.Add(stableRows[index].Values.Cast<object>().ToArray());
            var gridRow = tableGrid.Rows[rowIndex];
            gridRow.Tag = index;
            CsvGridRowPresentation.SetRowIndicator(
                gridRow,
                (index + 2).ToString(),
                $"Source logical record {index + 2}");
        }

        CsvGridRowHeaderBehavior.RefreshPresentationLayout(tableGrid);
        var compactIndicatorWidth = indicatorColumn.Width;
        var compactNativeHeaderWidth = tableGrid.RowHeadersWidth;
        Require(compactIndicatorWidth >= indicatorColumn.MinimumWidth,
            "Row-indicator content width is below its DPI-scaled minimum.");
        Require(compactNativeHeaderWidth > 0,
            "Native glyph-only row-header width was not measured.");
        Require(
            tableGrid.Rows.Cast<DataGridViewRow>().All(static row => row.HeaderCell.Value is null),
            "Logical record labels leaked back into native glyph cells.");
        Require(
            string.Equals(
                Convert.ToString(tableGrid.Rows[0].Cells[indicatorColumn.Index].Value),
                "2",
                StringComparison.Ordinal) &&
            string.Equals(
                Convert.ToString(tableGrid.Rows[1].Cells[indicatorColumn.Index].Value),
                "3",
                StringComparison.Ordinal),
            "Native AOT row-indicator values do not retain logical record numbers.");

        // The number cell is a separate hit target in read-only mode while the
        // narrow native row header remains the framework-owned current-row lane.
        tableGrid.ClearSelection();
        tableGrid.RaiseCellMouseDown(indicatorColumn.Index, rowIndex: 1);
        Require(tableGrid.Rows[1].Selected,
            "Read-only row-indicator click did not select the complete row.");
        Require(tableGrid.CurrentCell?.ColumnIndex == 0,
            "Read-only indicator click did not preserve the first CSV data cell as current.");

        for (var index = 0; index < stableRows.Count; index++)
        {
            tableGrid.Rows[index].Tag = stableRows[index].Id;
        }

        Require(
            CsvGridRowHeaderBehavior.SelectWholeRow(tableGrid, rowIndex: 0),
            "Native AOT row-header selection did not select the stable row.");
        Require(
            tableGrid.Rows[0].Selected,
            "Native AOT row-header selection did not mark the complete row selected.");
        Require(
            CsvGridSelectionSnapshot.Capture(tableGrid).Count == 1,
            "Native AOT row-header selection did not synchronize stable selection state.");

        tableGrid.ReadOnly = false;
        Require(
            tableGrid.RowHeadersWidth == compactNativeHeaderWidth,
            "Entering Edit mode changed the glyph-only native row-header width.");
        Require(
            CsvGridRowHeaderBehavior.HasSelectorColumn(tableGrid),
            "Native AOT selector column did not appear in Edit mode.");
        var selectorColumn = tableGrid.Columns[CsvGridRowHeaderBehavior.SelectorColumnName] ??
            throw new InvalidOperationException("Selector column lookup failed.");
        Require(
            selectorColumn.DisplayIndex == tableGrid.Columns.Count - 1,
            "Native AOT selector column is not the rightmost visible column.");
        Require(
            indicatorColumn.Index == 2 && tableGrid.Columns[0].Name == "A",
            "Edit-mode presentation changed CSV column indexes.");

        CsvGridRowHeaderBehavior.ClearManagedSelection(tableGrid);
        tableGrid.RaiseCellMouseDown(indicatorColumn.Index, rowIndex: 1);
        var indicatorSelection = CsvGridSelectionSnapshot.Capture(tableGrid);
        Require(
            indicatorSelection.Count == 1 &&
            indicatorSelection[0].Id == stableRows[1].Id &&
            tableGrid.Rows[1].Selected,
            "Edit-mode row-indicator click did not forward to stable complete-row selection.");

        CsvGridRowPresentation.SetRowIndicator(
            tableGrid.Rows[1],
            "new:10000 *",
            "Pending inserted row 10000; not yet applied");
        CsvGridRowHeaderBehavior.RefreshPresentationLayout(tableGrid);
        var expandedIndicatorWidth = indicatorColumn.Width;
        Require(
            expandedIndicatorWidth >= compactIndicatorWidth,
            "Structural row label did not expand the content-driven indicator width.");
        Require(
            tableGrid.RowHeadersWidth == compactNativeHeaderWidth,
            "Structural labels expanded the native glyph lane.");

        CsvGridRowPresentation.SetRowIndicator(
            tableGrid.Rows[1],
            "3",
            "Source logical record 3");
        CsvGridRowHeaderBehavior.RefreshPresentationLayout(tableGrid);
        Require(
            indicatorColumn.Width <= expandedIndicatorWidth,
            "Removing the structural label did not release unnecessary indicator width.");

        tableGrid.EnableHeadersVisualStyles = false;
        tableGrid.DefaultCellStyle.BackColor = Color.Black;
        tableGrid.DefaultCellStyle.ForeColor = Color.White;
        tableGrid.RowHeadersDefaultCellStyle.BackColor = Color.Black;
        tableGrid.RowHeadersDefaultCellStyle.ForeColor = Color.White;
        CsvGridRowHeaderBehavior.RefreshPresentationLayout(tableGrid);
        Require(
            tableGrid.Rows.Cast<DataGridViewRow>().All(static row => row.HeaderCell.Value is null),
            "Dark-mode refresh restored native row-header labels.");
        Require(
            indicatorColumn.DefaultCellStyle.BackColor.IsEmpty &&
            indicatorColumn.DefaultCellStyle.ForeColor.IsEmpty,
            "Row indicator hard-coded colors instead of inheriting the grid theme.");

        tableGrid.ReadOnly = true;
        Require(
            !CsvGridRowHeaderBehavior.HasSelectorColumn(tableGrid),
            "Native AOT selector column was not removed after leaving Edit mode.");
        Require(
            CsvGridRowPresentation.HasRowIndicatorColumn(tableGrid),
            "Leaving Edit mode removed the read-only row-indicator column.");

        tableGrid.Rows.Clear();
        tableGrid.Columns.Clear();
        tableGrid.RowHeadersVisible = false;
        tableGrid.RowHeadersVisible = true;
        tableGrid.Columns.Add("C", "C");
        CsvGridRowHeaderBehavior.SynchronizeTablePresentation(tableGrid);
        var reconstructedRowIndex = tableGrid.Rows.Add("value");
        CsvGridRowPresentation.SetRowIndicator(
            tableGrid.Rows[reconstructedRowIndex],
            "2",
            "Source logical record 2");
        CsvGridRowHeaderBehavior.RefreshPresentationLayout(tableGrid);
        Require(
            CsvGridRowPresentation.HasRowIndicatorColumn(tableGrid),
            "Native AOT reconstruction did not restore the row-indicator column.");
        Require(
            tableGrid.RowHeadersWidthSizeMode ==
                DataGridViewRowHeadersWidthSizeMode.AutoSizeToDisplayedHeaders,
            "Native AOT reconstruction did not restore native glyph auto-sizing.");
        Require(
            tableGrid.Rows[0].HeaderCell.Value is null,
            "Native AOT reconstruction put the number back into the glyph cell.");
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

    private sealed class TestDataGridView : DataGridView
    {
        internal void RaiseCellMouseDown(int columnIndex, int rowIndex)
        {
            OnCellMouseDown(
                new DataGridViewCellMouseEventArgs(
                    columnIndex,
                    rowIndex,
                    1,
                    1,
                    new MouseEventArgs(
                        MouseButtons.Left,
                        1,
                        1,
                        1,
                        0)));
        }
    }
}
