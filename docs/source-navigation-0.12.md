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

## Fresh-buffer safety

Source navigation is read-only, but stale offsets can still select the wrong source location. Therefore the implementation fails closed when source identity is not trustworthy.

In Edit mode the current active snapshot must still match the edit-session baseline for:

- document identity;
- Scintilla code page;
- complete content SHA-256.

In read-only mode the current buffer is parsed again using the active delimiter/header settings and the selected displayed row is compared against the fresh source record before navigation. A stale visual row is blocked and the user is told to Refresh.

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

`Source` is enabled only when the current visual row exposes a source identity.

- current real CSV cell: select that raw source field;
- current `#`/presentation cell: select the complete logical record;
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

Native AOT smoke validates successful UTF-8 mapping and fail-closed byte-length mismatch after trimming/native compilation.

## Safety boundary

Source navigation is not Apply. It never authorizes legacy encoding writes and does not change the accepted encoding-safe Apply policy. It also does not make inserted pending rows source-addressable before Apply.
