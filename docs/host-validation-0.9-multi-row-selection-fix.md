# Milestone 0.9 host correction — plugin-owned Ctrl/Shift selection

## Reported host behavior

During Notepad++ 8.9.7 x64 acceptance, Ctrl and Shift row-header gestures visibly selected multiple intended rows, but **Delete Row** removed only the current/last row.

The first correction attempted to promote the WinForms `SelectedCells` state to `SelectedRows`. The owner retest showed identical behavior, proving that the docked host selection collections and event timing cannot be treated as authoritative for this command.

## Final correction design

The plugin now owns row-header selection state independently from WinForms visual collections:

1. `CellMouseDown` identifies a left row-header gesture before the host completes its own selection handling;
2. the clicked row is read as a stable `CsvEditRowId`;
3. Ctrl toggles that stable ID in a plugin-owned set;
4. Shift computes a contiguous range from a plugin-owned stable anchor and the current structural display order;
5. the resulting stable-ID set is retained, never DataGridView row/cell objects;
6. a deferred visual update highlights the corresponding complete rows after host processing;
7. `CsvGridSelectionSnapshot` reads only the plugin-owned stable-ID set for explicit row-header selection;
8. a normal data-cell click clears the explicit row-header context and restores the documented current-row fallback.

The Delete command therefore no longer depends on `SelectedRows`, `SelectedCells`, WinForms selection anchors, or host event ordering.

## Interaction rules

- plain row-header click: select exactly that row and set the Shift anchor;
- Ctrl+row-header click: toggle that row without changing other selected rows;
- Shift+row-header click: replace selection with the contiguous anchor-to-clicked range;
- Ctrl+Shift+row-header click: add the contiguous range;
- Ctrl deselection of the final selected row creates an explicit empty row selection and does not silently delete `CurrentRow`;
- ordinary cell click: clear explicit row selection and use single current-row fallback.

## Safety properties retained

- stable IDs remain the only model identities;
- no DataGridView row, cell, visual index, or collection is retained;
- the complete target set is captured before mutation;
- core batch deletion remains fully prevalidated and atomic;
- no editor call occurs before Apply;
- Revert All remains exact;
- Apply remains one whole-buffer replacement in one undo action;
- non-UTF-8 and conflict paths remain blocked.

## Automated regression

The Native AOT smoke verifies:

- plugin-owned non-adjacent Ctrl selection survives a deliberately cleared DataGridView visual selection;
- three stable IDs are deleted as one batch;
- exact Revert All;
- Shift range selection from a stable anchor;
- explicit empty selection suppresses current-row fallback;
- ordinary cell context restores current-row fallback.

## Required targeted host retest

Using the original 0.9 synthetic five-row CSV:

- Ctrl-select Alpha, Gamma, and Epsilon by their left row headers;
- confirm the command reads `Delete Rows (3)`;
- delete and confirm only Beta and Delta remain;
- Revert All;
- Shift-select Beta through Delta;
- confirm the command reads `Delete Rows (3)`;
- delete and confirm only Alpha and Epsilon remain;
- Revert All.

The complete `docs/host-validation-0.9.md` matrix remains required after this targeted correction passes.
