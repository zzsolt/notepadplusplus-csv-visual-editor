# CSV Visual Editor

CSV Visual Editor is a Notepad++ plugin for working with CSV files through a graphical, spreadsheet-like table.

> Development status: **0.8 row-operations alpha accepted in Notepad++ 8.9.7 x64**. Edit mode supports cell editing plus explicit Add Row and Delete Row operations for UTF-8 editor buffers. Deterministic minimal-difference serialization, fresh-buffer conflict checks, and one Scintilla undo transaction protect Apply. Saving to disk remains a normal Notepad++ action.

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
- explicit **Add Row** and **Delete Row** controls
- stable source-row identities independent of visible grid position
- synthetic session-local identities for inserted rows
- changed-cell, changed-row, inserted-row, and deleted-row counts
- `*` marker beside changed source rows and `new:n *` markers beside inserted rows
- readable 112-pixel row headers for inserted-row labels
- spreadsheet-style full-row selection from the left row header while ordinary cell clicks remain cell selections
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
4. Select **Add Row** to insert after the selected row, or append when no row is selected.
5. Select **Delete Row** to mark the selected source row for deletion; deleting a newly inserted row cancels that insertion.
6. Changed source rows keep their original logical-record number and add `*`; inserted rows use `new:n *`.
7. Click a normal cell to select/edit that cell, or click the left row header to select the complete row.
8. Select **Revert All** to discard every cell, insertion, and deletion change without touching the editor.
9. Select **Apply** to re-read the active editor and perform a fresh conflict check.
10. Apply is permitted only when both the session baseline and current editor buffer use Scintilla code page 65001 (UTF-8).
11. A document, code-page, content, or unsupported-encoding condition blocks Apply with zero replacement calls.
12. A valid Apply replaces the active editor buffer once inside one Scintilla undo action.
13. Save through Notepad++ when ready.

While Edit mode is active, Refresh, delimiter, header, search, and sort transitions are locked. A dirty Edit mode cannot be exited implicitly; use Apply or Revert All.

## Row-operation behavior

- Grid display indexes are never treated as persistent source identities.
- Existing data rows retain their original parser record identity.
- Inserted rows receive unique negative session-local identities internally.
- The header is outside the deletable data-row model.
- A source row remains restorable after pending deletion.
- Deleting an inserted row cancels the insertion.
- Add/Delete first commits the active cell editor or refuses the operation.
- Ordinary cell clicks remain cell selections; left row-header clicks select the complete stable row.
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
- Source logical-record numbers remain visible after filtering, sorting, and editing.
- Edit mode clears view-only filtering/sorting before structural operations begin.
- An edit session is rejected for parser errors, row-limited projections, omitted records/columns, stale parser output, or mismatched snapshot/dialect metadata.
- Apply is blocked when document identity, code page, or editor-content SHA-256 changed.
- Apply is also blocked for non-UTF-8 editor buffers until explicit round-trip encoding validation is implemented.
- No Scintilla replacement method is called for NoChanges or conflict states.
- Cell-only and structural Apply share the same single host-write coordinator.
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
- write-back for non-UTF-8 code pages with strict round-trip encoding validation
- multi-row structural operations
- row movement
- visual column-header editing
- conflict merge/rebase workflow
- Plugins Admin release packaging

## Repository layout

```text
src/CsvVisualEditor.Core/              Snapshot, CSV, table, view, serialization, edit-session, row operations, and Apply coordination
src/CsvVisualEditor/                   Notepad++ plugin, Scintilla adapter, and WinForms user interface
tests/CsvVisualEditor.Core.SmokeTests/ Dependency-free bootstrap regression checks
tests/CsvVisualEditor.Core.Tests/      xUnit.net v3 parser, view, serializer, row-operation, and Apply matrix
tests/CsvVisualEditor.NativeAot.SmokeTests/ Native AOT WinForms, structural Apply, and conflict runtime gate
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
Refresh | Delimiter | Header | Edit | Add Row | Delete Row | Apply | Revert All | change count
Search | In: All columns/<column> | Clear | Diagnostics (n)
```

## Validation boundary

Milestones 0.1–0.5 were manually accepted in Notepad++ 8.9.7 x64.

Milestone 0.6 safe-editing foundation passed 112/112 xUnit tests, strict build, Native AOT serializer/edit-session/conflict execution, and full Native AOT plugin publishing.

Milestone 0.7 passed 122/122 xUnit tests, strict build, Native AOT Apply-coordinator execution, full Native AOT plugin publishing, and complete Notepad++ 8.9.7 x64 owner acceptance on 2026-07-22.

Milestone 0.8 passed 159/159 xUnit tests, strict build, Native AOT structural preview, Apply/conflict/Revert execution, row-header selection smoke tests, full win-x64 Native AOT plugin publishing, and complete Notepad++ 8.9.7 x64 owner acceptance on 2026-07-23. The final targeted retest confirmed readable `new:n *` labels, cell selection, complete-row header selection, Delete Row targeting, reconstruction/reopen behavior, and light/dark mode behavior.

See:

- [`docs/safe-editing-foundation.md`](docs/safe-editing-foundation.md)
- [`docs/host-validation-0.7.md`](docs/host-validation-0.7.md)
- [`docs/row-operations-foundation.md`](docs/row-operations-foundation.md)
- [`docs/host-validation-0.8.md`](docs/host-validation-0.8.md)
- [`docs/host-validation-0.8-row-header-fix.md`](docs/host-validation-0.8-row-header-fix.md)
- [`docs/host-acceptance-0.8.md`](docs/host-acceptance-0.8.md)

## Roadmap

1. **Accepted:** active Notepad++ buffer snapshot.
2. **Accepted:** delimiter detection and record-aware CSV parser.
3. **Accepted:** read-only visual table.
4. **Accepted:** diagnostics, search, filtering, and stable view sorting.
5. **Accepted:** deterministic serialization, dirty tracking, and conflict planning.
6. **Accepted:** UTF-8 editable cells, single-undo Apply, Save ownership, and real-host conflict acceptance.
7. **Accepted:** stable Add Row/Delete Row operations and spreadsheet-style row-header selection.
8. **Next:** multi-row selection and batch deletion using stable row identities and the existing one-undo Apply path.
9. Add verified non-UTF-8 round-trip writing, source navigation, row movement, and visual header editing.
10. Package releases for eventual Notepad++ Plugins Admin submission.
