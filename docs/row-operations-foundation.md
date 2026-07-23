# Row operations foundation

## Status

Milestone 0.8 development, structural preview increment. Stable row identity, insertion/deletion state, live source-cell editing, and deterministic minimal-difference structural preview generation are implemented on the draft branch. Structural Apply and WinForms Add Row/Delete Row controls are not enabled yet. Current serializer validation is pending the branch CI result.

## Purpose

Row insertion and deletion cannot safely use the current visible grid index because filtering, sorting, prior insertions, and deletions can all change visual positions. Every editable row therefore needs a stable identity independent of its current display position.

## Identity model

- Existing data records retain their parser `SourceRecordIndex` as a non-negative identity.
- The header record is not part of the data-row operation model.
- Newly inserted rows receive unique negative, session-local identities.
- Inserting or deleting another row never changes an existing row identity.
- A deleted source row remains addressable for explicit restore until Revert All or a future successful structural Apply rebuilds the session.
- Deleting an inserted row cancels that insertion and removes the synthetic identity.

## Current core API

```text
CsvEditRowId
CsvEditRowSnapshot
CsvRowEditPreview
CsvRowEditModel
```

Supported in-memory operations:

```text
AppendRow
InsertRowBefore
InsertRowAfter
DeleteRow
RestoreDeletedRow
SetCellValue
RevertAll
CreatePreview
GetVisibleRows
GetAllRows
GetRow
```

## State ownership

- Existing source-row cell values remain owned by the accepted `CsvEditSession`.
- `CsvRowEditModel.SetCellValue` delegates source-row changes to that session.
- Inserted-row values are owned by the structural model.
- Row snapshots read current source values from the session instead of retaining stale copies.
- Combined changed-row counts avoid double-counting a source row that was both cell-edited and deleted.
- `RevertAll` restores both the cell-edit session and structural insertion/deletion state.

## Separator and deletion policy

The preview reconstructs the complete logical record stream while preserving unchanged raw source records.

### Boundaries between records

- When the left surviving row is an original source record and it has an original separator after it, that exact separator is retained.
- When the left row is inserted, the document's detected serialization `NewLine` is used.
- When an original last row had no separator but a new row follows it, the detected serialization `NewLine` is inserted to prevent record concatenation.

### Terminal separator

- The original document's terminal separator is retained exactly when at least one logical output record remains.
- A document that originally had no terminal newline does not gain one merely because a row was inserted or deleted.
- A document that originally had a terminal newline retains it after append or deletion.
- If every record is deleted in headerless mode, only the source prefix such as a BOM remains; an orphan terminal newline is not emitted.

### Deletion

- First-row deletion reconnects the preceding header or source record using the preceding survivor's separator.
- Middle-row deletion retains the separator of the left surviving source record.
- Last-row deletion follows the original terminal-newline state rather than turning the removed row's incoming separator into a new terminal newline.
- Deleting every data row preserves the header and the original terminal-newline state.

### Inserted rows

- Inserted rows use the complete editable column width.
- Missing values are explicit empty strings.
- Delimiters, quotes, embedded newlines, and boundary whitespace are quoted by `CsvSerializer`.
- Inserted values are copied; caller-owned arrays cannot mutate model state later.

## Minimal-difference preview

- Source prefix/BOM is retained.
- Unchanged source records retain their exact original raw record text.
- Dirty source records are regenerated using the accepted deterministic serializer.
- Padded source fields are emitted only through the highest edited non-empty field, matching the accepted 0.7 policy.
- Deleted source rows are omitted.
- Inserted rows are serialized at their stable structural position.
- The preview includes SHA-256 plus changed-cell, changed-row, inserted-row, and deleted-row counts.
- Preview creation performs no Notepad++ or disk write.

## Invariants

- The model is created only from a matching `ActiveDocumentSnapshot`, verified `CsvParseResult`, complete source-order `CsvTableProjection`, and existing `CsvEditSession`.
- Row-limited projections are rejected.
- The model and session must share the same baseline content SHA-256.
- Parser spans must reconstruct the original editor snapshot exactly before structural editing begins.
- Values beyond the editable column count are rejected.
- Deleted rows cannot be edited or used as insertion anchors.
- Empty CSV input has no columns and therefore rejects row insertion.
- Structural preview is a separate type and does not alter the accepted 0.7 Apply contract yet.

## Deliberately not enabled yet

- Add Row/Delete Row WinForms controls;
- structural `CsvEditApplyPlan` integration;
- Scintilla replacement of a structural preview;
- structural host undo/redo acceptance;
- row movement;
- header insertion/deletion;
- non-UTF-8 Apply;
- direct disk writes.

## Planned integration sequence

1. Complete the structural separator/deletion/combined-edit test matrix and Native AOT validation.
2. Add conflict-checked structural Apply planning around `CsvRowEditPreview`.
3. Reuse the accepted single-undo replacement coordinator without adding another host-write path.
4. Add combined dirty counters and stable row IDs to the WinForms grid.
5. Expose Add Row/Delete Row controls only after core Apply and Native AOT tests are green.
6. Complete Notepad++ 8.9.7 x64 host acceptance before merge.

## Safety boundary

All accepted 0.7 guarantees remain unchanged:

- UTF-8-only Apply;
- fresh document/code-page/content conflict checks;
- no replacement call for conflicts;
- one whole-document replacement in one Scintilla undo transaction;
- Notepad++ remains Save owner;
- no direct disk write;
- no source-value logging.
