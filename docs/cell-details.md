# Cell details

Select a data cell and press **Alt+Enter**, use **Table > Cell details**, or click
the cell-details icon. The resizable window shows the complete value, its UTF-16
length, space/tab counts and separate CRLF, LF and CR counts. Empty values are
distinguished from values containing only whitespace. The limit is 1,048,576
UTF-16 units per cell; oversized values are rejected, never truncated.

In read-only mode this is inspection only. To change the value, enable **Edit**
in the table first, then reopen the cell details. **Accept changes** or
**Ctrl+Enter** updates only that cell in the pending edit session. The table's
**Apply** still performs the checked Notepad++ update; **Revert All** discards all
pending changes. **Cancel**, **Esc** and closing the dialog discard only its
unaccepted changes. An unchanged value does not create a change.

## Exact text and preview

The editable **Exact text** tab uses reversible notation so Windows controls
cannot silently normalize existing line endings:

| Notation | Cell value |
| --- | --- |
| Middle dot (`·`) | Space (U+0020) |
| `\\` | One backslash |
| `\t` | Tab |
| `\r` | Carriage return |
| `\n` | Line feed |
| `\r\n` | CRLF line ending |
| `\uXXXX` | A UTF-16 code unit, with four hexadecimal digits |

A literal middle dot is written `\u00B7`. Ordinary spaces and new line breaks
typed into the editor are also accepted. Unknown or incomplete escapes disable
acceptance and show their position. **Preview** is a read-only visual rendering;
it is never used as the stored value. **Wrap text** affects display only. Use Tab
to move focus and type `\t` to insert a tab.

The selected stable row and physical column are checked before acceptance. A
refreshed table, removed row or independently changed cell rejects the stale edit.
Unsupported display languages continue to use the English interface.
