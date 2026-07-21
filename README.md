# CSV Visual Editor

CSV Visual Editor is a Notepad++ plugin for working with CSV files through a graphical, spreadsheet-like table.

> Development status: **0.3 CSV parser alpha under validation**. The plugin reads the active Notepad++ editor buffer, including unsaved changes. The host-independent core now contains delimiter detection and logical-record parsing; binding parsed rows to the visual grid is the next milestone.

## Current capabilities

- 64-bit Windows / Notepad++ target
- C# and .NET 10
- Windows Forms `DataGridView`
- dockable plugin panel
- light/dark mode response
- `Open Visual Table`, `Refresh Table`, and `About` commands
- direct active-buffer reading through Notepad++/Scintilla
- unsaved editor changes included in an immutable snapshot
- document identity, character/byte lengths, code page, caret/selection state, modified state, capture time, and SHA-256 decoded-content identity
- explicit 64 MiB snapshot safety limit with a visible error instead of silent truncation
- comma, semicolon, and tab delimiter candidates
- explainable delimiter scoring with confidence and diagnostics
- record-aware CSV parser supporting quoted delimiters, doubled quotes, and embedded CRLF/LF
- empty/trailing fields, blank records, source character spans, BOM-like decoded input, and malformed-input diagnostics
- dependency-free bootstrap smoke checks plus an xUnit.net v3 parser matrix
- Native AOT publishing, so the target machine should not require a separately installed .NET runtime

The current docked panel still displays sanitized snapshot metadata rather than parsed CSV values. Parser correctness is being validated independently before UI binding.

## Not implemented yet

- parsed CSV rows and columns in the visual grid
- header-mode selection and header inference
- manual delimiter override UI
- automatic refresh after editor changes
- filtering, sorting, search, or large-file virtualization
- cell editing or write-back

## Repository layout

```text
src/CsvVisualEditor.Core/              Host-independent snapshot, CSV, and table-domain logic
src/CsvVisualEditor/                   Notepad++ plugin and WinForms user interface
tests/CsvVisualEditor.Core.SmokeTests/ Dependency-free bootstrap regression checks
tests/CsvVisualEditor.Core.Tests/      xUnit.net v3 parser and detector matrix
docs/                                  Public architecture, parser, and test documentation
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

Run the xUnit.net v3 parser matrix:

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

Use **Refresh** after changing the active document. The metadata should update from the current editor buffer even before the document is saved to disk.

## Validation boundary

Milestone 0.1 plugin loading, docking, commands, dark mode, shutdown, restart, and Native AOT operation were manually accepted in Notepad++ 8.9.7.

Milestone 0.2 active-buffer reading was accepted after successful automated validation and complete Notepad++ 8.9.7 x64 host testing, including unsaved edits, tab switching, both Refresh paths, untitled buffers, dark mode, lifecycle behavior, and confirmation that editor text was not modified.

Milestone 0.3 parser correctness is validated in the host-independent core before parsed data is exposed in the UI. See `docs/csv-parser.md` and `docs/testing.md`.

## Roadmap

1. **Accepted:** read and identify the active Notepad++ editor buffer.
2. **Current:** detect comma, semicolon, and tab dialects and parse logical CSV records.
3. Populate the visual table, expose diagnostics, and add user overrides.
4. Add sorting, filtering, search, and large-file handling.
5. Add safe two-way cell editing with conflict and undo/redo support.
6. Package releases for eventual Notepad++ Plugins Admin submission.
