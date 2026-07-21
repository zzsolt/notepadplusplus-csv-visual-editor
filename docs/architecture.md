# Architecture

CSV Visual Editor is split into a host-independent core and a thin Notepad++/WinForms integration layer.

## Projects

- `CsvVisualEditor.Core`: CSV-domain and table-model logic that can be validated without Notepad++ or Windows Forms.
- `CsvVisualEditor`: Native AOT Notepad++ plugin and dockable WinForms user interface.
- `CsvVisualEditor.Core.SmokeTests`: dependency-free executable smoke checks for the core project.

## Current milestone

Version 0.1 validates the technical shell:

1. Notepad++ can load the Native AOT plugin DLL.
2. The Plugins menu exposes commands for opening and refreshing the visual table.
3. A dockable WinForms panel hosts a read-only `DataGridView`.
4. CI builds the core project, runs smoke checks, and publishes an x64 plugin artifact.

Reading and parsing the active document is intentionally deferred to the next milestone so that plugin loading, docking, packaging, dark mode, and CI can be validated independently.
