# Cell context menu

Right-click a data cell to open its commands. Right-clicking outside the selection selects that cell; clicking inside a selected rectangle or selected whole rows preserves the selection and makes the clicked cell current. The menu key or Shift+F10 opens the same menu for the current cell. Headers, row indicators and unused space are not data-cell targets. During active text input, the text box keeps its usual text-editing context menu.

Read-only mode offers Cell details, Source, Copy, column summary, filtering, reset/refresh and space visibility. Edit mode additionally offers Cut, Paste, Transform, explicit row selection, Add/Delete Row and Apply/Revert All. Unavailable commands are disabled. Delete Row requires explicit whole-row selection; merely right-clicking a cell does not authorize deletion. Clipboard commands require a rectangular cell selection.

Menu actions use the same commands as the toolbar. Changes remain pending until Apply; Revert All discards pending changes. A refreshed/replaced table or changed selection invalidates an open menu rather than acting on a different target.
