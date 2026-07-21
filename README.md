# CSV Visual Editor

CSV Visual Editor is a Notepad++ plugin for working with CSV files through a graphical, spreadsheet-like table.

> Development status: **0.1 bootstrap MVP**. The current branch validates the loadable Native AOT plugin shell and dockable WinForms grid. Reading the active CSV document is the next milestone.

## Current capabilities

- 64-bit Windows / Notepad++ target
- C# and .NET 10
- Windows Forms `DataGridView`
- dockable plugin panel
- light/dark mode response
- `Open Visual Table`, `Refresh Table`, and `About` commands
- Native AOT publishing, so the target machine should not require a separately installed .NET runtime
- automated core smoke checks and GitHub Actions build

## Repository layout

```text
src/CsvVisualEditor.Core/              Host-independent table and CSV-domain logic
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

## Validation boundary

CI can prove that the source restores, builds, the core smoke checks pass, and a Native AOT artifact is produced. A real Notepad++ loading and docking test must still be performed manually on Windows before the 0.1 milestone is considered fully accepted.

## Roadmap

1. Read the active Notepad++ document.
2. Detect comma, semicolon, and tab delimiters.
3. Parse quoted fields and embedded line breaks correctly.
4. Populate the visual table and report parse errors.
5. Add sorting, filtering, search, and large-file handling.
6. Add safe two-way cell editing with conflict and undo/redo support.
7. Package releases for eventual Notepad++ Plugins Admin submission.
