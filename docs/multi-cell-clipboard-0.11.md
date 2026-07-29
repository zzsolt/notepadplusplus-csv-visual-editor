# Milestone 0.11 — Multi-cell selection, copy, and paste

## Goal

Add spreadsheet-style rectangular cell selection and clipboard operations without weakening the stable-row identity, encoding-safe Apply, conflict, undo, deletion, and Revert guarantees accepted through milestone 0.10.

## Initial scope

- rectangular selection over real CSV data cells;
- Shift extension from a stable cell anchor;
- Ctrl+C export as tab-separated rows with CRLF clipboard line endings;
- Ctrl+V import from tab-separated or single-cell clipboard text;
- deterministic broadcasting of a single clipboard value over a selected rectangle;
- exact-size rectangular paste when clipboard and destination dimensions match;
- paste beginning at the current real CSV cell when no rectangle is active;
- atomic validation before mutating the edit model;
- one pending edit-model transaction, followed by the existing conflict-checked and encoding-safe Apply path;
- no interaction with presentation-only `#` or `Select` columns.

## Explicitly out of scope for the first increment

- Excel-specific HTML or BIFF clipboard formats;
- automatic row or column creation during paste;
- discontiguous cell ranges;
- formulas;
- column-header copy/paste;
- direct Scintilla writes during clipboard operations;
- changing the complete-row deletion authority.

## Identity and mapping rules

A selected cell is identified by:

```text
CsvEditRowId + physical CSV column index
```

The following are presentation details and must never be persistent identity:

- DataGridView row index;
- display index;
- sorted visible position;
- DataGridView cell object;
- `SelectedCells` enumeration order;
- the `#` indicator column;
- the `Select` checkbox column.

The selection controller resolves visible gestures into stable row IDs and physical CSV indexes at event time. All copy and paste plans are then host-independent.

## Rectangle model

```text
anchor: CsvCellAddress
active: CsvCellAddress
rows: ordered stable row IDs in current view order
columns: inclusive physical CSV index range
```

The controller may render a rectangle in the current view, but the edit plan stores stable addresses. Sorting or filtering invalidates and clears an active rectangle unless the entire stable address set can be reconstructed unambiguously.

## Clipboard serialization

Copy produces plain text:

- columns separated by `\t`;
- rows separated by `\r\n`;
- no CSV quoting is applied because this is a spreadsheet clipboard interchange format;
- embedded tabs and line breaks remain literal cell content only when the target clipboard API can preserve them unambiguously; otherwise the first increment rejects such copy with a visible safe message rather than emitting ambiguous data.

Paste parses clipboard text as a rectangular tabular matrix:

1. normalize CRLF/LF/CR row boundaries for parsing;
2. split rows, preserving empty trailing cells;
3. split each row on tabs, preserving empty cells;
4. require a rectangular matrix;
5. reject NUL and unsupported control characters;
6. build an immutable paste plan before changing any edit-model value.

## Paste shape policy

Allowed:

1. `1 x 1` clipboard into one cell;
2. `1 x 1` clipboard broadcast over an active rectangle;
3. exact clipboard rectangle into an equally sized active rectangle;
4. clipboard rectangle starting at the current cell when no rectangle is active, provided all destination cells already exist.

Rejected atomically:

- non-rectangular clipboard data;
- destination overflow;
- attempts to write presentation columns;
- attempts to write header-only or unavailable cells;
- partially valid matrices;
- any edit-model validation failure.

No partial paste is allowed.

## Interaction with complete-row selection

Cell rectangle and complete-row selection are mutually exclusive modes:

- clicking or extending a real CSV cell clears complete-row selection;
- selecting a row through native row header, `#`, or `Select` clears the cell rectangle;
- Delete Row remains enabled only for explicit complete-row selection;
- keyboard Delete on a cell rectangle is not implemented in the first increment;
- existing plain/Ctrl/Shift row-selection semantics remain unchanged.

## Apply and encoding safety

Clipboard paste modifies only the pending edit model. It does not write the Notepad++ buffer.

The existing Apply path remains authoritative:

```text
clipboard text
  -> validated paste matrix
  -> atomic edit-model update
  -> deterministic CSV preview
  -> document/code-page/content conflict checks
  -> strict full-replacement encoding round-trip
  -> one Scintilla replacement in one undo action
```

Supported legacy code pages may proceed only when the complete replacement text is strictly representable. Non-representable content is blocked before any editor call.

## Required tests

### Core

- rectangular matrix parsing with CRLF, LF, and CR;
- preservation of empty leading, middle, and trailing cells;
- rejection of ragged rows;
- 1x1 broadcast;
- exact-size paste;
- current-cell-origin paste;
- row/column overflow rejection;
- all-or-nothing edit-model mutation;
- stable row-ID mapping after view sort;
- presentation-column exclusion;
- conflict and encoding preflight regression;
- Windows-1250 representable and non-representable paste cases.

### UI/Native AOT smoke

- Shift rectangle extension;
- copy ordering by visible row and physical CSV column;
- paste into current cell and active rectangle;
- cell selection clears complete-row deletion context;
- row selection clears cell rectangle;
- `#` and `Select` never become clipboard data;
- existing batch Delete and Revert remain unchanged.

## Host acceptance

The real Notepad++ 8.9.7 x64 matrix must include:

- copy to and paste from Notepad, Excel, and LibreOffice Calc plain-text clipboard paths;
- UTF-8 and Windows-1250 documents;
- Hungarian accented content;
- unrepresentable character rejection in Windows-1250;
- sorting before copy/paste;
- one-step Apply undo/redo;
- Save and reopen;
- dock hide/show and reconstruction;
- no regression to complete-row selection and deletion.

## Branch and PR policy

```text
Public branch: agent/multi-cell-clipboard
Internal branch: agent/multi-cell-clipboard
Version target: 0.11.0-alpha
```

Both PRs remain draft until automated gates and owner host acceptance pass. Public merges first; internal continuity merges second after recording the actual public merge SHA.
