# Milestone 0.9 multi-row deletion foundation

## Scope

Milestone 0.9 extends the accepted 0.8 row-header selection behavior so users can select multiple complete CSV data rows and delete them as one pending structural operation.

The phase does not add another editor-write path. It reuses stable `CsvEditRowId` identities, deterministic structural serialization, fresh-buffer conflict checks, and the existing one-undo Apply coordinator.

## User behavior

- ordinary cell clicks continue to select/edit individual cells;
- clicking a row header selects the complete row;
- Ctrl+row-header click adds or removes a non-adjacent row from the selection;
- Shift+row-header click selects a contiguous row range;
- Delete control acts on every selected complete row;
- when no complete row selection exists, Delete falls back only to the current stable row if that behavior remains unambiguous;
- the status area reports how many source rows and inserted rows are pending deletion/cancellation;
- Revert All restores the exact original structural state;
- Apply writes all pending changes as one undoable editor-buffer replacement.

## Identity and atomicity

- DataGridView indexes are presentation state only.
- Before mutation, the current selection is copied immediately into a deduplicated immutable set of `CsvEditRowId` values.
- Selection enumeration order must not influence output.
- Validation occurs for the complete ID set before any row is mutated.
- A failed active-cell commit or invalid selected identity results in zero structural changes.
- Source rows are marked for deletion and remain restorable.
- Selected inserted rows are removed by cancelling those insertions.
- Mixed source/inserted selections are processed atomically.

## Serialization

Batch deletion reuses the accepted `CsvRowEditModel` preview rules:

- unchanged surviving source records retain exact raw text;
- deleted source records are omitted;
- cancelled inserted rows are omitted;
- mixed CRLF/LF separators remain deterministic;
- original terminal-newline state remains independent and preserved;
- delete-first, delete-middle, delete-last, delete-all, adjacent, and non-adjacent selections are covered explicitly.

## Apply safety

- document, code-page, and content conflicts continue to expose no replacement payload and call no editor method;
- successful Apply remains one whole-buffer replacement inside one Scintilla undo action;
- Notepad++ remains responsible for the modified marker and Save;
- non-UTF-8 Apply remains blocked;
- no direct disk write or automatic Apply is introduced.

## Technical baseline

The implementation retains `DataGridViewSelectionMode.RowHeaderSelect` so cell clicks remain cell selections while row-header clicks select rows. Edit mode will enable `MultiSelect` for row-header selection. The current `SelectedRows` collection is treated only as a transient UI snapshot and is converted immediately to stable row IDs before mutation.

Pinned dependency remains:

```text
Npp.DotNet.Plugin 1.0.0-alpha.10
```

No dependency upgrade is part of milestone 0.9.

## Required automated matrix

### Core model

- one, adjacent, and non-adjacent source-row batches;
- only inserted rows;
- mixed source and inserted rows;
- first/middle/last/all data rows;
- duplicate ID normalization;
- selection order independence;
- invalid-ID atomic refusal;
- exact Revert All;
- mixed-EOL and terminal-newline preservation;
- changed-row and deletion counters.

### Fake host

- NoChanges produces zero calls;
- document/code-page/content conflicts produce zero calls;
- Ready uses exactly Begin/Replace/Selection/End;
- one replacement only;
- replacement failure still closes the undo action;
- selection restoration remains non-critical.

### Native AOT WinForms

- Ctrl and Shift multi-row selection with `RowHeaderSelect` and `MultiSelect=true`;
- stable ID extraction from selected rows;
- selection rebuild behavior;
- mixed structural preview and Apply execution.

## Required Notepad++ host matrix

- Ctrl selection of non-adjacent source rows;
- Shift selection of a contiguous range;
- mixed source/inserted selection;
- Delete Selected Rows and Revert All;
- Apply followed by one Ctrl+Z and one Ctrl+Y;
- normal Save ownership;
- sorting before Edit mode and source-order restoration;
- first/last/all-row deletion;
- quotes, multiline values, Unicode, mixed EOL, and terminal-newline preservation;
- content and active-document conflicts;
- non-UTF-8 refusal;
- panel reopen and light/dark mode.

## Out of scope

- row movement;
- keyboard Delete-key integration unless separately proven safe;
- visual column-header editing;
- non-UTF-8 write support;
- source navigation;
- conflict merge/rebase;
- direct disk writes.
