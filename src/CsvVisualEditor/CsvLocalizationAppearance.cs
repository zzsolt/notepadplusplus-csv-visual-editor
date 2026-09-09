namespace CsvVisualEditor;

using CsvVisualEditor.Localization;

/// <summary>Interface direction does not reorder physical CSV columns or change values.</summary>
internal static class CsvLocalizationAppearance
{
    internal static void Apply(Control root)
    {
        if (!L10n.IsRightToLeft) return;
        root.RightToLeft = RightToLeft.Yes;
        // Mirroring the native dock would also mirror CSV data-column order. Keep
        // the host layout stable; labels/menus/edit controls still use bidi text.
        foreach (var control in Descendants(root))
        {
            if (control is DataGridView) control.RightToLeft = RightToLeft.No;
            if (control is Label label) label.TextAlign = ContentAlignment.MiddleRight;
            if (control is TextBox box && box.ReadOnly) box.TextAlign = HorizontalAlignment.Right;
        }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
