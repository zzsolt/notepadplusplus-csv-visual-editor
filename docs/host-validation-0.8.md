# Milestone 0.8 host validation

## Target environment

- Notepad++ 8.9.7 x64
- Windows x64
- CSV Visual Editor 0.8.0-alpha
- UTF-8 test documents for successful Apply scenarios

Use synthetic data only. Confirm the SHA-256 values supplied with the test package before installation.

## Safety expectations

- Edit mode remains explicit and off by default.
- Add Row and Delete Row are enabled only in Edit mode.
- The header is never exposed as a deletable data row.
- Row operations use stable row identity, not the current visible grid index.
- Revert All never changes the Notepad++ editor buffer.
- Apply changes the active editor buffer only.
- Successful Apply currently requires Scintilla code page 65001 (UTF-8).
- A non-UTF-8 editor buffer is rejected before any replacement call.
- The plugin never saves directly to disk.
- No editor call occurs after document, code-page, content, or unsupported-encoding conflict.
- One Ctrl+Z undoes the complete combined cell/insertion/deletion Apply.
- One Ctrl+Y redoes the complete combined Apply.

## Test A — initial controls and locked transitions

Use a UTF-8 document:

```csv
EmailAddress,UserName,Group
alpha@example.invalid,alpha,Users
beta@example.invalid,beta,Admins
gamma@other.invalid,gamma,Users
delta@example.invalid,delta,Admins
```

1. Open the visual table.
2. Confirm the grid is read-only before Edit.
3. Confirm Add Row, Delete Row, Apply, and Revert All are disabled before Edit.
4. Enter Edit mode.
5. Confirm Add Row is enabled.
6. Confirm Delete Row is enabled only when a data row is selected.
7. Confirm Refresh, delimiter, header, Search, Clear, and sorting are disabled or refused.
8. Confirm all toolbar controls remain reachable at the normal dock width; resize narrower and verify ToolStrip overflow remains usable.
9. Confirm source rows display their original CSV logical-record numbers.

## Test B — Add Row and inserted-row identity

Using Test A data:

1. Select the `beta` row, whose source logical-record number is `3`.
2. Select **Add Row**.
3. Confirm a blank row appears immediately after `beta`.
4. Confirm the new row header is `new:1 *` or the equivalent first synthetic new-row marker.
5. Enter:

```text
inserted@example.invalid | inserted | Guests
```

6. Add a second row after the inserted row.
7. Confirm it receives a different synthetic identity such as `new:2 *`.
8. Confirm original source row numbers remain unchanged.
9. Confirm the dirty summary reports two inserted rows and the expected changed-row count.
10. Select Revert All and confirm both inserted rows disappear while the editor text remains unchanged.

## Test C — Delete source row and cancel inserted row

1. Enter Edit mode again.
2. Select the source row `beta` and choose **Delete Row**.
3. Confirm `beta` disappears from the pending grid.
4. Confirm the deletion count becomes one.
5. Add a new row, then immediately select it and choose Delete Row.
6. Confirm the inserted row disappears and the insertion count returns to zero; it must not become a source deletion.
7. Select Revert All.
8. Confirm `beta` returns in its original source position and with source row number `3`.
9. Confirm the Notepad++ editor buffer is unchanged.

## Test D — combined cell edit, insertion, deletion, Apply, undo, redo, and Save

1. Enter Edit mode.
2. Change `alpha` UserName to `alpha-edited`.
3. Delete the `beta` source row.
4. Add a new row after `gamma`:

```text
new@example.invalid | new-user | Guests
```

5. Confirm the dirty summary reports one changed cell, three unique changed rows, one insertion, and one deletion.
6. Select Apply.
7. Confirm the editor now contains the cell edit, omits `beta`, and contains the new row at the selected position.
8. Confirm Notepad++ displays the modified-document marker.
9. Confirm the plugin exits Edit mode and rebuilds the table from the editor buffer.
10. Press Ctrl+Z once.
11. Confirm the entire original CSV returns in one step.
12. Press Ctrl+Y once.
13. Confirm the complete combined change returns in one step.
14. Save normally through Notepad++.
15. Confirm the modified marker clears.

## Test E — source identity after sorting

1. Undo or recreate the original Test A data.
2. Outside Edit mode, sort EmailAddress descending.
3. Confirm row headers continue to show original source logical-record numbers.
4. Enter Edit mode.
5. Confirm source order is restored before structural editing.
6. Delete source row `3` (`beta`), regardless of where it appeared in the previous sorted view.
7. Apply.
8. Confirm `beta` is the deleted record and no other source row was removed.
9. Undo once and confirm the exact original CSV returns.

## Test F — header-only CSV receives its first data row

Use a UTF-8 document containing only:

```csv
Name,Age,City
```

1. Open the visual table and enter Edit mode.
2. Confirm Add Row is enabled even though there are zero data rows.
3. Select Add Row.
4. Enter:

```text
Alice | 30 | Budapest
```

5. Apply.
6. Confirm the editor contains the original header followed by one data record.
7. Confirm the original terminal-newline state is preserved:
   - a header ending with newline produces a new row and retains a terminal newline;
   - a header without newline gains only the required interior boundary and no terminal newline.
8. Undo once and confirm the exact header-only source returns.

## Test G — quoting and multiline values in inserted rows

Use:

```csv
Name,Note,City
Alice,plain,Budapest
```

Add a row containing:

- Name: ` Bob ` with leading and trailing spaces;
- Note: `contains,comma and "quote"`;
- City: a multiline value containing an actual line break and `東京 🗼`.

Apply and confirm:

- boundary whitespace is retained through quoting;
- the comma-containing value is quoted;
- embedded quotes are doubled;
- the multiline value remains one logical CSV field;
- Unicode is preserved;
- Refresh reparses the result without errors;
- one Ctrl+Z restores the exact original source.

## Test H — mixed separators and terminal-newline preservation

Create synthetic UTF-8 CSV variants with CRLF, LF, and a controlled mixed-EOL file.

Validate:

1. insertion between source rows retains the left source row's original separator;
2. the boundary after an inserted row follows the detected document newline policy;
3. append to a file without terminal newline does not add a terminal newline;
4. append to a file with terminal newline retains one terminal newline;
5. deleting the final row from a file without terminal newline does not create one;
6. deleting the final row from a file with terminal newline preserves it;
7. undo restores the exact original bytes as represented by the editor text.

## Test I — inconsistent-width preservation

Use:

```csv
Name,Age
Alice,30
Bob
Charlie,42,Extra
```

1. Select Comma manually if required.
2. Confirm the existing width diagnostics.
3. Enter Edit mode.
4. Edit Bob's padded Age cell to `40`.
5. Delete Alice.
6. Add a new two-column row.
7. Apply.
8. Confirm Bob becomes `Bob,40`.
9. Confirm Charlie remains `Charlie,42,Extra` without data loss.
10. Confirm the inserted row has the expected editable column width.
11. Undo once and confirm the exact original inconsistent-width text returns.

## Test J — content and active-document conflicts

### Content conflict

1. Start a dirty session containing a cell edit and/or row operation.
2. Modify the same Notepad++ editor buffer directly.
3. Select Apply.
4. Confirm Apply is blocked with a content-change message.
5. Confirm the direct editor change is not overwritten.
6. Confirm the dirty structural session remains available for review or Revert All.

### Wrong active document

1. Start a dirty session in document A.
2. Switch to document B.
3. Select Apply.
4. Confirm Apply is blocked because another document is active.
5. Confirm document B is unchanged.
6. Return to document A and Apply or Revert All.

## Test K — non-UTF-8 Apply refusal

1. Create a synthetic CSV and convert it in Notepad++ to a non-UTF-8 encoding such as ANSI/Windows-1252.
2. Enter Edit mode.
3. Add, delete, or modify a row.
4. Select Apply.
5. Confirm the UTF-8/Scintilla 65001 warning appears.
6. Confirm the editor buffer is unchanged.
7. Confirm the disk file is unchanged.
8. Confirm the pending structural session remains available for Revert All.

## Test L — lifecycle, dark mode, and no-change behavior

While a dirty structural Edit session exists:

- invoke global Refresh Table and confirm it is refused;
- hide and reopen the panel and confirm the session and synthetic row identities remain;
- attempt to exit Edit mode and confirm Apply/Revert All is required;
- switch light/dark mode and confirm all new controls, row headers, selection, and status text remain readable;
- confirm no automatic Apply occurs.

For a clean session:

- enter Edit mode without changing anything;
- confirm Apply and Revert All remain disabled;
- exit Edit mode normally;
- confirm editor and disk content remain unchanged.

After successful Apply or Revert All:

- panel hide/reopen works;
- active-tab refresh works normally;
- restart loads the editor text normally;
- no pending grid-only changes are silently written.

## Acceptance result

Record PASS/FAIL for every section. Do not commit screenshots containing private paths, real addresses, credentials, or production CSV values.
