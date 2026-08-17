# 0.11 host feedback — clipboard accepted, spreadsheet UI follow-up

## Owner-observed host result

The latest real Notepad++ host test confirms that Excel-to-plugin rectangular Ctrl+V now works correctly when a CSV data cell is selected with a single click. This closes the click-state-dependent keyboard paste discrepancy from the preceding packages.

The same host test exposed a separate presentation issue:

- the explicit Copy/Cut/Paste toolbar commands were not visible;
- the existing top area was too dense and looked like a raw collection of controls rather than a spreadsheet command surface;
- the owner requested a more Excel-like, compact GUI while preserving the accepted editing behavior.

No source CSV values from the host test are recorded in this repository.

## UI correction

The clipboard toolbar attachment is now resilient to delayed Notepad++ docking hierarchy creation. It tries the already-created WinForms tree immediately and performs a bounded deferred retry after handle/visibility transitions instead of relying on one construction-time traversal.

The two existing tool strips are reorganized rather than increasing the dock height:

### Command row

```text
Paste  Cut  Copy  |  Edit  Add Row  Delete Row  |  Apply  Revert All                    changes
```

### Data/view row

```text
Refresh  |  Delimiter  |  Header  |  Search  In  Clear  |  Diagnostics
```

This keeps clipboard commands at a stable left-side location and separates mutating commands from parsing/search controls.

## Spreadsheet presentation adjustments

The table keeps its existing stable row identity, dedicated row number column, selection model, virtualization, edit model, conflict checks, encoding safety, and Notepad++ Apply/Save ownership. Presentation-only refinements include:

- slightly taller spreadsheet rows;
- compact cell padding;
- left-aligned padded column headers;
- centered native row headers;
- explicit cell/header grid borders;
- cell tooltips retained for truncated content;
- no new file-writing path and no direct clipboard mutation of the Notepad++ buffer.

## Acceptance boundary

The keyboard Ctrl+V host discrepancy is accepted by owner observation. The reorganized toolbar and visual polish require a new host package check before PR #11 is merged. Verify at minimum:

1. Paste/Cut/Copy are visible immediately after opening the dock.
2. Copy remains available in read-only table mode; Cut/Paste become available in Edit mode.
3. Excel rectangular Ctrl+V still works after one click.
4. Toolbar Paste produces the same result as Ctrl+V.
5. Toolbar Copy and Cut work for one cell and a rectangular selection.
6. Revert All restores pending Cut/Paste changes.
7. Existing row selection/Delete, Apply, undo/redo, UTF-8/Windows-1250, Save/reopen, search, sort, and large-table behavior do not regress.

Keep public and internal PR #11 draft and unmerged until this UI follow-up passes the real host check.
