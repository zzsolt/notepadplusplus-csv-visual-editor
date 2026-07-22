# CSV Visual Editor

CSV Visual Editor is a Notepad++ plugin for working with CSV files through a graphical, spreadsheet-like table.

> Development status: **0.6 safe-editing foundation validated; the current Notepad++ interface remains read-only**. The core now provides deterministic minimal-difference serialization, dirty tracking, and conflict planning, but no editor-buffer or disk write is enabled yet.

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
- read-only table cells with source logical-record numbers in row headers
- content-aware full-width columns with readable minimum widths and horizontal-scroll fallback
- case-insensitive search across all columns or one selected column
- 250 ms search debounce
- stable, three-state view sorting: ascending, descending, source order
- dedicated **Table** and **Diagnostics** tabs
- structured diagnostics with severity, code, logical record, character offset, and message
- host-independent edit-session baseline and per-cell dirty tracking
- deterministic comma/semicolon/tab CSV serialization
- minimal-difference previews that preserve unchanged raw records and line separators
- document, code-page, and content-hash conflict planning
- visible display limits: at most 10,000 rows, 512 columns, and 250,000 cells in the current alpha
- dependency-free bootstrap checks, xUnit.net v3 tests, and Native AOT runtime smoke tests
- Native AOT publishing, so the target machine should not require a separately installed .NET runtime

## Safety behavior

- Automatic detection is used only with `Medium` or `High` confidence.
- `Low`, ambiguous, or absent suggestions require a manual delimiter choice.
- Parser errors produce partial read-only results plus diagnostics; source text is not rewritten.
- Inconsistent record widths are padded only in the rectangular view; parser records remain unchanged.
- Search, filtering, and sorting operate only on immutable projected rows.
- Stable sorting retains source order for values that compare equally.
- Source logical-record numbers remain visible after filtering and sorting.
- An edit session is rejected for parser errors, row-limited projections, omitted records/columns, or stale parser output.
- Apply planning is blocked when document identity, code page, or editor-content SHA-256 changed.
- Unchanged records retain their exact original raw representation in an edit preview.
- More than 10,000 data rows or a 250,000-cell budget are visibly limited in the grid status.
- More than 512 columns, or a row wider than the aggregate cell budget, are rejected visibly; no columns are silently hidden.
- The current plugin interface does not write to the editor buffer or disk.

## Not implemented yet

- automatic header inference
- automatic refresh after editor changes
- navigation from a diagnostic or visual row to the source editor position
- advanced filter expressions
- large-file virtualization beyond the current explicit display limits
- editable DataGridView cells
- Apply/Revert user-interface controls
- one-transaction Scintilla write-back and host undo/redo integration
- conflict dialog and real-host editing acceptance

## Repository layout

```text
src/CsvVisualEditor.Core/              Snapshot, CSV, table, view, serialization, and edit-session logic
src/CsvVisualEditor/                   Notepad++ plugin and WinForms user interface
tests/CsvVisualEditor.Core.SmokeTests/ Dependency-free bootstrap regression checks
tests/CsvVisualEditor.Core.Tests/      xUnit.net v3 parser, table, view, serializer, and edit-session matrix
tests/CsvVisualEditor.NativeAot.SmokeTests/ Native AOT WinForms and core runtime regression gate
docs/                                  Public architecture and validation documentation
```

## Build requirements

- Windows 10 or later
- .NET 10 SDK
- Visual Studio Build Tools with the C++ toolchain required by Native AOT
- 64-bit Notepad++ for manual loading tests

## Build and publish

From the repository root:

```powershell
dotnet restore src/CsvVisualEditor/CsvVisualEditor.csproj -r win-x64

dotnet publish src/CsvVisualEditor/CsvVisualEditor.csproj `
    -c Release `
    -f net10.0-windows `
    -r win-x64 `
    -o artifacts/CsvVisualEditor
```

Run the dependency-free bootstrap checks:

```powershell
dotnet run `
    --project tests/CsvVisualEditor.Core.SmokeTests/CsvVisualEditor.Core.SmokeTests.csproj `
    -c Release
```

Run the xUnit.net v3 matrix:

```powershell
dotnet run `
    --project tests/CsvVisualEditor.Core.Tests/CsvVisualEditor.Core.Tests.csproj `
    -c Release
```

The conventional test project pins `xunit.v3.mtp-v2` version `3.2.2`.

## Manual installation

Copy the published files into:

```text
<Notepad++ installation>\plugins\CsvVisualEditor\
```

The primary DLL must therefore be located at:

```text
<Notepad++ installation>\plugins\CsvVisualEditor\CsvVisualEditor.dll
```

Restart Notepad++, then use:

```text
Plugins → CSV Visual Editor → Open Visual Table
```

The current panel exposes:

```text
Refresh | Delimiter: Auto/Comma/Semicolon/Tab | Header: First row/No header
Search: <text> | In: All columns/<column> | Clear | Diagnostics (n)
```

Use **Refresh** after changing the active document. Search and sorting are view-only and do not modify the editor text.

## Validation boundary

Milestones 0.1–0.5 were manually accepted in Notepad++ 8.9.7 x64, including snapshot handling, parsing, read-only table rendering, search, sorting, diagnostics, dark mode, lifecycle behavior, and no-mutation guarantees.

Milestone 0.6 safe-editing foundation passed 112/112 xUnit tests, strict build, Native AOT serializer/edit-session/conflict execution, and full Native AOT plugin publishing. It is host-independent and introduces no visible editing or write path, so no new manual Notepad++ package is required for this foundation increment.

See [`docs/safe-editing-foundation.md`](docs/safe-editing-foundation.md) for the edit-session and conflict model.

## Roadmap

1. **Accepted:** active Notepad++ buffer snapshot.
2. **Accepted:** delimiter detection and record-aware CSV parser.
3. **Accepted:** read-only visual table with explicit delimiter/header controls.
4. **Accepted:** diagnostics UI, search, filtering, stable view sorting, and host lifecycle validation.
5. **Validated foundation:** deterministic serialization, dirty tracking, minimal-difference preview, and conflict planning.
6. **Next host increment:** editable cells, Apply/Revert, one Scintilla undo transaction, conflict UI, and host undo/redo acceptance.
7. Package releases for eventual Notepad++ Plugins Admin submission.
