# Architecture

CSV Visual Editor is split into a host-independent core and a thin Notepad++/WinForms integration layer.

## Projects

- `CsvVisualEditor.Core`: immutable editor snapshots, CSV dialect detection, record-aware parsing, diagnostics, safe table-build policy, and read-only table projection.
- `CsvVisualEditor`: Native AOT Notepad++ plugin, Scintilla adapter, minimal orchestration, and dockable WinForms user interface.
- `CsvVisualEditor.Core.SmokeTests`: dependency-free executable checks for the accepted bootstrap and snapshot baseline.
- `CsvVisualEditor.Core.Tests`: xUnit.net v3 parser, detector, table-builder, and table-projection matrix.

## End-to-end read flow

The live editor buffer is the source of truth. The plugin does not reopen the active path from disk.

```text
Notepad++ / Scintilla active buffer
        │
        │ PluginData.Editor.GetText() and metadata
        ▼
NotepadActiveDocumentReader
        │
        ▼
ActiveDocumentSnapshot
        │
        ▼
CsvTableBuilder
        ├── automatic CsvDialectDetector
        │       └── Medium/High confidence only
        ├── or explicit comma/semicolon/tab override
        ├── CsvParser
        └── CsvTableProjector
                │
                ▼
        CsvTableBuildResult
        ├── Empty
        ├── DelimiterSelectionRequired
        └── Ready(parse result + bounded projection)
                │
                ▼
read-only WinForms DataGridView or safe metadata/error state
```

`IActiveDocumentReader` belongs to the host-independent core. `NotepadActiveDocumentReader` belongs to the plugin project and is the only component that knows about `PluginData`, Notepad++, or Scintilla.

`CsvTableBuilder` owns delimiter trust policy, header mode, parsing, and visual limits. These decisions are therefore testable without Notepad++ or WinForms.

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

## CSV parser model

The host-independent parser model contains:

- `CsvDialect` and explicit `CsvHeaderMode`;
- `CsvSourceSpan` for zero-based decoded-string character offsets;
- immutable `CsvCell` and `CsvRecord`;
- `CsvDiagnostic` with severity, stable code, offset, and optional record index;
- immutable `CsvParseResult`;
- delimiter candidate scores and detection result with confidence.

Collections are copied into read-only views at model boundaries so UI code cannot mutate parser output.

## Record-aware parser

`CsvParser` is a character-state machine. It never splits physical lines before quote processing.

It supports:

- comma, semicolon, and tab;
- CRLF, LF, and lone CR record separators;
- quoted delimiters;
- doubled quote escapes;
- embedded line breaks inside quoted fields;
- empty and trailing fields;
- blank logical records;
- Unicode and leading decoded U+FEFF handling;
- partial malformed-input results with structured diagnostics.

Expected record width is the modal field count among non-blank logical records. Differing widths produce warnings, not destructive normalization.

## Dialect detection

`CsvDialectDetector` evaluates comma, semicolon, and tab candidates with the same quote and logical-record semantics as the parser.

Scoring considers:

- logical-record field-count consistency;
- modal width;
- multi-field records;
- delimiter occurrences outside quotes;
- parser errors;
- sample size;
- decimal-comma-like numeric adjacency.

The detector uses a bounded default sample of 20 logical records and 1 MiB decoded characters. It returns every candidate score plus `None`, `Low`, `Medium`, or `High` confidence.

`CsvTableBuilder` accepts automatic selection only at `Medium` or `High` confidence. `Low`, ambiguous, and absent suggestions return `DelimiterSelectionRequired`; the UI must not silently guess.

## Table-build result

`CsvTableBuildOptions` contains:

- optional delimiter override;
- explicit `FirstRecord` or `NoHeader` mode;
- maximum displayed rows;
- maximum displayed columns;
- maximum aggregate displayed cells.

`CsvTableBuildResult` has three states:

- `Empty` — decoded input contains no characters;
- `DelimiterSelectionRequired` — automatic evidence is not trustworthy and candidate scores are retained;
- `Ready` — parse result and bounded projection are available.

This keeps structural policy outside `CsvGridForm` and makes weak-detection/manual-override behavior part of the core test gate.

## Read-only table projection

`CsvTableProjector` converts `CsvParseResult` into a rectangular immutable view model without changing parser records.

### Header modes

The toolbar exposes two explicit choices:

- **First row is header** — the first logical record supplies display headers and is not displayed as a data row.
- **No header row** — every logical record remains data and columns are named `Column 1` through `Column N`.

Header inference is not implemented. The selected behavior is visible at all times.

### Display column names

When the first record is used as a header:

- surrounding and repeated whitespace is normalized for display;
- embedded line breaks become spaces;
- empty names receive deterministic `Column N` fallbacks;
- duplicates are disambiguated case-insensitively with ` (2)`, ` (3)`, and so on;
- source cell values are not changed.

### Inconsistent widths

The projection column count is the maximum field count among parsed records. Shorter records receive empty strings only in the rectangular display model. The original `CsvRecord.Cells` collections remain unchanged, and parser warnings stay available.

### Source identity

Every projected row retains:

- the original logical-record index;
- the original decoded-text source span.

The projected one-based logical-record number is rendered in the dedicated `#` row-indicator column. Native DataGridView row headers remain glyph-only current-row/selection targets, so the active glyph cannot displace the number column.

## Display limits

The current alpha has explicit UI safety limits:

- maximum 10,000 displayed data rows;
- maximum 512 displayed columns;
- maximum 250,000 aggregate displayed cells.

The displayed row count is the smallest count allowed by the row and aggregate-cell budgets. For example, a 100-column result can display at most 2,500 rows.

When rows exceed a limit, the first permitted rows are shown and the status line states displayed and total counts. This is visible truncation of the view only; parse results are not changed.

When columns exceed 512, or a single row cannot fit the aggregate cell budget, table rendering is refused with a visible error. No columns are silently hidden.

These limits are not a final large-file strategy. Virtual mode, paging, cancellation, and measured performance remain later work.

## WinForms presentation

The docked panel contains:

- Refresh button;
- Delimiter selector: Auto detect, Comma, Semicolon, Tab;
- Header selector: First row is header, No header row;
- read-only `DataGridView`;
- status line showing document, row/column counts, delimiter source/confidence, header mode, and parser diagnostic counts.

Cells use programmatic edit mode and all columns are non-sortable. Changing either selector rereads and rebuilds the current live editor buffer.

### Milestone 0.9 row presentation

`CsvGridRowPresentation` separates framework-owned row-header behavior from plugin-owned labels:

- the native row-header cell contains no record text and retains WinForms current-row glyph, theme, DPI, RTL, high-contrast, and accessibility behavior;
- native width uses `AutoSizeToAllHeaders`; no read-only/Edit fixed-width pair exists;
- a frozen read-only `#` column displays aligned logical-record numbers and pending `*` / `new:n *` state;
- that column is physically appended after CSV data columns but assigned display index zero, preserving every CSV data-column index;
- content measurement occurs after rendering, so only labels that actually exist affect width;
- the Edit-only selector remains physically and visually last;
- native row-header, `#`-cell, and selector gestures converge on the same plugin-owned stable-ID selection state.

The plugin intentionally does not custom-paint native row-header text. Reproducing private glyph/theme/text layout would increase DPI, dark-mode, high-contrast, RTL, accessibility, and framework-version risk.

## Refresh model

Snapshot acquisition and table rebuilding occur when:

- the panel is first created;
- a hidden panel is shown again;
- the panel Refresh button is pressed;
- `Plugins → CSV Visual Editor → Refresh Table` is invoked;
- delimiter or header selection changes.

Automatic refresh after arbitrary editor modifications remains deferred until notification, debounce, stale-state, hidden-panel, and large-buffer behavior are designed.

## Safety and error behavior

- snapshots above 64 MiB Scintilla byte length are refused visibly;
- weak automatic delimiter detection requires explicit selection;
- parser errors produce partial read-only tables plus structural counts;
- no source values are written to logs or diagnostics;
- table projection never mutates parser output;
- row and cell limiting is visible;
- column overflow is refused instead of hidden;
- unexpected host or table failures produce generic non-destructive messages;
- no editor write, disk write, serialization, conflict handling, or undo/redo path exists.

## Milestone boundary

### Accepted 0.1

- Native AOT plugin loading, commands, docking, dark mode, lifecycle, CI, and packaging.

### Accepted 0.2

- live active-buffer snapshot including unsaved and untitled content;
- explicit refresh and safety/error states;
- complete Notepad++ 8.9.7 x64 host acceptance.

### Accepted 0.3

- delimiter detection with confidence and diagnostics;
- record-aware parser and immutable source spans;
- strict xUnit.net v3 parser matrix and Native AOT regression.

### Current 0.4

- host-independent table-build policy;
- explicit delimiter and header controls;
- bounded read-only table projection;
- parsed CSV values in DataGridView;
- row source numbers and diagnostic summary;
- developer/contact information in About;
- pending Windows CI and Notepad++ 8.9.7 x64 host acceptance.

### Deferred

- detailed diagnostics list;
- source navigation and character-to-Scintilla position mapping;
- automatic refresh/stale indicator;
- search, filtering, stable view sorting, and virtualization;
- editing, serialization, conflict detection, rollback, and Notepad++ undo/redo.
