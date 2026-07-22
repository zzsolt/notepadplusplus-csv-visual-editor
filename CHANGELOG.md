# Changelog

All notable changes to CSV Visual Editor will be documented in this file.

## [Unreleased]

### Added

- Read-only visual CSV table backed by immutable parser output.
- Visible delimiter selector for automatic, comma, semicolon, and tab modes.
- Visible header selector for First row is header and No header row modes.
- Host-independent table projection with deterministic fallback, duplicate, empty, whitespace, and multiline header handling.
- Source logical-record numbers in DataGridView row headers.
- Manual delimiter recovery when automatic detection is weak, ambiguous, or unavailable.
- Explicit 10,000-row and 512-column visual-table limits with non-silent behavior.
- xUnit table-projection tests for header modes, unique names, inconsistent widths, limits, and empty input.
- Developer and contact information in the About dialog.
- `0.4.0-alpha` Native AOT test-package naming.

### Changed

- The panel now renders parsed CSV values instead of snapshot metadata when a trustworthy or explicitly selected dialect is available.
- Automatic detection is accepted only at Medium or High confidence.
- Inconsistent-width records are padded only in the rectangular view and remain unchanged in parser output.
- CI diagnostics now cover the parser and table-projection test matrix together.
- About text describes the 0.4 read-only table boundary.

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
