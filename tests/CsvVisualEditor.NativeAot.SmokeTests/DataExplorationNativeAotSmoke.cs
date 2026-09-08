namespace CsvVisualEditor.NativeAot.SmokeTests;

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using CsvVisualEditor.Core;

internal static class DataExplorationNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        Directory.CreateDirectory("artifacts/ui-review");
        var p = Projection();
        foreach (var dark in new[] { false, true }) TestDialogs(p, dark);
        TestLimitsAndCancellation(p);
        MeasureBoundedView();
        Console.WriteLine("Data exploration: filter draft/preview/apply/cancel, sorting, profile, themes and bounded-view benchmark PASS.");
    }

    private static void TestDialogs(CsvTableProjection p, bool dark)
    {
        var background = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
        var foreground = dark ? Color.Gainsboro : Color.Black;
        var mode = dark ? "dark" : "light";
        using var dialog = new CsvDataViewDialog(p, CsvDataViewDefinition.Empty, "", null, background, foreground);
        dialog.Show(); Application.DoEvents();
        Button(dialog, "CsvAddFilter").PerformClick();
        var row = Find<Control>(dialog, "CsvFilterRow");
        Find<TextBox>(row, "CsvFilterValue").Text = "alpha";
        Button(dialog, "CsvPreviewView").PerformClick();
        Require(dialog.Preview?.VisibleRowCount == 2 && dialog.Result is null, "Preview must not apply a draft.");
        Find<ComboBox>(row, "CsvFilterColumn").SelectedIndex = 1;
        Find<ComboBox>(row, "CsvFilterOperator").SelectedIndex = 11; // Number >
        Find<TextBox>(row, "CsvFilterValue").Text = "1,5";
        Button(dialog, "CsvPreviewView").PerformClick();
        Require(dialog.Preview is null && Find<Label>(dialog, "CsvViewPreview").Text.StartsWith("Check the rules:", StringComparison.Ordinal), "Invalid decimal must be a visible validation error.");
        Find<TextBox>(row, "CsvFilterValue").Text = "2";
        Button(dialog, "CsvPreviewView").PerformClick();
        Require(dialog.Preview?.VisibleRowCount == 2, "Numeric filter must compare numbers, not text.");
        Find<ComboBox>(dialog, "CsvFilterCombination").SelectedIndex = 1;
        Button(dialog, "CsvAddFilter").PerformClick();
        var second = Descendants(dialog).Where(c => c.Name == "CsvFilterRow").Last();
        Find<TextBox>(second, "CsvFilterValue").Text = "gamma";
        Button(dialog, "CsvPreviewView").PerformClick();
        Require(dialog.Preview?.VisibleRowCount == 3, "ANY conditions must combine the draft correctly.");
                foreach (var width in new[] { 740, 1000 })
        {
            dialog.Width = width; Application.DoEvents();
            foreach (var filterRow in Descendants(dialog).Where(c => c.Name == "CsvFilterRow"))
            {
                var panel = filterRow.Parent!;
                var expected = panel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 6;
                Require(filterRow.Width == expected, "AutoSize rules must fill the available row width, not collapse to preferred content width.");
                var headings = Find<TableLayoutPanel>(dialog, "CsvFilterHeadings");
                var value = Find<TextBox>(filterRow, "CsvFilterValue");
                var valueHeading = headings.GetControlFromPosition(2, 0)!;
                Require(Math.Abs(value.PointToScreen(Point.Empty).X - valueHeading.PointToScreen(Point.Empty).X) <= 8,
                    "The literal-value editor must align with its heading after resize.");
                var check = Find<CheckBox>(filterRow, "CsvFilterMatchCase");
                Require(check.Width >= check.PreferredSize.Width, "Match-case checkbox must not be clipped.");
                var cols = filterRow.Controls.Cast<Control>().OrderBy(c => c.Left).ToArray();
                Require(cols.All(c => c.Width > 20 && c.Right <= filterRow.ClientSize.Width), "Filter controls must remain inside the row.");
                for (var i = 1; i < cols.Length; i++) Require(cols[i - 1].Right <= cols[i].Left, "Filter controls must not overlap.");
            }
            Require(Button(dialog, "CsvApplyView").Visible, "Apply must remain visible after resize.");
        }
        Save(dialog, $"data-filters-{mode}.png");
        Find<TabControl>(dialog, "CsvViewRuleTabs").SelectedIndex = 1;
        Button(dialog, "CsvAddSort").PerformClick();
        var sort = Find<Control>(dialog, "CsvSortRow");
        Find<ComboBox>(sort, "CsvSortColumn").SelectedIndex = 1;
        Find<ComboBox>(sort, "CsvSortKind").SelectedIndex = 1;
        Find<ComboBox>(sort, "CsvSortDirection").SelectedIndex = 1;
        Button(dialog, "CsvPreviewView").PerformClick();
        Require(dialog.Preview?.Rows[0].Values[1] == "10", "Number descending must place 10 above 3.");
        Require(sort.Width == sort.Parent!.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 6,
            "Sort levels must fill the available row width.");
        Save(dialog, $"data-sorting-{mode}.png");
        Button(dialog, "CsvApplyView").PerformClick();
        Require(dialog.DialogResult == DialogResult.OK && dialog.Result is { Filters.Count: 2, SortKeys.Count: 1 }, "Apply must publish the validated immutable definition.");
        var view = CsvTableViewBuilder.Build(p, new() { DataView = dialog.Result! });
        using var summary = new CsvColumnSummaryDialog(p, view, 1, background, foreground);
        summary.Show(); Application.DoEvents();
        Require(summary.Profile is { RowCount: 3, NumericCount: 2, Minimum: 3m, Maximum: 10m }, "Summary must follow visible numeric values only.");
        Find<ComboBox>(summary, "CsvProfileColumn").SelectedIndex = 0;
        Require(summary.Profile is { RowCount: 3, DistinctCount: 3 }, "Summary column selection must refresh the profile.");
        Require(Find<DataGridView>(summary, "CsvFrequentValues").ReadOnly, "Profile values must never become an editor.");
        summary.Width = 570; Application.DoEvents();
        Require(Find<DataGridView>(summary, "CsvFrequentValues").Height > 60, "Statistics must leave usable space for frequent values.");
        Save(summary, $"data-summary-{mode}.png");
        Button(summary, "CsvCloseSummary").PerformClick();
    }

    private static void TestLimitsAndCancellation(CsvTableProjection p)
    {
        var initial = new CsvDataViewDefinition([new(0, CsvFilterOperator.Contains, "alpha")]);
        using var dialog = new CsvDataViewDialog(p, initial, "", null, SystemColors.Control, Color.Black);
        dialog.Show(); Application.DoEvents();
        Button(dialog, "CsvResetRules").PerformClick();
        Button(dialog, "CsvPreviewView").PerformClick();
        Require(dialog.Preview?.VisibleRowCount == 4 && dialog.Result is null && initial.Filters.Count == 1, "Reset rules must remain an unapplied draft.");
        for (var i = 0; i < 10; i++) Button(dialog, "CsvAddFilter").PerformClick();
        Require(Descendants(dialog).Count(c => c.Name == "CsvFilterRow") == 8 && !Button(dialog, "CsvAddFilter").Enabled, "UI must enforce the eight-condition limit.");
        Button(dialog, "CsvRemoveFilter").PerformClick();
        Require(Button(dialog, "CsvAddFilter").Enabled, "Removing a condition must restore Add availability.");
        Find<TabControl>(dialog, "CsvViewRuleTabs").SelectedIndex = 1;
        for (var i = 0; i < 4; i++) Button(dialog, "CsvAddSort").PerformClick();
        Require(Descendants(dialog).Count(c => c.Name == "CsvSortRow") == 3 && !Button(dialog, "CsvAddSort").Enabled, "UI must enforce three sort levels.");
        Button(dialog, "CsvPreviewView").PerformClick();
        Require(dialog.Preview is null && Find<Label>(dialog, "CsvViewPreview").Text.Contains("different column", StringComparison.Ordinal), "Duplicate sort columns must be rejected without modifying the active view.");
        Button(dialog, "CsvCancelView").PerformClick();
        Require(dialog.Result is null && initial.Filters[0].Value == "alpha", "Cancel must preserve the original definition.");
    }

    private static void MeasureBoundedView()
    {
        var rows = Enumerable.Range(0, 10_000).Select(i => new CsvTableRow(i + 1,
            Enumerable.Range(0, 25).Select(c => c == 0 ? (i % 5).ToString(CultureInfo.InvariantCulture) : (i + c).ToString(CultureInfo.InvariantCulture)), new CsvSourceSpan(i * 10, 10))).ToArray();
        var p = new CsvTableProjection(Enumerable.Range(0, 25).Select(i => new CsvTableColumn(i, "Column " + i)), rows, rows.Length, 0, false);
        var definition = new CsvDataViewDefinition([new(0, CsvFilterOperator.Equals, "1"), new(1, CsvFilterOperator.GreaterThanOrEqual, "5000")],
            sortKeys: [new(1, CsvTableSortDirection.Descending, CsvSortKind.Number), new(2, CsvTableSortDirection.Ascending)]);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var clock = Stopwatch.StartNew();
        var result = CsvTableViewBuilder.Build(p, new() { DataView = definition });
        var profile = CsvColumnProfile.Build(result, 1, 25);
        clock.Stop();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(result.VisibleRowCount == 1000 && profile.NumericCount == 1000, "Bounded benchmark must check its actual output.");
        Require(clock.Elapsed.TotalSeconds < 30, "Bounded-view calculation must finish without a pathological scan.");
        Console.WriteLine($"Data exploration measured: 10000 rows x 25 columns; 1000 matches; filter + two-key sort + profile: {clock.Elapsed.TotalMilliseconds:F2} ms, {bytes} thread-allocated bytes.");
        File.WriteAllText("artifacts/ui-review/data-exploration-benchmark.txt", $"Rows=10000\nColumns=25\nMatches=1000\nElapsedMilliseconds={clock.Elapsed.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture)}\nThreadAllocatedBytes={bytes}\n");
    }

    private static CsvTableProjection Projection() => new(
        [new CsvTableColumn(0, "Name"), new CsvTableColumn(1, "Amount")],
        new[] { new[] { "alpha", "10" }, new[] { "ALPHA", "2" }, new[] { "beta", "3" }, new[] { "gamma", "" } }
            .Select((r, i) => new CsvTableRow(i + 1, r, new CsvSourceSpan(i * 10, 10))), 4, 0, false);

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static T Find<T>(Control root, string name) where T : Control => Descendants(root).OfType<T>().First(c => c.Name == name);
    private static Button Button(Control root, string name) => Find<Button>(root, name);
    private static void Save(Form form, string name)
    {
        Application.DoEvents();
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(Path.Combine("artifacts/ui-review", name));
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
