namespace CsvVisualEditor;

using CsvVisualEditor.Localization;
using Npp.DotNet.Plugin;

partial class Main
{
    private bool _languageReady;

    private void InitializeInterfaceLanguage()
    {
        if (_languageReady) return;
        _languageReady = true;
        L10n.InitializeFromNativeLanguage(NotepadUiLanguage.ReadFilename(PluginData.NppData.NppHandle));
        LocalizeNativeCommands();
    }

    private static void LocalizeNativeCommands()
    {
        // Command IDs belong to Notepad++; refresh them only after initialization.
        PluginData.FuncItems.RefreshItems();
        TextKey?[] keys = [TextKey.Native_OpenTable, TextKey.Native_RefreshTable,
            TextKey.Native_FilterSort, TextKey.Native_ColumnSummary, null, TextKey.Common_About, TextKey.Cell_Title];
        for (var index = 0; index < keys.Length && index < PluginData.FuncItems.Items.Count; index++)
        {
            if (keys[index] is not TextKey key) continue;
            var item = PluginData.FuncItems.Items[index];
            var caption = L10n.Get(key);
            Npp.DotNet.Plugin.I18n.Menu.SetMenuItemText(item.CmdID, caption);
            item.ItemName = caption;
            PluginData.FuncItems.Items[index] = item;
        }
    }
}
