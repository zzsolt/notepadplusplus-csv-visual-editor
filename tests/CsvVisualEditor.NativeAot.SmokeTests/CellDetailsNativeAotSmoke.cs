namespace CsvVisualEditor.NativeAot.SmokeTests;

using System.Runtime.CompilerServices;
using CsvVisualEditor.Core;
using CsvVisualEditor.Localization;

internal static class CellDetailsNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        const string source = "  text\t\r\nsecond\nthird\rend \u00b7 \\n \U0001f642";
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
                foreach (var width in new[] { 580, 900 })
                {
                    dialog.Width = width; Application.DoEvents();
                    var editor = Find<TextBox>(dialog, "CsvCellValueEditor");
                    var accept = Find<Button>(dialog, "CsvCellAccept");
                    Require(editor.Height >= 60 && editor.Width >= 200, "Localized wrapping must leave usable editing space.");
                    Require(editor.RightToLeft != RightToLeft.Yes && editor.TextAlign == HorizontalAlignment.Left,
                        "Arabic interface must not mirror or realign CSV notation.");
                    Require(accept.Visible && accept.Right <= accept.Parent!.ClientSize.Width,
                        "Translated acceptance must remain visible in a narrow dialog.");
                }
                using (var image = new Bitmap(dialog.Width, dialog.Height))
                {
                    dialog.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                    image.Save($"artifacts/ui-review/cell-details-{code}-{(dark ? "dark" : "light")}.png");
                }
                dialog.EditorText = "wrong\\q";
                Require(!dialog.ValidateValue() && !dialog.CanAccept && !dialog.AcceptValue() && dialog.Result is null,
                    "Invalid escapes must fail closed in the actual dialog.");
                dialog.EditorText = "";
                Require(dialog.ValidateValue() && dialog.CanAccept, "An empty value is a valid cell edit.");
                dialog.EditorText = CsvCellTextCodec.Encode(source + "\r\nnew\tvalue");
                Require(dialog.AcceptValue() && dialog.Result == source + "\r\nnew\tvalue",
                    "Actual dialog must return exact data, not its normalized preview.");
            }
            using var cancelled = new CsvCellDetailsDialog(source, "2 / 1", true, SystemColors.Control, Color.Black);
            cancelled.EditorText = "discarded";
            cancelled.Close();
            Require(cancelled.Result is null, "Closing without acceptance must discard edits.");
        }
        finally { L10n.SetLanguage("en"); }
        Console.WriteLine("Cell details: seven languages, light/dark, narrow layout, exact escapes, no-op, cancel, read-only and accepted values PASS.");
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
