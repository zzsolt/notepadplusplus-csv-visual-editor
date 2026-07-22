# Milestone 0.7 host validation

## Target environment

- Notepad++ 8.9.7 x64
- Windows x64
- CSV Visual Editor 0.7.0-alpha

Use synthetic data only. Confirm the SHA-256 values supplied with the test package before installation.

## Safety expectations

- Edit mode is explicit and off by default.
- Revert All never changes the Notepad++ editor buffer.
- Apply changes the active editor buffer only.
- The plugin never saves directly to disk.
- No editor call occurs after document, code-page, or content conflict.
- One Ctrl+Z must undo the complete Apply.
- One Ctrl+Y must redo the complete Apply.

## Test A — basic Edit, dirty state, and Revert All

Use:

```csv
EmailAddress,UserName,Group
alpha@example.invalid,alpha,Users
beta@example.invalid,beta,Admins
gamma@other.invalid,gamma,Users
delta@example.invalid,delta,Admins
```

1. Open the visual table.
2. Confirm the grid is read-only before Edit.
3. Select **Edit**.
4. Confirm Refresh, Delimiter, Header, Search, and sorting are disabled.
5. Change `beta` to `beta-edited`.
6. Confirm the original source row number remains `3` and displays `*`.
7. Confirm change count reports one cell in one row.
8. Select **Revert All**.
9. Confirm the grid returns to `beta`, the `*` disappears, and the editor text remains unchanged.
10. Leave Edit mode successfully after the session is clean.

## Test B — Apply, modified marker, undo, redo, and Save ownership

1. Enter Edit mode again.
2. Change `beta` to `beta-applied`.
3. Select **Apply**.
4. Confirm the Notepad++ editor text changes immediately.
5. Confirm Notepad++ displays its modified-document marker.
6. Confirm the plugin leaves Edit mode and rebuilds the table from the editor.
7. Press Ctrl+Z once.
8. Confirm the complete pre-Apply CSV returns in one step.
9. Press Ctrl+Y once.
10. Confirm the complete applied CSV returns in one step.
11. Use normal Notepad++ Save.
12. Confirm the modified marker clears through the normal Notepad++ workflow.

## Test C — structural CSV serialization

Use:

```csv
Name,Note,City
Alice,plain,Budapest
Bob,simple,Szeged
```

In Edit mode set cells to:

- `Alice` Note: `contains,comma`
- `Bob` Note: `contains "quote"`
- `Bob` City: a multiline value containing an actual line break
- one value: `東京 🗼`
- one value with leading and trailing spaces

Apply and confirm:

- delimiter-containing values are quoted;
- quotes are doubled;
- multiline values remain one logical CSV field;
- Unicode is preserved;
- boundary spaces are preserved through quoting;
- Refresh reparses the result without errors;
- one Ctrl+Z restores the exact original text.

## Test D — inconsistent-width preservation

Use:

```csv
Name,Age
Alice,30
Bob
Charlie,42,Extra
```

1. Select Comma manually if automatic detection requires it.
2. Confirm `CSV004` warnings for source records 3 and 4.
3. Enter Edit mode.
4. Edit Bob's padded Age cell to `40`.
5. Apply.
6. Confirm Bob becomes `Bob,40`.
7. Confirm Charlie remains `Charlie,42,Extra` without data loss.
8. Undo once and confirm the exact original inconsistent-width text returns.

## Test E — content conflict

1. Enter Edit mode and change one grid cell.
2. Without applying, modify the same Notepad++ editor buffer directly.
3. Select Apply in the plugin.
4. Confirm Apply is blocked with a content-change message.
5. Confirm the plugin does not overwrite the direct editor change.
6. Confirm the dirty grid session remains available for review or Revert All.

## Test F — wrong active document conflict

1. Start a dirty Edit session in document A.
2. Switch to document B.
3. Select Apply.
4. Confirm Apply is blocked because another document is active.
5. Confirm document B is unchanged.
6. Return to document A and either Apply or Revert All.

## Test G — lifecycle and locked transitions

While a dirty Edit session exists:

- invoke the global **Refresh Table** command and confirm it is refused;
- hide and reopen the panel and confirm the session remains;
- attempt to exit Edit mode and confirm Apply/Revert All is required;
- confirm dark mode remains readable;
- confirm no automatic Apply occurs.

After Revert All or successful Apply:

- panel hide/reopen works;
- active-tab refresh works normally;
- restart loads the editor text normally;
- no pending grid-only changes are silently written.

## Test H — no-change behavior

1. Enter Edit mode without changing a cell.
2. Confirm Apply remains disabled.
3. Exit Edit mode normally.
4. Confirm editor and disk content remain unchanged.

## Acceptance result

Record PASS/FAIL for every section. Do not commit screenshots containing private paths, real addresses, credentials, or production CSV values.
