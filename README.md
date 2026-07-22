# CSV Visual Editor

CSV Visual Editor is a Notepad++ plugin for working with CSV files through a graphical, spreadsheet-like table.

> Development status: **0.7 editable-grid alpha under host validation**. Edit mode can modify the active Notepad++ editor buffer through deterministic minimal-difference serialization, fresh-buffer conflict checks, and one Scintilla undo transaction. Saving to disk remains a normal Notepad++ action.

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
- per-cell dirty tracking with changed-cell and changed-record counts
- `*` dirty marker beside the original source logical-record number
- Revert All without modifying the editor buffer
- deterministic comma/semicolon/tab CSV serialization
- minimal-difference previews that preserve unchanged raw records and line separators
- fresh document, code-page, and content-hash conflict checks immediately before Apply
- one whole-document Scintilla replacement inside one undo action
- normal Notepad++ modified-marker and Save ownership
- visible display limits: at most 10,000 rows, 512 columns, and 250,000 cells in the current alpha
- dependency-free bootstrap checks, xUnit.net v3 tests, and Native AOT runtime smoke tests
- Native AOT publishing, so the target machine should not require a separately installed .NET runtime

## Edit and Apply behavior

1. Open a trustworthy, fully displayed CSV table.
2. Select **Edit**. Search and sorting are cleared and source order is restored.
3. Edit data cells directly in the grid.
4. Dirty records keep their original row number and add `*`.
5. Select **Revert All** to discard every grid edit without touching the editor.
6. Select **Apply** to re-read the active editor and perform a fresh conflict check.
7. A conflict blocks Apply with zero editor writes.
8. A valid Apply replaces the active editor buffer once inside one Scintilla undo action.
9. Save through Notepad++ when ready.

While Edit mode is active, Refresh, delimiter, header, search, and sort transitions are locked. A dirty Edit mode cannot be exited implicitly; use Apply or Revert All.

## Safety behavior

- Automatic detection is used only with `Medium` or `High` confidence.
- `Low`, ambiguous, or absent suggestions require a manual delimiter choice.
- Parser errors produce partial read-only results plus diagnostics; source text is not rewritten.
- Inconsistent record widths are padded only in the rectangular view; parser records remain unchanged.
- Search, filtering, and sorting operate only on immutable projected rows.
- Stable sorting retains source order for values that compare equally.
- Source logical-record numbers remain visible after filtering, sorting, and editing.
- An edit session is rejected for parser errors, row-limited projections, omitted records/columns, or stale parser output.
- Apply is blocked when document identity, code page, or editor-content SHA-256 changed.
- No Scintilla method is called for NoChanges or conflict states.
- Unchanged records retain their exact original raw representation.
- Missing display-only fields are not written unless the user edits them.
- Extra and trailing fields remain preserved.
- Apply changes only the editor buffer; no direct disk write or save-point manipulation occurs.
- More than 10,000 data rows or a 250,000-cell budget are visibly limited in the grid status.
- More than 512 columns, or a row wider than the aggregate cell budget, are rejected visibly; no columns are silently hidden.

## Not implemented yet

- automatic header inference
- automatic refresh after editor changes
- navigation from a diagnostic or visual row to the source editor position
- advanced filter expressions
- large-file virtualization beyond the current explicit display limits
- row insertion/deletion
- visual column-header editing
- conflict merge/rebase workflow
- Plugins Admin release packaging

## Repository layout

```text
src/CsvVisualEditor.Core/              Snapshot, CSV, table, view, serialization, edit-session, and Apply coordination
src/CsvVisualEditor/                   Notepad++ plugin, Scintilla adapter, and WinForms user interface
tests/CsvVisualEditor.Core.SmokeTests/ Dependency-free bootstrap regression checks
tests/CsvVisualEditor.Core.Tests/      xUnit.net v3 parser, view, serializer, edit-session, and Apply matrix
tests/CsvVisualEditor.NativeAot.SmokeTests/ Native AOT WinForms and core runtime regression gate
docs/                                  Public architecture and validation documentation
```

## Build requirements

- Windows 10 or later
- .NET 10 SDK
- Visual Studio Build Tools with the C++ toolchain required by Native AOT
- 64-bit Notepad++ for manual loading tests

## Build and publish

```powershell
dotnet restore src/CsvVisualEditor/CsvVisualEditor.csproj -r win-x64

dotnet publish src/CsvVisualEditor/CsvVisualEditor.csproj `
    -c Release `
    -f net10.0-windows `
    -r win-x64 `
    -o artifacts/CsvVisualEditor
```

Run the xUnit.net v3 matrix:

```powershell
dotnet run `
    --project tests/CsvVisualEditor.Core.Tests/CsvVisualEditor.Core.Tests.csproj `
    -c Release
```

## Manual installation

Copy the published DLL into:

```text
<Notepad++ installation>\plugins\CsvVisualEditor\CsvVisualEditor.dll
```

Restart Notepad++, then use:

```text
Plugins → CSV Visual Editor → Open Visual Table
```

The panel exposes:

```text
Refresh | Delimiter | Header | Edit | Apply | Revert All | change count
Search | In: All columns/<column> | Clear | Diagnostics (n)
```

## Validation boundary

Milestones 0.1–0.5 were manually accepted in Notepad++ 8.9.7 x64.

Milestone 0.6 safe-editing foundation passed 112/112 xUnit tests, strict build, Native AOT serializer/edit-session/conflict execution, and full Native AOT plugin publishing.

Milestone 0.7 currently passes the expanded core test matrix, Native AOT Apply-coordinator execution, and full Native AOT plugin publishing. Complete Notepad++ 8.9.7 x64 acceptance is required before merge, especially one-step Ctrl+Z/Ctrl+Y, modified-marker, normal Save ownership, and conflict blocking.

See:

- [`docs/safe-editing-foundation.md`](docs/safe-editing-foundation.md)
- [`docs/host-validation-0.7.md`](docs/host-validation-0.7.md)

## Roadmap

1. **Accepted:** active Notepad++ buffer snapshot.
2. **Accepted:** delimiter detection and record-aware CSV parser.
3. **Accepted:** read-only visual table.
4. **Accepted:** diagnostics, search, filtering, and stable view sorting.
5. **Accepted foundation:** deterministic serialization, dirty tracking, and conflict planning.
6. **Current:** editable cells, single-undo Apply, and real-host undo/redo/conflict acceptance.
7. Add row/column operations and source navigation.
8. Package releases for eventual Notepad++ Plugins Admin submission.
