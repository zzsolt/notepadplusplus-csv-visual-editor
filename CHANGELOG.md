# Changelog

All notable changes to CSV Visual Editor will be documented in this file.

## [Unreleased]

### 0.12.6 visual regression repair

- Restore standard UI typography and vertical cell borders; remove the forced value font.
- Render legible 3-DIP orange space dots, with higher contrast and separate display-only whitespace slots. Original CSV, clipboard and edit values remain unchanged. Native in-cell editing remains standard.
- Repair the interrupted search-field border, keep scope/count/navigation grouped, cap query width and remove redundant idle text/clear icon. Narrow layouts no longer inherit a 620px form minimum.
- Keep initial table focus on a data cell rather than the presentation-only record-number lane. Avoid redundant invalidations while a cleared search index is already null.
- Add full production-DLL load/render/search review in an isolated, checksum-pinned portable Notepad++ 8.9.8, with captured panel images. This does not replace user acceptance, multi-monitor/DPI or Apply/Undo tests.


### Additional 0.12.5 audit corrections

- Use monospaced value cells in the table and Transform for clearly separated solid space dots; keep normal UI header fonts and raw values.
- Improve narrow search scope width and dark About styling; share the new About dialog with the existing Notepad++ Plugins menu.
- Restore the explicitly accepted Windows-1250 host Apply policy while preserving strict full-buffer lossless preflight, conflict checks and one undo. Do not authorize additional code pages.
- Retire prior background builds before a fresh snapshot read, including failed-read paths, so old results cannot replace a newer error state.
- Add actual-grid light/dark pixel tests and production-host-policy Native AOT regressions. Real Notepad++ host validation remains pending.

### 0.12.5 GUI refinement and search navigation

- Replace narrow-font space rings with filled, pixel-aligned dots. Guarantee a gap by choosing one diameter per font/DPI rather than varying individual glyph shapes. Test actual consecutive runs, not just isolated markers.
- Replace the cascading search textbox with one responsive search bar, cell counts, previous/next navigation, direct menu focus and keyboard access. Preserve row-filter semantics and separate clearing text from resetting all view options.
- Index matching visible cells once; do not cache potentially stale values by row number. Install results after row rebuilding and invalidate on mutations. Preserve clipping, native edit/selection, clipboard and Apply.
- Add About with build information, developer Zolnai Zsolt, approved contact zzsolt@gmail.com and a support invitation. Links open only on an explicit click; no donation service is invented.
- Soften grid separators, reduce status-line clutter with complete hover details, add useful no-results/diagnostics empty states and enable buffered grid painting.
- Fix repeated command-surface tooltip prefixes, menu handling of literal ampersands, and invalid sort-enum values being treated as descending.
- Add Core and Native AOT regression coverage, responsive-layout screenshots and repeat-install/disposal checks. New host validation remains pending.

### 0.12.4 search and whitespace refinement

- Frame matching CSV cells in gold using the same effective query, column scope and ordinal-ignore-case semantics as the row filter. Keep native selection and raw values; clear cached matches on each render, sort, query change and Edit transition.
- Bound the visible-cell match cache to 4,096 entries; do not retain cell values or allocate per-cell styles.
- Use fixed DPI-dependent, pixel-snapped space-ring geometry independent of measured glyph widths. Reuse the pen and graphics state across each paint pass.
- Give search its own toolbar row so delimiter/header controls no longer displace the input; retain the complete Search menu.
- Owner reported missing cell search indication and inconsistent ring appearance in 0.12.3, with everything else good. New correction requires host retest; screenshots and source values were not committed.
- Base version 0.12.4-alpha with unique run/attempt package names; additional Native AOT pixel and search-state regressions.

### 0.12.3 icon toolbar and complete menu

- Compact original outline icons with command-name tooltips and accessible names; permanent Table/Edit/View/CSV/Search dropdown menus share the existing command handlers and enabled/checked state.
- Reach delimiter, header, search text/column, sort, table and diagnostics from menus, including when toolbar controls overflow.
- Theme-aware menus, DPI-scaled icons and themed Transform dialog follow the plugin's current Notepad++ light/dark colors.
- Refine space marks to small hollow orange dots; View > Show spaces toggles presentation in the table and subsequent Transform dialogs. Cell tooltips count real spaces, leading/trailing spaces and length.
- Base version 0.12.3-alpha; unique build/run-attempt ZIP names remain enabled.
- Owner reported the preceding 0.12.2 whitespace host checklist successful on 2026-09-07; host version and installed DLL hash were not supplied. This report does not establish a result for the new GUI or previously unreported Source cases.
- GUI host validation is pending: see `docs/host-validation-0.12-gui-hu.md`.

### 0.12.2 orange whitespace dots

- Paint small orange space dots in the primary CSV table and Transform previews, including virtual rows and selected cells.
- Preserve actual cell/clipboard/edit/source values; leave native in-cell editing and presentation columns alone.
- Add Native AOT bitmap/virtual-grid checks and a targeted Hungarian host checklist.
- Advance the base version to 0.12.2-alpha; retain unique CI run/attempt package names.


### 0.12.1 preview and package correction

- Make preview whitespace visible with space markers, escaped controls/Unicode whitespace, explicit boundaries and complete UTF-16 lengths. Real values remain unchanged.
- Distinguish literal marker characters and avoid splitting surrogate pairs in shortened samples.
- Give every CI package a unique `0.12.1-alpha.<run>.<attempt>` version/name, including reruns. Record ZIP/DLL checksums in candidate logs.
- Add seven Native AOT formatting checks; real-host readability acceptance remains pending.

### Added

- Edit-mode **Transform** command with literal replacement, outer-whitespace trimming and invariant uppercase/lowercase operations.
- Selected cells/complete rows, current physical CSV column, or all data cells as explicit scopes.
- Immutable before/after preview, exact affected-cell/row counts, bounded on-screen samples and explicit acceptance into pending edits.
- Full prevalidation of previewed values (including unchanged targets), stable row identities, wrong-session rejection and bounded replacement growth before mutation.
- Core regression and Native AOT transform/dialog coverage; step-by-step synthetic host checks in `docs/bulk-transforms-0.12.md`.

### Validation boundary

- Owner reported the previously supplied read-only stale Source F1/recovery test successful on 2026-09-06. Exact host version and installed DLL hash were not supplied with this report.
- The detailed historical Transform/Source evidence is retained in the host documents; Source F2–F4 and exact Windows-1250 Source offsets are not established by the latest whitespace checklist report. This development phase does not close milestone 0.12.

## [0.9.0-alpha] — 2026-07-29

### Fixed

- Removed logical-record/state text from the native `DataGridViewRowHeaderCell`, eliminating direct visual coupling between the current-row glyph and the active row number.
- Replaced the rejected fixed 64/112-pixel row-header policy with a glyph-only native header plus a dedicated, content-measured `#` row-indicator column.
- Prevented ordinary Edit mode from reserving permanent space for `new:n *` labels that do not exist.

### Changed

- Native row headers now use WinForms `AutoSizeToAllHeaders` for framework-owned glyph, DPI, theme, RTL, high-contrast, and editing-icon capacity.
- Logical-record numbers and `n *` / `new:n *` state labels are centered in a frozen read-only column that is physically appended but visually first.
- The `#` cell is an additional whole-row gesture target; native row-header and checkbox behavior continue to share the same stable-ID selection model.
- Structural label tooltips explain modified and inserted pending state.
- Unused row-error icon capacity is disabled; native current-row editing indication remains enabled.

### Safety

- Physical CSV data-column indexes remain unchanged; both presentation columns are added only after CSV columns.
- The row-indicator and selector never enter the CSV model, serialization, conflict detection, or Apply path.
- Existing checkbox/full-row/Ctrl/Shift/Delete/Revert stable-ID behavior remains authoritative.
- Custom native row-header painting was intentionally avoided so WinForms retains theme, DPI, high-contrast, RTL, and accessibility ownership.

### Validation status

- Native AOT coverage now verifies glyph-only native headers, aligned indicator values, content-driven structural width, unchanged CSV indexes, selector placement, dark-mode refresh, reconstruction, and accepted selection/deletion behavior.
- The owner accepted the final package in Notepad++ 8.9.7 x64 on 2026-07-29 and explicitly authorized closing milestone 0.9.


### Added

- Atomic `CsvRowEditModelBatchOperations.DeleteRows` API using stable `CsvEditRowId` targets.
- Immutable `CsvBatchDeleteResult` with target, source-deletion, inserted-cancellation, affected, and remaining-row counts.
- Duplicate-ID normalization and complete prevalidation before the first structural mutation.
- Mixed source-row deletion and inserted-row cancellation in one deterministic operation.
- Checkbox-style **Select** column shown only in Edit mode and appended after real CSV columns.
- Shared plugin-owned stable complete-row selection state for selector boxes and row-header gestures.
- Plain, Ctrl, Shift, and Ctrl+Shift row-header selection based on stable IDs and a stable anchor.
- Two-way visual synchronization: selector boxes highlight complete rows, and row-header selection updates selector boxes.
- Dynamic **Delete Row** / **Delete Rows (n)** command state.
- Deterministic neighboring-row focus after batch deletion.
- Nine batch-operation xUnit regressions covering atomicity, duplicates, ordering, mixed rows, BOM, mixed EOL, terminal newlines, inserted-only cleanup, and exact Revert All.
- Native AOT synchronized selector/row-header, batch preview, Apply, conflict-zero-call, Revert, and selector-lifecycle execution.
- Public multi-row deletion architecture and Notepad++ 0.9 host-validation matrix.
- `0.9.0-alpha` assembly, About, workflow artifact, and package version.

### Changed

- The working explicit selector is now the visible state indicator for row-header selection rather than a separate interaction model.
- Row-header Ctrl/Shift gestures update stable `CsvEditRowId` targets directly and do not derive deletion intent from `SelectedRows` or `SelectedCells`.
- Clicking an ordinary CSV data cell clears complete-row selection and disables Delete.
- Current-cell row deletion fallback was removed; Delete requires at least one explicitly selected complete row.
- Selector marks retain only stable `CsvEditRowId` values and never enter the CSV cell model.
- Delete captures the explicit stable-ID set before mutation and performs one core batch operation.
- Selection enumeration order no longer affects structural output.
- Button text and tooltip communicate one-row versus multi-row deletion targets.
- Status messages distinguish source rows marked for deletion from inserted rows removed by cancellation.
- Leaving Edit mode removes the selector column and clears complete-row selection.
- Native AOT diagnostic artifacts include both publish and execution logs.

### Safety

- DataGridView rows, cells, indexes, and selection collections remain presentation state only.
- Selector state is never serialized as CSV data.
- Appending the selector preserves all existing CSV data-column indexes.
- Ordinary cell focus cannot accidentally become a row-deletion target.
- The complete unique stable-ID set is validated before any row mutation.
- An unknown stable identity produces zero partial changes.
- Built-in DataGridView row deletion remains disabled.
- Selected inserted rows are cancelled; selected source rows remain restorable through Revert All.
- NoChanges and document/code-page/content conflicts continue to call no editor method.
- Batch Apply reuses the existing single Begin/Replace/Selection/End coordinator.
- UTF-8-only Apply, one full-buffer replacement, normal Notepad++ Save ownership, and no direct disk writes remain unchanged.

### Validated

- 168/168 xUnit tests passed.
- Strict core build and bootstrap smoke passed.
- Native AOT explicit selector appearance, placement, visual full-row synchronization, atomic deletion, and exact Revert All passed.
- Native AOT plain/Ctrl/Shift row-header selection, selector synchronization, ordinary-cell zero-target behavior, and selector removal passed.
- Native AOT mixed batch deletion, deterministic preview, Apply ordering, and conflict zero-call behavior passed.
- Full win-x64 Native AOT plugin publish and `0.9.0-alpha` package creation passed in final CI `30387337728` on public head `ee686b75e65800dd1e6e959e477d60fd1d2234d1`.
- The owner accepted synchronized checkbox/native-header/`#` selection, batch deletion, Revert, and the final row-presentation package in Notepad++ 8.9.7 x64.

## [0.8.0-alpha] — 2026-07-23

### Added

- Stable `CsvEditRowId` identities for source and inserted data rows.
- Host-independent `CsvRowEditModel` with append, insert-before, insert-after, delete, restore, and Revert All transitions.
- Immutable `CsvEditRowSnapshot` and `CsvRowEditPreview` models.
- Explicit **Add Row** and **Delete Row** controls in Edit mode.
- Header-only editing so a CSV header can receive its first data row.
- Changed-cell, unique changed-row, inserted-row, and deleted-row counts.
- `new:n *` row-header markers for inserted rows.
- Deterministic structural preview generation with SHA-256.
- Shared `CsvEditorReplacementPlan` for cell-only and structural Apply paths.
- Structural fake-host tests for NoChanges, document/code-page/content conflicts, exact one-undo ordering, failure cleanup, and selection restoration.
- Native AOT execution of structural preview, structural Apply, content-conflict zero-call behavior, and exact Revert All.
- Public row-operation architecture and 0.8 host-validation guides.
- `0.8.0-alpha` assembly, About, workflow artifact, and package version.

### Changed

- Source-row values remain owned by `CsvEditSession`; the structural model reads them live instead of duplicating mutable state.
- Edit-mode grid rows store stable `CsvEditRowId` values rather than visual row indexes.
- Edit mode renders the structural source-order model and locks filtering and sorting.
- Add Row inserts after the selected stable row identity, or appends when no row is selected.
- Delete Row deletes only the selected stable data-row identity; deleting an inserted row cancels that insertion.
- Revert All restores cell edits, source deletions, insertions, source order, and exact original preview text.
- Cell-only and structural Apply entry points converge on one private Begin/Replace/Selection/End coordinator.
- Dirty status and the status bar expose combined cell, row, insertion, and deletion counts.
- Row headers use a readable 112-pixel width for complete `new:n *` labels.
- Ordinary cell clicks remain cell selections while left row-header clicks select the complete stable row.

### Serialization

- Unchanged source records retain exact raw source text.
- Dirty source records retain the accepted original/padded field-count policy.
- Inserted rows serialize the complete editable column width with explicit empty strings.
- Interior boundaries retain the left surviving source record's exact separator when available.
- Boundaries after inserted rows use the detected serialization newline.
- Append after a source record without a terminal separator creates exactly one required record boundary.
- Original terminal-newline state is preserved independently from interior record boundaries.
- Deleting every headerless row retains only the source prefix, including an optional BOM.

### Safety

- Structural model creation requires matching document identity, code page, baseline SHA-256, dialect, source-record sequence, header record, column set, and complete source-order projection.
- Row-limited and malformed projections remain non-editable.
- Deleted rows cannot be edited or used as insertion anchors.
- Visual DataGridView indexes are never persistent source identities.
- Structural NoChanges and conflict plans expose no replacement text or replacement hash.
- No editor target method is called for structural NoChanges or conflict states.
- UTF-8-only Apply remains enforced before the Notepad++ replacement adapter is created.
- Structural Apply remains one complete editor-buffer replacement inside one Scintilla undo transaction.
- Saving remains a normal Notepad++ action; no direct disk write or save-point manipulation was added.

### Validated

- 159/159 xUnit tests passed.
- Strict core build and bootstrap smoke passed.
- Native AOT structural preview, shared Apply coordinator, content-conflict zero-call path, and exact Revert All passed.
- Native AOT row-header discovery, preferred width, complete-row selection, and reconstruction reapplication passed.
- Full win-x64 Native AOT plugin publish and `0.8.0-alpha` package creation passed.
- Complete Notepad++ 8.9.7 x64 owner acceptance passed on 2026-07-23.

## [0.7.0-alpha] — 2026-07-22

### Added

- Explicit Edit mode, disabled by default.
- Editable DataGridView cells only while Edit mode is active.
- Apply and Revert All controls.
- Visible changed-cell and changed-record counts.
- Dirty-record `*` markers beside original source logical-record numbers.
- Host-independent `CsvEditorApplyCoordinator` and `IEditorReplacementTarget` contract.
- Apply results for Applied, NoChanges, document conflict, code-page conflict, and content conflict.
- Thin Notepad++/Scintilla whole-document replacement adapter.
- Fake-host tests for zero-call conflict behavior, exact one-undo call ordering, selection clamping, and failure cleanup.
- Native AOT Apply-coordinator execution for successful replacement and conflict blocking.
- Public 0.7 host-validation guide.
- `0.7.0-alpha` assembly, About, and package version.

### Changed

- Entering Edit mode clears search/sort state and restores source order.
- Refresh, delimiter, header, search, and sorting transitions are locked during Edit mode.
- A dirty Edit session cannot be exited implicitly; Apply or Revert All is required.
- Hiding and reopening the panel retains the Edit session instead of refreshing it.
- Global Refresh Table refuses to discard an active Edit session.
- A successful Apply rebuilds the table and session from the resulting editor buffer.
- Selection-restoration failure is recorded as non-critical after a successful document replacement.
- Apply is permitted only for Scintilla code page 65001 (UTF-8); viewing remains available for other readable code pages.

### Safety

- Apply reads a fresh active-document snapshot immediately before replacement.
- No Scintilla replacement method is called for NoChanges or any conflict state.
- Non-UTF-8 Apply is blocked before constructing the replacement adapter.
- Ready Apply performs exactly one whole-document replacement inside one undo action.
- `EndUndoAction` executes in `finally` after a successful Begin.
- The plugin does not set or clear the Notepad++ save point.
- Saving to disk remains a normal Notepad++ action.
- No automatic Apply or direct disk write exists.

### Validated

- 122/122 xUnit tests passed.
- Strict core build and bootstrap smoke passed.
- Native AOT serializer, edit-session, Apply coordinator, conflict blocking, and diagnostics UI smoke passed.
- Full win-x64 Native AOT plugin publish and installable package creation passed.
- Complete Notepad++ 8.9.7 x64 owner acceptance passed on 2026-07-22.

## [0.6.0-alpha] — 2026-07-22

### Added

- Host-independent deterministic CSV serialization and `CsvEditSession`.
- Per-cell dirty tracking, reversion, minimal-difference previews, conflict plans, and Native AOT coverage.

### Validated

- 112/112 xUnit tests passed.
- Accepted public squash merge: `5184de060c775d2df6db63fa9b42a9be3cf212c1`.

## [0.5.0-alpha] — 2026-07-22

### Added

- Search, filtering, stable three-state sorting, Table/Diagnostics tabs, and structured diagnostics.

### Validated

- 81/81 xUnit tests and complete Notepad++ host acceptance passed.
- Accepted public squash merge: `9aa11992142297c7c40c021d71c3565a71977217`.

## [0.4.0-alpha] — 2026-07-22

### Added

- Read-only visual CSV table, delimiter/header controls, source row numbers, limits, Native AOT grid runtime, DPI-aware layout, and About details.

### Validated

- 69/69 xUnit tests and complete Notepad++ host acceptance passed.
- Accepted public squash merge: `d17dd7f46f600fac2896f21239d7833dd00dd727`.

## [0.3.0-alpha] — 2026-07-21

### Added

- Explainable comma, semicolon, and tab detection.
- Record-aware parser for quoted delimiters, doubled quotes, and embedded CRLF/LF.
- Empty/trailing fields, blank records, Unicode, BOM, source spans, and diagnostics.
- xUnit.net v3 parser test project.

### Validated

- 38/38 parser and detector tests, Native AOT publish, and package creation passed.

## [0.2.0-alpha] — 2026-07-21

### Added

- Immutable active-editor snapshot.
- Notepad++/Scintilla live-buffer adapter, including unsaved changes.
- Metadata, SHA-256 identity, and 64 MiB safety limit.

### Validated

- Complete Notepad++ 8.9.7 x64 snapshot, dark-mode, lifecycle, and no-mutation acceptance passed.

## [0.1.0-alpha] — 2026-07-21

### Added

- Initial C#/.NET 10 Native AOT Notepad++ plugin shell.
- Dockable DataGridView panel and CI packaging.

### Validated

- Successful loading and runtime acceptance in Notepad++ 8.9.7 x64.
