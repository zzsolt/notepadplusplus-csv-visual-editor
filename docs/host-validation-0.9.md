# Milestone 0.9 host validation

## Target environment

- Notepad++ 8.9.7 x64
- Windows x64
- CSV Visual Editor 0.9.0-alpha
- UTF-8 test documents for successful Apply scenarios

Use synthetic data only. Confirm the SHA-256 values supplied with the test package before installation.

## Safety expectations

- Edit mode remains explicit and off by default.
- Ordinary data-cell clicks remain cell selections and are not deletion targets.
- The **Select** column, native left row-header lane, and adjacent `#` row-indicator column are synchronized views of one complete-row selection state.
- Checkbox clicks visually highlight complete rows.
- Plain/Ctrl/Shift gestures from either the native row header or `#` row-indicator cell check the matching selector boxes.
- Delete is disabled when no complete row is explicitly selected.
- Delete operates on stable row identities captured before mutation.
- Source rows are marked for deletion; inserted rows are cancelled.
- Revert All never changes the Notepad++ editor buffer.
- Apply changes only the active editor buffer.
- Successful Apply requires Scintilla code page 65001 (UTF-8).
- Conflicts and unsupported encodings perform zero replacement calls.
- One Ctrl+Z undoes the complete batch Apply and one Ctrl+Y redoes it.
- The plugin never saves directly to disk.


## Test 0 — focused row-layout acceptance gate

Use the five-row CSV from Test A and perform this section before any mutation test.

### Read-only mode

1. Open the visual table at the normal Notepad++ dock width.
2. Confirm the far-left native row-header lane is narrow and contains the current-row arrow but no number text.
3. Confirm the adjacent frozen `#` column contains logical-record numbers `2` through `6` as one visually aligned vertical column.
4. Move the current cell through all five rows. Confirm the arrow moves in the native lane while every number remains at exactly the same horizontal position as before; the active number must not shift relative to inactive numbers.
5. Confirm there is no excessive blank band before the first CSV data column.
6. Click both the narrow native row header and the number cell for different rows. Confirm each selects the complete row.
7. Sort Name descending and confirm the `#` values retain their original source logical-record numbers.

### Edit mode and structural state

8. Enter Edit mode without changing data. Confirm the native row-header lane and ordinary source-number column do not make a large mode-sized jump.
9. Confirm the **Select** column appears at the far right and all real CSV columns retain their order.
10. Change one source cell. Confirm that source row becomes `n *` in the `#` column without moving the current-row arrow into the number column.
11. Add two rows. Confirm their labels are `new:1 *` and `new:2 *`; only the `#` column grows as much as required for those actual labels.
12. Hover a dirty and inserted label and confirm its tooltip explains the pending state.
13. Revert All. Confirm the inserted labels disappear and the `#` column releases unnecessary width; the native glyph lane remains unchanged.
14. Exit and re-enter Edit mode. Confirm the compact ordinary-source layout is stable.

### Lifecycle, theme, and DPI

15. Hide and reopen the panel; confirm the same row/glyph separation.
16. Switch Notepad++ light/dark mode; confirm arrow, numbers, markers, selection, and tooltips remain readable.
17. At Windows 100%, 125%, and 150% scaling where available, reopen the panel and repeat steps 2–5. Confirm neither clipping nor an excessive permanent band appears.

**Acceptance boundary:** automated tests verify the separation policy, but this section remains pending until the owner confirms it in Notepad++ 8.9.7 x64. Do not describe the visual defect as finally resolved before that confirmation.

## Test A — controls, selector visibility, and ordinary cell behavior

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
3. Before Edit mode, confirm the grid is read-only, the `#` row-indicator column is visually first, and no **Select** column is visible.
4. Enter Edit mode.
5. Confirm a **Select** column appears after the real CSV columns.
6. Click a normal CSV data cell.
7. Confirm only that cell is selected, no selector box is checked, and **Delete Row** is disabled.
8. Confirm cell editing, Add Row, Apply, and Revert All retain their 0.8 behavior.

## Test B — checkbox selection and visual synchronization

1. Check Alpha in the **Select** column.
2. Confirm the complete Alpha row becomes highlighted.
3. Check Gamma and Epsilon.
4. Confirm exactly Alpha, Gamma, and Epsilon are highlighted as complete rows.
5. Confirm all three matching selector boxes are checked.
6. Confirm the Delete control reads `Delete Rows (3)`.
7. Click an ordinary Beta data cell.
8. Confirm every selector box clears, the full-row highlights disappear, only the Beta cell remains selected, and Delete becomes disabled.

## Test C — Ctrl non-adjacent row-header selection

1. Click the left row header of Alpha.
2. Hold Ctrl and click the left row headers of Gamma and Epsilon.
3. Confirm exactly Alpha, Gamma, and Epsilon are highlighted as complete rows.
4. Confirm their three selector boxes are checked.
5. Confirm the Delete control reads `Delete Rows (3)`.
6. Release Ctrl and select **Delete Rows (3)**.
7. Confirm Alpha, Gamma, and Epsilon disappear from the pending grid.
8. Confirm Beta and Delta remain in original source order.
9. Confirm the status reports three source rows marked for deletion.
10. Confirm no editor text changed before Apply.
11. Select Revert All and confirm all five source rows return with their original source numbers.

## Test D — Shift contiguous range

1. Enter Edit mode from the original Test A source.
2. Click the Beta row header.
3. Hold Shift and click the Delta row header.
4. Confirm Beta, Gamma, and Delta are selected as a contiguous complete-row range.
5. Confirm all three selector boxes are checked.
6. Confirm the button reads `Delete Rows (3)`.
7. Delete the selected rows.
8. Confirm Alpha and Epsilon remain.
9. Revert All and confirm exact source restoration.

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

4. Select Beta and Inserted-1 with their selector boxes.
5. Ctrl-click the row headers of Delta and Inserted-2.
6. Confirm all four matching boxes are checked and all four complete rows are highlighted.
7. Confirm the button reads `Delete Rows (4)`.
8. Delete the batch.
9. Confirm Beta and Delta are counted as source deletions.
10. Confirm both inserted rows are removed by cancelling their insertions.
11. Confirm the insertion count returns to zero and source deletion count becomes two.
12. Revert All and confirm the exact original five-row source returns with no inserted rows.

## Test F — no current-cell deletion fallback

1. Enter Edit mode with no complete row selected.
2. Click one ordinary cell in Gamma.
3. Confirm no selector box is checked.
4. Confirm **Delete Row** is disabled.
5. Pressing or clicking elsewhere in the cell area must not mark Gamma for deletion.
6. Click Gamma's left row header.
7. Confirm the Gamma checkbox becomes checked, the complete row is highlighted, and **Delete Row** becomes enabled.
8. Delete and confirm only Gamma is marked for deletion.
9. Revert All.

## Test G — batch Apply, undo, redo, and Save

1. Enter Edit mode.
2. Change Alpha Department to `Editors`.
3. Add one inserted row after Beta:

```text
Inserted | Guests | true
```

4. Select Beta and Delta using either synchronized method.
5. Delete the two selected source rows.
6. Confirm the dirty summary reflects one cell edit, one surviving insertion, and two source deletions.
7. Select Apply.
8. Confirm the editor contains Alpha with `Editors`, omits Beta and Delta, and contains the inserted row.
9. Confirm Notepad++ shows its modified-document marker.
10. Press Ctrl+Z once and confirm the complete original CSV returns in one step.
11. Press Ctrl+Y once and confirm the complete combined change returns in one step.
12. Save normally through Notepad++ and confirm the modified marker clears.

## Test H — first, last, and all rows

### First and last

1. Select Alpha and Epsilon with Ctrl+row-header clicks or selector boxes.
2. Confirm both methods show the same two checked/highlighted rows.
3. Delete and Apply.
4. Confirm only the middle three source records remain.
5. Confirm the original terminal-newline state is preserved.
6. Undo once and confirm exact restoration.

### All data rows

1. Click the Alpha row header and Shift-click Epsilon.
2. Confirm every data-row checkbox is checked.
3. Delete all selected rows.
4. Confirm the header remains and zero data rows are pending.
5. Apply.
6. Confirm the editor contains only the header with the original terminal-newline state.
7. Undo once and confirm the exact original source.

## Test I — sorting and stable identity

1. Outside Edit mode, sort Name descending.
2. Confirm the `#` row-indicator column retains original source logical-record numbers.
3. Enter Edit mode and confirm source order is restored.
4. Select source rows Beta and Delta using row headers and verify their boxes.
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

1. Select non-adjacent rows containing multiline and Unicode values.
2. Confirm each selected logical record has one checked selector and one complete-row highlight.
3. Delete them as one batch.
4. Edit a padded cell in a surviving short row.
5. Apply.
6. Confirm surviving raw records retain their values and extra/trailing fields.
7. Confirm the multiline record is either fully present or fully deleted as one logical record.
8. Confirm Unicode remains intact.
9. Undo once and confirm exact source restoration.

## Test K — mixed EOL and terminal-newline preservation

Create controlled UTF-8 files with:

- CRLF only;
- LF only;
- mixed CRLF/LF/CR;
- with terminal newline;
- without terminal newline.

For each variant:

1. delete adjacent explicitly selected rows;
2. undo;
3. delete non-adjacent first and last rows;
4. Apply;
5. confirm surviving source separators follow the accepted minimal-difference policy;
6. confirm the original terminal-newline state is preserved;
7. confirm one Ctrl+Z restores the exact original editor text.

## Test L — content and document conflicts

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

## Test M — non-UTF-8 refusal

1. Convert a synthetic CSV in Notepad++ to ANSI/Windows-1252.
2. Enter Edit mode and explicitly select multiple rows.
3. Delete the batch.
4. Select Apply.
5. Confirm the UTF-8/65001 warning appears.
6. Confirm editor and disk content remain unchanged.
7. Confirm the pending session remains available for Revert All.

## Test N — lifecycle, synchronization reset, and dark mode

While a dirty batch session exists:

- hide and reopen the panel and confirm the pending structural session remains;
- confirm no automatic Apply occurs;
- confirm global Refresh is refused;
- confirm Edit mode cannot exit without Apply/Revert All;
- switch light/dark mode and confirm checkboxes, selected rows, current cells, native glyph lane, `#` labels, counters, and buttons remain readable;
- after a deletion rebuild, confirm deleted row marks disappear and the neighboring current row is deterministic;
- after an ordinary cell click, confirm row marks and checkbox checks clear;
- after Revert All or successful Apply, confirm subsequent checkbox, Ctrl, and Shift complete-row selections work normally;
- exit Edit mode and confirm the selector column disappears while the compact `#` row-indicator column remains.

## Acceptance result

Record PASS/FAIL for every section. Do not commit screenshots containing private paths, real addresses, credentials, or production CSV values.


## Owner acceptance — 2026-07-29

The owner reported that the final package was “tökéletes lett” in the target Notepad++ 8.9.7 x64 environment and explicitly authorized closing milestone 0.9 and starting milestone 0.10. This closes the previously pending native-glyph/row-label visual gate and confirms that the accepted checkbox, complete-row, Ctrl, Shift, Delete, batch deletion, and Revert behavior did not regress in the accepted package.

The final automated evidence remains CI run `30387337728`, job `90369811550`, with 168/168 xUnit tests, Native AOT runtime smoke, win-x64 publish, and packaging passing. The owner did not provide a separate item-by-item A–N transcript; the milestone closure records the owner-level final-package acceptance and explicit merge authorization rather than inventing individual test observations.
