# Notepad++ host validation — 0.12 source navigation

Target: real Notepad++ x64 host. Keep public/internal PR #12 draft and unmerged until owner acceptance.

## Latest owner report — 2026-09-06

The owner reported the supplied **F1 read-only stale-content blocking and Refresh recovery** protocol successful. This is a user-reported host PASS for that case. Exact Windows/Notepad++ version and installed DLL hash were not supplied with this report; do not infer new package provenance. F2–F4, exact Windows-1250 Source byte boundaries and fresh 0.11 regression remain unreported. The new Transform command has its own [host matrix](bulk-transforms-0.12.md), currently NOT RUN.

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
6. Apply the pending inserted row, refresh the table, and confirm `Source` is then available for the newly source-backed row.
7. Revert All from a separate pending-edit scenario; confirm navigation for source rows remains available.

## F. Stale/conflict blocking

Run each case independently from a freshly rendered disposable CSV. Keep the
panel open; do not reopen it or Refresh between the mutation and Source.
Before pressing Source, leave the editor caret at a visibly different position
and note the editor selection and modified marker. Source must not move that
selection, change text, or change the modified marker in a blocked case.

| ID | Action after rendering | Expected result |
|---|---|---|
| F1 | Read-only: insert one character directly in the Notepad++ buffer, then Source from the stale table. | Content-changed block; status requests Refresh. |
| F2 | Fresh table: enter Edit, make a pending grid edit and finish the in-cell edit; also change the Notepad++ buffer directly; then Source from a source-backed row. | Content-changed block; pending grid edit remains pending; no Apply. |
| F3 | Render document A, keep its table, activate a distinct document B, then Source. Identical synthetic text in A and B helps isolate identity from content. | Another-document block; no old offset used in B. |
| F4 | Render a UTF-8 ASCII-only CSV; change its actual editor encoding to Windows-1250 without refreshing the table; then Source. | Code-page-changed block, provided the host exposes a changed Scintilla code page. |

For F4, a menu action that leaves the underlying Scintilla code page unchanged
does not establish code-page-conflict coverage. Report the actual status and
selected encoding; mark the isolated check NOT VERIFIED if the precondition
cannot be established. Never treat UTF-8 versus UTF-8 BOM as proof of a code-page change.

After F1, Refresh and confirm Source works again against the changed buffer.
After F2, use Revert All to discard pending grid edits, leave Edit mode, then
Refresh before continuing. After F3, return to unchanged A and check Source
again. Record recovery separately from the blocking result.

## G. Windows-1250 navigation

Use this synthetic fixture, encoded as actual Windows-1250, with comma delimiter
and the first record as header:

```csv
id,text,target
1,áéíóöőúüűáéíóöőúüű,TARGET_1
2,őűáé,TARGET_2
```

1. Confirm the host reports Windows-1250 and all accents are readable; an ANSI
   label alone is insufficient to establish which Windows code page is in use.
2. Refresh the Visual Editor, then select the first data row's accented cell and
   press Source. Selection must include all 18 letters and neither adjacent comma.
3. Select `TARGET_1`, then `TARGET_2`, pressing Source for each. The selection must
   contain exactly the eight ASCII characters, without a comma or newline.
4. Select each complete data row via `#` and press Source. Selection must start at
   its ID, end after its target value, and exclude the following record separator.
5. Repeat cell and whole-row checks in Edit mode on source-backed rows without Apply.
6. Confirm Source itself leaves text and modified state unchanged.

Record read-only cell/row and Edit cell/row results separately. A visually correct
result from a UTF-8 buffer does not prove Windows-1250 byte positions. Windows-1250
Apply was already host-validated in the accepted encoding milestone; this Source
test does not withdraw that authorization or change strict lossless Apply preflight.

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

## Owner host evidence received — 2026-08-20 and 2026-08-25

The owner supplied real Notepad++ x64 screenshots and direct test results for the verified 0.12 package. Screenshots are not committed to the repository and no CSV values are copied into this record.

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
- complete-row Source navigation over a multiline logical record selects the whole logical record across the physical line break, excluding the following record separator;
- a pending inserted row has no source location before Apply: when that inserted row is current, the `Source` command is disabled rather than guessing a location;
- after Apply and Refresh create real source text for that row, Source navigation is available and works for the newly source-backed row.

Evidence boundary: these host results do not separately prove stale/conflict blocking, Windows-1250 byte positions, or a fresh full 0.11 regression matrix. Do not mark those items PASS unless separately exercised.

## Acceptance record

Record Windows version, Notepad++ version and x64 architecture, installed DLL
SHA-256 (or the verified package identity), actual encoding, case ID and
PASS/FAIL/NOT RUN. For failures, include only sanitized status text and whether
selection, text or pending edits changed. Do not include real CSV values or paths.

Previously recorded host PASS remains historical evidence for the tested package;
it is not a new host run for every documentation/CI commit. Stale/conflict cases,
Windows-1250 Source positions and a fresh 0.11 regression remain pending until
reported. Do not infer unperformed checks from screenshots or automated CI.
Merge PR #12 only after the owner explicitly accepts the package.
