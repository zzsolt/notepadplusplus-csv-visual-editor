# Milestone 0.9 row presentation audit — native glyph decoupling

## Status

The synchronized checkbox, complete-row, Ctrl, Shift, Delete, and Revert behavior is already owner-accepted. The remaining owner gate is visual validation in the real Notepad++ 8.9.7 x64 docked host.

The fixed `64px` read-only / `112px` Edit-mode row-header policy was rejected after owner screenshots showed two independent problems:

- the native current-row arrow and logical-record number still occupied one row-header cell, so the active label appeared optically displaced from inactive labels;
- Edit mode permanently reserved enough width for the longest structural label even when every visible row was an ordinary source row.

Screenshots remain transient evidence and are not committed.

## Root-cause audit

The WinForms `DataGridViewRowHeaderCell` does not lay out a row number in a neutral rectangle.

Its native paint path:

1. subtracts border, cell padding, and visual-style theme margins;
2. gives the current-row arrow, pencil, new-row star, or arrow-star first priority;
3. reserves the glyph lane whenever the cell is large enough, including for rows where no glyph is painted;
4. applies additional fixed text margins;
5. optionally reserves a row-error icon lane;
6. applies `DataGridViewContentAlignment` only inside the remaining text rectangle.

Consequences:

- `MiddleCenter` does not center the label in the full visible row-header band;
- a wider fixed cell increases the optical separation between the native glyph and the label;
- padding cannot cancel the private glyph reservation safely;
- a width that looks acceptable at one DPI/theme is not a durable contract;
- structural labels and current-row glyphs compete for one framework-owned cell.

The plugin does not use `DataGridViewRow.ErrorText`, so reserving row-error capacity has no product value.

## Alternatives evaluated

### Keep text in the native row header and choose another fixed width — rejected

This changes only the symptom. It cannot remove the native glyph lane, does not adapt to actual structural labels, and repeats the 64/112 failure at other DPI/theme/font combinations.

### Keep native text and use only `AutoSizeToAllHeaders` — rejected as the complete solution

Native auto-sizing is safer than fixed pixels, but the current-row glyph and the number still share one cell and one optical group. It therefore cannot guarantee that the active number reads as one aligned numeric column independent of the glyph.

### Custom-paint the native row-header text — rejected

A custom painter could ask WinForms to paint the background/border/glyph and then draw text separately. It would still have to duplicate or infer private theme margins, glyph geometry, RTL behavior, clipping, high-contrast colors, editing/new-row icon states, and text accessibility semantics. That is a brittle dependency on internal WinForms paint behavior and a poor Native AOT/host-maintenance tradeoff.

### One compact native width in both modes — rejected

It avoids a mode jump but cannot display `new:n *` and dirty markers without clipping, truncation, or moving state somewhere else. It also leaves the active-number/glyph coupling intact.

### Dedicated row-number/state column beside a glyph-only native row header — selected

This cleanly separates two responsibilities that WinForms otherwise combines:

- the native row header remains the framework-owned current-row glyph lane and whole-row hit target;
- a normal read-only `DataGridViewTextBoxColumn` renders logical-record numbers and structural labels as one aligned vertical column.

## Selected design

`CsvGridRowPresentation` owns the layout policy:

- native row-header cells contain no logical-record text;
- `RowHeadersWidthSizeMode` is `AutoSizeToAllHeaders`, so WinForms sizes its own glyph/theme/DPI lane;
- `ShowEditingIcon` remains enabled, preserving the native current-row arrow/pencil indication;
- `ShowRowErrors` is disabled because this plugin never assigns row-level error text;
- the `#` row-indicator column is read-only, frozen, centered, non-sortable, and visually first;
- it is physically appended after every CSV data column, so existing CSV column indexes remain unchanged;
- its width is measured from the labels that actually exist after each render, with only a DPI-scaled compact minimum;
- ordinary source rows therefore use the same compact number column in read-only and Edit modes;
- dirty source rows add `*`; inserted rows use `new:n *`; explanatory text is also available as a cell tooltip;
- structural text can expand only the indicator column and never the native glyph lane;
- removing/reverting structural labels lets the indicator shrink again;
- the Edit-only **Select** column remains visually rightmost and physically after all presentation/data columns.

The number cell is also a separate row-selection hit target. In Edit mode it forwards plain/Ctrl/Shift intent to the existing plugin-owned stable-ID selection state. The narrow native row header continues to support the same gestures. A normal CSV data-cell click still clears complete-row selection.

## DPI, theme, and accessibility

- Native glyph, theme-margin, RTL, high-contrast, and editing-icon painting remains entirely owned by WinForms.
- The native header width is framework auto-sized rather than set to a fixed device-pixel constant.
- The indicator minimum is scaled from logical pixels through the control's current `DeviceDpi`; content measurement uses normal DataGridView/TextRenderer behavior.
- DPI and font changes trigger a presentation refresh.
- Light/dark mode changes retain inherited grid colors; the indicator column does not hard-code foreground or background colors.
- Logical-record/state text is a normal accessible cell value with a descriptive tooltip.
- The native row header remains a native accessible row-selection target.

## Automated regression coverage

The Native AOT WinForms smoke now verifies:

- native row headers are glyph-only and contain no logical-record values;
- native width uses `AutoSizeToAllHeaders` and remains unchanged when entering Edit mode;
- current-row editing indication remains enabled and unused row-error capacity is disabled;
- the indicator is physically appended, visually first, frozen, centered, read-only, and theme-inheriting;
- CSV data columns retain their original physical indexes;
- read-only indicator clicks select complete rows;
- stable row-header selection still feeds `CsvGridSelectionSnapshot`;
- the selector appears only in Edit mode and remains visually rightmost;
- structural labels expand only the content-driven indicator column;
- removing a structural label releases unnecessary width;
- dark-mode refresh and table reconstruction preserve the policy;
- checkbox/full-row/Ctrl/Shift/ordinary-cell/Delete/Revert Native AOT paths continue to execute with the new presentation column.

## Owner host gate still required

Automated checks can prove the architecture and invariants, but they cannot certify the final docked-host appearance. The change must remain described as **host-validation pending** until the owner repeats the focused Notepad++ 8.9.7 x64 test in `docs/host-validation-0.9.md` and confirms the screenshots visually.
