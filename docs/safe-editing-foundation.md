# Safe editing foundation

Milestone 0.6 introduces the host-independent model required before CSV Visual Editor may safely modify a Notepad++ editor buffer.

This milestone does **not** enable editable grid cells and performs no Scintilla or disk write.

## Design goals

- retain an immutable baseline for the active document;
- track edits by source record and column;
- serialize changed CSV records deterministically;
- preserve unchanged source text with minimal differences;
- block apply planning when the active document has changed;
- never silently discard malformed, padded, or extra fields.

## Edit-session baseline

`CsvEditSessionBaseline` records:

- document path and display name;
- UTF-8 SHA-256 identity of the decoded editor text;
- Scintilla code page;
- baseline capture time.

An apply plan is `Ready` only while document identity, code page, and content SHA-256 still match this baseline.

## Parser-result verification

Before an edit session is created, the active snapshot is reparsed with the same dialect. The supplied and fresh parse results must match in:

- dialect and expected field count;
- record count, record index, and source span;
- cell count, value, source span, and quoted state;
- diagnostic severity, code, message, character offset, and record index.

This prevents stale parser output from being accepted for different text, even when both inputs have the same length and compatible source spans.

## Dirty tracking

The session maintains:

- original and current value for every rectangular cell;
- per-cell dirty state;
- changed-cell count;
- changed-record count;
- original source-record identity;
- original field count for every logical record.

Setting an already-current value is a no-op. Reverting a cell to its original value removes its dirty state. `RevertAll` restores all values and counters.

## Deterministic serialization

`CsvSerializationPolicy` captures:

- comma, semicolon, or tab delimiter;
- quote character;
- first unquoted CRLF, LF, or CR record separator;
- leading U+FEFF state;
- terminal-newline state.

A regenerated field is quoted when it contains:

- the delimiter;
- the quote character;
- CR or LF;
- leading or trailing whitespace.

Embedded quote characters are doubled.

## Minimal-difference preview

The session stores the original raw text and following separator of each logical record.

- unchanged records are copied exactly;
- only changed records are regenerated;
- mixed original CRLF/LF/CR separators remain untouched;
- BOM and any source prefix remain untouched;
- editing a display-padded field extends only its source record;
- required intermediate empty fields are emitted;
- clearing an existing trailing field preserves the original field count;
- reverting all edits restores the exact original text.

`CsvEditPreview` exposes the replacement text, changed-cell count, changed-record count, and replacement SHA-256. It does not apply the text.

## Apply-plan states

`CsvEditApplyPlan` returns one of:

```text
NoChanges
Ready
DocumentIdentityChanged
CodePageChanged
ContentChanged
```

Conflict plans contain no replacement preview.

## Session-creation restrictions

Editing is rejected when:

- parser errors exist;
- the visual projection is row-limited;
- the projection omits parsed columns or records;
- parser and projection source order differ;
- parser spans do not match or reconstruct the snapshot;
- fresh parsing does not match the supplied parser result.

Warnings such as inconsistent field counts remain representable: original field counts and extra values are retained, and display padding is not silently converted into source fields unless the user edits those cells.

## Validation

The foundation passed:

- 112/112 xUnit tests;
- strict Release build;
- dependency-free bootstrap smoke;
- Native AOT serializer execution;
- Native AOT edit-session dirty/revert execution;
- Native AOT Ready and ContentChanged planning;
- full win-x64 Native AOT plugin publish.

## Next host increment

A later milestone may expose editable cells only after it implements:

- fresh-snapshot conflict verification immediately before Apply;
- one Scintilla undo transaction for the complete replacement;
- Apply and Revert controls;
- conflict UI instead of overwrite;
- Notepad++-managed disk saving;
- real-host undo/redo and external-change validation.
