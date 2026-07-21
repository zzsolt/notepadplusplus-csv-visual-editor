# CSV dialect detection and parsing

Milestone 0.3 adds host-independent CSV detection and parsing to `CsvVisualEditor.Core`.

## Supported dialect candidates

Automatic detection evaluates exactly three delimiters:

- comma (`,`)
- semicolon (`;`)
- tab

The quote character defaults to the double quote (`"`). Header interpretation is represented by `CsvHeaderMode` but is not inferred automatically in this milestone.

## Detection model

Each candidate is parsed using the same logical-record parser. The detector scores:

- the modal field count;
- field-count consistency across sampled non-blank logical records;
- the number of multi-field records;
- delimiter occurrences outside quoted fields;
- parser errors;
- inconsistent record widths.

Comma candidates receive an additional caution penalty when most commas are between digits and the structure has only two fields, because that pattern can represent decimal-comma prose rather than CSV.

The result contains:

- an optional suggested dialect;
- `None`, `Low`, `Medium`, or `High` confidence;
- every candidate score and its supporting counts;
- diagnostics for no reliable delimiter, low confidence, or ambiguity.

A low-confidence suggestion must remain overrideable by the user. No reliable candidate results in no suggestion rather than a silent guess.

## Parser behavior

The parser is an explicit character-state machine. It does not split the input into physical lines before processing.

Supported behavior includes:

- empty and trailing fields;
- comma, semicolon, and tab delimiters;
- quoted delimiters;
- doubled quote escaping (`""`);
- CRLF, LF, and CR record separators;
- CRLF/LF inside quoted fields;
- blank logical records;
- leading decoded U+FEFF handling;
- source character spans for records and cells;
- modal expected-field-count calculation;
- diagnostics for inconsistent record widths;
- partial, non-destructive results for malformed input.

Error diagnostics cover:

- quote inside an unquoted field;
- unexpected text after a closing quote;
- unterminated quoted field.

The parser never writes to the Notepad++ buffer or disk. Grid population and editing are outside milestone 0.3.

## Tests

The original dependency-free smoke harness remains as a bootstrap regression gate. A conventional xUnit.net v3 test project provides the larger parser matrix.

Pinned package:

```text
xunit.v3.mtp-v2 3.2.2
```

Run from the repository root:

```powershell
dotnet run `
    --project tests/CsvVisualEditor.Core.Tests/CsvVisualEditor.Core.Tests.csproj `
    -c Release
```
