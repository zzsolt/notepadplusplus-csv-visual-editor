namespace CsvVisualEditor;

using System.Diagnostics;
using System.Reflection;

internal sealed class CsvAboutDialog : Form
{
    internal const string DeveloperName = "Zolnai Zsolt";
    internal const string ContactEmail = "zzsolt@gmail.com";
    internal const string ProjectUrl = "https://github.com/zzsolt/notepadplusplus-csv-visual-editor";
    internal const string SupportText = "Enjoying CSV Visual Editor? Please support the project.\n" +
        "Star it on GitHub, share your feedback, or contact the developer about contributing. Thank you!";
    internal static string DisplayVersion => typeof(CsvAboutDialog).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Development build";
    private readonly Font _titleFont;
    private readonly Label _message = new() { AutoSize = true, MaximumSize = new Size(460, 0), UseMnemonic = false };

    internal CsvAboutDialog(Color background, Color foreground)
    {
        Text = "About CSV Visual Editor";
        Name = "CsvAboutDialog";
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = background;
        ForeColor = foreground;
        _titleFont = new Font(SystemFonts.MessageBoxFont!.FontFamily, 18, FontStyle.Bold);
        var layout = new TableLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1,
            Dock = DockStyle.Fill, Padding = new Padding(24), MinimumSize = new Size(510, 0)
        };
        void Add(Control control)
        {
            control.Margin = new Padding(0, 0, 0, 12);
            control.ForeColor = foreground;
            control.BackColor = background;
            layout.Controls.Add(control);
        }
        Add(new Label { Text = "CSV Visual Editor", Font = _titleFont, AutoSize = true });
        Add(new Label { Text = "A visual CSV editor for Notepad++", AutoSize = true });
        Add(new TextBox
        {
            Text = "Version: " + DisplayVersion, ReadOnly = true, BorderStyle = BorderStyle.None,
            Width = 460, AccessibleName = "Version and build", TabStop = true
        });
        Add(new Label { Text = "Developer: " + DeveloperName, AutoSize = true });
        var contact = Link("Contact: " + ContactEmail, "mailto:" + ContactEmail, background);
        contact.AccessibleName = "Contact developer " + ContactEmail;
        Add(contact);
        Add(new Label { Text = SupportText, AutoSize = true, MaximumSize = new Size(460, 0), UseMnemonic = false });
        Add(Link("Project on GitHub", ProjectUrl, background));
        Add(_message);
        var close = new Button
        {
            Text = "Close", AutoSize = true, DialogResult = DialogResult.OK, Padding = new Padding(14, 4, 14, 4),
            FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false, BackColor = background, ForeColor = foreground
        };
        close.FlatAppearance.BorderColor = CsvSearchBar.Blend(background, foreground, 35);
        close.FlatAppearance.MouseOverBackColor = CsvSearchBar.Blend(background, foreground, 10);
        Shown += (_, _) => close.Select();
        var footer = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        footer.Controls.Add(close);
        Add(footer);
        Controls.Add(layout);
        AcceptButton = CancelButton = close;
    }

    private LinkLabel Link(string caption, string target, Color background)
    {
        var link = new LinkLabel
        {
            Text = caption, AutoSize = true, UseMnemonic = false,
            LinkColor = background.GetBrightness() < 0.5f ? Color.LightSkyBlue : Color.FromArgb(0, 90, 175),
            LinkBehavior = LinkBehavior.HoverUnderline, AccessibleDescription = target
        };
        link.LinkClicked += (_, _) =>
        {
            // Only these compile-time targets can be launched, and only on an explicit click.
            if (target != ProjectUrl && target != "mailto:" + ContactEmail) return;
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                _message.Text = "No associated application could be opened. Contact: " + ContactEmail;
            }
        };
        return link;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _titleFont?.Dispose();
    }
}
