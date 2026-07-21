# Changelog

All notable changes to CSV Visual Editor will be documented in this file.

## [Unreleased]

### Added

- Host-independent CSV dialect model for comma, semicolon, and tab delimiters.
- Explainable delimiter detection with candidate scores, confidence levels, ambiguity diagnostics, and decimal-comma caution.
- Character-state CSV parser that processes logical records without splitting quoted multiline fields.
- Support for empty and trailing fields, quoted delimiters, doubled quotes, embedded CRLF/LF, blank records, and leading U+FEFF handling.
- Immutable CSV record/cell models with raw decoded-text source spans.
- Structured diagnostics for malformed quotes and inconsistent field counts.
- xUnit.net v3 parser test project pinned to `xunit.v3.mtp-v2` 3.2.2.
- Public parser and test-strategy documentation.
- `0.3.0-alpha` Native AOT test-package naming.

### Changed

- CI now runs both the dependency-free bootstrap smoke checks and the xUnit parser matrix.
- About text describes the 0.3 parser boundary.

## [0.2.0-alpha] — 2026-07-21

### Added

- Immutable `ActiveDocumentSnapshot` core model.
- Host abstraction for reading the active editor document.
- Notepad++/Scintilla adapter that reads the current editor buffer, including unsaved changes.
- Snapshot metadata for document identity, character and editor-byte lengths, code page, caret and selection, modified state, UTC capture time, and SHA-256 content identity.
- Explicit 64 MiB snapshot safety limit with a visible non-destructive error state.
- Docked metadata view and refresh behavior for the active buffer.
- Core smoke checks for snapshot creation, hashing, normalization, and invalid input.
- `0.2.0-alpha` Native AOT test package.

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
