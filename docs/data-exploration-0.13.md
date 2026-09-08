# 0.13 - Column filters, multi-sort and column summary

## Baseline

The owner accepted 0.12.6-alpha.357.1 on 2026-09-08 and explicitly authorized
milestone closure. Both PR #12 instances are merged. Accepted public main is
`c00a1a106fcd7bbb97e94e07cebb57de3854c954`; internal main is
`982e648c1121e5610be218ef9c37a0ca643af257`. Aggregate acceptance is not an
invented itemized result for previously unreported host cases.

This phase preserves the approved whitespace renderer, typography and inline
search. It adds view-only data tools, not another CSV editing model.

## Filter and sort

Open **View > Filter and sort** or its funnel icon. The native plugin menu also
provides **Filter and Sort** after a table is open.

**Filters:** Add up to eight conditions. Each chooses a physical CSV column,
an operator and a literal value. ALL requires every condition; ANY requires
at least one. An empty condition list includes every displayed row. Text
comparisons ignore case unless the Case checkbox is selected. Operands are
literal, including leading/trailing spaces; they are not regular expressions.
Contains, does-not-contain, equals, does-not-equal, starts-with and ends-with
are supported. Empty means exactly zero characters. Whitespace-only means a
non-empty value entirely made of whitespace, including tabs and line breaks.
Its opposite includes empty values. Numeric operators are =, >, >=, < and <=.

**Sorting:** Add up to three different column keys, from highest to lowest
priority. Choose Text or Number and Ascending or Descending for each. Text
sorting is ordinal and case-insensitive. Invalid/empty numeric values sort
last in either direction. Equal keys retain source order. Remove all sort
levels to restore source order. Clicking a main-grid header switches to the
existing single-column text sort while retaining the advanced filters.

Numbers use an explicit decimal dot, optional sign and optional surrounding
whitespace, for example `-12.5`. Grouping, decimal commas, currencies, exponents,
NaN and infinity are not interpreted as numbers. A value must fit exactly in
.NET decimal precision/range: overflow, underflow and silent rounding are
rejected rather than causing false equality. The original CSV text is never
reformatted or converted. A nonnumeric cell cannot satisfy a numeric predicate.

**Preview** reports the matching row count but leaves the active table alone.
**Apply view** validates and applies the draft; it does not perform the main
Edit/Apply source write. **Cancel** keeps the previous view. **Reset rules**
resets only the dialog draft until Apply view is clicked. Changing any option
invalidates the displayed preview. Validation errors are shown inside the dialog.

The existing quick search intersects with the advanced conditions. Its counter
still counts cells matching the quick-search text, not all condition matches.
**View > Reset view** clears quick search, column scope, conditions and sort keys.
Entering Edit restores the full source-order table and disables view tools;
refreshing the document or changing delimiter/header interpretation clears
schema-dependent rules. Rules are session-local, not saved as presets.

## Column summary

Select an ordinary data cell and open **View > Column summary** or its summary
icon. Choose another column in the dialog to recalculate. The summary shows:

- visible-row, exactly empty, whitespace-only and numeric counts;
- exact distinct values, repeated occurrences and the number of repeated values;
- minimum/maximum of exact numeric values and maximum UTF-16 value length;
- up to 20 most frequent values, with counts and empty/whitespace classification.

Distinct values are ordinal and case-sensitive, including empty values.
Repeated occurrences equal visible rows minus distinct values; this is a
single-column frequency statistic, not a claim that entire CSV records are
duplicates. Frequency ties use ordinal value order. The frequency table is
read-only and uses the accepted paint-only whitespace indicators.

## Scope and safety

Filtering, sorting and summaries operate on the existing bounded projection:
at most 10,000 data rows, 512 columns and 250,000 cells. A row-limited projection
is explicitly labelled, and the tool never silently claims to cover undisplayed
source rows. Stable source-record identity remains attached to each displayed
row for navigation and copying. Filtered-out rows are hidden, not deleted.

There is no source/file write, data normalization, persistence, new dependency
or license change in this phase. Existing UTF-8/Windows-1250 authorization,
strict encoding/conflict checks, one-undo Apply and Notepad++-owned Save remain.

## Regression coverage

Core checks cover ALL/ANY, every predicate, exact whitespace/case/Unicode,
quick-search composition, full values beyond the paint limit, exact numeric
precision, multi-key priority and deterministic ties, invalid-value placement,
immutable options, bounds and current-view column profiles.

Windows Native AOT checks the actual dialogs: preview/apply/cancel/reset,
invalid numeric rules, eight/three limits, column selection, resize alignment,
read-only summary, menu/icon command parity and light/dark controls. It also
measures filter + two-key sort + profile over 10,000 rows x 25 columns and
asserts the actual output. Timing is a runner observation, not a universal
performance guarantee. The accepted graphics/encoding/clipboard suites remain.

The exact production DLL is exercised in an isolated, checksum-pinned portable
Notepad++ host. The check opens both new native-menu commands, previews and
applies a filter, reads filtered/full summaries, verifies Cancel/Reset, compares
the Scintilla buffer before/after, and retains search/clear/docking/About checks.
Only a complete successful required gate can produce a candidate package.

Owner acceptance of 0.13 is separate from the accepted 0.12 baseline. Real
keyboard gestures, multi-monitor/theme transitions and previously unreported
Source/Edit/Transform/Apply/Undo cases are not inferred from this bounded gate.
