# Changelog

All notable changes to CSV Visual Editor will be documented in this file.

## [Unreleased]

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
- No editable grid, Scintilla write, or disk write is enabled in this foundation increment.

### Validated

- 112/112 xUnit tests passed.
- Strict core Release build and bootstrap smoke passed.
- Native AOT deterministic serialization passed.
- Native AOT dirty tracking, preview, Revert All, Ready planning, and ContentChanged blocking passed.
- Full win-x64 Native AOT plugin publish passed.

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
- xUnit coverage for search, selected-column filtering, stable sorting, filter/sort composition, invalid indexes, and projection immutability.
- Native AOT runtime coverage for table search, sorting, ToolStrip search controls, debounce timer, TabControl, and diagnostics DataGridView.
- `0.5.0-alpha` Native AOT test-package naming.

### Changed

- CSV data columns use programmatic sort glyphs while the underlying parser projection remains unchanged.
- Status text reports matching-row counts and the active view sort.
- Dark-mode handling covers both toolbars, both tabs, the visual table, and diagnostics grid.
- `CsvDialectDetectionResult.Diagnostics` is exposed through the `IReadOnlyList<CsvDiagnostic>` abstraction rather than a concrete collection type.
- About text describes the 0.5 read-only search and diagnostics boundary.

### Safety

- Search, filtering, and sorting remain entirely view-only.
- Stable sorting preserves original source order for equal values.
- Source logical-record numbers remain attached to filtered and sorted rows.
- No Scintilla write, disk write, serializer, edit, or write-back path was added.

### Validated

- 81/81 xUnit tests passed.
- Native AOT search, filtering, stable sorting, ToolStrip, Timer, TabControl, and diagnostics-grid runtime paths passed.
- Notepad++ 8.9.7 x64 owner acceptance passed for layout, search, selected-column filtering, Clear, three-state sorting, source row identity, valid and malformed diagnostics, delimiter/header rebuild, Refresh, active-tab switching, hide/reopen, restart, dark mode, and no editor/disk mutation.

## [0.4.0-alpha] — 2026-07-22

### Added

- Read-only visual CSV table backed by immutable parser output.
- Host-independent `CsvTableBuilder` result states for empty input, manual delimiter requirement, and ready tables.
- Visible delimiter selector for automatic, comma, semicolon, and tab modes.
- Visible header selector for First row is header and No header row modes.
- Host-independent table projection with deterministic fallback, duplicate, empty, whitespace, and multiline header handling.
- Source logical-record numbers in DataGridView row headers.
- Manual delimiter recovery when automatic detection is weak, ambiguous, or unavailable.
- Explicit 10,000-row, 512-column, and 250,000-cell visual limits with non-silent behavior.
- xUnit table-builder and projection tests for delimiter gating, header modes, unique names, inconsistent widths, limits, and empty input.
- Native AOT runtime smoke library that executes CSV table building and DataGridView population for both automatic and manual-comma modes.
- DPI-aware initial dock-width policy with tests for default, user-sized, small-host, and high-DPI layouts.
- Content-aware, bounded column fill-weight calculation with deterministic row sampling and multiline handling.
- Developer and contact information in the About dialog.

### Changed

- The panel renders parsed CSV values instead of snapshot metadata when a trustworthy or explicitly selected dialect is available.
- Automatic detection is accepted only at Medium or High confidence.
- Inconsistent-width records are padded only in the rectangular view and remain unchanged in parser output.
- The displayed row count is constrained by both row and aggregate-cell budgets.
- A newly created right-side panel that is still at the Notepad++ default width is expanded once to a usable width after first display.
- Existing user-sized wider dock layouts are preserved, and small Notepad++ windows retain a minimum editor area.
- Visual CSV columns use `DataGridView` Fill sizing, weighted by sampled header and cell lengths.
- Every visual CSV column retains a 90-pixel minimum width, with horizontal scrolling for wide tables.
- Column fill sizing follows subsequent dock-panel resizing while preserving manual column resize support.
- CI diagnostics cover strict build, bootstrap smoke, xUnit core tests, Native AOT table runtime execution, and plugin publishing separately.

### Fixed

- Preserved the public parameterless `DataGridViewRowHeaderCell` constructor required by WinForms reflective row-header creation under Native AOT.
- Corrected the unusably narrow first-open dock layout caused by host registration before the derived WinForms `ClientSize` is applied.
- Removed unused right-side grid workspace for tables whose natural column widths are narrower than the dock panel.

### Validated

- 69/69 xUnit tests passed.
- Native AOT table/DataGridView runtime smoke passed.
- Notepad++ 8.9.7 x64 owner acceptance passed.
- Accepted public squash merge: `d17dd7f46f600fac2896f21239d7833dd00dd727`.

## [0.3.0-alpha] — 2026-07-21

### Added

- Host-independent CSV dialect model for comma, semicolon, and tab delimiters.
- Explainable delimiter detection with candidate scores, confidence levels, ambiguity diagnostics, and decimal-comma caution.
- Character-state CSV parser that processes logical records without splitting quoted multiline fields.
- Support for empty and trailing fields, quoted delimiters, doubled quotes, embedded CRLF/LF, blank records, Unicode, and leading U+FEFF handling.
- Immutable CSV record/cell models with raw decoded-text source spans.
- Structured diagnostics for malformed quotes and inconsistent field counts.
- xUnit.net v3 parser test project pinned to `xunit.v3.mtp-v2` 3.2.2.
- Public parser and test-strategy documentation.

### Validated

- Strict core build, 38/38 parser and detector tests, Native AOT publish, and clean x64 package creation.

## [0.2.0-alpha] — 2026-07-21

### Added

- Immutable `ActiveDocumentSnapshot` core model.
- Host abstraction for reading the active editor document.
- Notepad++/Scintilla adapter that reads the current editor buffer, including unsaved changes.
- Snapshot metadata for document identity, character and editor-byte lengths, code page, caret and selection, modified state, UTC capture time, and SHA-256 content identity.
- Explicit 64 MiB snapshot safety limit with a visible non-destructive error state.
- Docked metadata view and refresh behavior for the active buffer.
- Core smoke checks for snapshot creation, hashing, normalization, and invalid input.

### Changed

- `Open Visual Table` and both Refresh actions acquire the active editor snapshot.
- The panel shows sanitized snapshot metadata instead of bootstrap placeholder columns.

### Validated

- Automated build, smoke-test, Native AOT publish, and package creation.
- Complete Notepad++ 8.9.7 x64 host acceptance, including unsaved buffers, tab switching, both Refresh paths, untitled buffers, dark mode, lifecycle behavior, and no editor modification.

## [0.1.0-alpha] — 2026-07-21

### Added

- Initial C#/.NET 10 Notepad++ plugin project based on the official `Npp.DotNet.Plugin` WinForms integration model.
- Dockable `DataGridView` shell with refresh command and status bar.
- Native AOT x64 publish workflow.
- Host-independent core project and dependency-free smoke checks.
- Public architecture and build documentation.

### Validated

- Automated restore, build, smoke-test, Native AOT publish, and package creation.
- Successful manual loading and runtime acceptance in Notepad++ 8.9.7.
