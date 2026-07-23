# Milestone 0.9 host validation

## Target environment

- Notepad++ 8.9.7 x64
- Windows x64
- CSV Visual Editor 0.9.0-alpha
- UTF-8 test documents for successful Apply scenarios

Use synthetic data only. Confirm the SHA-256 values supplied with the test package before installation.

## Safety expectations

- Edit mode remains explicit and off by default.
- Ordinary cell clicks remain cell selections.
- Left row-header clicks select complete rows.
- Ctrl+row-header click selects or deselects non-adjacent rows.
- Shift+row-header click selects a contiguous range.
- Delete operates on stable row identities captured before mutation.
- Source rows are marked for deletion; inserted rows are cancelled.
- Revert All never changes the Notepad++ editor buffer.
- Apply changes only the active editor buffer.
- Successful Apply requires Scintilla code page 65001 (UTF-8).
- Conflicts and unsupported encodings perform zero replacement calls.
- One Ctrl+Z undoes the complete batch Apply and one Ctrl+Y redoes it.
- The plugin never saves directly to disk.

## Test A — controls and ordinary cell behavior

Use:

```csv
Name,Department,Enabled
Alpha,Users,true
Beta,Admins,true
Gamma,Users,false
Delta,Guests,true
Epsilon,Admins,false
```

1. Open the visual table.
2. Confirm the About dialog reports `0.9.0-alpha`.
3. Before Edit mode, confirm the grid is read-only.
4. Enter Edit mode.
5. Click a normal data cell.
6. Confirm only the cell is selected, not the complete row.
7. Confirm the Delete control reads **Delete Row** and targets the current stable row when no complete row is selected.
8. Confirm cell editing, Add Row, Apply, and Revert All retain their 0.8 behavior.

## Test B — Ctrl non-adjacent row selection

1. Click the left row header of Alpha.
2. Hold Ctrl and click the left row headers of Gamma and Epsilon.
3. Confirm exactly Alpha, Gamma, and Epsilon are highlighted as complete rows.
4. Confirm the Delete control reads `Delete Rows (3)`.
5. Release Ctrl and select **Delete Rows (3)**.
6. Confirm Alpha, Gamma, and Epsilon disappear from the pending grid.
7. Confirm Beta and Delta remain in original source order.
8. Confirm the status reports three source rows marked for deletion.
9. Confirm no editor text changed before Apply.
10. Select Revert All and confirm all five source rows return with their original source numbers.

## Test C — Shift contiguous range

1. Enter Edit mode from the original Test A source.
2. Click the Beta row header.
3. Hold Shift and click the Delta row header.
4. Confirm Beta, Gamma, and Delta are selected as a contiguous range.
5. Confirm the button reads `Delete Rows (3)`.
6. Delete the selected rows.
7. Confirm Alpha and Epsilon remain.
8. Revert All and confirm exact source restoration.

## Test D — mixed source and inserted rows

1. Enter Edit mode.
2. Add a new row after Alpha:

```text
Inserted-1 | Temporary | true
```

3. Add another new row after Delta:

```text
Inserted-2 | Temporary | false
```

4. Using Ctrl+row-header click, select Beta, Inserted-1, Delta, and Inserted-2.
5. Confirm four complete rows are selected and the button reads `Delete Rows (4)`.
6. Delete the batch.
7. Confirm Beta and Delta are counted as source deletions.
8. Confirm both inserted rows are removed by cancelling their insertions.
9. Confirm the insertion count returns to zero and source deletion count becomes two.
10. Revert All and confirm the exact original five-row source returns with no inserted rows.

## Test E — current-cell fallback

1. Enter Edit mode with no complete row selected.
2. Click one ordinary cell in Gamma.
3. Confirm the Delete control reads **Delete Row**.
4. Select Delete Row.
5. Confirm only Gamma is marked for deletion.
6. Confirm no previously selected row is accidentally included.
7. Revert All.

## Test F — batch Apply, undo, redo, and Save

1. Enter Edit mode.
2. Change Alpha Department to `Editors`.
3. Add one inserted row after Beta:

```text
Inserted | Guests | true
```

4. Select Beta and Delta with Ctrl+row-header click.
5. Delete the two selected source rows.
6. Confirm the dirty summary reflects one cell edit, one surviving insertion, and two source deletions.
7. Select Apply.
8. Confirm the editor contains Alpha with `Editors`, omits Beta and Delta, and contains the inserted row.
9. Confirm Notepad++ shows its modified-document marker.
10. Press Ctrl+Z once and confirm the complete original CSV returns in one step.
11. Press Ctrl+Y once and confirm the complete combined change returns in one step.
12. Save normally through Notepad++ and confirm the modified marker clears.

## Test G — first, last, and all rows

### First and last

1. Select Alpha and Epsilon using Ctrl.
2. Delete and Apply.
3. Confirm only the middle three source records remain.
4. Confirm the original terminal-newline state is preserved.
5. Undo once and confirm exact restoration.

### All data rows

1. Select the full range Alpha through Epsilon with Shift.
2. Delete all selected rows.
3. Confirm the header remains and zero data rows are pending.
4. Apply.
5. Confirm the editor contains only the header with the original terminal-newline state.
6. Undo once and confirm the exact original source.

## Test H — sorting and stable identity

1. Outside Edit mode, sort Name descending.
2. Confirm row headers retain original source logical-record numbers.
3. Enter Edit mode and confirm source order is restored.
4. Select source rows Beta and Delta using their row headers.
5. Delete and Apply.
6. Confirm exactly Beta and Delta were removed.
7. Undo once and confirm the exact original CSV.

## Test I — multiline, quotes, Unicode, and mixed widths

Use synthetic UTF-8 data containing:

- a quoted comma;
- doubled quotes;
- an actual line break inside one quoted field;
- `Budapest`, `東京`, and emoji;
- one short row and one row with an extra trailing field.

1. Select non-adjacent rows containing multiline and Unicode values.
2. Delete them as one batch.
3. Edit a padded cell in a surviving short row.
4. Apply.
5. Confirm surviving raw records retain their values and extra/trailing fields.
6. Confirm the multiline record is either fully present or fully deleted as one logical record.
7. Confirm Unicode remains intact.
8. Undo once and confirm exact source restoration.

## Test J — mixed EOL and terminal-newline preservation

Create controlled UTF-8 files with:

- CRLF only;
- LF only;
- mixed CRLF/LF/CR;
- with terminal newline;
- without terminal newline.

For each variant:

1. delete adjacent rows;
2. undo;
3. delete non-adjacent first and last rows;
4. Apply;
5. confirm surviving source separators follow the accepted minimal-difference policy;
6. confirm the original terminal-newline state is preserved;
7. confirm one Ctrl+Z restores the exact original editor text.

## Test K — content and document conflicts

### Content conflict

1. Start a dirty session containing a batch deletion.
2. Modify the same editor buffer directly.
3. Select Apply.
4. Confirm Apply is blocked and the direct editor change is not overwritten.
5. Confirm the pending batch session remains available for review or Revert All.

### Active-document conflict

1. Start a dirty batch session in document A.
2. Switch to document B.
3. Select Apply.
4. Confirm Apply is blocked and document B is unchanged.
5. Return to document A and Apply or Revert All.

## Test L — non-UTF-8 refusal

1. Convert a synthetic CSV in Notepad++ to ANSI/Windows-1252.
2. Enter Edit mode and select multiple rows.
3. Delete the batch.
4. Select Apply.
5. Confirm the UTF-8/65001 warning appears.
6. Confirm editor and disk content remain unchanged.
7. Confirm the pending session remains available for Revert All.

## Test M — lifecycle, selection reset, and dark mode

While a dirty batch session exists:

- hide and reopen the panel and confirm the pending structural session remains;
- confirm no automatic Apply occurs;
- confirm global Refresh is refused;
- confirm Edit mode cannot exit without Apply/Revert All;
- switch light/dark mode and confirm selected rows, current cell, row headers, counters, and buttons remain readable;
- after a deletion rebuild, confirm stale DataGridView rows are not retained and the neighboring current row is deterministic;
- after Revert All or successful Apply, confirm subsequent Ctrl/Shift selections work normally.

## Acceptance result

Record PASS/FAIL for every section. Do not commit screenshots containing private paths, real addresses, credentials, or production CSV values.
