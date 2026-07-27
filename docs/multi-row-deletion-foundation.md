# Milestone 0.9 multi-row deletion foundation

## Scope

Milestone 0.9 extends the accepted 0.8 structural row model so users can explicitly select multiple CSV data rows and delete them as one pending structural operation.

The phase does not add another editor-write path. It reuses stable `CsvEditRowId` identities, deterministic structural serialization, fresh-buffer conflict checks, and the existing one-undo Apply coordinator.

## User behavior

- ordinary cell clicks continue to select/edit individual cells;
- entering Edit mode appends a checkbox-style **Select** column after the real CSV columns;
- clicking a selector cell toggles that stable row and highlights the complete row;
- clicking a left row header selects the complete row and checks its selector box;
- Ctrl+row-header toggles non-adjacent rows;
- Shift+row-header selects a contiguous range from a stable anchor;
- checkbox and row-header interactions always display the same complete-row selection;
- clicking an ordinary CSV data cell clears complete-row selection;
- Delete is disabled unless at least one complete row is explicitly selected;
- Delete acts on every selected row;
- the status area reports how many source rows and inserted rows are pending deletion/cancellation;
- Revert All restores the exact original structural state;
- Apply writes all pending changes as one undoable editor-buffer replacement;
- leaving Edit mode removes the selector column and clears selection.

## Why the earlier row-header attempts failed

Three earlier interaction designs passed automated tests but failed owner testing in the real Notepad++ 8.9.7 x64 docked WinForms host:

1. direct `SelectedRows` capture;
2. post-event `SelectedCells` promotion;
3. a separate plugin-owned row-header gesture state that was not synchronized with the later working selector UI.

The failure was not the concept of row-header selection itself; it was deriving deletion intent from transient WinForms collections or keeping row-header intent separate from the visible working state. The current design makes checkbox and row-header input write to one stable-ID model, then renders DataGridView highlighting and checkboxes from that model.

## Identity and atomicity

- DataGridView indexes and UI objects are presentation state only.
- Complete-row selection retains only stable `CsvEditRowId` values plus one stable Shift anchor.
- Selector and row-header gestures mutate the same selection state.
- Before mutation, selected IDs are copied in current structural order.
- Selection enumeration order must not influence output.
- Validation occurs for the complete ID set before any row is mutated.
- A failed active-cell commit or invalid selected identity results in zero structural changes.
- Source rows are marked for deletion and remain restorable.
- Selected inserted rows are removed by cancelling those insertions.
- Mixed source/inserted selections are processed atomically.
- The selector column is presentation-only and is never serialized.
- The selector is appended after all real CSV columns so CSV data-column indexes remain unchanged.
- Ordinary current-cell focus is never promoted to a deletion target.

## Serialization

Batch deletion reuses the accepted `CsvRowEditModel` preview rules:

- unchanged surviving source records retain exact raw text;
- deleted source records are omitted;
- cancelled inserted rows are omitted;
- mixed CRLF/LF separators remain deterministic;
- original terminal-newline state remains independent and preserved;
- delete-first, delete-middle, delete-last, delete-all, adjacent, and non-adjacent selected rows are covered explicitly.

## Apply safety

- document, code-page, and content conflicts continue to expose no replacement payload and call no editor method;
- successful Apply remains one whole-buffer replacement inside one Scintilla undo action;
- Notepad++ remains responsible for the modified marker and Save;
- non-UTF-8 Apply remains blocked;
- no direct disk write or automatic Apply is introduced.

## Technical baseline

The implementation uses an edit-only `DataGridViewCheckBoxColumn` as a visible selector. Checkbox display is custom-painted; selector values are not part of row data and do not flow through the CSV cell-edit handler.

`CsvGridManagedRowSelection` owns the stable selected-ID set and Shift anchor. `CsvGridRowHeaderBehavior` translates selector clicks and row-header gestures into that state, then synchronizes complete-row highlighting and checkbox painting. `CsvGridSelectionSnapshot` returns only those explicit complete-row stable IDs. `SelectedRows`, `SelectedCells`, the current cell, and visual indexes are not deletion authority.

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

- selector appears only in editable mode and remains after CSV columns;
- checkbox selection visually highlights complete rows;
- plain and Ctrl row-header gestures synchronize selector state and full-row highlighting;
- Shift uses a stable anchor and structural display order;
- toggling removes only the intended complete-row target;
- ordinary cell context produces zero deletion targets;
- selector removal clears selection outside Edit mode;
- mixed structural preview and Apply execution remain intact;
- read-only row-header width remains compatible.

## Required Notepad++ host matrix

- visible selector appearance/removal with Edit mode;
- checkbox-to-full-row visual synchronization;
- row-header-to-checkbox synchronization;
- Ctrl non-adjacent and Shift contiguous complete-row selection;
- ordinary cell click clears complete-row selection and disables Delete;
- mixed source/inserted selection;
- dynamic `Delete Rows (n)` count;
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
