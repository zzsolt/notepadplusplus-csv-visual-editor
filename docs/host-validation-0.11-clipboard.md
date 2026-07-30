# Notepad++ host validation — 0.11 multi-cell clipboard and large-grid performance

Target host: Notepad++ 8.9.7 x64. Keep public and internal PR #11 draft and unmerged until this matrix is accepted.

## Installation

Install the x64 package as:

```text
Notepad++/plugins/CsvVisualEditor/CsvVisualEditor.dll
```

Use a UTF-8 CSV and a Windows-1250 CSV containing at least four data rows and three columns.

## A. Plugin-to-spreadsheet copy

1. In read-only mode, select one data cell and press Ctrl+C. Paste into Notepad++ and verify only the cell value is copied.
2. Select a contiguous 2 × 2 rectangle of CSV data cells.
3. Press Ctrl+C and paste into Microsoft Excel and LibreOffice Calc.
4. Confirm four values occupy four cells in the same shape.
5. Confirm `#` and `Select` are never included.
6. Try a non-rectangular Ctrl selection. The plugin must reject it without modifying the document.
7. Enter in-cell editing and verify Ctrl+C still copies selected text inside the editor.

## B. Excel-to-plugin paste from one target cell

1. Enter Edit mode.
2. Copy a 2 × 2 Excel range.
3. Single-click the intended top-left CSV data cell.
4. Press Ctrl+V once.
5. Confirm four values populate four distinct CSV cells.
6. Confirm no literal tab or multiline Excel payload appears in one cell.

## C. Exact selected rectangle and broadcast

1. Select a contiguous 2 × 2 destination in the plugin.
2. Paste a 2 × 2 spreadsheet range and confirm exact positional replacement.
3. Select a 2 × 2 target and paste one clipboard cell. The value must broadcast to all four cells.
4. Add a new row, then paste into it. The inserted row must retain its `new:n *` identity.

## D. Native in-cell text paste

1. Enter the text editor of one CSV cell.
2. Copy one ordinary word without tab or line break.
3. Paste at the caret.
4. Confirm normal text-level insertion remains available.

## E. Rejection and atomicity

- paste a differently shaped multi-cell rectangle;
- paste beyond the final row or column;
- paste malformed ragged tabular text;
- verify zero partial changes;
- verify complete-row deletion selection blocks cell paste;
- verify read-only mode blocks paste and leaves the buffer unchanged.

## F. Revert, Apply, and undo

1. Confirm successful paste updates dirty indicators and row labels.
2. Revert All before Apply must discard all pasted values and leave the Notepad++ buffer untouched.
3. Apply must write the complete result to the active Notepad++ buffer.
4. One Ctrl+Z must restore the exact original buffer.
5. One Ctrl+Y must restore the applied buffer.
6. Notepad++ remains responsible for Save.

## G. Encoding and conflict regression

Repeat representable clipboard edits in:

- UTF-8;
- Windows-1250 with Hungarian characters.

Non-representable replacement text must remain blocked before host write. Changing the active document, code page, or buffer after entering Edit mode must still block Apply.

## H. Existing row-operation regression

Recheck:

- checkbox, native row header, and `#` complete-row selection;
- plain, Ctrl, Shift, and Ctrl+Shift gestures;
- Delete Rows;
- inserted-row cancellation;
- exact Revert All;
- ordinary CSV-cell click clears deletion context.

## I. Large CSV load responsiveness

Use a valid CSV containing many thousands of rows.

1. Open or refresh the plugin.
2. Confirm a loading state appears immediately.
3. Confirm the Notepad++ window remains responsive while parsing/projection runs.
4. Record approximate rows, columns, file size, and elapsed time until the table is usable.
5. Scroll near the first, middle, and last displayed rows.
6. Verify row labels and cell values remain aligned.
7. Test search/filter and copy a small rectangle.
8. Enter Edit mode and verify the explicit lazy edit-session preparation completes.
9. Recheck a cell edit, Add Row, Delete Rows, Revert All, and Apply.

## Acceptance record

Record PASS/FAIL for each section, package hash, Notepad++ version, Windows scale/theme, UTF-8 result, Windows-1250 result, and the measured large-file elapsed time in PR #11. Do not merge PR #11 until the owner confirms the corrected package in the real host.
