# 0.11 host feedback — clipboard accepted, spreadsheet UI follow-up

## Owner-observed host results

The real Notepad++ host confirms that Excel-to-plugin rectangular Ctrl+V works correctly when a CSV data cell is selected with a single click. The click-state-dependent keyboard paste discrepancy is accepted.

A subsequent package intended to add explicit Copy/Cut/Paste commands and spreadsheet-style presentation produced no visible UI change in the real host. The owner supplied a screenshot only as layout evidence. No CSV values from that screenshot are stored in this repository.

## Root cause of the invisible UI

The dynamic spreadsheet UI attachment searched for the primary table using both:

- `RowHeadersVisible == true`; and
- `SelectionMode == DataGridViewSelectionMode.CellSelect`.

That condition can never match the accepted table configuration because `CsvGridRowPresentation.ConfigureNativeRowHeaders` deliberately sets `SelectionMode = RowHeaderSelect` to preserve the complete-row selection behavior accepted in milestone 0.9.

The same UI helper also attempted to change the table back to `CellSelect` after attaching. Had the discovery succeeded, that would have risked regressing the accepted row-header interaction contract.

## Corrected UI attachment

The primary table is now identified independently from selection mode:

- it must be the plugin's `CsvDataGridView` type;
- native row headers must be visible;
- the diagnostics grid is excluded because its row headers are hidden;
- ordinary unrelated `DataGridView` controls are excluded.

Spreadsheet presentation no longer changes `SelectionMode`. The accepted `RowHeaderSelect` behavior remains authoritative.

The two existing tool strips are reorganized as:

### Command row

```text
Paste  Cut  Copy  |  Edit  Add Row  Delete Row  |  Apply  Revert All                    changes
```

### Data/view row

```text
Refresh  |  Delimiter  |  Header  |  Search  In  Clear  |  Diagnostics
```

Presentation-only refinements include compact padding, slightly taller rows, padded left-aligned headers, centered native row headers, explicit grid borders, and header/cell tooltips. No fixed color palette is introduced, preserving host theme compatibility.

## Regression guard

A Windows Native AOT smoke test now explicitly proves that spreadsheet UI discovery:

- accepts the real `RowHeaderSelect` primary table;
- does not depend on a particular selection mode;
- rejects the diagnostics grid;
- rejects an unrelated ordinary `DataGridView`.

## Final automated evidence for the correction

```text
Public implementation head:
eca1dc3c6a28393d9c54d62c42b2706af395886d

CI run: 32045980659 — PASS
CI job: 95433899510 — PASS
Core xUnit: 228/228 PASS
Errors: 0
Failed: 0
Skipped: 0
Not Run: 0
Time: 1.009s
Native AOT runtime smoke: PASS
win-x64 Native AOT plugin publish: PASS
Installable ZIP creation/upload: PASS

Artifact ID: 9292987832
Artifact name: CsvVisualEditor-0.11.0-alpha-win-x64
Outer artifact size: 7,778,356 bytes
Outer artifact SHA-256:
f9b69b6f351ac17e5097b61ddbbbbc18a0ff8e56594e4869cecfa7ae094ab9e6

Inner install ZIP size: 7,797,179 bytes
Inner install ZIP SHA-256:
6a352e49d9ba121ad25b3e2e469f67d61fca5a54274393652af1f7fc4e3275c4

CsvVisualEditor.dll size: 20,622,336 bytes
DLL SHA-256:
cc0109f8ad907343e5cdaf7bc34379e6e4e11ba721958738d6c9d7f4068dbed4
```

## Acceptance boundary

Single-click Excel Ctrl+V is owner-accepted. The corrected toolbar/GUI still requires a real-host check. Verify that:

1. Paste/Cut/Copy are visibly present immediately after opening the dock.
2. Copy is available in read-only mode; Cut/Paste become available in Edit mode.
3. Toolbar Paste matches the already accepted one-click Ctrl+V behavior.
4. Copy/Cut work for one cell and rectangular selections.
5. Native row-header / `#` / Select complete-row selection remains unchanged.
6. Revert All, Apply, one-step undo/redo, UTF-8/Windows-1250, Save/reopen, search/sort, and large-table behavior do not regress.

Keep public and internal PR #11 draft and unmerged until this corrected UI package passes the real-host check.
