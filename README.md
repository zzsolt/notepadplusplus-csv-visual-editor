# CSV Visual Editor

A 64-bit Notepad++ plugin for viewing, editing, filtering and inspecting CSV data in a docked table.

## Features

- Comma, semicolon and tab delimiters, automatic detection and optional headers.
- Quoted fields, embedded line breaks, Unicode and parser diagnostics.
- Search, column filters, multi-column text/numeric sorting and column summaries.
- Cell editing, row operations, rectangular copy/cut/paste and previewed text transformations.
- Navigation from a table cell to its source field and optional visible space markers.
- Conflict-checked Apply with one Notepad++ undo action. Save remains under Notepad++ control.
- Natural multiline cell details and mode-aware right-click commands for data cells.
- Interface translations: English, Hungarian, Simplified Chinese, Hindi, Spanish, Arabic and French; other Notepad++ languages fall back to English.

## Version

The current stable maintenance line is **1.0.1** for 64-bit Notepad++ on Windows.
The production release gate is tested with Notepad++ 8.9.8 x64.

## Installation

For manual installation, create the plugin folder and extract the release ZIP
contents into it:

```text
plugins/CsvVisualEditor/CsvVisualEditor.dll
```

Use the **x64** package with **64-bit Notepad++**. No separate .NET runtime is required. The release ZIP also contains the project license and third-party notices. Keep a backup of an existing DLL before replacing it.

A Plugins Admin submission is prepared for the official Notepad++ plugin list. Direct installation from Notepad++ becomes available only after that upstream entry is accepted.

Open **Plugins > CSV Visual Editor > Open Visual Table**. The table reads the active document, including unsaved edits. Grid changes remain pending until **Apply**.

See the [user guide](docs/user-guide.md) for commands, supported encodings and table limits, [building and contributing](docs/building.md) for source builds, or [Plugins Admin packaging](packaging/nppPluginList/README.md) for the upstream submission format.

## License

CSV Visual Editor project code is licensed under **GNU GPL v3.0 only
(GPL-3.0-only)**. See [LICENSE](LICENSE). Bundled third-party/runtime components
remain under their own licenses; see [THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt).

## Support

Developer: **Zolnai Zsolt**. Contact: **zzsolt@gmail.com**.

Enjoy the plugin? Support the project by reporting issues, contributing improvements or sharing it with others.
