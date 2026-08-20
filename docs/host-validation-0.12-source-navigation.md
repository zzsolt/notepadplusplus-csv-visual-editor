# Notepad++ host validation — 0.12 source navigation

Target: real Notepad++ x64 host. Keep public/internal PR #12 draft and unmerged until owner acceptance.

## Installation

Install the x64 package as:

```text
Notepad++/plugins/CsvVisualEditor/CsvVisualEditor.dll
```

Use disposable test CSV data only. Do not record real/private CSV values in issues or screenshots.

## A. Command surface

1. Open a normal UTF-8 CSV and open CSV Visual Editor.
2. Confirm the command row contains `Paste`, `Cut`, `Copy`, and `Source` before the Edit group.
3. Confirm the accepted 0.11 row/header/clipboard layout is otherwise unchanged.

## B. UTF-8 cell navigation

1. Use a UTF-8 CSV containing at least one multibyte character before the target cell (for example Hungarian accented text).
2. Click a real CSV data cell in the visual table.
3. Click `Source`.
4. Confirm Notepad++ selects exactly that raw CSV field in the editor.
5. Repeat with a quoted field and confirm the source selection includes the CSV quote characters.
6. Repeat with a quoted multiline field and confirm the complete raw field is selected across the physical line break.
7. Confirm document text and modified state do not change.

## C. Row-level navigation

1. Click the dedicated `#` cell for a row, or otherwise leave the current cell on a presentation column.
2. Click `Source`.
3. Confirm the complete logical CSV record is selected in Notepad++, excluding the record separator.
4. Repeat for first and last data records.

## D. Sort and filter stability

1. Sort by a CSV column so display order differs from source order.
2. Select a row now displayed in a different position and click `Source`.
3. Confirm navigation goes to the row's actual original source record, not the displayed row number.
4. Apply a search filter and repeat.
5. Clear sort/filter and confirm normal navigation still works.

## E. Edit mode

1. Enter Edit mode and select an unchanged source-backed cell.
2. Click `Source`; confirm the original source cell is selected in Notepad++.
3. Make a pending cell edit in the visual model without Apply; navigation should still target the original source location because the Notepad++ buffer remains unchanged.
4. Add a pending inserted row and make it current.
5. Confirm `Source` is disabled or safely reports that the row has no source location until Apply.
6. Revert All; confirm navigation for source rows remains available.

## F. Stale/conflict blocking

1. In read-only mode, change the Notepad++ buffer directly after the table was rendered, without Refresh.
2. Click `Source` on a stale visual row.
3. Confirm navigation is blocked and the status instructs the user to Refresh; no text changes occur.
4. Enter Edit mode from a fresh table, then modify the Notepad++ buffer externally.
5. Confirm `Source` is blocked by the edit-session content conflict.
6. Repeat by changing the active document and, where practical, the code page.

## G. Windows-1250 navigation

1. Open a Windows-1250 CSV containing representable Hungarian characters.
2. Navigate to cells before and after accented characters.
3. Confirm source selection boundaries are exact.
4. Confirm this navigation does not imply or enable an Apply write beyond the existing production policy.

## H. Existing behavior regression

Recheck the accepted high-value paths:

- Excel rectangular Ctrl+V after one click;
- Copy in read-only mode;
- Cut/Paste in Edit mode;
- row-header / `#` / Select complete-row selection;
- Delete Rows and Revert All;
- Apply and one-step undo/redo on the normal supported path;
- search/sort;
- large-table load/scroll remains usable.

## Owner host evidence received — 2026-08-20

The owner supplied real Notepad++ x64 screenshots for the verified 0.12 package. The screenshots are not committed to the repository and no CSV values are copied into this record.

Observed PASS behavior:

- `Source` is present on the spreadsheet command row;
- read-only data-cell navigation selects the corresponding raw source field in Notepad++;
- Edit-mode data-cell navigation selects the corresponding original source field;
- Edit-mode complete-row selection followed by `Source` selects the complete logical source record;
- read-only complete-row selection followed by `Source` selects the complete logical source record;
- after sorting so display order differs from source order, `Source` still selects the correct original source location;
- after applying a search filter, `Source` still selects the correct original source field rather than the displayed row position;
- an ordinary quoted field is selected using its complete raw CSV field span, including the surrounding quote characters;
- a quoted multiline field is selected as one raw source field across its physical line break;
- a field containing escaped CSV quote characters is selected using its complete raw CSV representation, not only its decoded display value;
- complete-row Source navigation over a multiline logical record selects the whole logical record across the physical line break, excluding the following record separator.

Evidence boundary: these screenshots do not separately prove pending inserted-row blocking, stale/conflict blocking, Windows-1250 byte positions, or a fresh full 0.11 regression matrix. Do not mark those items PASS unless separately exercised.

## Acceptance record

Record package hash and PASS/FAIL for the sections actually exercised. Do not infer unperformed checks from screenshots or automated CI. Merge PR #12 only after the owner explicitly accepts the package.
