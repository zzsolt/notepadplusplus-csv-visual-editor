# Row operations foundation

## Status

Milestone 0.8 development foundation. This increment introduces a host-independent structural row model only. It does **not** expose Add Row/Delete Row controls and does **not** include structural changes in Apply yet.

## Purpose

Row insertion and deletion cannot safely use the current visible grid index because filtering, sorting, prior insertions, and deletions can all change visual positions. Every editable row therefore needs a stable identity independent of its current display position.

## Identity model

- Existing data records retain their parser `SourceRecordIndex` as a non-negative identity.
- The header record is not part of the data-row operation model.
- Newly inserted rows receive unique negative, session-local identities.
- Inserting or deleting another row never changes an existing row identity.
- A deleted source row remains addressable for explicit restore until Revert All or Apply integration rebuilds the session.
- Deleting an inserted row cancels that insertion and removes the synthetic identity.

## Current core API

```text
CsvEditRowId
CsvEditRowSnapshot
CsvRowEditModel
```

Supported in-memory operations:

```text
AppendRow
InsertRowBefore
InsertRowAfter
DeleteRow
RestoreDeletedRow
RevertAll
GetVisibleRows
GetAllRows
GetRow
```

## Invariants

- The model is created only from a complete, source-order `CsvTableProjection` matching the existing `CsvEditSession`.
- Row-limited projections are rejected.
- Inserted values are copied into a fixed-width row matching the complete editable column set.
- Missing inserted values are padded with empty strings.
- Values beyond the editable column count are rejected.
- Deleted rows cannot be insertion anchors.
- Revert All removes all inserted rows, restores all deleted source rows, and restores baseline source order.
- The model never writes to Notepad++, disk, or the wrapped cell-edit session.

## Deliberately not implemented in this increment

- Add Row/Delete Row WinForms controls;
- serialization of inserted rows;
- omission of deleted rows from `CsvEditSession.CreatePreview`;
- combined cell and structural dirty counters;
- line-separator placement policy during insertion;
- structural Apply or host writes;
- row movement;
- header insertion/deletion;
- direct disk writes.

## Planned integration sequence

1. Validate stable row identity and structural state transitions under the existing test matrix.
2. Define deterministic inserted-record separator placement for:
   - middle insertion;
   - append with and without terminal newline;
   - empty data section;
   - mixed CRLF/LF input.
3. Integrate structural state into edit preview generation while retaining raw source text for unchanged records.
4. Add structural change counts and conflict-checked one-undo Apply.
5. Expose Add Row/Delete Row controls only after core serialization and Native AOT tests are green.
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
