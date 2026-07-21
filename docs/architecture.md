# Architecture

CSV Visual Editor is split into a host-independent core and a thin Notepad++/WinForms integration layer.

## Projects

- `CsvVisualEditor.Core`: immutable editor snapshots and later CSV/table-domain logic that can be validated without Notepad++ or Windows Forms.
- `CsvVisualEditor`: Native AOT Notepad++ plugin, Scintilla adapter, and dockable WinForms user interface.
- `CsvVisualEditor.Core.SmokeTests`: dependency-free executable smoke checks for the core project.

## Host boundary

The editor buffer is the source of truth. The plugin must not reopen the current file from disk when reading content because the Notepad++ buffer may contain unsaved changes.

```text
Notepad++ / Scintilla
        │
        │ PluginData.Editor.GetText(), metadata calls
        ▼
NotepadActiveDocumentReader
        │
        │ IActiveDocumentReader
        ▼
ActiveDocumentSnapshot
        │
        ├── current metadata view
        └── future delimiter detection and CSV parser
```

`IActiveDocumentReader` belongs to the host-independent core. `NotepadActiveDocumentReader` belongs to the plugin project and is the only current component that knows about `PluginData`, Notepad++, or Scintilla.

## Snapshot model

`ActiveDocumentSnapshot` is immutable and contains:

- document path or untitled identity;
- display name;
- full decoded editor text;
- .NET character count;
- Scintilla-reported byte length;
- Scintilla code page;
- caret and anchor positions;
- selection length;
- modified state;
- UTC capture timestamp;
- SHA-256 identity of the UTF-8 representation of the decoded text.

The SHA-256 value is a stable content identity. It is not presented as an internal Notepad++ buffer revision number.

## Safety boundary

The current adapter refuses snapshots larger than 64 MiB based on the Scintilla-reported byte length. The limit is explicit and produces a visible error. Content is never silently truncated.

The panel shows only snapshot metadata in milestone 0.2. It does not show the document text, which reduces accidental exposure of sensitive CSV values during development screenshots.

## Refresh model

Milestone 0.2 uses explicit refresh:

- opening a newly created panel reads the active buffer;
- reopening a hidden panel reads the active buffer;
- the toolbar Refresh button reads the active buffer;
- `Plugins → CSV Visual Editor → Refresh Table` reads the active buffer.

Automatic refresh on buffer activation or modification is intentionally deferred until notification, throttling, stale-state, and hidden-panel behavior are designed.

## Error behavior

- known size-limit errors are displayed in the panel;
- unexpected host-read failures produce a generic non-destructive message;
- exceptions do not modify the editor buffer;
- CSV parsing is not attempted when snapshot acquisition fails.

## Milestone boundary

### Accepted milestone 0.1

- Native AOT plugin loading;
- menu commands;
- dockable WinForms panel;
- dark mode;
- clean shutdown/restart;
- CI and packaging.

### Current milestone 0.2

- active editor-buffer snapshot;
- unsaved content included;
- immutable core model and source identity;
- sanitized metadata display;
- explicit refresh and size/error states.

### Deferred

- delimiter detection and dialect confidence;
- CSV parsing and logical record spans;
- grid population with CSV cells;
- automatic refresh and stale-state notifications;
- editing, serialization, conflict detection, and Notepad++ undo/redo.