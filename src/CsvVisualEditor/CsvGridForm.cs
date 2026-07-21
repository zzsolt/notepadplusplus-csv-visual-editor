namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using Npp.DotNet.Plugin;
using Npp.DotNet.Plugin.Winforms;
using Npp.DotNet.Plugin.Winforms.Classes;

internal sealed class CsvGridForm : DockingForm
{
    private const string FormTitle = "CSV Visual Editor";
    private static readonly NppTbMsg InitialDockPosition = NppTbMsg.DWS_DF_CONT_RIGHT;

    private readonly ToolStrip _toolStrip;
    private readonly ToolStripButton _refreshButton;
    private readonly DataGridView _grid;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _statusLabel;

    public CsvGridForm(int dialogId, string pluginModuleName, Icon formIcon)
        : base(dialogId, pluginModuleName, FormTitle, null, formIcon, InitialDockPosition)
    {
        _refreshButton = new ToolStripButton("Refresh")
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ToolTipText = "Refresh the visual table from the active Notepad++ document"
        };
        _refreshButton.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);

        _toolStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };
        _toolStrip.Items.Add(_refreshButton);

        _grid = new DataGridView
        {
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToOrderColumns = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            Dock = DockStyle.Fill,
            EditMode = DataGridViewEditMode.EditProgrammatically,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        _statusLabel = new ToolStripStatusLabel
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _statusStrip = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            SizingGrip = false
        };
        _statusStrip.Items.Add(_statusLabel);

        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(900, 520);
        Controls.Add(_grid);
        Controls.Add(_statusStrip);
        Controls.Add(_toolStrip);
        MinimumSize = new Size(420, 240);
        Text = FormTitle;
        ResumeLayout(performLayout: true);

        AttachEventHandlers();
        ShowBootstrapState();
        ToggleDarkMode(PluginData.Notepad.IsDarkModeEnabled());
    }

    public event EventHandler? RefreshRequested;

    public void ShowBootstrapState()
    {
        _grid.Rows.Clear();
        _grid.Columns.Clear();

        foreach (var columnName in TableColumnNameGenerator.Create(3))
        {
            _grid.Columns.Add(columnName.Replace(' ', '_'), columnName);
        }

        _statusLabel.Text =
            "Plugin shell ready. Active-document CSV loading is the next implementation phase.";
    }

    public override void ToggleDarkMode(bool isDark)
    {
        if (isDark)
        {
            var theme = new DarkMode.DarkModeColors();
            BackColor = theme.SofterBackground;
            ForeColor = theme.Text;
            _toolStrip.BackColor = theme.SofterBackground;
            _toolStrip.ForeColor = theme.Text;
            _statusStrip.BackColor = theme.SofterBackground;
            _statusStrip.ForeColor = theme.Text;
            _grid.BackgroundColor = theme.SofterBackground;
            _grid.DefaultCellStyle.BackColor = theme.SofterBackground;
            _grid.DefaultCellStyle.ForeColor = theme.Text;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = theme.SofterBackground;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = theme.Text;
            _grid.EnableHeadersVisualStyles = false;
        }
        else
        {
            BackColor = SystemColors.Control;
            ForeColor = SystemColors.ControlText;
            _toolStrip.BackColor = SystemColors.Control;
            _toolStrip.ForeColor = SystemColors.ControlText;
            _statusStrip.BackColor = SystemColors.Control;
            _statusStrip.ForeColor = SystemColors.ControlText;
            _grid.BackgroundColor = SystemColors.AppWorkspace;
            _grid.DefaultCellStyle.BackColor = SystemColors.Window;
            _grid.DefaultCellStyle.ForeColor = SystemColors.WindowText;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
            _grid.EnableHeadersVisualStyles = true;
        }

        Invalidate(invalidateChildren: true);
    }

    protected override void AttachEventHandlers()
    {
        base.AttachEventHandlers();
        _grid.Focus();
    }
}
