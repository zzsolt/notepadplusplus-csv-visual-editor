# Test strategy

CSV Visual Editor uses two complementary host-independent test layers plus mandatory real-host acceptance when UI behavior changes.

## Bootstrap smoke tests

`CsvVisualEditor.Core.SmokeTests` is a dependency-free executable retained to protect the accepted snapshot and basic fallback-header baseline.

## Core xUnit tests

`CsvVisualEditor.Core.Tests` uses xUnit.net v3 with Microsoft Testing Platform v2.

Pinned package:

```text
xunit.v3.mtp-v2 3.2.2
```

The stable 3.2.2 release is used instead of a prerelease major line.

### Parser and detector matrix

Coverage includes:

- comma, semicolon, and tab;
- empty and trailing fields;
- quoted delimiters;
- doubled quotes;
- embedded CRLF and LF;
- lone CR record separators;
- blank records and final record separators;
- Unicode preservation;
- malformed quoting diagnostics;
- inconsistent record widths;
- leading U+FEFF handling;
- raw source spans;
- wide records;
- high-, medium-, low-, none-, and ambiguous-detection behavior;
- decimal-comma caution;
- bounded logical-record and decoded-character detector sampling.

### Table-projection matrix

Coverage includes:

- First row is header behavior;
- No header row behavior;
- deterministic `Column N` fallback names;
- empty, duplicate, whitespace, and multiline header normalization;
- case-insensitive unique display names;
- source logical-record identity preservation;
- inconsistent-width view padding without parser mutation;
- visible row-limit metadata;
- column-limit refusal instead of silent hiding;
- empty input;
- rejection of unresolved header mode and invalid limits.

## CI order

CI performs:

1. restore;
2. strict core Release build;
3. bootstrap smoke tests;
4. combined parser/detector/table xUnit run with retained log artifact;
5. plugin and WinForms compilation;
6. win-x64 Native AOT publish;
7. clean installation ZIP creation;
8. package and publish-diagnostics upload.

Native AOT publishing does not start when the core xUnit gate fails.

## Manual Notepad++ acceptance

Milestone 0.4 changes visible WinForms behavior, so automated tests are not sufficient. Test the produced x64 DLL in Notepad++ 8.9.7 for:

- automatic comma, semicolon, and tab detection;
- manual delimiter override;
- safe refusal of low/ambiguous automatic detection;
- both header modes;
- duplicate and empty display headers;
- inconsistent-width records;
- multiline quoted cells;
- read-only behavior and source preservation;
- row source numbers;
- status counts and parser diagnostics;
- dark mode;
- hide/reopen, tab switching, unsaved edits, Refresh, shutdown, and restart;
- About developer/contact text.

No supplied screenshot containing real paths or document values is committed to either repository.
