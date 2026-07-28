# CSV Visual Editor

CSV Visual Editor is a Notepad++ plugin for working with CSV files through a graphical, spreadsheet-like table.

> Development status: **0.9 multi-row deletion alpha under Notepad++ host validation**. Edit mode supports cell editing, Add Row, synchronized checkbox/row-header complete-row selection, and atomic batch deletion for UTF-8 editor buffers. Deterministic minimal-difference serialization, fresh-buffer conflict checks, and one Scintilla undo transaction protect Apply. Saving to disk remains a normal Notepad++ action.

> The native-glyph/row-number separation is covered by automated checks but remains pending owner visual acceptance in Notepad++ 8.9.7 x64.

## Current capabilities

- 64-bit Windows / Notepad++ target
- C# and .NET 10
- Windows Forms `DataGridView`
- dockable plugin panel with an automatically usable initial width
- light/dark mode response
- `Open Visual Table`, `Refresh Table`, and `About` commands
- direct active-buffer reading through Notepad++/Scintilla
- unsaved editor changes included in an immutable snapshot
- explicit 64 MiB snapshot safety limit with a visible error instead of silent truncation
- comma, semicolon, and tab delimiter candidates
- reliable automatic delimiter detection with explainable scores, confidence, and diagnostics
- manual delimiter override when automatic detection is weak, ambiguous, or unwanted
- explicit **First row is header** and **No header row** choices
- record-aware parsing for quoted delimiters, doubled quotes, and embedded CRLF/LF
- empty/trailing fields, blank records, Unicode, source character spans, and malformed-input diagnostics
- deterministic fallback names for headerless CSV data
- display-only normalization of empty, duplicate, whitespace, and multiline column headers
- content-aware full-width columns with readable minimum widths and horizontal-scroll fallback
- case-insensitive search across all columns or one selected column
- 250 ms search debounce
- stable, three-state view sorting: ascending, descending, source order
- dedicated **Table** and **Diagnostics** tabs
- structured diagnostics with severity, code, logical record, character offset, and message
- explicit Edit mode, off by default
- direct data-cell editing
- explicit **Add Row** and dynamic **Delete Row / Delete Rows (n)** controls
- stable source-row identities independent of visible grid position
- synthetic session-local identities for inserted rows
- changed-cell, changed-row, inserted-row, and deleted-row counts
- centered logical-record numbers in a dedicated frozen `#` row-indicator column
- `*` marker beside changed source rows and `new:n *` markers beside inserted rows in that content-sized column
- compact glyph-only native row headers auto-sized by WinForms for DPI/theme/current-row indication
- checkbox-style **Select** column shown only in Edit mode
- complete-row selection by checkbox, native row-header click, or `#` row-indicator click
- Ctrl+row-header non-adjacent selection and Shift+row-header contiguous range selection
- two-way synchronization: checked boxes visually select complete rows, and row-header selection checks the matching boxes
- ordinary data-cell clicks clear complete-row selection and disable Delete
- atomic batch deletion using immutable stable-row-ID snapshots
- mixed source-row deletion and inserted-row cancellation in one operation
- Revert All without modifying the editor buffer
- deterministic comma/semicolon/tab CSV serialization
- minimal-difference previews that preserve unchanged raw records and line separators
- deterministic insertion/deletion separator and terminal-newline handling
- fresh document, code-page, and content-hash conflict checks immediately before Apply
- Apply restricted to Scintilla code page 65001 (UTF-8) in the current writable alpha
- one whole-document Scintilla replacement inside one undo action
- normal Notepad++ modified-marker and Save ownership
- visible display limits: at most 10,000 rows, 512 columns, and 250,000 cells in the current alpha
- dependency-free bootstrap checks, xUnit.net v3 tests, and Native AOT runtime smoke tests
- Native AOT publishing, so the target machine should not require a separately installed .NET runtime

## Edit and Apply behavior

1. Open a trustworthy, fully displayed CSV table. Viewing is not limited to UTF-8.
2. Select **Edit**. Search and sorting are cleared and source order is restored.
3. Edit data cells directly in the grid.
4. Select **Add Row** to insert after the current row, or append when no row is current.
5. Select complete rows through synchronized presentation targets:
   - click checkbox cells in the **Select** column;
   - click the narrow native row header or the adjacent `#` row-indicator cell, using Ctrl for non-adjacent rows or Shift for a contiguous range.
6. Every explicitly selected row is highlighted as a complete row and has its **Select** checkbox checked.
7. Clicking an ordinary CSV data cell clears complete-row selection. **Delete Row** remains disabled for cell-only focus.
8. Select **Delete Row** or **Delete Rows (n)** to delete all explicitly selected stable rows as one pending operation.
9. Selected source rows are marked for deletion; selected inserted rows are removed by cancelling their insertions.
10. The frozen `#` column keeps aligned logical-record numbers; changed source rows add `*`, and inserted rows use `new:n *`.
11. Select **Revert All** to discard every cell, insertion, and deletion change without touching the editor.
12. Select **Apply** to re-read the active editor and perform a fresh conflict check.
13. Apply is permitted only when both the session baseline and current editor buffer use Scintilla code page 65001 (UTF-8).
14. A document, code-page, content, or unsupported-encoding condition blocks Apply with zero replacement calls.
15. A valid Apply replaces the active editor buffer once inside one Scintilla undo action.
16. Save through Notepad++ when ready.

While Edit mode is active, Refresh, delimiter, header, search, and sort transitions are locked. A dirty Edit mode cannot be exited implicitly; use Apply or Revert All.

## Row-operation behavior

- Grid display indexes are never treated as persistent source identities.
- Existing data rows retain their original parser record identity.
- Inserted rows receive unique negative session-local identities internally.
- The header is outside the deletable data-row model.
- A source row remains restorable after pending deletion.
- Deleting an inserted row cancels the insertion.
- Add/Delete first commits the active cell editor or refuses the operation.
- Delete has no current-cell fallback: at least one complete row must be explicitly selected.
- The complete batch target set is validated before the first model mutation.
- Duplicate selected identities are normalized.
- Selection enumeration order never influences serialized output.
- Invalid stable identities produce zero partial batch changes.
- The selector and row-indicator columns are presentation-only and are never serialized as CSV data.
- Checkbox, native-row-header, and `#`-cell gestures update one plugin-owned stable-ID selection model.
- DataGridView `SelectedRows` and `SelectedCells` are not authoritative model identity.
- Both presentation columns are physically appended after real CSV columns, so editable CSV column indexes remain unchanged; display order alone places `#` first and **Select** last.
- After deletion, focus moves deterministically to the smallest removed display position, clamped to the remaining grid.
- Unchanged source records retain their exact raw text.
- Dirty source records and inserted records use deterministic CSV serialization.
- Middle insertion/deletion preserves the left surviving source record's original separator when possible.
- The original terminal-newline state is preserved independently.
- Header-only CSV data can enter Edit mode and receive its first data row.

## Safety behavior

- Automatic detection is used only with `Medium` or `High` confidence.
- `Low`, ambiguous, or absent suggestions require a manual delimiter choice.
- Parser errors produce partial read-only results plus diagnostics; source text is not rewritten.
- Inconsistent record widths are padded only in the rectangular view; parser records remain unchanged.
- Search, filtering, and sorting operate only on immutable projected rows.
- Stable sorting retains source order for values that compare equally.
- Source logical-record numbers remain visible and horizontally aligned in the frozen `#` column after filtering, sorting, and editing.
- Edit mode clears view-only filtering/sorting before structural operations begin.
- An edit session is rejected for parser errors, row-limited projections, omitted records/columns, stale parser output, or mismatched snapshot/dialect metadata.
- Apply is blocked when document identity, code page, or editor-content SHA-256 changed.
- Apply is also blocked for non-UTF-8 editor buffers until explicit round-trip encoding validation is implemented.
- No Scintilla replacement method is called for NoChanges or conflict states.
- Cell-only, single-row, and batch structural Apply share the same single host-write coordinator.
- Unchanged records retain their exact original raw representation.
- Missing display-only fields are not written unless the user edits them.
- Extra and trailing fields remain preserved.
- Apply changes only the editor buffer; no direct disk write or save-point manipulation occurs.
- More than 10,000 data rows or a 250,000-cell budget are visibly limited in the grid status.
- More than 512 columns, or a row wider than the aggregate cell budget, are rejected visibly; no columns are silently hidden.
