# Changelog

All notable changes to CSV Visual Editor will be documented in this file.

## [Unreleased]

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

- `Open Visual Table` and both Refresh actions now acquire the active editor snapshot.
- The panel no longer shows bootstrap placeholder columns; it shows sanitized snapshot metadata.
- About text and public documentation now describe the 0.2 snapshot boundary.

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