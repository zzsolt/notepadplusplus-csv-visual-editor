# Milestone 0.9 multi-row deletion foundation

## Scope

Milestone 0.9 extends the accepted 0.8 structural row model so users can explicitly mark multiple CSV data rows and delete them as one pending structural operation.

The phase does not add another editor-write path. It reuses stable `CsvEditRowId` identities, deterministic structural serialization, fresh-buffer conflict checks, and the existing one-undo Apply coordinator.

## User behavior

- ordinary cell clicks continue to select/edit individual cells;
- entering Edit mode appends a checkbox-style **Select** column after the real CSV columns;
- clicking a selector cell marks or unmarks that stable row without editing CSV data;
- marked rows may be adjacent or non-adjacent and do not require Ctrl or Shift;
- Delete control acts on every marked row;
- when no selector mark exists, Delete falls back to the current stable row;
- the status area reports how many source rows and inserted rows are pending deletion/cancellation;
- Revert All restores the exact original structural state;
- Apply writes all pending changes as one undoable editor-buffer replacement;
- leaving Edit mode removes the selector column and clears marks.

## Why row-header Ctrl/Shift was rejected

Three row-header interaction designs passed automated tests but failed owner testing in the real Notepad++ 8.9.7 x64 docked WinForms host:

1. direct `SelectedRows` capture;
2. post-event `SelectedCells` promotion;
3. plugin-owned Ctrl/Shift row-header gesture state.

In each case, multiple Name cells appeared selected, the command remained `Delete Row`, and only the current/last row was deleted. Milestone 0.9 therefore does not depend on row-header Ctrl/Shift selection.

## Identity and atomicity

- DataGridView indexes and UI objects are presentation state only.
- Selector marks retain only stable `CsvEditRowId` values.
- Before mutation, marked IDs are copied in current structural order.
- Selection enumeration order must not influence output.
- Validation occurs for the complete ID set before any row is mutated.
- A failed active-cell commit or invalid marked identity results in zero structural changes.
- Source rows are marked for deletion and remain restorable.
- Marked inserted rows are removed by cancelling those insertions.
- Mixed source/inserted selections are processed atomically.
- The selector column is presentation-only and is never serialized.
- The selector is appended after all real CSV columns so CSV data-column indexes remain unchanged.

## Serialization

Batch deletion reuses the accepted `CsvRowEditModel` preview rules:

- unchanged surviving source records retain exact raw text;
- deleted source records are omitted;
- cancelled inserted rows are omitted;
- mixed CRLF/LF separators remain deterministic;
- original terminal-newline state remains independent and preserved;
- delete-first, delete-middle, delete-last, delete-all, adjacent, and non-adjacent marked rows are covered explicitly.

## Apply safety

- document, code-page, and content conflicts continue to expose no replacement payload and call no editor method;
- successful Apply remains one whole-buffer replacement inside one Scintilla undo action;
- Notepad++ remains responsible for the modified marker and Save;
- non-UTF-8 Apply remains blocked;
- no direct disk write or automatic Apply is introduced.

## Technical baseline

The implementation uses an edit-only `DataGridViewCheckBoxColumn` as an explicit selector. Checkbox display is custom-painted; selector values are not part of row data and do not flow through the CSV cell-edit handler. Marked rows are stored in a plugin-owned stable-ID set and converted to immutable deletion targets immediately before mutation.

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

- selector appears only in editable mode;
- selector is appended after CSV data columns;
- non-adjacent stable rows can be marked independently from visual grid selection;
- toggling removes only the intended mark;
- no-mark current-row fallback remains;
- selector removal clears marks outside Edit mode;
- mixed structural preview and Apply execution remain intact;
- read-only row-header width and ordinary single-row behavior remain compatible.

## Required Notepad++ host matrix

- visible selector appearance/removal with Edit mode;
- non-adjacent marked source rows;
- mixed source/inserted marks;
- dynamic `Delete Rows (n)` count;
- Delete Marked Rows and Revert All;
- no-mark current-row fallback;
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
- row-header Ctrl/Shift as the batch-selection mechanism;
- visual column-header editing;
- non-UTF-8 write support;
- source navigation;
- conflict merge/rebase;
- direct disk writes.
