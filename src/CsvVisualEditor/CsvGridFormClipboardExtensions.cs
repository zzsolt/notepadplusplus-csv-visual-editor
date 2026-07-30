namespace CsvVisualEditor;

/// <summary>
/// Completes the clipboard controller's form integration without exposing the form's
/// private rendering implementation. CsvGridClipboardController writes the pasted
/// values back through DataGridView cells after the stable edit-model transaction.
/// Those assignments synchronously raise CsvGridForm.OnGridCellValueChanged, which
/// performs the canonical dirty-indicator, row-label, command-state, and status refresh.
/// This explicit boundary documents that behavior and keeps the controller independent
/// from private form controls.
/// </summary>
internal static class CsvGridFormClipboardExtensions
{
    internal static void RefreshClipboardEditState(this CsvGridForm form)
    {
        ArgumentNullException.ThrowIfNull(form);

        // SynchronizeGridValues has already assigned every changed cell. DataGridView
        // raises CellValueChanged synchronously for those assignments, and CsvGridForm
        // refreshes all canonical edit state from that event. No second presentation
        // mutation is required here. Keeping this method explicit avoids reflection or
        // duplicated private UI logic and provides a stable controller/form seam.
    }
}
