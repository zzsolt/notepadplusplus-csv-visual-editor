# Milestone 0.9 visual polish — centered row numbers with mode-aware width

## Owner evidence

The synchronized checkbox and complete-row selection behavior passed owner testing in Notepad++ 8.9.7 x64. The owner then requested centered logical-record numbers in the left row headers.

The first centering package kept the existing 112-pixel structural-edit width in read-only mode. Centering the number inside that full width placed it far from the current-row arrow and created a large empty band before the first CSV column. The owner correctly rejected that package.

The supplied screenshots were used only as transient acceptance evidence and were not committed.

## Corrected change

`CsvGridRowHeaderBehavior.ConfigureRowHeaders` now applies horizontal and vertical centering together with mode-aware width:

```csharp
grid.RowHeadersWidth = grid.ReadOnly
    ? CompactRowHeaderWidth
    : PreferredRowHeaderWidth;
grid.RowHeadersDefaultCellStyle.Alignment =
    DataGridViewContentAlignment.MiddleCenter;
```

Widths:

- read-only table: 64 pixels, keeping the arrow and ordinary logical-record numbers visually compact;
- Edit mode: 112 pixels, retaining space for structural labels such as `new:n *`;
- leaving Edit mode returns immediately to the compact width;
- reconstruction reapplies the width matching the current mode.

The correction retains:

- complete-row checkbox and row-header selection synchronization;
- Ctrl/Shift row-header gestures;
- stable `CsvEditRowId` deletion authority;
- presentation-only selector state;
- unchanged CSV serialization and Apply safety.

## Automated regression

The Native AOT row-header smoke verifies:

- 64-pixel compact width in read-only mode;
- `MiddleCenter` alignment in read-only and Edit modes;
- automatic expansion to 112 pixels when Edit mode begins;
- selector appearance in Edit mode;
- automatic return to 64 pixels and selector removal when Edit mode ends;
- compact width and centering after table reconstruction.

## Targeted host check

1. Open the synthetic five-row CSV outside Edit mode.
2. Confirm the row-header area is compact, with the current-row arrow and numbers `2`–`6` visually close together.
3. Confirm the numbers are centered inside the compact row-header cells and the first CSV column is not pushed unnecessarily to the right.
4. Enter Edit mode and confirm the row header expands for structural labels while centering remains.
5. Confirm checkbox, plain/Ctrl/Shift row selection, Delete button behavior, and `new:n *` visibility remain unchanged.
6. Exit Edit mode and confirm the row header returns to compact width.
