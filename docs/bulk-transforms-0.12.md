# Previewed bulk transformations — 0.12 development

[Részletes magyar tesztlépések](host-validation-0.12-transforms-hu.md)

The **Transform** toolbar command is available in Edit mode. It operates on pending CSV values. Preview and acceptance never write the Notepad++ buffer or files. The existing **Apply** command retains document/content/code-page checks, strict lossless encoding and one editor undo transaction.

## Operations and scopes

| Option | Meaning |
|---|---|
| Replace text (literal) | Replace every non-overlapping occurrence, without regular expressions. Empty replacement deletes matches; empty Find is rejected. Match case uses ordinal comparison. |
| Trim outer whitespace | Remove leading/trailing Unicode whitespace, including tabs/newlines; internal whitespace is preserved. |
| UPPERCASE / lowercase | Culture-independent invariant casing, including Hungarian accented letters. |
| Selected cells / rows | Selected real CSV cells plus all CSV cells of explicitly selected complete rows. |
| Current column | All visible model rows in the current physical CSV data column. Selecting `#`/`Select` does not choose a data column. |
| All data cells | All non-deleted rows in the bounded Edit model, including pending inserted rows. |

When the first record is interpreted as the header, it is not part of the editable data scope. With no header, the first record is ordinary data. Edit mode already clears view sorting/filtering; transformations do not add a hidden filtered-only scope.

**Preview** shows exact target, changed-cell and changed-row counts, plus the first 50 changes. Sample values are shortened after 240 characters and line breaks/tabs/backslashes are escaped for readability; the pending values are not shortened. Changing any option invalidates the preview. **Accept changes** is enabled only after a preview with changes. Cancel, Escape and the window close button discard the preview. Enter runs Preview, not acceptance.

The preview is bound to its original edit-model instance. Every target's original value and row presence/deletion state is checked before mutation, including target cells that did not match. A stale preview is rejected as a whole. Other pending edits outside the target set are preserved. Targets use stable row IDs and physical column indexes; `#` and `Select` remain presentation only. Accepted changes update the existing grid in place to retain selection and viewport.

The operation is bounded to 250,000 unique target cells and 16 Mi UTF-16 result characters across those targets. Literal replacement growth is measured before allocating replacement strings. Exceeding a limit rejects the entire preview; reduce the scope. Large Edit-mode previews are synchronous within those explicit bounds; real-host responsiveness is still a validation gate.

## Automated coverage and evidence boundary

Core tests cover literal/case-sensitive/case-insensitive replacement, invariant casing, whitespace/empty values, nonrecursive replacement, duplicate targets, pending originals, unchanged targets becoming stale, unknown/deleted/cancelled targets, cross-model rejection, inserted rows, serialization, exact Revert All and allocation limits. Native AOT smoke executes transformation, serialization, single-undo Apply, conflict zero calls, stale rejection, Revert All and construction of the actual dialog. Full CI and production publish are required before package delivery.

Real Notepad++ tests below are **NOT RUN** until the owner reports them. Automated results do not establish host PASS.

Implementation commit `67ed59178559782de53133764f674b7eed4d1ce2` passed [CI 34019138228](https://github.com/zzsolt/notepadplusplus-csv-visual-editor/actions/runs/34019138228), job `101448355524`, tested merge `d844a451143ad6fd2c99042ef29981af14dc82b5`: 272/272 Core tests, 0 errors/failures/skips/not-run, 1.384s; 9/9 package-policy checks, Native AOT runtime and production publish PASS. Its six diagnostic artifacts contain no install ZIP. The subsequent candidate adds cell-limit, no-header and Windows-1250 transform/Apply tests; its own final run and package hashes are recorded in PR #12 before delivery.

## Host test preparation

1. Install only the explicitly linked, fully green candidate build. Close Notepad++ before replacing the DLL. Record its SHA-256 (PowerShell `Get-FileHash`), Windows version, Notepad++ version and x64.
2. Open a new untitled UTF-8 document. Paste this entirely synthetic CSV, preserving the spaces inside quotes:

   ```csv
   Name,City,Note
   "  anna  ","  Budapest  ","alpha alpha"
   "  BÉLA  ","  Szeged  ","Alpha beta"
   "  csilla  ","  Pécs  ","keep  inner  spaces"
   ```

3. Open CSV Visual Editor. Set comma delimiter and first-record header explicitly, then Refresh. Do not save the file; unsaved-buffer operation is intentional.
4. Use a fresh copy of the fixture for each case, or Revert All and refresh as specified. Record only case ID, PASS/FAIL/NOT RUN and a sanitized observation. No real CSV values/screenshots/paths should be committed.

## T1 — Preview, cancel and selected-cell trim

1. Confirm Transform is disabled in read-only mode. Click Edit.
2. Click the `anna` data cell once. Open Transform. Choose **Trim outer whitespace**, leave **Selected cells / rows**, click Preview.
3. Expect one target, one changed cell and one changed row; Before has surrounding spaces, After is `anna`. The grid and Notepad++ text must still have the original values.
4. Click Cancel. Confirm no new pending change. Reopen, choose Trim, Preview, then Accept changes.
5. Only that cell becomes `anna`; the city and other names keep their spaces. Notepad++ text remains exactly the original fixture. Revert All restores the spaces and clears dirty state.

## T2 — Current column and literal replacement

1. In a fresh Edit session click the first row's Note cell, then Transform.
2. Choose Replace text, Current column, Find `alpha`, Replace with `X`, Match case checked. Preview must show three target cells and one changed cell: `alpha alpha` becomes `X X`.
3. Uncheck Match case. The previous preview and Accept changes must become unavailable until Preview is clicked again.
4. Preview again: two changed cells; the second note becomes `X beta`. Accept. Names/cities and the third note are unchanged; source text is unchanged before Apply.
5. Revert All. Optionally repeat with empty replacement to delete only matches. An empty Find must not produce an applicable preview.

## T3 — Complete-row selection and inserted rows

1. In Edit mode select the second complete row via its row header, `#` or Select checkbox. Open Transform, choose Trim and Selected cells / rows.
2. Expect three target cells and two changes (name/city); no `#` or Select target. Accept. Complete-row selection should remain visibly selected and usable by Delete Rows.
3. Revert All. Add a row, enter a value with outer spaces and select that cell. Trim with preview/accept must work for the inserted row as well.
4. The inserted row still has no original Source address before Apply. Revert All removes the inserted row and restores source-backed values.

## T4 — All data cells, casing and no-op

1. In a fresh Edit session choose Transform, Trim, All data cells. Expect nine target cells and six changed cells in three rows.
2. Accept; inner spaces in `keep  inner  spaces` must remain. Header labels remain unchanged.
3. Preview Trim again: zero changed cells, Accept changes disabled. Cancel.
4. Open Transform and preview UPPERCASE, then lowercase. Hungarian letters must remain valid. Option changes require a new preview each time. Cancel or accept intentionally, then Revert All.

## T5 — Apply, one Undo/Redo and conflicts

1. Accept the six-cell All data cells Trim. Before Apply, confirm Notepad++ still contains the original fixture.
2. Click the main toolbar Apply. Confirm all six changes reach the active buffer together; no disk save is performed.
3. Focus the Notepad++ editor and press Ctrl+Z once. All six changes must revert together. Ctrl+Y once restores all six. Refresh the plugin before further navigation/editing.
4. Restart with the original fixture. Accept a transform, then directly change the Notepad++ buffer. Main Apply must block the stale replacement and preserve the external text and pending edits. Record a sanitized status, then Revert All, leave Edit and Refresh.

## T6 — Windows-1250 and representability

1. Use a fresh synthetic fixture encoded as actual Windows-1250, with accented Hungarian data. Preview/accept casing and trimming, then Apply. Representable values must be written losslessly.
2. In another fresh Windows-1250 Edit session, replace a synthetic marker with an emoji using Transform. Preview/accept may create pending Unicode text; main Apply must block the unrepresentable replacement before any host write.
3. Confirm original source bytes/text and editor undo state are unchanged by the blocked Apply; pending edits remain available for correction or Revert All. Do not log the rejected content.

Source navigation F2–F4 and exact Windows-1250 source offsets remain separate checks in [the Source host matrix](host-validation-0.12-source-navigation.md). The previously reported F1 success does not stand in for those checks or for these new Transform tests.
