# Milestone 0.11 — native paste routing and large-grid performance discrepancy

## Owner host findings

The owner tested the previous `0.11.0-alpha` package in the real Notepad++ 8.9.7 x64 host and reported two blocking failures:

1. Excel-to-plugin paste still did not populate a rectangular range.
2. A CSV containing many thousands of rows took so long to load that the owner could not wait for completion.

These findings supersede the earlier assumption that an application-level WinForms message filter had solved Excel paste routing. Milestone 0.11 remains unaccepted and both PRs remain draft and unmerged.

## Paste root cause and replacement architecture

Notepad++ owns the outer native Windows message loop. `Application.AddMessageFilter` and an editing-control `KeyDown` handler are therefore not reliable interception points for all docked-plugin clipboard commands. In addition, the WinForms cell editor can consume Ctrl+V as a native paste before a managed key event is raised.

The replacement path is attached directly to the controls that receive the command:

- `CsvDataGridView.ProcessCmdKey` handles Ctrl+C and Ctrl+V while the grid owns focus;
- `CsvEditingControlPasteHook`, implemented as a `NativeWindow`, subclasses the active editing-control HWND and intercepts the actual `WM_PASTE` message;
- ordinary one-cell text still falls through to the native in-cell editor;
- text containing a tab, CR, or LF is routed through `CsvClipboardMatrix` and the atomic stable-ID paste plan;
- malformed, mismatched, or overflowing tabular data cannot fall through into one literal CSV cell.

No clipboard operation writes directly to Scintilla or disk.

## Large-grid root cause and replacement architecture

The previous read-only path performed several expensive operations on the UI thread:

- parsing and table projection;
- eager construction of the complete edit session and row-edit model even before Edit mode was requested;
- materialization of every DataGridView row and cell object;
- repeated all-row/all-cell measurement for native row headers and the `#` indicator column.

The replacement path:

- performs parsing and projection on a background task with cancellation and generation checks;
- shows an immediate loading state in the docked form;
- creates the edit session and row-edit model lazily only when the user enters Edit mode;
- uses virtual read-only rows when there are at least 1,000 rows or 40,000 visible data cells;
- batch-adds rows for smaller and editable tables;
- sizes row presentation using displayed native headers and a representative maximum label rather than rescanning all cells.

## Automated verification boundary

The implementation must pass strict build, the complete xUnit suite, Native AOT runtime smoke, win-x64 Native AOT plugin publish, and packaging before a new owner package is supplied.

Automated success does not establish that either host issue is solved. The owner must retest in Notepad++ 8.9.7 x64.

## Required owner retest

### Excel paste

1. Copy a 2 × 2 Excel range.
2. Enter Edit mode in the plugin.
3. Single-click the intended top-left CSV data cell and press Ctrl+V once.
4. Confirm that four values occupy four distinct cells.
5. Repeat with an already-selected 2 × 2 target.
6. Paste one ordinary word inside an active cell editor and confirm native caret-level insertion remains available.
7. Verify Revert All, Apply, one-step undo/redo, UTF-8 and Windows-1250, and Save/reopen.

### Large CSV

1. Open a valid CSV containing many thousands of rows.
2. Open or refresh the plugin and confirm that the loading state appears immediately and Notepad++ remains responsive.
3. Record the approximate row/column count and elapsed time until the table is usable.
4. Scroll near the beginning, middle, and end of the virtual read-only grid.
5. Test search/filter and copy from the large read-only table.
6. Enter Edit mode and confirm that the explicit edit-session preparation completes without corrupting row identity or existing row operations.

Do not merge public or internal PR #11 until these host checks pass.
