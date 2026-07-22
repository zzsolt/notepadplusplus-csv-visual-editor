# Milestone 0.5 host validation

## Environment

- Date: 2026-07-22
- Host: Notepad++ 8.9.7 x64
- Plugin: CSV Visual Editor 0.5.0-alpha
- Automated tests: 81/81 PASS
- Native AOT runtime and plugin publish: PASS

## Accepted behavior

The owner completed the milestone 0.5 host matrix with synthetic or sanitized CSV data.

Confirmed PASS:

- plugin loading and automatic usable dock width;
- two tool rows and Table/Diagnostics tabs;
- case-insensitive all-column search;
- selected-column filtering;
- responsive debounced search;
- Clear restoring every row and source order;
- ascending, descending, and original-source-order header-click cycle;
- visible sort glyph;
- original logical-record numbers retained after filtering and sorting;
- valid CSV `Diagnostics (0)` state;
- Low-confidence automatic delimiter refusal;
- explicit delimiter recovery;
- structured diagnostics for inconsistent record widths;
- display-only padding of missing fields;
- preservation of extra fields in generated display columns;
- delimiter/header rebuild resetting view state safely;
- Refresh after unsaved editor changes;
- active document-tab switching;
- hide/reopen and restart lifecycle;
- application dark mode;
- editor text unchanged;
- disk content unchanged.

## Inconsistent-width diagnostic example

Synthetic input:

```csv
Name,Age
Alice,30
Bob
Charlie,42,Extra
```

Automatic mode correctly refused unsafe delimiter selection at Low confidence. After explicit Comma selection, the parser returned a usable read-only rectangular view and two warnings:

```text
CSV004 | Record 3 | Record has 1 fields; expected 2 based on the sampled mode.
CSV004 | Record 4 | Record has 3 fields; expected 2 based on the sampled mode.
```

The third display column preserved `Extra`; no source data was discarded or rewritten.

## Privacy

Screenshots are not stored in the repository. Only sanitized outcomes are retained.

## Conclusion

Milestone 0.5 is accepted. Safe two-way editing remains a separate milestone and must introduce deterministic serialization, dirty tracking, conflict protection, and undo/redo integration before any editor-buffer write is enabled.
