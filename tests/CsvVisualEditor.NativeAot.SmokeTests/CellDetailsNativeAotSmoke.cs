namespace CsvVisualEditor.NativeAot.SmokeTests;

using System.Runtime.CompilerServices;
using CsvVisualEditor.Core;
using CsvVisualEditor.Localization;

internal static class CellDetailsNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        const string source = "  text\t\r\nsecond\nthird\rend · \\n 🙂";
        Directory.CreateDirectory("artifacts/ui-review");
        try
        {
            foreach (var code in new[] { "en", "hu", "zh-CN", "hi", "es", "ar", "fr" })
            foreach (var dark in new[] { false, true })
            {
                L10n.SetLanguage(code);
                var background = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
                var foreground = dark ? Color.Gainsboro : Color.Black;
                using (var read = new CsvCellDetailsDialog(source, L10n.Format(TextKey.Cell_Location, 2, 1), false, background, foreground))
                {
                    Require(read.Result is null && !read.CanAccept, "Read-only inspection must not offer changes.");
                    Require(CsvCellTextCodec.TryDecode(read.EditorText, out var actual, out _) && actual == source,
                        "The actual native text control must retain every source code unit.");
                    Require(!read.AcceptValue() && read.Result is null, "Read-only acceptance must be impossible.");
                }
                using var dialog = new CsvCellDetailsDialog(source, L10n.Format(TextKey.Cell_Location, 2, 1), true, background, foreground);
                dialog.Show(); Application.DoEvents();
                Require(dialog.Text == L10n.Get(TextKey.Cell_Title), "Cell details title must use the maintained catalog.");
                Require(dialog.AcceptButton is null && dialog.CancelButton is not null, "Enter must not unexpectedly accept cell edits.");
                Require(!dialog.CanAccept, "An unchanged value must be a no-op.");
                var tabs = Descendants(dialog).OfType<TabControl>().Single();
                Require(tabs.SelectedIndex == 1, "Editable cell details must open on ordinary text editing, not escape notation.");
                foreach (var width in new[] { 580, 900 })
                {
                    dialog.Width = width; Application.DoEvents();
                    var natural = Find<TextBox>(dialog, "CsvCellValuePreview");
                    var accept = Find<Button>(dialog, "CsvCellAccept");
                    Require(natural.Height >= 60 && natural.Width >= 200, "Localized wrapping must leave usable editing space.");
                    Require(!natural.ReadOnly, "The ordinary text tab must be editable in Edit mode.");
                    Require(natural.RightToLeft != RightToLeft.Yes && natural.TextAlign == HorizontalAlignment.Left,
                        "Arabic interface must not mirror or realign CSV content.");
                    Require(accept.Visible && accept.Right <= accept.Parent!.ClientSize.Width,
                        "Translated acceptance must remain visible in a narrow dialog.");
                }
                using (var image = new Bitmap(dialog.Width, dialog.Height))
                {
                    dialog.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                    image.Save($"artifacts/ui-review/cell-details-{code}-{(dark ? "dark" : "light")}.png");
                }

                dialog.NaturalText = "A B\\C";
                Require(dialog.ValidateValue() && dialog.CanAccept,
                    "Ordinary spaces and a literal backslash must be accepted in the natural editor.");
                Require(CsvCellTextCodec.TryDecode(dialog.EditorText, out var naturalValue, out _) && naturalValue == "A B\\C",
                    "Natural editing must not duplicate a literal backslash in stored cell data.");

                dialog.EditorText = "wrong\\q";
                Require(!dialog.ValidateValue() && !dialog.CanAccept && !dialog.AcceptValue() && dialog.Result is null,
                    "Invalid exact-notation escapes must fail closed.");
                dialog.NaturalText = "";
                Require(dialog.ValidateValue() && dialog.CanAccept, "An empty value is a valid cell edit.");
                dialog.NaturalText = source + "\r\nnew\tvalue";
                Require(dialog.ValidateValue() && dialog.AcceptValue() && dialog.Result == source + "\r\nnew\tvalue",
                    "Natural editing must preserve existing mixed line endings and return actual cell data.");
            }
            using var cancelled = new CsvCellDetailsDialog(source, "2 / 1", true, SystemColors.Control, Color.Black);
            cancelled.NaturalText = "discarded";
            cancelled.Close();
            Require(cancelled.Result is null, "Closing without acceptance must discard edits.");
        }
        finally { L10n.SetLanguage("en"); }
        Console.WriteLine("Cell details: seven languages, natural editing, literal spaces/backslashes, mixed line endings, exact validation, cancel and read-only values PASS.");
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static T Find<T>(Control root, string name) where T : Control =>
        Descendants(root).OfType<T>().Single(control => control.Name == name);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
