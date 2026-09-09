# User guide

## Reading CSV

Open **Visual Table** from the plugin menu. The active Notepad++ buffer is the source, including unsaved text. Choose a delimiter or use automatic detection; choose whether the first row contains headers. Quoted delimiters, doubled quotes and embedded line breaks are supported. The Diagnostics tab reports malformed input.

**Show spaces** displays markers without changing cell values. **Source** selects the corresponding field in Notepad++. New rows have no source position until applied, and navigation is blocked when the source has changed.

## Search and data inspection

The search box searches all columns or a selected column. Previous/next moves between matching cells; the counter counts cells, not occurrences within a cell.

**Filter and sort** supports up to eight column conditions combined with ALL or ANY, plus three sort priorities. Text conditions are literal and optionally case-sensitive. Empty means zero characters; whitespace-only means a non-empty value containing only whitespace. Numeric conditions use exact dot-decimal notation, for example `-12.5`; grouping, decimal commas and exponents are not interpreted as numbers. Invalid numeric values sort last in either direction. Equal sort keys retain source order.

**Preview** shows the matching row count. **Apply view** changes only the view, not the CSV. **Cancel** retains the existing view. **Reset rules** changes the dialog draft; **Reset view** clears search, conditions and sorting. Quick search intersects with active conditions.

**Column summary** reports statistics for the currently visible rows: empty, whitespace-only, distinct and repeated values, numeric range, maximum length and the twenty most frequent values. Distinct values are case-sensitive. Repeated values in one column do not necessarily indicate duplicate records.

## Editing

Enter **Edit** to change cells, add or delete rows, cut/paste rectangles, or open **Transform**. Entering Edit restores the full displayed table in source order and disables view filters. Complete-row selection supports ordinary, Ctrl and Shift gestures. Presentation columns are excluded from clipboard data and serialization.

**Transform** previews literal replacement, outer-whitespace trimming or invariant case conversion. Accepting a preview updates pending grid edits only. **Revert All** discards pending changes. **Apply** checks that the active document and encoding have not changed, then writes through Notepad++ in a single undo transaction. Use Notepad++ to save the file.

Apply supports UTF-8 and Windows-1250 and rejects unrepresentable text rather than substituting characters. Other readable encodings do not imply write permission. Refreshing or changing delimiter/header interpretation resets schema-dependent view rules.

## Limits

The plugin limits a snapshot to 64 MiB and the displayed table to 10,000 rows, 512 columns and 250,000 cells. Filtering and summaries cover this displayed projection, not undisplayed rows. Large cell previews are shortened for rendering; source values are not truncated. Clipboard input is bounded to prevent excessive allocation.
