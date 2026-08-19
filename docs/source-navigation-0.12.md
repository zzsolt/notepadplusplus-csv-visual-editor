# Milestone 0.12 — source navigation and Scintilla byte mapping

## Goal

Allow the visual CSV table to select the originating CSV cell or logical record directly in the active Notepad++ editor without modifying the document, weakening buffer ownership, or confusing DataGridView display position with source identity.

## Stable navigation identity

Navigation uses:

```text
parser logical-record index + optional physical CSV column index
```

It must not use:

- DataGridView display row index as persistent identity;
- sorted/filter position as source identity;
- the `#` presentation column as a CSV column;
- the edit-only `Select` presentation column as a CSV column;
- a fabricated source position for an inserted pending row.

A nullable column index means navigate to the complete logical record. A physical column index means navigate to that raw CSV field when the field exists in source text.

## Source spans

The existing parser already retains raw decoded-text character spans:

- `CsvRecord.SourceSpan` spans one complete logical record and excludes its record separator;
- `CsvCell.SourceSpan` spans the raw field, including CSV quotes when the field was quoted;
- multiline quoted fields therefore remain one navigable source field even when they cross physical lines.

Projected padding is not a raw source field. If a displayed padded cell has no source `CsvCell`, the host navigation layer falls back to selecting its complete logical record rather than inventing a character position.

## Scintilla position contract

Scintilla positions are byte-oriented while parser spans are UTF-16 character offsets. The conversion therefore uses the active Scintilla code page explicitly.

Recognized profiles are the existing explicit encoding profiles:

- UTF-8 / 65001;
- US-ASCII / 20127;
- Windows-1250;
- Windows-1252.

Conversion uses strict encoder fallbacks. Unsupported code pages, unrepresentable text, invalid UTF-16 boundaries, or encoder failures are blocked.

Before a navigation plan is authorized, the strict encoded byte length of the complete current buffer must exactly equal the byte length reported by Scintilla. This prevents navigation with a code-page interpretation that does not describe the actual editor buffer.

## Immutable rendered baseline

A complete Ready table installs one source-navigation baseline containing the exact immutable `ActiveDocumentSnapshot` and matching `CsvParseResult` used to render that table.

The previous baseline is cleared immediately when a new load/refresh starts. Empty, delimiter-selection-required, loading, error, cancelled, or stale-generation states do not retain navigation positions from an older table. A replacement baseline is installed only after the new Ready result is actually presented.

The baseline is associated with the primary plugin `CsvDataGridView` through a weak table and is not serialized into grid cells or row labels.

## Fresh-buffer safety

Source navigation is read-only, but stale offsets can still select the wrong source location. Every `Source` action therefore reads a fresh active Scintilla snapshot and compares it with the exact rendered baseline before any host position is exposed.

Required equality:

- same document identity;
- same Scintilla code page;
- same complete decoded-content SHA-256.

The retained parser result is also verified against its immutable source snapshot. Any mismatch fails closed and tells the user to return to/Refresh the displayed CSV.

This same rendered baseline works in both read-only and Edit modes. Pending visual edits do not change the Notepad++ buffer, so source-backed Edit rows continue to navigate to their original raw source locations until Apply. An external editor change, document switch, or code-page change blocks navigation immediately.

## Row versus cell intent

The accepted row-header and `#` gestures intentionally leave a real data cell as `CurrentCell` while visually selecting the complete row. Navigation therefore does not infer intent from `CurrentCell.ColumnIndex` alone.

- complete visual row selection means logical-record navigation;
- ordinary cell-only focus means raw-field navigation;
- a presentation column also means logical-record navigation;
- inserted rows have no source identity until Apply.

This preserves the milestone 0.9/0.11 row-selection contract instead of changing DataGridView selection semantics for navigation.

## Host action

A successful plan contains exact byte-oriented anchor and caret positions. The host adapter calls Scintilla selection only. It does not:

- replace text;
- enter an undo action;
- alter the pending edit model;
- write a file;
- save the document.

The editor remains the authority for caret, selection and viewport presentation.

## UI behavior

The spreadsheet command row becomes:

```text
Paste  Cut  Copy  Source | Edit  Add Row  Delete Row | Apply  Revert All | changes
```

`Source` is enabled only when the current visual row exposes a source identity and the rendered table has a retained source baseline.

- current real CSV cell: select that raw source field;
- complete-row / presentation context: select the complete logical record;
- inserted pending row: disabled / blocked until Apply creates real source text;
- projected padded data cell: fall back to complete logical record;
- stale/conflicting source: block with zero document changes.

## Automated coverage

Core tests cover:

- UTF-8 multibyte byte offsets;
- Windows-1250 Hungarian text;
- whole-record navigation;
- quoted multiline fields;
- missing projected fields;
- unknown source records;
- document/code-page/content conflicts;
- unsupported code pages;
- whole-buffer byte-length mismatch;
- UTF-16 surrogate-boundary failure.

Native AOT smoke validates successful UTF-8 mapping and fail-closed byte-length mismatch after trimming/native compilation. Production Native AOT plugin publish additionally compiles the rendered-baseline host wiring.

## Safety boundary

Source navigation is not Apply. It never authorizes legacy encoding writes and does not change the accepted encoding-safe Apply policy. It also does not make inserted pending rows source-addressable before Apply.
