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

## Final automated evidence

```text
Verified implementation head:
a184102c0e6d799feeab885d0448039a2a3137c5

CI run: 30544656058 — PASS
CI job: 90877643050 — PASS
Restore: PASS
Strict core build: PASS
Bootstrap smoke: PASS
Core xUnit: 228/228 PASS
Errors: 0
Failed: 0
Skipped: 0
Not Run: 0
Time: 0.976s
Windows Native AOT runtime smoke: PASS
win-x64 Native AOT plugin publish: PASS
Installable package creation/upload: PASS

Artifact ID: 8760277999
Artifact name: CsvVisualEditor-0.11.0-alpha-win-x64
Artifact size: 7,737,569 bytes
Outer artifact ZIP SHA-256:
3340fb6cf67626f119c02a397bc9473e1d05f620adf63296db3d4c9dddb93dd3

Inner install ZIP size: 7,753,943 bytes
Inner install ZIP SHA-256:
5b62acf4f127bcd8dc44ee85f41b654bd1d72ecae930b5062c85e14ad7bf8c64

CsvVisualEditor.dll size: 20,545,536 bytes
CsvVisualEditor.dll SHA-256:
a70cfce6eec99dacafc91dd134d8dd13f65172cd013e40e2748806fa2129da50

Install layout:
CsvVisualEditor/CsvVisualEditor.dll
```

The xUnit suite increased from 221 to 228 tests. The new tests cover the bounded virtual-row policy and negative input validation; the Native AOT smoke also validates that the large-grid policy remains available after trimming. Plugin publish proves that the direct grid command path, native window hook, background-load integration, lazy edit model, and virtual DataGridView code compile under the production Native AOT configuration.

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
