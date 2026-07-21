# CSV Visual Editor

CSV Visual Editor is a Notepad++ plugin for working with CSV files through a graphical, spreadsheet-like table.

> Development status: **0.2 active-document snapshot alpha**. The plugin can read an immutable copy of the active Notepad++ editor buffer, including unsaved changes, and display sanitized snapshot metadata. CSV parsing and table rows are the next milestones.

## Current capabilities

- 64-bit Windows / Notepad++ target
- C# and .NET 10
- Windows Forms `DataGridView`
- dockable plugin panel
- light/dark mode response
- `Open Visual Table`, `Refresh Table`, and `About` commands
- direct active-buffer reading through Notepad++/Scintilla
- unsaved editor changes included in the snapshot
- immutable snapshot with document identity, character/byte lengths, code page, caret/selection state, modified state, capture time, and SHA-256 content identity
- explicit 64 MiB snapshot safety limit with a visible error instead of silent truncation
- Native AOT publishing, so the target machine should not require a separately installed .NET runtime
- automated core smoke checks and GitHub Actions build

The panel intentionally displays snapshot metadata rather than document content in this milestone. This makes buffer access testable without copying potentially sensitive CSV values into screenshots.

## Not implemented yet

- delimiter detection
- quoted-field-aware CSV parsing
- CSV rows and columns in the visual grid
- automatic refresh after editor changes
- filtering, sorting, search, or large-file virtualization
- cell editing or write-back

## Repository layout

```text
src/CsvVisualEditor.Core/              Host-independent snapshot, table, and CSV-domain logic
src/CsvVisualEditor/                   Notepad++ plugin and WinForms user interface
tests/CsvVisualEditor.Core.SmokeTests/ Dependency-free automated smoke checks
docs/                                  Public architecture documentation
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

Run the host-independent smoke checks:

```powershell
dotnet run `
    --project tests/CsvVisualEditor.Core.SmokeTests/CsvVisualEditor.Core.SmokeTests.csproj `
    -c Release
```

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

For milestone 0.2, CI can validate the snapshot model and native build. A real Notepad++ test must additionally confirm that changing unsaved text and pressing Refresh changes the character count, modified state, and content identity shown in the panel.

## Roadmap

1. **Current:** read and identify the active Notepad++ editor buffer.
2. Detect comma, semicolon, and tab delimiters.
3. Parse quoted fields and embedded line breaks correctly.
4. Populate the visual table and report parse errors.
5. Add sorting, filtering, search, and large-file handling.
6. Add safe two-way cell editing with conflict and undo/redo support.
7. Package releases for eventual Notepad++ Plugins Admin submission.