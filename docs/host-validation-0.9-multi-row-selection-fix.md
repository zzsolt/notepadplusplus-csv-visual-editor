# Milestone 0.9 host correction — Ctrl/Shift batch deletion

## Reported host behavior

During Notepad++ 8.9.7 x64 acceptance, Ctrl and Shift row-header gestures visibly selected multiple intended rows, but **Delete Row** removed only the current/last row.

The observed WinForms state contained one selected cell per intended row while `DataGridView.SelectedRows` was empty. The deletion snapshot therefore used its documented current-row fallback instead of the intended batch.

## Correction

`CsvGridRowHeaderBehavior` now normalizes every modified row-header gesture before the deletion snapshot is read:

1. capture all transient row indexes represented by `SelectedRows` and `SelectedCells`;
2. restore `RowHeaderSelect` and `MultiSelect` policy defensively;
3. clear the transient selection;
4. mark every captured row as a complete selected row;
5. allow `CsvGridSelectionSnapshot` to copy the resulting rows to immutable stable `CsvEditRowId` targets.

The normalization runs only from a row-header click. Ordinary data-cell clicks retain cell selection and the existing single current-row fallback.

## Safety properties retained

- stable IDs remain the only model identities;
- the complete target set is captured before mutation;
- core batch deletion remains fully prevalidated and atomic;
- no editor call occurs before Apply;
- Revert All remains exact;
- Apply remains one whole-buffer replacement in one undo action;
- non-UTF-8 and conflict paths remain blocked.

## Automated regression

The Native AOT multi-row smoke now reproduces the real-host symptom by creating a cell-backed non-adjacent visual selection with an empty `SelectedRows` collection. It verifies that normalization produces two complete selected rows and that the stable-ID batch removes both intended source records.

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
