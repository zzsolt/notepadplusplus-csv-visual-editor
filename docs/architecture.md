# Architecture

CSV Visual Editor is split into a host-independent core and a thin Notepad++/WinForms integration layer.

## Projects

- `CsvVisualEditor.Core`: immutable editor snapshots, CSV dialect detection, record-aware parsing, diagnostics, and later table-domain logic.
- `CsvVisualEditor`: Native AOT Notepad++ plugin, Scintilla adapter, and dockable WinForms user interface.
- `CsvVisualEditor.Core.SmokeTests`: dependency-free executable checks for the accepted bootstrap and snapshot baseline.
- `CsvVisualEditor.Core.Tests`: xUnit.net v3 parser and detector matrix.

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
        └── CsvDialectDetector → CsvParser → immutable CSV model
```

`IActiveDocumentReader` belongs to the host-independent core. `NotepadActiveDocumentReader` belongs to the plugin project and is the only component that knows about `PluginData`, Notepad++, or Scintilla.

The detector and parser accept decoded strings and do not reference Notepad++, Scintilla, Windows Forms, or disk I/O.

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

The SHA-256 value is a stable decoded-content identity, not an internal Notepad++ buffer revision or original-file byte hash.

## CSV core model

Milestone 0.3 introduces immutable host-independent models:

- `CsvDialect` and `CsvHeaderMode`;
- `CsvSourceSpan` for zero-based character offsets in decoded text;
- `CsvCell` and `CsvRecord`;
- `CsvDiagnostic` with severity, stable code, offset, and optional record index;
- `CsvParseResult`;
- `CsvDelimiterCandidateScore` and `CsvDialectDetectionResult`.

Collections are copied into read-only views at model boundaries so later UI code cannot mutate parser output accidentally.

## Record-aware parser

`CsvParser` is a character-state machine. It does not call `Split` on physical lines before quote processing.

Main states are conceptually:

```text
field start
    ├── quote → quoted field
    ├── delimiter → empty field
    ├── line break → end logical record
    └── other → unquoted field

quoted field
    ├── doubled quote → literal quote
    ├── closing quote → after-closing-quote state
    └── any character, including CR/LF → field content

after closing quote
    ├── delimiter → next field
    ├── record separator → next record
    ├── end of text → finish
    └── other → diagnostic and lossless recovery
```

The parser preserves partial results for malformed input and adds diagnostics rather than modifying source text or silently discarding characters.

Expected record width is the modal field count among non-blank logical records. Differing widths produce warnings, not destructive normalization.

## Dialect detection

`CsvDialectDetector` evaluates comma, semicolon, and tab candidates using the same parser implementation.

Scoring considers:

- logical-record field-count consistency;
- modal field width;
- number of multi-field records;
- delimiter occurrences outside quoted fields;
- parser errors and inconsistent widths;
- sample size;
- decimal-comma-like numeric adjacency.

The result includes all candidate scores and a confidence level. Weak or ambiguous structures produce diagnostics and must remain user-overridable. No reliable candidate produces no suggestion rather than an invented delimiter.

## Safety boundary

The current adapter refuses snapshots larger than 64 MiB based on the Scintilla-reported byte length. The limit is explicit and produces a visible error. Content is never silently truncated.

The panel still shows snapshot metadata rather than source values. Parser tests use synthetic data, and source text is not logged.

## Refresh model

The accepted explicit-refresh behavior is:

- opening a newly created panel reads the active buffer;
- reopening a hidden panel reads the active buffer;
- the toolbar Refresh button reads the active buffer;
- `Plugins → CSV Visual Editor → Refresh Table` reads the active buffer.

Automatic refresh remains deferred until notification, throttling, stale-state, and hidden-panel behavior are designed.

## Error behavior

- known snapshot-size errors are displayed in the panel;
- unexpected host-read failures produce a generic non-destructive message;
- parser errors are structured diagnostics attached to partial results;
- exceptions and parsing never modify the editor buffer or disk.

## Milestone boundary

### Accepted milestone 0.1

- Native AOT plugin loading;
- menu commands;
- dockable WinForms panel;
- dark mode;
- clean shutdown/restart;
- CI and packaging.

### Accepted milestone 0.2

- active editor-buffer snapshot;
- unsaved content included;
- immutable core model and content identity;
- sanitized metadata display;
- explicit refresh and size/error states;
- complete Notepad++ 8.9.7 x64 host acceptance.

### Current milestone 0.3

- explicit CSV dialect model;
- delimiter candidate scoring and confidence;
- record-aware quoted-field parser;
- immutable rows/cells/source spans;
- parser diagnostics;
- conventional xUnit.net v3 test matrix.

### Deferred

- binding parsed records to the DataGridView;
- header inference and user override controls;
- source navigation;
- automatic refresh and stale-state notifications;
- editing, serialization, conflict detection, and Notepad++ undo/redo.
