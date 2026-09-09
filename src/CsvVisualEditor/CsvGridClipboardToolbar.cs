namespace CsvVisualEditor;

using CsvVisualEditor.Localization;

using System.Runtime.CompilerServices;

/// <summary>
/// Adds explicit spreadsheet clipboard/navigation commands and reorganizes the visual
/// editor's existing command bars without changing the grid's accepted selection semantics.
/// </summary>
internal static class CsvGridClipboardToolbar
{
    private const string CopyButtonName = "CsvClipboardCopyButton";
    private const string CutButtonName = "CsvClipboardCutButton";
    private const string PasteButtonName = "CsvClipboardPasteButton";
    private const string GoToSourceButtonName = "CsvGoToSourceButton";
    private const string TransformButtonName = "CsvTransformButton";
    private const int MaximumDeferredAttachAttempts = 4;

    private static readonly ConditionalWeakTable<CsvGridForm, AttachmentState> States = new();

    internal static bool TryAttach(CsvGridForm form)
    {
        ArgumentNullException.ThrowIfNull(form);

        var state = States.GetValue(form, static _ => new AttachmentState());
        if (!state.EventsAttached)
        {
            state.EventsAttached = true;
            form.HandleCreated += (_, _) => ResetAndQueueAttach(form, state);
            form.VisibleChanged += (_, _) =>
            {
                if (form.Visible && !state.Attached)
                {
                    ResetAndQueueAttach(form, state);
                }
            };
        }

        if (TryAttachNow(form, state))
        {
            return true;
        }

        QueueAttach(form, state);
        return true;
    }

    private static bool TryAttachNow(CsvGridForm form, AttachmentState state)
    {
        if (form.IsDisposed || form.Disposing)
        {
            return false;
        }

        if (state.Attached && HasCommandButtons(form))
        {
            return true;
        }

        var grid = FindTableGrid(form);
        var commandStrip = FindCommandStrip(form);
        var viewStrip = FindViewStrip(form, commandStrip);
        if (grid is null || commandStrip is null || viewStrip is null)
        {
            return false;
        }

        if (!TryRebuildCommandBars(
                form,
                grid,
                commandStrip,
                viewStrip,
                out var updateAvailability))
        {
            return false;
        }

        ApplySpreadsheetPresentation(grid);
        WireAvailabilityRefresh(form, grid, updateAvailability);
        updateAvailability();

        state.Attached = true;
        state.DeferredAttachAttempts = 0;
        return true;
    }

    private static bool TryRebuildCommandBars(
        CsvGridForm form,
        DataGridView grid,
        ToolStrip commandStrip,
        ToolStrip viewStrip,
        out Action updateAvailability)
    {
        updateAvailability = static () => { };

        var refreshButton = FindNamedButton(commandStrip, "CsvRefreshButton");
        var editButton = FindNamedButton(commandStrip, "CsvEditButton");
        var addRowButton = FindNamedButton(commandStrip, "CsvAddRowButton");
        var deleteRowButton = FindNamedButton(commandStrip, "CsvDeleteRowButton");
        var applyButton = FindNamedButton(commandStrip, "CsvApplyButton");
        var revertButton = FindNamedButton(commandStrip, "CsvRevertAllButton");
        var dirtyLabel = commandStrip.Items.OfType<ToolStripLabel>().FirstOrDefault(static item => item.Name == "CsvDirtyLabel");
        var delimiterLabel = commandStrip.Items.OfType<ToolStripLabel>().FirstOrDefault(static item => item.Name == "CsvDelimiterLabel");
        var headerLabel = commandStrip.Items.OfType<ToolStripLabel>().FirstOrDefault(static item => item.Name == "CsvHeaderLabel");
        var commandCombos = commandStrip.Items.OfType<ToolStripComboBox>().ToArray();
        var delimiterCombo = commandCombos.ElementAtOrDefault(0);
        var headerCombo = commandCombos.ElementAtOrDefault(1);
        var clearButton = FindNamedButton(viewStrip, "CsvResetViewButton");
        var diagnosticsButton = FindNamedButton(viewStrip, "CsvDiagnosticsButton");

        if (refreshButton is null ||
            editButton is null ||
            addRowButton is null ||
            deleteRowButton is null ||
            applyButton is null ||
            revertButton is null ||
            delimiterLabel is null ||
            headerLabel is null ||
            delimiterCombo is null ||
            headerCombo is null ||
            clearButton is null ||
            diagnosticsButton is null)
        {
            return false;
        }

        RemoveExistingCommandButtons(form);

        var pasteButton = CreateButton(
            PasteButtonName,
            L10n.Get(TextKey.Common_Paste),
            L10n.Get(TextKey.Commands_PasteSpreadsheetCellsIntoTheSelectedCSVCell));
        var cutButton = CreateButton(
            CutButtonName,
            L10n.Get(TextKey.Common_Cut),
            L10n.Get(TextKey.Commands_CopyTheSelectedCSVCellsAndClearThem));
        var copyButton = CreateButton(
            CopyButtonName,
            L10n.Get(TextKey.Common_Copy),
            L10n.Get(TextKey.Commands_CopyTheSelectedCSVCellRectangleToThe));
        var goToSourceButton = CreateButton(
            GoToSourceButtonName,
            L10n.Get(TextKey.Commands_Source),
            L10n.Get(TextKey.Commands_SelectTheCurrentCSVCellOrRowIn));

        var transformButton = CreateButton(TransformButtonName, L10n.Get(TextKey.Commands_Transform),
            L10n.Get(TextKey.Commands_PreviewTextReplacementTrimmingOrCasingInPending));
        transformButton.Click += (_, _) => form.ShowTransforms();

        pasteButton.Click += (_, _) =>
        {
            CsvGridClipboardController.TryPasteFromClipboard(grid, form);
            grid.Focus();
        };
        cutButton.Click += (_, _) =>
        {
            CsvGridClipboardController.TryCutSelection(grid, form);
            grid.Focus();
        };
        copyButton.Click += (_, _) =>
        {
            CsvGridClipboardController.TryCopySelection(grid, form);
            grid.Focus();
        };
        goToSourceButton.Click += (_, _) =>
        {
            CsvGridSourceNavigationController.TryNavigate(grid, form);
        };

        commandStrip.SuspendLayout();
        viewStrip.SuspendLayout();
        try
        {
            commandStrip.Items.Clear();
            viewStrip.Items.Clear();

            ConfigureStrip(commandStrip, verticalPadding: 2);
            ConfigureStrip(viewStrip, verticalPadding: 1);

            // Spreadsheet command row. Clipboard and source navigation stay at fixed,
            // visible positions; editing/apply actions are separated into logical groups.
            commandStrip.Items.Add(pasteButton);
            commandStrip.Items.Add(cutButton);
            commandStrip.Items.Add(copyButton);
            commandStrip.Items.Add(goToSourceButton);
            commandStrip.Items.Add(CreateSeparator());
            commandStrip.Items.Add(editButton);
            commandStrip.Items.Add(addRowButton);
            commandStrip.Items.Add(deleteRowButton);
            commandStrip.Items.Add(transformButton);
            commandStrip.Items.Add(CreateSeparator());
            commandStrip.Items.Add(applyButton);
            commandStrip.Items.Add(revertButton);

            if (dirtyLabel is not null)
            {
                dirtyLabel.Alignment = ToolStripItemAlignment.Right;
                commandStrip.Items.Add(dirtyLabel);
            }

            // Interpretation/search row. These controls affect the current visual view,
            // not the pending edit transaction, so they live together on the second row.
            viewStrip.Items.Add(refreshButton);
            viewStrip.Items.Add(CreateSeparator());
            viewStrip.Items.Add(delimiterLabel);
            viewStrip.Items.Add(delimiterCombo);
            viewStrip.Items.Add(CreateSeparator());
            viewStrip.Items.Add(headerLabel);
            viewStrip.Items.Add(headerCombo);
            viewStrip.Items.Add(CreateSeparator());
            viewStrip.Items.Add(clearButton);
            viewStrip.Items.Add(CreateSeparator());
            viewStrip.Items.Add(diagnosticsButton);

            CompactItems(commandStrip);
            CompactItems(viewStrip);

            if (dirtyLabel is not null)
            {
                dirtyLabel.Margin = new Padding(10, 1, 4, 1);
            }
        }
        finally
        {
            viewStrip.ResumeLayout(performLayout: true);
            commandStrip.ResumeLayout(performLayout: true);
        }

        form.InstallCommandSurface();

        updateAvailability = () =>
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
            transformButton.Enabled = hasTarget && form.IsEditMode;
            goToSourceButton.Enabled =
                hasTarget && CsvGridSourceNavigationController.CanNavigate(grid);
        };

        return true;
    }

    private static void WireAvailabilityRefresh(
        CsvGridForm form,
        DataGridView grid,
        Action updateAvailability)
    {
        grid.SelectionChanged += (_, _) => updateAvailability();
        grid.CurrentCellChanged += (_, _) => updateAvailability();
        grid.ReadOnlyChanged += (_, _) => updateAvailability();
        grid.RowsAdded += (_, _) => updateAvailability();
        grid.RowsRemoved += (_, _) => updateAvailability();
        grid.ColumnAdded += (_, eventArgs) =>
        {
            ApplyColumnPresentation(eventArgs.Column);
            updateAvailability();
        };
        grid.ColumnRemoved += (_, _) => updateAvailability();
        form.VisibleChanged += (_, _) => updateAvailability();
    }

    private static void ApplySpreadsheetPresentation(DataGridView grid)
    {
        // Do not change SelectionMode here. CsvGridRowPresentation intentionally uses
        // RowHeaderSelect so native row-header gestures and ordinary cell selection can
        // coexist. UI polish must never override that accepted interaction contract.
        grid.RowTemplate.Height = Math.Max(grid.RowTemplate.Height, 24);
        grid.DefaultCellStyle.Padding = new Padding(4, 1, 4, 1);
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(5, 2, 5, 2);
        grid.RowHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ShowCellToolTips = true;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;

        foreach (DataGridViewColumn column in grid.Columns)
        {
            ApplyColumnPresentation(column);
        }
    }

    private static void ApplyColumnPresentation(DataGridViewColumn column)
    {
        if (!CsvGridRowHeaderBehavior.IsPresentationColumn(column))
        {
            column.ToolTipText = column.HeaderText ?? string.Empty;
        }
    }

    private static void ConfigureStrip(ToolStrip strip, int verticalPadding)
    {
        strip.GripStyle = ToolStripGripStyle.Hidden;
        strip.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
        strip.CanOverflow = true;
        strip.ShowItemToolTips = true;
        strip.Padding = new Padding(4, verticalPadding, 4, verticalPadding);
        strip.RenderMode = ToolStripRenderMode.System;
    }

    private static void CompactItems(ToolStrip strip)
    {
        foreach (ToolStripItem item in strip.Items)
        {
            item.Margin = item switch
            {
                ToolStripSeparator => new Padding(4, 1, 4, 1),
                ToolStripLabel => new Padding(2, 1, 1, 1),
                _ => new Padding(1, 1, 1, 1)
            };

            if (item is ToolStripButton button)
            {
                button.Padding = new Padding(5, 0, 5, 0);
            }
        }
    }

    private static ToolStripSeparator CreateSeparator() =>
        new()
        {
            AutoSize = true
        };

    private static ToolStripButton CreateButton(
        string name,
        string text,
        string toolTipText) =>
        new(text)
        {
            Name = name,
            AutoSize = true,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ToolTipText = toolTipText,
            Overflow = ToolStripItemOverflow.AsNeeded
        };

    private static ToolStripButton? FindButton(
        ToolStrip strip,
        Func<string, bool> predicate) =>
        strip.Items
            .OfType<ToolStripButton>()
            .FirstOrDefault(button => predicate(button.Text ?? string.Empty));

    private static ToolStripLabel? FindLabel(ToolStrip strip, string text) =>
        strip.Items
            .OfType<ToolStripLabel>()
            .FirstOrDefault(label => string.Equals(label.Text, text, StringComparison.Ordinal));

    private static DataGridView? FindTableGrid(Control root) =>
        EnumerateControls(root)
            .OfType<DataGridView>()
            .FirstOrDefault(CsvDataGridView.IsPrimaryTableGridCandidate);

    private static ToolStrip? FindCommandStrip(Control root) =>
        EnumerateControls(root).OfType<ToolStrip>().FirstOrDefault(static strip => strip.Name == "CsvCommandStrip");

    private static ToolStrip? FindViewStrip(Control root, ToolStrip? commandStrip) =>
        EnumerateControls(root).OfType<ToolStrip>().FirstOrDefault(static strip => strip.Name == "CsvInterpretationStrip");

    private static ToolStripButton? FindNamedButton(ToolStrip strip, string name) =>
        strip.Items.OfType<ToolStripButton>().FirstOrDefault(item => item.Name == name);

    private static bool HasCommandButtons(Control root)
    {
        var names = EnumerateControls(root)
            .OfType<ToolStrip>()
            .SelectMany(static strip => strip.Items.Cast<ToolStripItem>())
            .Select(static item => item.Name)
            .ToHashSet(StringComparer.Ordinal);

        return names.Contains(CopyButtonName) &&
               names.Contains(CutButtonName) &&
               names.Contains(PasteButtonName) &&
               names.Contains(GoToSourceButtonName) &&
               names.Contains(TransformButtonName);
    }

    private static void RemoveExistingCommandButtons(Control root)
    {
        foreach (var strip in EnumerateControls(root).OfType<ToolStrip>())
        {
            for (var index = strip.Items.Count - 1; index >= 0; index--)
            {
                var item = strip.Items[index];
                if (item.Name is CopyButtonName or
                    CutButtonName or
                    PasteButtonName or
                    GoToSourceButtonName or
                    TransformButtonName)
                {
                    strip.Items.RemoveAt(index);
                    item.Dispose();
                }
            }
        }
    }

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

    private static void ResetAndQueueAttach(CsvGridForm form, AttachmentState state)
    {
        state.DeferredAttachAttempts = 0;
        QueueAttach(form, state);
    }

    private static void QueueAttach(CsvGridForm form, AttachmentState state)
    {
        if (state.Attached ||
            state.AttachQueued ||
            state.DeferredAttachAttempts >= MaximumDeferredAttachAttempts ||
            form.IsDisposed ||
            form.Disposing ||
            !form.IsHandleCreated)
        {
            return;
        }

        state.AttachQueued = true;
        try
        {
            form.BeginInvoke((Action)(() =>
            {
                state.AttachQueued = false;
                state.DeferredAttachAttempts++;

                if (TryAttachNow(form, state))
                {
                    return;
                }

                if (form.Visible &&
                    state.DeferredAttachAttempts < MaximumDeferredAttachAttempts)
                {
                    QueueAttach(form, state);
                }
            }));
        }
        catch (InvalidOperationException)
        {
            state.AttachQueued = false;
        }
    }

    private sealed class AttachmentState
    {
        internal bool Attached { get; set; }

        internal bool AttachQueued { get; set; }

        internal int DeferredAttachAttempts { get; set; }

        internal bool EventsAttached { get; set; }
    }
}
