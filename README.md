# CSV Visual Editor

CSV Visual Editor is a Notepad++ plugin for working with CSV files through a graphical, spreadsheet-like table.

> Development status: **0.4 read-only visual table alpha under validation**. The plugin reads the active Notepad++ editor buffer, including unsaved changes, detects or explicitly selects the delimiter, parses logical CSV records, and displays them in a read-only grid.

## Current capabilities

- 64-bit Windows / Notepad++ target
- C# and .NET 10
- Windows Forms `DataGridView`
- dockable plugin panel
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
- visible display limits: at most 10,000 rows, 512 columns, and 250,000 cells in the current alpha
- dependency-free bootstrap smoke checks plus an xUnit.net v3 parser/table matrix
- Native AOT publishing, so the target machine should not require a separately installed .NET runtime

## Safety behavior

- Automatic detection is used only with `Medium` or `High` confidence.
- `Low`, ambiguous, or absent suggestions require a manual delimiter choice.
- Parser errors produce partial read-only results plus diagnostics; source text is not rewritten.
- Inconsistent record widths are padded only in the rectangular view; parser records remain unchanged.
- More than 10,000 data rows or a 250,000-cell budget are visibly limited in the grid status.
- More than 512 columns, or a row wider than the aggregate cell budget, are rejected visibly; no columns are silently hidden.
- The plugin does not write to the editor buffer or disk in this milestone.

## Not implemented yet

- automatic header inference
- automatic refresh after editor changes
- detailed diagnostic list UI
- source navigation
- filtering, sorting, search, or large-file virtualization
- cell editing or write-back

## Repository layout

```text
src/CsvVisualEditor.Core/              Host-independent snapshot, CSV, and table-domain logic
src/CsvVisualEditor/                   Notepad++ plugin and WinForms user interface
tests/CsvVisualEditor.Core.SmokeTests/ Dependency-free bootstrap regression checks
tests/CsvVisualEditor.Core.Tests/      xUnit.net v3 parser, detector, and table-projection matrix
docs/                                  Public architecture, parser, table, and test documentation
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

Run the xUnit.net v3 parser and table matrix:

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

The toolbar exposes:

```text
Refresh | Delimiter: Auto/Comma/Semicolon/Tab | Header: First row/No header
```

Use **Refresh** after changing the active document. Changing either dropdown also rebuilds the current table from the live editor buffer.

## Validation boundary

Milestone 0.1 plugin loading, docking, commands, dark mode, shutdown, restart, and Native AOT operation were manually accepted in Notepad++ 8.9.7 x64.

Milestone 0.2 active-buffer reading was accepted after complete Notepad++ 8.9.7 x64 host testing, including unsaved edits, tab switching, both Refresh paths, untitled buffers, dark mode, lifecycle behavior, and confirmation that editor text was not modified.

Milestone 0.3 delimiter detection and record-aware parsing passed the strict host-independent test matrix and Native AOT regression gate.

Milestone 0.4 requires both automated validation and manual Notepad++ 8.9.7 x64 acceptance because the CSV rows are now exposed in the UI.

## Roadmap

1. **Accepted:** active Notepad++ buffer snapshot.
2. **Accepted:** delimiter detection and record-aware CSV parser.
3. **Current:** read-only visual table with explicit delimiter/header controls.
4. Add diagnostics UI, search, filtering, stable view sorting, and scalable rendering.
5. Add safe two-way cell editing with conflict and undo/redo support.
6. Package releases for eventual Notepad++ Plugins Admin submission.
