namespace CsvVisualEditor;

/// <summary>
/// Adds explicit spreadsheet clipboard commands to the visual editor toolbar.
/// The buttons use the same stable clipboard controller as keyboard and native
/// WM_COPY/WM_CUT/WM_PASTE routing, so there is one behavior regardless of focus.
/// </summary>
internal static class CsvGridClipboardToolbar
{
    private const string CopyButtonName = "CsvClipboardCopyButton";
    private const string CutButtonName = "CsvClipboardCutButton";
    private const string PasteButtonName = "CsvClipboardPasteButton";

    internal static bool TryAttach(CsvGridForm form)
    {
        ArgumentNullException.ThrowIfNull(form);

        var grid = FindTableGrid(form);
        var toolStrip = FindMainToolStrip(form);
        if (grid is null || toolStrip is null)
        {
            return false;
        }

        if (toolStrip.Items
            .Cast<ToolStripItem>()
            .Any(static item => string.Equals(
                item.Name,
                CopyButtonName,
                StringComparison.Ordinal)))
        {
            return true;
        }

        var copyButton = CreateButton(
            CopyButtonName,
            "Copy",
            "Copy the selected CSV-cell rectangle to the Windows clipboard (Ctrl+C)");
        var cutButton = CreateButton(
            CutButtonName,
            "Cut",
            "Copy the selected CSV-cell rectangle and clear it in the pending Edit session (Ctrl+X)");
        var pasteButton = CreateButton(
            PasteButtonName,
            "Paste",
            "Paste spreadsheet cells from the Windows clipboard into the current CSV-cell target (Ctrl+V)");

        copyButton.Click += (_, _) =>
        {
            CsvGridClipboardController.TryCopySelection(grid, form);
            grid.Focus();
        };
        cutButton.Click += (_, _) =>
        {
            CsvGridClipboardController.TryCutSelection(grid, form);
            grid.Focus();
        };
        pasteButton.Click += (_, _) =>
        {
            CsvGridClipboardController.TryPasteFromClipboard(grid, form);
            grid.Focus();
        };

        var editButton = toolStrip.Items
            .OfType<ToolStripButton>()
            .FirstOrDefault(static button =>
                string.Equals(button.Text, "Edit", StringComparison.Ordinal) ||
                string.Equals(button.Text, "Exit Edit", StringComparison.Ordinal));
        var insertIndex = editButton is null
            ? toolStrip.Items.Count
            : toolStrip.Items.IndexOf(editButton);

        toolStrip.Items.Insert(insertIndex++, copyButton);
        toolStrip.Items.Insert(insertIndex++, cutButton);
        toolStrip.Items.Insert(insertIndex++, pasteButton);
        toolStrip.Items.Insert(insertIndex, new ToolStripSeparator());

        void UpdateAvailability()
        {
            if (form.IsDisposed || form.Disposing || grid.IsDisposed || grid.Disposing)
            {
                return;
            }

            var hasDataColumns = grid.Columns
                .Cast<DataGridViewColumn>()
                .Any(static column => !CsvGridRowHeaderBehavior.IsPresentationColumn(column));
            var hasTarget = hasDataColumns && grid.Rows.Count > 0;
            copyButton.Enabled = hasTarget;
            cutButton.Enabled = hasTarget && form.IsEditMode;
            pasteButton.Enabled = hasTarget && form.IsEditMode;
        }

        grid.SelectionChanged += (_, _) => UpdateAvailability();
        grid.ReadOnlyChanged += (_, _) => UpdateAvailability();
        grid.RowsAdded += (_, _) => UpdateAvailability();
        grid.RowsRemoved += (_, _) => UpdateAvailability();
        grid.ColumnAdded += (_, _) => UpdateAvailability();
        grid.ColumnRemoved += (_, _) => UpdateAvailability();
        form.VisibleChanged += (_, _) => UpdateAvailability();

        UpdateAvailability();
        return true;
    }

    private static ToolStripButton CreateButton(
        string name,
        string text,
        string toolTipText) =>
        new(text)
        {
            Name = name,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ToolTipText = toolTipText
        };

    private static CsvDataGridView? FindTableGrid(Control root) =>
        EnumerateControls(root)
            .OfType<CsvDataGridView>()
            .FirstOrDefault(static grid => grid.RowHeadersVisible);

    private static ToolStrip? FindMainToolStrip(Control root) =>
        EnumerateControls(root)
            .OfType<ToolStrip>()
            .FirstOrDefault(static strip =>
                strip.Items
                    .OfType<ToolStripButton>()
                    .Any(static button =>
                        string.Equals(button.Text, "Edit", StringComparison.Ordinal) ||
                        string.Equals(button.Text, "Exit Edit", StringComparison.Ordinal)));

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in EnumerateControls(child))
            {
                yield return descendant;
            }
        }
    }
}
