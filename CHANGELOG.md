# Changelog

All notable changes to CSV Visual Editor will be documented in this file.

## [Unreleased]

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
- The first writable alpha permits Apply only for Scintilla code page 65001 (UTF-8); viewing remains available for other readable code pages.

### Safety

- Apply reads a fresh active-document snapshot immediately before replacement.
- No Scintilla replacement method is called for NoChanges or any conflict state.
- Non-UTF-8 Apply is blocked before constructing the replacement adapter, preventing lossy code-page conversion.
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
- Accepted host coverage included Edit/Revert, dirty indicators, UTF-8 Apply, one-step Ctrl+Z/Ctrl+Y, modified marker, normal Save ownership, structural and multiline CSV values, Unicode, inconsistent-width preservation, content and active-document conflict blocking, lifecycle behavior, no-change behavior, and non-UTF-8 Apply refusal.

## [0.6.0-alpha] — 2026-07-22

### Added

- Host-independent `CsvSerializationPolicy` for delimiter, quote, record-separator, terminal-newline, and leading-BOM behavior.
- Deterministic comma, semicolon, and tab CSV serialization.
- Safe quoting for delimiters, quotes, CR/LF, and leading/trailing whitespace.
- `CsvEditSession` with immutable active-document baseline.
- Per-cell dirty tracking and changed-cell/record counts.
- Cell and full-session reversion.
- Minimal-difference edit previews that retain unchanged raw records and exact following separators.
- Original field-count retention for inconsistent-width records.
- Safe extension of display-padded records only when padded cells are edited.
- `CsvEditApplyPlan` conflict states for document identity, code page, and editor-content changes.
- Strict stale-parser verification by reparsing the active snapshot and comparing records, cells, spans, quoted states, and diagnostics.
- Preview replacement SHA-256.
- Public safe-editing architecture documentation.
- Native AOT serializer, edit-session, reversion, Ready-plan, and content-conflict runtime coverage.

### Safety

- Parser-error and row-limited projections cannot start an edit session.
- Stale parser output cannot be reused for different source text, even with matching length and compatible spans.
- Unchanged records are never unnecessarily normalized.
- Missing display-only fields are not silently written into source records.
- Extra and trailing source fields are retained.
- Conflict plans contain no replacement preview.
- No editable grid, Scintilla write, or disk write was enabled in this foundation increment.

### Validated

- 112/112 xUnit tests passed.
- Strict core Release build and bootstrap smoke passed.
- Native AOT deterministic serialization passed.
- Native AOT dirty tracking, preview, Revert All, Ready planning, and ContentChanged blocking passed.
- Full win-x64 Native AOT plugin publish passed.
- Accepted public squash merge: `5184de060c775d2df6db63fa9b42a9be3cf212c1`.

## [0.5.0-alpha] — 2026-07-22

### Added

- Host-independent `CsvTableViewBuilder` for immutable search, filtering, and stable view sorting.
- Case-insensitive search across every visible column.
- Optional search scope limited to one selected column.
- 250 ms search debounce in the WinForms panel.
- Three-state programmatic column sorting: ascending, descending, and original source order.
- Dedicated Table and Diagnostics tabs.
- Detailed diagnostics grid with severity, code, logical-record number, character offset, and message.
- Combined delimiter-detection and parser diagnostics.
- Clear action for the current search and view sort.
- xUnit and Native AOT coverage for search, filtering, stable sorting, ToolStrip, Timer, TabControl, and diagnostics DataGridView.

### Safety

- Search, filtering, and sorting remain entirely view-only.
- Stable sorting preserves original source order for equal values.
- Source logical-record numbers remain attached to filtered and sorted rows.
- No Scintilla write, disk write, edit, or write-back path was added.

### Validated

- 81/81 xUnit tests passed.
- Complete Notepad++ 8.9.7 x64 owner acceptance passed.
- Accepted public squash merge: `9aa11992142297c7c40c021d71c3565a71977217`.

## [0.4.0-alpha] — 2026-07-22

### Added

- Read-only visual CSV table backed by immutable parser output.
- Automatic/manual delimiter and explicit header controls.
- Host-independent rectangular projection with special-header handling.
- Source logical-record numbers in DataGridView row headers.
- Explicit row, column, and aggregate-cell limits.
- Native AOT DataGridView runtime smoke.
- DPI-aware initial dock-width policy.
- Content-aware full-width column sizing.
- Developer and contact information in About.

### Fixed

- Preserved the WinForms row-header constructor required under Native AOT.
- Corrected the unusably narrow first-open dock layout.
- Removed unused right-side grid workspace.

### Validated

- 69/69 xUnit tests passed.
- Complete Notepad++ 8.9.7 x64 owner acceptance passed.
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
