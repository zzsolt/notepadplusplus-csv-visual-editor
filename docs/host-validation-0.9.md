# Milestone 0.9 host validation

## Target environment

- Notepad++ 8.9.7 x64
- Windows x64
- CSV Visual Editor 0.9.0-alpha
- UTF-8 test documents for successful Apply scenarios

Use synthetic data only. Confirm the SHA-256 values supplied with the test package before installation.

## Safety expectations

- Edit mode remains explicit and off by default.
- Ordinary CSV cells remain directly editable only in Edit mode.
- Edit mode shows a checkbox-style **Select** column after all real CSV columns.
- Selector marks are not CSV values and are never serialized.
- Multiple rows may be marked without Ctrl or Shift.
- Delete operates on stable row identities captured before mutation.
- With no selector marks, Delete falls back to the current stable row.
- Source rows are marked for deletion; inserted rows are cancelled.
- Revert All never changes the Notepad++ editor buffer.
- Apply changes only the active editor buffer.
- Successful Apply requires Scintilla code page 65001 (UTF-8).
- Conflicts and unsupported encodings perform zero replacement calls.
- One Ctrl+Z undoes the complete batch Apply and one Ctrl+Y redoes it.
- The plugin never saves directly to disk.

## Test A — controls, selector lifecycle, and ordinary cell behavior

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
3. Before Edit mode, confirm the grid is read-only and there is no **Select** column.
4. Enter Edit mode.
5. Confirm a narrow **Select** column appears at the right side, after `Enabled`.
6. Click a normal data cell and confirm it can be edited normally.
7. Confirm the selector did not shift the CSV data columns or write a checkbox value into the document model.
8. With no selector marks, confirm the Delete control reads **Delete Row** and targets the current stable row.
9. Exit Edit mode after Revert All or Apply and confirm the **Select** column disappears.

## Test B — non-adjacent explicit marks

1. Enter Edit mode using the original Test A source.
2. Mark Alpha, Gamma, and Epsilon in the **Select** column.
3. Confirm exactly those three checkboxes are checked.
4. Confirm the Delete control reads `Delete Rows (3)`.
5. Click an ordinary cell in another row and confirm the three marks remain.
6. Select **Delete Rows (3)**.
7. Confirm Alpha, Gamma, and Epsilon disappear from the pending grid.
8. Confirm Beta and Delta remain in original source order.
9. Confirm the status reports three source rows marked for deletion.
10. Confirm no editor text changed before Apply.
11. Select Revert All and confirm all five source rows return with their original source numbers.
12. Confirm stale selector marks are cleared after Revert All.

## Test C — contiguous rows without Shift

1. Enter Edit mode from the original source.
2. Mark Beta, Gamma, and Delta using their selector cells.
3. Confirm the button reads `Delete Rows (3)`.
4. Delete the marked rows.
5. Confirm Alpha and Epsilon remain.
6. Revert All and confirm exact source restoration.
7. Confirm selector marks are cleared.

## Test D — mark and unmark

1. Enter Edit mode.
2. Mark Alpha, Gamma, and Epsilon.
3. Click Gamma's selector again.
4. Confirm only Alpha and Epsilon remain marked.
5. Confirm the button reads `Delete Rows (2)`.
6. Delete and confirm only Alpha and Epsilon are removed.
7. Revert All.

## Test E — mixed source and inserted rows

1. Enter Edit mode.
2. Add a new row after Alpha:

```text
Inserted-1 | Temporary | true
```

3. Add another new row after Delta:

```text
Inserted-2 | Temporary | false
```

4. Mark Beta, Inserted-1, Delta, and Inserted-2 in the selector column.
5. Confirm four rows are marked and the button reads `Delete Rows (4)`.
6. Delete the batch.
7. Confirm Beta and Delta are counted as source deletions.
8. Confirm both inserted rows are removed by cancelling their insertions.
9. Confirm insertion count returns to zero and source deletion count becomes two.
10. Revert All and confirm the exact original five-row source returns with no inserted rows or stale marks.

## Test F — current-cell fallback

1. Enter Edit mode with no selector checkbox marked.
2. Click one ordinary cell in Gamma.
3. Confirm the Delete control reads **Delete Row**.
4. Select Delete Row.
5. Confirm only Gamma is marked for deletion.
6. Confirm no previously marked row is accidentally included.
7. Revert All.

## Test G — batch Apply, undo, redo, and Save

1. Enter Edit mode.
2. Change Alpha Department to `Editors`.
3. Add one inserted row after Beta:

```text
Inserted | Guests | true
```

4. Mark Beta and Delta in the selector column.
5. Delete the two marked source rows.
6. Confirm the dirty summary reflects one cell edit, one surviving insertion, and two source deletions.
7. Select Apply.
8. Confirm the editor contains Alpha with `Editors`, omits Beta and Delta, and contains the inserted row.
9. Confirm Notepad++ shows its modified-document marker.
10. Press Ctrl+Z once and confirm the complete original CSV returns in one step.
11. Press Ctrl+Y once and confirm the complete combined change returns in one step.
12. Save normally through Notepad++ and confirm the modified marker clears.

## Test H — first, last, and all rows

### First and last

1. Mark Alpha and Epsilon.
2. Delete and Apply.
3. Confirm only the middle three source records remain.
4. Confirm the original terminal-newline state is preserved.
5. Undo once and confirm exact restoration.

### All data rows

1. Mark all five data rows.
2. Confirm the button reads `Delete Rows (5)`.
3. Delete all marked rows.
4. Confirm the header remains and zero data rows are pending.
5. Apply.
6. Confirm the editor contains only the header with the original terminal-newline state.
7. Undo once and confirm the exact original source.

## Test I — sorting and stable identity

1. Outside Edit mode, sort Name descending.
2. Confirm row headers retain original source logical-record numbers.
3. Enter Edit mode and confirm source order is restored.
4. Mark source rows Beta and Delta.
5. Delete and Apply.
6. Confirm exactly Beta and Delta were removed.
7. Undo once and confirm the exact original CSV.

## Test J — multiline, quotes, Unicode, and mixed widths

Use synthetic UTF-8 data containing:

- a quoted comma;
- doubled quotes;
- an actual line break inside one quoted field;
- `Budapest`, `東京`, and emoji;
- one short row and one row with an extra trailing field.

1. Mark non-adjacent rows containing multiline and Unicode values.
2. Delete them as one batch.
3. Edit a padded cell in a surviving short row.
4. Apply.
5. Confirm surviving raw records retain their values and extra/trailing fields.
6. Confirm the multiline record is either fully present or fully deleted as one logical record.
7. Confirm Unicode remains intact.
8. Undo once and confirm exact source restoration.

## Test K — mixed EOL and terminal-newline preservation

Create controlled UTF-8 files with:

- CRLF only;
- LF only;
- mixed CRLF/LF/CR;
- with terminal newline;
- without terminal newline.

For each variant:

1. mark and delete adjacent rows;
2. undo;
3. mark and delete non-adjacent first and last rows;
4. Apply;
5. confirm surviving source separators follow the accepted minimal-difference policy;
6. confirm the original terminal-newline state is preserved;
7. confirm one Ctrl+Z restores the exact original editor text.

## Test L — content and document conflicts

### Content conflict

1. Start a dirty session containing a marked batch deletion.
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

## Test M — non-UTF-8 refusal

1. Convert a synthetic CSV in Notepad++ to ANSI/Windows-1252.
2. Enter Edit mode and mark multiple rows.
3. Delete the batch.
4. Select Apply.
5. Confirm the UTF-8/65001 warning appears.
6. Confirm editor and disk content remain unchanged.
7. Confirm the pending session remains available for Revert All.

## Test N — lifecycle, mark reset, and dark mode

While a dirty batch session exists:

- hide and reopen the panel and confirm the pending structural session remains;
- confirm no automatic Apply occurs;
- confirm global Refresh is refused;
- confirm Edit mode cannot exit without Apply/Revert All;
- switch light/dark mode and confirm selector checkboxes, current cell, row headers, counters, and buttons remain readable;
- after a deletion rebuild, confirm stale DataGridView rows and stale selector marks are not retained;
- confirm the neighboring current row is deterministic;
- after Revert All or successful Apply, confirm the selector starts clean;
- after leaving Edit mode, confirm the selector column is removed.

## Acceptance result

Record PASS/FAIL for every section. Do not commit screenshots containing private paths, real addresses, credentials, or production CSV values.
