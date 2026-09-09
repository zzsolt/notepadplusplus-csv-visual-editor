namespace CsvVisualEditor;

using CsvVisualEditor.Localization;

using System.Diagnostics;
using System.Reflection;

internal sealed class CsvAboutDialog : Form
{
    internal const string DeveloperName = "Zolnai Zsolt";
    internal const string ContactEmail = "zzsolt@gmail.com";
    internal const string ProjectUrl = "https://github.com/zzsolt/notepadplusplus-csv-visual-editor";
    internal static string SupportText => L10n.Get(TextKey.About_EnjoyingCSVVisualEditorPleaseSupportTheProject);
    internal static string DisplayVersion => typeof(CsvAboutDialog).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? L10n.Get(TextKey.About_DevelopmentBuild);
    private readonly Font _titleFont;
    private readonly Label _message = new() { AutoSize = true, MaximumSize = new Size(460, 0), UseMnemonic = false };

    internal CsvAboutDialog(Color background, Color foreground)
    {
        Text = L10n.Get(TextKey.About_AboutCSVVisualEditor);
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
        Add(new Label { Text = L10n.Get(TextKey.About_AVisualCSVEditorForNotepad), AutoSize = true });
        Add(new TextBox
        {
            Text = L10n.Format(TextKey.About_Version, DisplayVersion), ReadOnly = true, BorderStyle = BorderStyle.None,
            Width = 460, AccessibleName = L10n.Get(TextKey.About_VersionAndBuild), TabStop = true
        });
        Add(new Label { Text = L10n.Format(TextKey.About_InterfaceLanguage, L10n.LanguageName), AutoSize = true });
        Add(new Label { Text = L10n.Format(TextKey.About_Developer, DeveloperName), AutoSize = true });
        var contact = Link(L10n.Format(TextKey.About_Contact, ContactEmail), "mailto:" + ContactEmail, background);
        contact.AccessibleName = L10n.Format(TextKey.About_ContactDeveloper, ContactEmail);
        Add(contact);
        Add(new Label { Text = SupportText, AutoSize = true, MaximumSize = new Size(460, 0), UseMnemonic = false });
        Add(Link(L10n.Get(TextKey.About_ProjectOnGitHub), ProjectUrl, background));
        Add(_message);
        var close = new Button
        {
            Text = L10n.Get(TextKey.Common_Close), AutoSize = true, DialogResult = DialogResult.OK, Padding = new Padding(14, 4, 14, 4),
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
        CsvLocalizationAppearance.Apply(this);
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
                _message.Text = L10n.Format(TextKey.About_NoAssociatedApplicationCouldBeOpenedContact, ContactEmail);
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
