# Test strategy

CSV Visual Editor uses two complementary host-independent test layers.

## Bootstrap smoke tests

`CsvVisualEditor.Core.SmokeTests` is a dependency-free executable retained to protect the accepted snapshot and basic table-helper baseline.

## Parser unit tests

`CsvVisualEditor.Core.Tests` uses xUnit.net v3 with Microsoft Testing Platform v2.

Pinned package:

```text
xunit.v3.mtp-v2 3.2.2
```

The stable 3.2.2 release is used instead of the available 4.0.0 prerelease line.

The parser matrix covers:

- supported delimiters;
- empty and trailing fields;
- quoted delimiters;
- doubled quotes;
- embedded CRLF and LF;
- blank records and final record separators;
- malformed quoting diagnostics;
- inconsistent record widths;
- leading U+FEFF handling;
- raw source spans;
- wide records;
- high-, medium-, low-, none-, and ambiguous-detection behavior;
- decimal-comma caution behavior.

CI restores and runs both test layers before Native AOT publishing.
