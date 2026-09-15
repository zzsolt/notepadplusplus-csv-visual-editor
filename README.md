# CSV Visual Editor

A 64-bit Notepad++ plugin for viewing, editing, filtering and inspecting CSV data in a docked table.

## Features

- Comma, semicolon and tab delimiters, automatic detection and optional headers.
- Quoted fields, embedded line breaks, Unicode and parser diagnostics.
- Search, column filters, multi-column text/numeric sorting and column summaries.
- Cell editing, row operations, rectangular copy/cut/paste and previewed text transformations.
- Navigation from a table cell to its source field and optional visible space markers.
- Conflict-checked Apply with one Notepad++ undo action. Save remains under Notepad++ control.
- Interface translations: English, Hungarian, Simplified Chinese, Hindi, Spanish, Arabic and French; other Notepad++ languages fall back to English.

## Installation

Close Notepad++ and extract the package into its `plugins` directory:

```text
plugins/CsvVisualEditor/CsvVisualEditor.dll
```

Use the **x64** package with **64-bit Notepad++**. No separate .NET runtime is required. Keep a backup of an existing DLL before replacing it.

Open **Plugins > CSV Visual Editor > Open Visual Table**. The table reads the active document, including unsaved edits. Grid changes remain pending until **Apply**.

See the [user guide](docs/user-guide.md) for commands, supported encodings and table limits, or [building and contributing](docs/building.md) for source builds.

## Support

Developer: **Zolnai Zsolt**. Contact: **zzsolt@gmail.com**.

Enjoy the plugin? Support the project by reporting issues, contributing improvements or sharing it with others.
