# Milestone 0.8 row-header host fix

## Host feedback

The initial Notepad++ 8.9.7 x64 owner validation found that the structural row operations themselves worked, but two related row-header usability defects remained:

1. an inserted row label such as `new:1 *` was visually truncated to `new:`;
2. clicking the left row-header area did not select the complete row, while clicking a cell only moved the current-row arrow.

All other tested 0.8 behavior was reported as working.

## Root cause

- The table used a fixed 72-pixel row-header width, which did not leave enough room for the current-row glyph plus the complete synthetic row label.
- The table used `DataGridViewSelectionMode.CellSelect`, so the row header did not provide the expected spreadsheet-style full-row selection behavior.
- During form construction the CSV table temporarily displays a metadata/bootstrap state with hidden row headers, so the behavior must identify the table independently of current row-header visibility and reapply its policy after table reconstruction.

## Fix

A dedicated `CsvGridRowHeaderBehavior` now:

- identifies the CSV table through its unique clipboard policy even during the hidden bootstrap state;
- sets the preferred row-header width to 112 pixels;
- uses `DataGridViewSelectionMode.RowHeaderSelect` whenever the visual CSV table is active;
- keeps ordinary cell clicks as cell selections;
- makes a left click on the row header commit any active cell edit and select the complete row;
- reapplies width and selection policy after columns or rows are reconstructed.

## Automated evidence

```text
Public branch: agent/row-operations
Fix head: fa12a1e14b7ea36548ce08eed44e9b81cfe24e8c
CI: 30002502486 — PASS
Core xUnit: 159/159 PASS
Native AOT row-header bootstrap attachment: PASS
Native AOT preferred width: PASS
Native AOT RowHeaderSelect mode: PASS
Native AOT complete-row selection: PASS
Native AOT reconstruction reapplication: PASS
Full plugin Native AOT publish/package: PASS
```

## Targeted Notepad++ retest

1. Install the package built from the final documented fix head.
2. Open a UTF-8 CSV and enter Edit mode.
3. Add a row.
4. Confirm the complete marker is visible, for example:

```text
new:1 *
```

5. Click a normal data cell and confirm only that cell becomes the current selection.
6. Click the left row-header label/number and confirm every cell in that row is highlighted.
7. Confirm Delete Row acts on that selected row.
8. Add more rows and confirm `new:2 *`, `new:3 *`, and later labels remain readable.
9. Refresh/reopen the table outside Edit mode, re-enter Edit mode, and confirm row-header selection still works.
10. Check the same behavior in light and dark mode.

## Acceptance boundary

Keep public and internal PR #8 in draft until the owner confirms this targeted retest. After confirmation, record the complete 0.8 host acceptance and merge the public PR before the internal PR.
