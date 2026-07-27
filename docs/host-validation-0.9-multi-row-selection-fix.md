# Milestone 0.9 host correction — synchronized complete-row selection

## Reported host behavior

Earlier Ctrl/Shift row-header implementations failed in real Notepad++ 8.9.7 x64 when deletion targeting depended on WinForms selection collections or on a row-header-only state that was not visibly synchronized with the working selector UI.

The failed approaches were:

1. reading `DataGridView.SelectedRows`;
2. promoting `SelectedCells` to complete rows after the host event;
3. maintaining a separate plugin-owned row-header gesture state while the visible grid selection remained independent.

The accepted explicit **Select** column proved that stable-ID marking and atomic deletion work correctly in the actual docked host. The owner then required the checkbox and row-header interactions to become two synchronized views of the same complete-row selection state.

## Current design

Milestone 0.9 now uses one plugin-owned stable-ID selection model shared by both interaction methods:

1. Edit mode appends a checkbox-style **Select** column after the real CSV columns;
2. clicking a checkbox toggles the row's stable `CsvEditRowId`;
3. clicking a left row header replaces the complete-row selection with that row;
4. Ctrl+row-header toggles non-adjacent rows;
5. Shift+row-header selects the contiguous range from the stable anchor;
6. checkbox changes visually highlight the complete selected rows;
7. row-header changes check or uncheck the matching selector boxes;
8. clicking an ordinary CSV data cell clears complete-row selection;
9. Delete is disabled unless at least one complete row is explicitly selected;
10. `CsvGridSelectionSnapshot` reads only the shared stable-ID state, never `SelectedRows`, `SelectedCells`, or the current cell;
11. the selector column remains presentation-only and is never serialized;
12. leaving Edit mode removes the selector column and clears selection state.

The DataGridView full-row highlight is visual feedback only. Stable `CsvEditRowId` values remain the authoritative deletion targets.

## Interaction rules

- checkbox click: toggle that row and synchronize complete-row highlighting;
- plain row-header click: select exactly that row and check its box;
- Ctrl+row-header click: toggle that row while retaining the other complete-row targets;
- Shift+row-header click: select the contiguous anchor-to-clicked range and check every box in the range;
- Ctrl+Shift+row-header click: add the contiguous range;
- ordinary data-cell click: clear every complete-row mark, leave only cell focus, and disable Delete;
- `Delete Row` / `Delete Rows (n)`: operate only on explicitly selected complete rows;
- no current-cell fallback exists;
- Revert All restores pending data changes without making cell focus a deletion target;
- Exit Edit removes the selector column.

## Safety properties retained

- stable `CsvEditRowId` values are the only persistent row identities;
- no DataGridView row, cell, visual index, or selection collection is retained;
- the complete target set is captured before mutation;
- core batch deletion remains fully prevalidated and atomic;
- the selector column is never serialized as CSV data;
- ordinary cell focus cannot accidentally enable row deletion;
- no editor call occurs before Apply;
- Revert All remains exact;
- Apply remains one whole-buffer replacement in one undo action;
- non-UTF-8 and conflict paths remain blocked.

## Automated regression

The Native AOT smoke verifies:

- the selector column appears only in editable mode and remains after all CSV columns;
- three checkbox-selected rows are also highlighted as complete rows;
- checkbox-selected stable IDs delete atomically and Revert exactly;
- plain and Ctrl row-header gestures synchronize stable IDs, checkboxes, and visual full-row selection;
- Shift selection uses a stable anchor and visible structural order;
- ordinary cell context produces zero deletion targets;
- leaving editable mode removes the selector column.

## Required targeted host retest

Using the synthetic five-row CSV:

1. enter Edit mode and confirm a **Select** column appears;
2. click the Alpha checkbox and confirm the whole Alpha row becomes highlighted;
3. click the Gamma and Epsilon checkboxes and confirm all three full rows are highlighted and the command reads `Delete Rows (3)`;
4. click an ordinary Beta data cell and confirm every checkbox clears, only the cell remains selected, and Delete becomes disabled;
5. click the Alpha row header, Ctrl-click Gamma and Epsilon row headers, and confirm all three checkboxes become checked and `Delete Rows (3)` appears;
6. delete and confirm only Beta and Delta remain;
7. Revert All;
8. click the Beta row header, Shift-click the Delta row header, and confirm Beta, Gamma, and Delta are highlighted with checked boxes;
9. confirm `Delete Rows (3)`, delete, and verify only Alpha and Epsilon remain;
10. Revert All;
11. exit Edit mode and confirm the **Select** column disappears.

The complete `docs/host-validation-0.9.md` matrix remains required after this targeted synchronized-selection test passes.
