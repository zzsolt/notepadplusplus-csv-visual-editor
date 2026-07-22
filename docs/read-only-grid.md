# Read-only visual table

Milestone 0.4 connects the accepted live-buffer snapshot and CSV parser to the docked WinForms `DataGridView`.

## Toolbar controls

```text
Refresh | Delimiter: Auto detect / Comma / Semicolon / Tab | Header: First row is header / No header row
```

Changing either dropdown rebuilds the current table from the active Notepad++ editor buffer. Unsaved edits are therefore included.

## Delimiter behavior

### Auto detect

Automatic selection is used only when detection confidence is `Medium` or `High`.

When confidence is `Low`, ambiguous, or `None`, the plugin does not guess. The panel shows:

- document name;
- confidence;
- candidate scores;
- an instruction to choose comma, semicolon, or tab manually.

### Manual choice

A manual choice parses the current buffer with the selected delimiter even when automatic detection is weak. The choice changes only the view and never rewrites the document.

## Header behavior

### First row is header

The first logical CSV record supplies column display names and is excluded from displayed data rows.

Display-only cleanup:

- leading/trailing and repeated whitespace is normalized;
- line breaks become spaces;
- empty headers become `Column N`;
- duplicate headers receive ` (2)`, ` (3)`, and so on.

The underlying parsed first-record cell values remain unchanged.

### No header row

Every logical CSV record remains a data row. Columns are named `Column 1` through `Column N`.

## Rows and columns

- DataGridView cells are read-only.
- Sorting is disabled to preserve source order.
- Row headers show the original one-based logical-record number.
- Quoted multiline fields remain one logical row.
- Shorter inconsistent records receive empty cells only in the rectangular display model.
- Parser records are not padded, truncated, or mutated.

## Diagnostics

The status line shows:

- active document name;
- displayed and total row counts;
- column count;
- delimiter and whether it was automatic or manual;
- automatic confidence when applicable;
- selected header mode;
- parser error and warning counts.

Malformed CSV may still produce a partial read-only table. Diagnostics are structural and do not contain source values.

## Current limits

- snapshot input limit: 64 MiB according to Scintilla editor-byte length;
- displayed data rows: 10,000;
- displayed columns: 512;
- aggregate displayed cells: 250,000.

The actual displayed row count is the smallest value allowed by the row limit and the aggregate cell budget. For example, a 100-column table can display at most 2,500 rows in this alpha.

Row/cell limiting is stated explicitly in the status bar. Column overflow, or a single row wider than the aggregate cell budget, refuses rendering and shows an error; columns are never silently hidden.

## Non-goals of milestone 0.4

- editing or write-back;
- automatic refresh after every keystroke;
- source navigation;
- sorting, filtering, or search;
- virtual/paged large-file rendering;
- encoding or line-ending serialization.
