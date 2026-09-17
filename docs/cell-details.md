# Cell details

Select a data cell and press **Alt+Enter**, use the toolbar, or choose
**Table > Cell details**. The window is resizable and reports exact character,
space, tab and CRLF/LF/CR line-ending counts.

In the table's Edit mode, the window opens on **Edit**, an ordinary text editor.
Type spaces and backslashes normally. Enter inserts a real line break; it does
not accept the dialog. A literal `\n` remains a backslash followed by the letter
`n`. Existing line endings outside the changed range are retained, including
mixed CRLF/LF/CR data. New ordinary line breaks follow the cell's first existing
line-ending convention, or CRLF for a cell without line breaks.

**Exact text** remains an advanced view for inspecting or editing precise
characters. Its legend appears only on that tab. In this notation `\t`, `\n`,
`\r` and `\\` represent a tab, LF, CR and one literal backslash. A middle-dot
marker represents a space; `\u00B7` represents a literal middle dot. Changes in
one tab update the other without double-encoding the stored value.

**Accept changes** or **Ctrl+Enter** updates the pending cell edit only. The
main table's **Apply** command is still the only action that writes changes to
the Notepad++ document. **Cancel/Esc** discards unaccepted dialog changes.
Outside Edit mode the window is read-only. Invalid exact notation, oversized
values and a stale target are rejected rather than partially applied.
