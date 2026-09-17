namespace CsvVisualEditor.NativeAot.SmokeTests;

using CsvVisualEditor.Core;
using CsvVisualEditor.Localization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

internal static class CellContextMenuNativeAotSmoke
{
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

    [ModuleInitializer]
    internal static void Run()
    {
        var cases = 0;
        try
        {
            foreach (var language in new[] { "en", "hu", "zh-CN", "hi", "es", "ar", "fr" })
            foreach (var dark in new[] { false, true })
            {
                L10n.SetLanguage(language);
                using var form = new Form { Width = 850, Height = 480 };
                using var strip = new ToolStrip { Dock = DockStyle.Top };
                using var grid = new CsvDataGridView
                {
                    Dock = DockStyle.Fill, AllowUserToAddRows = false, MultiSelect = true,
                    ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText
                };
                form.Controls.Add(grid);
                form.Controls.Add(strip);
                grid.Columns.Add("CsvColumn0", "Value");
                grid.Columns.Add("CsvColumn1", "Note");
                grid.Rows.Add("one", "first"); grid.Rows.Add("two", "second"); grid.Rows.Add("three", "third");
                var ids = RowIds();
                for (var i = 0; i < 3; i++) grid.Rows[i].Tag = ids[i];
                Require(CsvGridRowHeaderBehavior.TryAttach(form), "Attach real stable row-selection behavior.");
                CsvGridRowHeaderBehavior.SynchronizeTablePresentation(grid);
                var names = new (string Name, TextKey Key)[]
                {
                    ("CsvCellDetailsButton", TextKey.Cell_Title), ("CsvGoToSourceButton", TextKey.Commands_Source),
                    ("CsvClipboardCopyButton", TextKey.Common_Copy), ("CsvClipboardCutButton", TextKey.Common_Cut),
                    ("CsvClipboardPasteButton", TextKey.Common_Paste), ("CsvTransformButton", TextKey.Commands_Transform),
                    ("CsvAddRowButton", TextKey.Table_AddRow), ("CsvDeleteRowButton", TextKey.Table_DeleteRow),
                    ("CsvApplyButton", TextKey.Common_Apply), ("CsvRevertAllButton", TextKey.Table_RevertAll),
                    ("CsvColumnSummaryButton", TextKey.Summary_Title), ("CsvDataViewButton", TextKey.Filter_Title),
                    ("CsvResetViewButton", TextKey.View_Reset), ("CsvRefreshButton", TextKey.Common_Refresh),
                    ("CsvShowSpacesButton", TextKey.View_ShowSpaces)
                };
                foreach (var (name, key) in names) strip.Items.Add(new ToolStripButton(L10n.Get(key)) { Name = name });
                ToolStripButton Button(string name) => strip.Items.OfType<ToolStripButton>().Single(b => b.Name == name);
                var clicks = 0;
                Button("CsvClipboardCopyButton").Click += (_, _) => clicks++;
                Button("CsvDeleteRowButton").Enabled = false;
                Button("CsvApplyButton").Enabled = false;
                Button("CsvRevertAllButton").Enabled = false;
                Button("CsvShowSpacesButton").Checked = true;
                using var surface = new CsvCommandSurface(strip);
                var background = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
                var foreground = dark ? Color.Gainsboro : Color.Black;
                surface.ApplyAppearance(background, foreground, 96);
                var editing = false;
                object identity = new();
                var commitAllowed = true;
                using var context = new CsvGridContextMenu(grid, () => commitAllowed,
                    () => editing, () => identity, name => Button(name));
                context.ApplyAppearance(background, foreground, surface.Menu.Renderer);
                form.Show(); Application.DoEvents();
                grid.ReadOnly = true;
                Require(context.PrepareCell(1, 1), "Prepare read-only data cell.");
                Require(grid.CurrentCell == grid.Rows[1].Cells[1] && grid.SelectedCells.Count == 1,
                    "Unselected right-click target must become the only selected data cell.");
                Require(Item(context, "CsvClipboardCopyButton").Enabled && !Has(context, "CsvClipboardCutButton") &&
                    !Has(context, "CsvApplyButton") && Has(context, "CsvColumnSummaryButton"),
                    "Read-only context must expose inspection/copy/view tools, not mutations.");
                Item(context, "CsvClipboardCopyButton").PerformClick();
                Require(clicks == 1, "Context menu dispatches the existing command exactly once.");
                foreach (var item in context.Menu.Items.OfType<ToolStripMenuItem>())
                    Require(item.Text == Button(item.Name).Text, "Every caption comes from the localized command source.");
                Require(context.Menu.RightToLeft == (L10n.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No) &&
                    grid.RightToLeft != RightToLeft.Yes, "RTL affects menus, never CSV columns.");
                Require(Item(context, "CsvShowSpacesButton").Checked, "Toggle state mirrors the source.");
                Capture(context, grid, language, dark, "read");

                // A rectangle stays a rectangle when another cell inside it is right-clicked.
                grid.ClearSelection();
                for (var row = 0; row < 2; row++) for (var col = 0; col < 2; col++) grid.Rows[row].Cells[col].Selected = true;
                Require(context.PrepareCell(0, 0) && grid.SelectedCells.Count == 4 &&
                    Item(context, "CsvClipboardCopyButton").Enabled, "Preserve existing rectangular selection.");
                grid.Rows[2].Cells[1].Selected = true;
                Require(context.PrepareCell(0, 0) && !Item(context, "CsvClipboardCopyButton").Enabled,
                    "Discontinuous selection must not pretend to be a clipboard rectangle.");
                Require(context.PrepareCell(2, 0) && grid.SelectedCells.Count == 1,
                    "Clicking outside a selection selects the new cell only.");
                var old = Item(context, "CsvClipboardCopyButton");
                identity = new object(); old.PerformClick();
                Require(clicks == 1, "A replaced view/model invalidates old commands.");
                context.PrepareCell(2, 0); old = Item(context, "CsvClipboardCopyButton");
                Button("CsvClipboardCopyButton").Enabled = false; old.PerformClick();
                Require(clicks == 1, "Recheck command availability at dispatch time.");
                Button("CsvClipboardCopyButton").Enabled = true;
                context.PrepareCell(2, 0); old = Item(context, "CsvClipboardCopyButton");
                grid.Rows[2].Cells[0].Value = "changed"; old.PerformClick();
                Require(clicks == 1, "Changing cell data invalidates a queued menu action.");
                var before = grid.CurrentCell;
                commitAllowed = false;
                Require(!context.PrepareCell(0, 1) && grid.CurrentCell == before, "Failed editor commit must not retarget.");
                commitAllowed = true;
                Require(!context.PrepareCell(-1, 0) && !context.PrepareCell(0, -1) &&
                    !context.PrepareCell(0, grid.Columns[CsvGridRowPresentation.RowIndicatorColumnName]!.Index),
                    "Headers and presentation columns are not data-cell contexts.");

                editing = true; grid.ReadOnly = false;
                Require(context.PrepareCell(0, 0), "Prepare Edit-mode context.");
                Require(Has(context, "CsvClipboardCutButton") && Has(context, "CsvClipboardPasteButton") &&
                    Has(context, "CsvTransformButton") && !Has(context, "CsvColumnSummaryButton") &&
                    !Item(context, "CsvDeleteRowButton").Enabled && !Item(context, "CsvApplyButton").Enabled,
                    "Edit menu respects pending/whole-row availability and omits read-only view tools.");
                Capture(context, grid, language, dark, "edit");
                Item(context, "CsvContextSelectRow").PerformClick();
                Require(CsvGridSelectionSnapshot.Capture(grid).Count == 1, "Explicit row-selection action uses stable row authority.");
                CsvGridRowHeaderBehavior.ToggleSelectorForTesting(grid, 1);
                Require(context.PrepareCell(1, 1) && CsvGridSelectionSnapshot.Capture(grid).Count == 2 &&
                    !Item(context, "CsvClipboardCopyButton").Enabled && !Item(context, "CsvClipboardPasteButton").Enabled,
                    "Right-click within selected whole rows retains row targets and rejects cell clipboard actions.");
                Require(context.PrepareCell(2, 0) && CsvGridSelectionSnapshot.Capture(grid).Count == 0,
                    "Right-click outside complete rows clears old deletion targets.");
                // Native message routing, including keyboard invocation, not only helper calls.
                SendMessage(grid.Handle, 0x007B, grid.Handle, new IntPtr(-1)); Application.DoEvents();
                Require(context.Menu.Visible, "Keyboard WM_CONTEXTMENU opens at the current cell.");
                context.Menu.Close();
                var point = grid.PointToScreen(new Point(5, grid.Height - 8));
                Require(!context.ShowAt(point), "Empty grid area must not open a stale cell menu.");
                context.Dispose();
                using var reinstalled = new CsvGridContextMenu(grid, () => true, () => editing, () => identity, name => Button(name));
                SendMessage(grid.Handle, 0x007B, grid.Handle, new IntPtr(-1)); Application.DoEvents();
                Require(reinstalled.Menu.Visible, "Reinstallation leaves one working native context handler.");
                reinstalled.Menu.Close();
                cases++;
            }
        }
        finally { L10n.SetLanguage("en"); }
        Console.WriteLine($"Cell context menus: {cases} language/theme cases; read/edit, native keyboard, selection, stale guards and lifecycle PASS.");
    }

    private static CsvEditRowId[] RowIds()
    {
        const string source = "Value,Note\none,first\ntwo,second\nthree,third";
        var snapshot = ActiveDocumentSnapshot.Create("context.csv", source, Encoding.UTF8.GetByteCount(source),
            65001, 0, 0, false, DateTimeOffset.UnixEpoch);
        var parse = CsvParser.Parse(source, CsvDialect.Create(',', headerMode: CsvHeaderMode.FirstRecord));
        var projection = CsvTableProjector.Create(parse, new CsvTableProjectionOptions { HeaderMode = CsvHeaderMode.FirstRecord });
        var edit = CsvEditSession.Create(snapshot, parse, projection);
        return CsvRowEditModel.Create(snapshot, parse, edit, projection).GetVisibleRows().Select(row => row.Id).ToArray();
    }
    private static ToolStripMenuItem Item(CsvGridContextMenu menu, string name) =>
        menu.Menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Name == name);
    private static bool Has(CsvGridContextMenu menu, string name) => menu.Menu.Items.OfType<ToolStripMenuItem>().Any(item => item.Name == name);
    private static void Capture(CsvGridContextMenu menu, DataGridView grid, string language, bool dark, string mode)
    {
        var point = grid.PointToScreen(new Point(120, 60));
        menu.Menu.Show(point); Application.DoEvents();
        Directory.CreateDirectory("artifacts/ui-review");
        using var image = new Bitmap(menu.Menu.Width, menu.Menu.Height);
        menu.Menu.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        image.Save($"artifacts/ui-review/context-menu-{language}-{(dark ? "dark" : "light")}-{mode}.png");
        menu.Menu.Close();
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
