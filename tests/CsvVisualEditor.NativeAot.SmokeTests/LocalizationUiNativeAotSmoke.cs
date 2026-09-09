namespace CsvVisualEditor.NativeAot.SmokeTests;

using System.Globalization;
using System.Runtime.CompilerServices;
using CsvVisualEditor.Core;
using CsvVisualEditor.Localization;

internal static class LocalizationUiNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        Directory.CreateDirectory("artifacts/ui-review");
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        var projection = new CsvTableProjection(
            [new CsvTableColumn(0, "Value"), new CsvTableColumn(1, "Amount")],
            [new CsvTableRow(1, ["  alpha  ", "12.5"], new CsvSourceSpan(0, 20)),
             new CsvTableRow(2, ["   ", "-12.5"], new CsvSourceSpan(20, 20)),
             new CsvTableRow(3, ["", "2"], new CsvSourceSpan(40, 10))], 3, 0, false);
        var reviewed = new HashSet<string>(["en", "hu", "de", "ar", "he", "ja", "zh-TW", "ab", "kab", "sgs", "ext"], StringComparer.Ordinal);
        try
        {
            foreach (var language in L10n.Languages.DistinctBy(static item => item.Code))
            {
                Require(L10n.HasCompleteCatalog(language.Code), "Catalog must be embedded and complete: " + language.Code);
                L10n.InitializeFromNativeLanguage(language.NativeFilename);
                Require(L10n.LanguageCode == language.Code, "Host filename must select its actual language.");
                Require(ReferenceEquals(culture, CultureInfo.CurrentCulture) && ReferenceEquals(uiCulture, CultureInfo.CurrentUICulture),
                    "Localization must not change application cultures.");
                Require(CsvUiText.ColumnName(new CsvTableColumn(0, "Column 1")) == "Column 1", "Real CSV headers must never be translated.");
                Require(CsvUiText.ColumnName(new CsvTableColumn(0, "Column 1", true)).StartsWith(L10n.Get(TextKey.Common_Column), StringComparison.Ordinal),
                    "Synthetic column labels must use the selected catalog.");
                foreach (var dark in reviewed.Contains(language.Code) ? new[] { false, true } : new[] { false })
                {
                    var background = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
                    var foreground = dark ? Color.Gainsboro : Color.Black;
                    using var filter = new CsvDataViewDialog(projection, CsvDataViewDefinition.Empty, "", null, background, foreground);
                    filter.Show(); Application.DoEvents();
                    Find<Button>(filter, "CsvAddFilter").PerformClick();
                    Find<TextBox>(filter, "CsvFilterValue").Text = "alpha";
                    Find<Button>(filter, "CsvPreviewView").PerformClick();
                    Require(filter.Preview?.VisibleRowCount == 1 && filter.Result is null, "Localized preview must retain view semantics.");
                    Require(Find<Label>(filter, "CsvViewPreview").Text == L10n.Format(TextKey.Filter_PreviewOfDisplayedRowsCSVUnchanged, 1, 3),
                        "Dynamic preview must use the selected catalog.");
                    foreach (var width in new[] { 740, 1000 })
                    {
                        filter.Width = width; Application.DoEvents();
                        var row = Find<Control>(filter, "CsvFilterRow");
                        var controls = row.Controls.Cast<Control>().OrderBy(static c => c.Left).ToArray();
                        Require(controls.All(c => c.Width >= 20 && c.Left >= 0 && c.Right <= row.Width), "Localized filter controls must fit.");
                        for (var i = 1; i < controls.Length; i++) Require(controls[i - 1].Right <= controls[i].Left, "Localized controls must not overlap.");
                        Require(Find<Button>(filter, "CsvApplyView").Visible, "Localized Apply view must remain visible.");
                    }
                    if (reviewed.Contains(language.Code)) Save(filter, language.Code, "filters", dark);
                    Find<Button>(filter, "CsvCancelView").PerformClick();
                    using var summary = new CsvColumnSummaryDialog(projection, CsvTableViewBuilder.Build(projection, new()), 0, background, foreground);
                    summary.Show(); Application.DoEvents();
                    var values = Find<DataGridView>(summary, "CsvFrequentValues");
                    Require(values.RightToLeft != RightToLeft.Yes && values.ReadOnly, "RTL must not reverse or enable editing of CSV columns.");
                    Require(summary.Profile is { RowCount: 3, EmptyCount: 1, WhitespaceOnlyCount: 1 }, "Localization must preserve raw summary values.");
                    Require(values.Height >= 60, "Translated summary labels must leave usable value rows.");
                    if (reviewed.Contains(language.Code)) Save(summary, language.Code, "summary", dark);
                    summary.Close();
                    if (reviewed.Contains(language.Code))
                    {
                        using var about = new CsvAboutDialog(background, foreground);
                        about.Show(); Application.DoEvents();
                        Require(about.Text == L10n.Get(TextKey.About_AboutCSVVisualEditor), "About caption must be localized.");
                        Save(about, language.Code, "about", dark);
                        about.Close();
                    }
                }
            }
            L10n.InitializeFromNativeLanguage("custom-unknown.xml");
            Require(L10n.LanguageCode == "en" && L10n.Get(TextKey.Common_Cancel) == "Cancel", "Unknown host language must fall back to English.");
        }
        finally { L10n.SetLanguage("en"); }
        Console.WriteLine("Localization: all catalogs, native filenames, translated dialogs, layout, RTL data preservation and English fallback PASS.");
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control control in root.Controls)
        {
            yield return control;
            foreach (var child in Descendants(control)) yield return child;
        }
    }
    private static T Find<T>(Control root, string name) where T : Control => Descendants(root).OfType<T>().First(c => c.Name == name);
    private static void Save(Form form, string language, string section, bool dark)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save($"artifacts/ui-review/localized-{language}-{section}-{(dark ? "dark" : "light")}.png");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
