# Milestone 0.9 host correction — explicit row selector

## Reported host behavior

Three separate implementations based on Ctrl/Shift row-header selection failed in real Notepad++ 8.9.7 x64. The docked WinForms host visibly highlighted cells, but the deletion command still received only the current row.

The failed approaches were:

1. reading `DataGridView.SelectedRows`;
2. promoting `SelectedCells` to complete rows after the host event;
3. maintaining a plugin-owned Ctrl/Shift row-header gesture state.

The owner retests showed that continuing to depend on row-header gesture timing was not a reliable product direction.

## Replacement design

Milestone 0.9 now uses an explicit **Select** column while Edit mode is active:

1. the column is appended after the real CSV data columns, so CSV column indexes remain unchanged;
2. clicking its checkbox-style cell toggles the row's stable `CsvEditRowId` in a plugin-owned set;
3. the visible checkbox is presentation only and does not modify CSV cell values;
4. `CsvGridSelectionSnapshot` reads the marked stable IDs in current structural order;
5. multiple marked rows are passed to the existing atomic batch-delete model;
6. structural rebuilds clear stale marks;
7. leaving Edit mode removes the selector column;
8. when no checkbox is marked, the accepted single-current-row **Delete Row** fallback remains available.

The selector does not depend on Ctrl, Shift, `SelectedRows`, `SelectedCells`, WinForms anchors, or row-header event ordering.

## Interaction rules

- enter Edit mode: a narrow **Select** column appears at the right side of the table;
- click a selector cell: the checkbox is toggled;
- mark several rows: the command changes to `Delete Rows (n)`;
- click a marked row again: only that mark is removed;
- click **Delete Rows (n)**: every marked stable row is deleted as one pending batch;
- no marked rows: **Delete Row** applies only to the current row;
- Revert All restores source rows and clears selector marks;
- Exit Edit removes the selector column.

## Safety properties retained

- stable `CsvEditRowId` values remain the only model identities;
- no DataGridView row, cell, visual index, or selection collection is retained;
- the complete target set is captured before mutation;
- core batch deletion remains fully prevalidated and atomic;
- the selector column is never serialized as CSV data;
- no editor call occurs before Apply;
- Revert All remains exact;
- Apply remains one whole-buffer replacement in one undo action;
- non-UTF-8 and conflict paths remain blocked.

## Automated regression

The Native AOT smoke verifies:

- the selector column appears only in editable mode and is appended after CSV columns;
- three non-adjacent stable rows can be marked independently from visual grid selection;
- clearing the DataGridView visual selection does not lose marked stable IDs;
- three marked rows are deleted as one atomic batch;
- exact Revert All;
- toggling a marked row removes only that target;
- no marked rows retain the single-current-row fallback;
- leaving editable mode removes the selector column.

## Required targeted host retest

Using the original synthetic five-row CSV:

- enter Edit mode and confirm a **Select** column appears;
- mark Alpha, Gamma, and Epsilon in that column;
- confirm the command reads `Delete Rows (3)`;
- delete and confirm only Beta and Delta remain;
- Revert All;
- mark Beta, Gamma, and Delta;
- confirm the command reads `Delete Rows (3)`;
- delete and confirm only Alpha and Epsilon remain;
- Revert All;
- exit Edit mode and confirm the **Select** column disappears.

The complete `docs/host-validation-0.9.md` matrix remains required after this targeted correction passes.
