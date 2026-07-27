# Milestone 0.9 visual polish — centered row numbers

## Owner evidence

The synchronized checkbox and complete-row selection behavior passed owner testing in Notepad++ 8.9.7 x64. The remaining visual issue was that source logical-record numbers in the left row-header cells were left aligned rather than centered.

The supplied screenshot was used only as transient acceptance evidence and was not committed.

## Change

`CsvGridRowHeaderBehavior.ConfigureRowHeaders` now applies:

```csharp
grid.RowHeadersDefaultCellStyle.Alignment =
    DataGridViewContentAlignment.MiddleCenter;
```

This centers row-header labels horizontally and vertically while retaining:

- the 112-pixel resizable row-header width;
- complete-row checkbox and row-header selection synchronization;
- Ctrl/Shift row-header gestures;
- stable `CsvEditRowId` deletion authority;
- presentation-only selector state;
- unchanged CSV serialization and Apply safety.

The same policy is reapplied when columns are reconstructed.

## Automated regression

The Native AOT row-header smoke verifies `MiddleCenter` alignment both after initial table construction and after row-header visibility/column reconstruction.

## Targeted host check

Open the synthetic five-row CSV in Edit mode and confirm that the visible source logical-record numbers (`2`, `3`, `4`, `5`, `6`) are centered in their left row-header cells. Also confirm that the current-row arrow, checkbox synchronization, Ctrl/Shift selection, and Delete button behavior remain unchanged.
