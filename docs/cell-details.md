# Cell details

Select a data cell and press **Alt+Enter**, use **Table > Cell details**, or click
the cell-details icon. The resizable window shows the complete value, its UTF-16
length, space/tab counts and separate CRLF, LF and CR counts. Empty values are
distinguished from values containing only whitespace. The limit is 1,048,576
UTF-16 units per cell; oversized values are rejected, never truncated.

In read-only mode this is inspection only. To change the value, enable **Edit**
in the table first, then reopen the cell details. Edit mode opens on the ordinary
text tab: type spaces normally, press Enter for a line break, and type a single
backslash when the cell needs one. **Accept changes** or **Ctrl+Enter** updates
only that cell in the pending edit session. The table's **Apply** still performs
the checked Notepad++ update; **Revert All** discards all pending changes.
**Cancel**, **Esc** and closing the dialog discard only its unaccepted changes.
An unchanged value does not create a change.

## Exact text and ordinary editing

The **Exact text** tab is an advanced reversible notation. It exists so mixed
Windows/Unix line endings, tabs, literal backslashes and invisible UTF-16 units
can be inspected or edited without native edit-control normalization:

| Notation | Cell value |
| --- | --- |
| Middle dot (`·`) or `\u00B7` notation shown by the UI | Space (U+0020) |
| `\\` | One literal backslash |
| `\t` | Tab |
| `\r` | Carriage return |
| `\n` | Line feed |
| `\r\n` | CRLF line ending |
| `\uXXXX` | A UTF-16 code unit, with four hexadecimal digits |

The doubled backslash in Exact text is notation only; it does not mean that two
backslashes are stored in the CSV cell. Ordinary editing is the default in Edit
mode precisely so users do not need to type escape notation for normal work.
Existing mixed line-ending kinds are preserved by ordinal position while editing
ordinary text; newly inserted line breaks use the cell's existing line-ending
style when one exists, otherwise the Windows CRLF default. The exact tab remains
available when a specific CR/LF sequence must be controlled explicitly.

The selected stable row and physical column are checked before acceptance. A
refreshed table, removed row or independently changed cell rejects the stale edit.
Unsupported display languages continue to use the English interface.
