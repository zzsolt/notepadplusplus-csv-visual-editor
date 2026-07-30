# Milestone 0.11 host discrepancy — Excel matrix paste

## Observed result

Target host: Notepad++ 8.9.7 x64.

The owner confirmed that copying a rectangular range from the plugin into Excel worked. Copying a 2 × 2 range from Excel back into the plugin did not use the rectangular paste path while the grid had no active cell editor. After clicking into one CSV cell, Ctrl+V worked only as a native text-editor paste and inserted the complete tab/newline payload into that single cell.

## Root cause

The original application-level clipboard filter deliberately returned control to the native DataGridView editing control whenever `DataGridView.IsCurrentCellInEditMode` was true. That rule was correct for ordinary single-cell text, but it also delegated Excel's Unicode tab/newline matrix to the one-cell editor. The payload therefore bypassed `CsvClipboardMatrix`, stable cell addressing, bounds validation, and `CsvClipboardPastePlan`.

## Correct routing rule

- Ctrl+C in an active cell editor remains native text copy.
- Ctrl+V with ordinary single-cell text in an active cell editor remains native text insertion.
- Ctrl+V containing a tab, CR, or LF is spreadsheet data and must be promoted to the grid paste route even when the editing control owns focus.
- The active cell edit is committed first, then the existing immutable matrix and atomic paste-plan path is used.
- Invalid shape, overflow, row-selection context, or model change must still produce zero partial paste.

## Required retest

1. Copy a simple 2 × 2 range in Excel.
2. Open a CSV in the plugin and enter Edit mode.
3. Single-click the intended top-left CSV data cell. It is acceptable for the in-cell editor to become active.
4. Press Ctrl+V once.
5. Verify that the four Excel values populate a 2 × 2 rectangle from the selected cell.
6. Verify that no literal tab or line break remains inside one CSV cell.
7. Run Revert All and confirm that all four pending values are discarded without changing the Notepad++ buffer.
8. Enter one cell editor, copy one ordinary word, and press Ctrl+V. Verify that the word is still inserted at the caret inside that cell.
9. Repeat the 2 × 2 paste into an exact selected 2 × 2 destination.
10. Repeat with Windows-1250-representable Hungarian text, then Apply, Save, close, and reopen.
11. Verify one-step Apply undo/redo and the accepted complete-row selection/Delete/Revert behavior.

The milestone remains draft and unmerged until this retest passes in the real host.
