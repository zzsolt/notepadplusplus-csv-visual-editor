namespace CsvVisualEditor;

/// <summary>Restore owned view commands after the clipboard toolbar is rebuilt.</summary>
internal static class CsvDataToolCommands
{
    internal static void Attach(ToolStrip strip, ToolStripButton filters,
        ToolStripButton summary, ToolStripSeparator separator)
    {
        if (!strip.Items.Contains(separator)) strip.Items.Add(separator);
        if (!strip.Items.Contains(filters)) strip.Items.Add(filters);
        if (!strip.Items.Contains(summary)) strip.Items.Add(summary);
    }
}
