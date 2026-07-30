# Notepad++ host validation — milestone 0.11 clipboard

Target host: Notepad++ 8.9.7 x64. Keep public and internal PR #11 draft and unmerged until this matrix is accepted.

## Installation

Install the x64 package as:

```text
Notepad++/plugins/CsvVisualEditor/CsvVisualEditor.dll
```

Use a UTF-8 CSV and a Windows-1250 CSV containing at least four data rows and three columns.

## Copy

1. In read-only mode, select one data cell and press Ctrl+C. Paste into Notepad++ and verify only the cell value is copied.
2. Select a 2 × 2 contiguous cell rectangle with mouse drag or Shift navigation and press Ctrl+C.
3. Paste into Notepad++, Excel, and LibreOffice. Verify tab-separated columns and row-separated lines, without CSV headers.
4. Verify the presentation-only `#` and `Select` columns are never included.
5. Try a non-rectangular Ctrl selection. The plugin must reject it without modifying the document.
6. Enter in-cell editing and verify Ctrl+C still copies selected text inside the editor rather than the grid rectangle.

## Paste

1. Enter Edit mode and paste a 2 × 2 Excel/LibreOffice rectangle into one current CSV cell. It must expand down and right.
2. Select an exact 2 × 2 target and paste a 2 × 2 matrix. All four pending values must update.
3. Select a 2 × 2 target and paste one clipboard cell. The value must broadcast to all four cells.
4. Select a 2 × 2 target and paste a differently shaped multi-cell matrix. Paste must be blocked with zero partial changes.
5. Paste a matrix that would extend beyond the last row or column. Paste must be blocked with zero partial changes.
6. Add a new row, then paste into that inserted row. The inserted row must retain its `new:n *` identity.
7. While complete rows are selected through row header, `#`, or `Select`, press Ctrl+V. Paste must be blocked until a CSV-cell target is selected.
8. In read-only mode press Ctrl+V. Paste must be blocked and the buffer must remain unchanged.
9. While editing text inside one cell, Ctrl+V must preserve standard text insertion behavior.

## Apply, encoding, and undo

1. After a successful multi-cell paste, verify dirty indicators and row labels update.
2. Apply once. The full replacement must be one Notepad++ undo transaction.
3. Ctrl+Z once must restore the exact original buffer; Ctrl+Y once must restore the pasted result.
4. Revert All before Apply must discard every pasted cell and leave the editor buffer untouched.
5. Repeat a representable paste and Apply in Windows-1250. Save, close, and reopen; the encoding and characters must remain correct.
6. Paste a character not representable in Windows-1250. Apply must be blocked by strict full-replacement encoding preflight with no host write.
7. Change the active document, code page, or buffer after entering Edit mode. Existing conflict checks must still block Apply.

## Row-selection regression

Verify checkbox, native row header, `#`, plain click, Ctrl, Shift, Ctrl+Shift, Delete Rows, inserted-row cancellation, and exact Revert All still behave as accepted in milestone 0.9.

## Acceptance record

Record the package hash, Notepad++ version, Windows scale/theme, UTF-8 result, Windows-1250 result, copy/paste matrix results, undo/redo result, and any observed discrepancy in PR #11.
