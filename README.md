# CSV Visual Editor

CSV Visual Editor is a 64-bit Notepad++ plugin for viewing and editing CSV data through a docked, spreadsheet-like table while keeping the active Notepad++ buffer authoritative.

> **Development status:** milestone **0.11 is owner-accepted and merged**. Milestone **0.12 is under development** on source navigation and previewed bulk text transformations. The 0.12 package is not accepted until automated gates and real Notepad++ host validation pass.

## Current GUI candidate: 0.12.4

The main toolbar uses compact graphical icons with command-name tooltips. A permanent **Table / Edit / View / CSV / Search** menu provides the same commands plus delimiter/header selection, search and sorting. Menus and icons share availability and Edit state; menu search remains usable when the dock is narrow. Icons scale with DPI and menus follow the current plugin light/dark palette.

**View > Show spaces** toggles small hollow orange space marks in the table and subsequently opened Transform previews. These are display indicators; cell and clipboard values remain real spaces. Hover a data cell for exact space counts. The switch is session-local and defaults on; native in-cell typing remains standard. Transform retains value boundaries and lengths even with marks hidden.

[Hungarian GUI host checklist](docs/host-validation-0.12-gui-hu.md). The owner reported the preceding 0.12.2 whitespace checks successful; the new GUI requires its own host validation. Milestone 0.12 remains draft and unmerged.

Search now has its own toolbar row. Matching data cells receive a gold inset frame, including selected cells; the full value and selected search column follow the row filter's existing case-insensitive rules. Clearing the query removes frames. Space rings use identical pixel geometry at each DPI in the table and Transform. The owner reported the other 0.12.3 GUI behavior good; these two corrections await a targeted host retest.

## Core principles

- The active unsaved Scintilla buffer is the source of truth; the plugin does not reopen the file from disk to build the table.
- Notepad++ owns document identity, modified state, undo/redo, caret/selection, and Save.
- Pending grid edits remain local until **Apply**.
- Apply is conflict-checked and performs one whole-buffer Scintilla replacement in one undo transaction.
- Direct disk overwrite is not a primary path.
- Core parsing/edit/navigation logic is host-independent and covered by xUnit and Native AOT smoke tests.

## Current accepted 0.11 capabilities

### CSV reading and table view

- comma, semicolon and tab dialect support;
- explainable automatic delimiter detection with manual override;
- explicit **First row is header** / **No header row** mode;
- logical-record parser with quoted delimiters, doubled quotes and embedded CRLF/LF;
- empty/trailing fields, blank records, Unicode and source spans;
- parser/detection diagnostics;
- deterministic display-only header cleanup/fallbacks;
- case-insensitive search across all columns or one column;
- three-state view sort: ascending, descending, source order;
- dedicated **Table** and **Diagnostics** tabs.

### Spreadsheet-style editing

- explicit Edit mode;
- direct cell editing;
- Add Row;
- stable source-row and inserted-row identities;
- synchronized complete-row selection through the edit-only **Select** checkbox, native row header, and frozen `#` row-indicator column;
- plain / Ctrl / Shift / Ctrl+Shift row selection;
- atomic multi-row Delete;
- exact Revert All;
- rectangular Copy/Cut/Paste using Unicode tab/newline matrices;
- Copy in read-only mode; Cut/Paste in Edit mode;
- exact rectangular paste, current-cell expansion and single-cell broadcast;
- Excel-compatible rectangular paste routing inside the real Notepad++ host;
- `#` and **Select** presentation columns are never serialized or copied as CSV data.

### Apply and encoding safety

- deterministic minimal-difference serialization;
- fresh document/code-page/content conflict checks immediately before Apply;
- explicit strict encoding profiles for UTF-8, ASCII, Windows-1250 and Windows-1252;
- no replacement-character encoding fallback;
- encoding representability and host-write authorization are separate concerns;
- blocked Apply states construct no editor replacement target;
- one complete Scintilla replacement in one undo action;
- Notepad++ remains responsible for Save.

### Large-table path

- parse/projection work runs in the background with cancellation and stale-result suppression;
- loading state appears immediately;
- complete edit model is created lazily only when Edit is requested;
- read-only rows virtualize at 1,000 rows or 40,000 visible cells;
- smaller/editable tables use batch materialization;
- row presentation avoids repeated whole-grid sizing scans.

Current alpha safety limits remain:

- 64 MiB active-buffer snapshot;
- 10,000 displayed rows;
- 512 columns;
- 250,000 displayed cells.

## 0.12 source navigation — current development

The same development branch adds **Transform** in Edit mode: literal replacement, whitespace trimming and invariant casing for selected cells/rows, one column or all data cells. Preview shows counts and before/after samples. Acceptance changes only pending edits; **Apply** retains the existing editor conflict/encoding/undo checks. See [operations, limits and step-by-step host tests](docs/bulk-transforms-0.12.md).

The 0.12 branch adds a **Source** command to the spreadsheet command row:

```text
Paste  Cut  Copy  Source | Edit  Add Row  Delete Row  Transform | Apply  Revert All | changes
```

The command navigates from the current visual CSV row/cell to the originating source selection in Notepad++ without modifying text.

Design rules:

- stable navigation identity is parser logical-record index + optional physical CSV column index;
- a real data cell targets its raw CSV field;
- a presentation cell targets the complete logical record;
- quoted fields include their raw quote characters;
- multiline quoted fields remain one source target;
- projected padded fields fall back to their complete logical record instead of inventing a field position;
- inserted pending rows have no source position until Apply;
- stale document/content/code-page state blocks navigation;
- parser UTF-16 character offsets are converted to byte-oriented Scintilla positions using the explicit active code-page profile;
- strict encoded whole-buffer length must match Scintilla's reported byte length before navigation is authorized;
- navigation changes only editor selection/caret/viewport state and never writes the buffer or disk.

See:

- `docs/source-navigation-0.12.md`
- `docs/host-validation-0.12-source-navigation.md`

## Command surface

Accepted 0.11 layout:

```text
Paste  Cut  Copy | Edit  Add Row  Delete Row | Apply  Revert All | changes
Refresh | Delimiter | Header | Search  In  Clear | Diagnostics
```

The 0.12 development branch inserts **Source** after Copy. The toolbar preserves the accepted `RowHeaderSelect` interaction model rather than changing DataGridView selection semantics as a presentation side effect.

## Build and deployment

Target:

- Windows x64;
- Notepad++ x64;
- .NET 10;
- WinForms `DataGridView`;
- Native AOT plugin DLL.

Installable package layout:

```text
CsvVisualEditor/
└── CsvVisualEditor.dll
```

Install under:

```text
<Notepad++>/plugins/CsvVisualEditor/CsvVisualEditor.dll
```

Development packages are host-test artifacts, not stable releases.

## Verification policy

Each milestone must pass:

1. restore;
2. strict Core build;
3. bootstrap smoke tests;
4. Core xUnit tests;
5. Windows Native AOT runtime smoke;
6. production win-x64 Native AOT plugin publish;
7. installable package creation;
8. real Notepad++ owner host validation for host-dependent behavior.

Automated green status is not treated as proof of Notepad++ docking/input/lifecycle behavior.

## Developer

Developer: **Zolnai Zsolt**  
Contact: **zzsolt@gmail.com**

License selection remains an owner decision before a stable public release.
