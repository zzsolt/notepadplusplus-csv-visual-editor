namespace CsvVisualEditor;

using CsvVisualEditor.Localization;

/// <summary>Shared, dependency-free presentation for the two view-only dialogs.</summary>
internal static class CsvDataToolStyle
{
    internal static void Apply(Control root, Color background, Color foreground)
    {
        var field = background.GetBrightness() < .5f ? Color.FromArgb(38, 40, 43) : SystemColors.Window;
        root.BackColor = background;
        root.ForeColor = foreground;
        foreach (Control child in root.Controls)
        {
            Apply(child, background, foreground);
            if (child is TextBox or ComboBox or DataGridView) child.BackColor = field;
            if (child is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = CsvSearchBar.Blend(background, foreground, 35);
                button.FlatAppearance.MouseOverBackColor = CsvSearchBar.Blend(background, foreground, 10);
            }
            if (child is DataGridView grid)
            {
                grid.BackgroundColor = field;
                grid.DefaultCellStyle.BackColor = field;
                grid.DefaultCellStyle.ForeColor = foreground;
                grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersDefaultCellStyle.BackColor = background;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = foreground;
                grid.GridColor = CsvSearchBar.Blend(field, foreground, 20);
            }
        }
    }

    internal static Button Button(string text, string name) => new()
    {
        Text = text, Name = name, AccessibleName = text.Replace("&", "", StringComparison.Ordinal),
        AutoSize = true, MinimumSize = new Size(86, 30), Margin = new Padding(4),
        UseVisualStyleBackColor = false
    };

    internal static ComboBox Combo(string name, params object[] items)
    {
        var combo = new ComboBox
        {
            Name = name, AccessibleName = AccessibleComboName(name), DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill, Margin = new Padding(4, 5, 4, 5), IntegralHeight = false,
            DropDownHeight = 260
        };
        combo.Items.AddRange(items);
        if (items.Length > 0) combo.SelectedIndex = 0;
        return combo;
    }

    private static string AccessibleComboName(string name) => L10n.Get(name switch
    {
        "CsvFilterCombination" => TextKey.Filter_Filters,
        "CsvFilterOperator" => TextKey.Filter_Condition,
        "CsvSortKind" => TextKey.Summary_Kind,
        "CsvSortDirection" => TextKey.Filter_Sorting,
        _ => TextKey.Common_Column
    });

    internal static Label Label(string text) => new()
    {
        Text = text, AutoSize = true, UseMnemonic = false, Margin = new Padding(4, 6, 4, 6),
        Anchor = AnchorStyles.Left
    };
}
