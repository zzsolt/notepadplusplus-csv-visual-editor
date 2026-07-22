# Milestone 0.7 host validation

## Acceptance status

**ACCEPTED — 2026-07-22**

The repository owner confirmed that every test in this document behaved as specified in Notepad++ 8.9.7 x64. The submitted evidence also showed:

- content-conflict Apply blocking while retaining the dirty grid session;
- non-UTF-8/ANSI Apply refusal with no editor content change;
- dirty row marker and changed-cell/record counters remaining visible.

Only synthetic test data was used. Screenshots are not committed to this repository.

## Target environment

- Notepad++ 8.9.7 x64
- Windows x64
- CSV Visual Editor 0.7.0-alpha
- UTF-8 test documents for successful Apply scenarios

## Safety expectations

- Edit mode is explicit and off by default.
- Revert All never changes the Notepad++ editor buffer.
- Apply changes the active editor buffer only.
- Successful Apply currently requires Scintilla code page 65001 (UTF-8).
- A non-UTF-8 editor buffer is rejected before any replacement call.
- The plugin never saves directly to disk.
- No editor call occurs after document, code-page, content, or unsupported-encoding conflict.
- One Ctrl+Z undoes the complete Apply.
- One Ctrl+Y redoes the complete Apply.

## Test A — basic Edit, dirty state, and Revert All

**Result: PASS**

Use a UTF-8 document:

```csv
EmailAddress,UserName,Group
alpha@example.invalid,alpha,Users
beta@example.invalid,beta,Admins
gamma@other.invalid,gamma,Users
delta@example.invalid,delta,Admins
```

Validated behavior:

1. The grid is read-only before Edit.
2. Edit mode enables cell editing.
3. Refresh, Delimiter, Header, Search, and sorting are disabled.
4. A changed source record retains row number `3` and displays `*`.
5. The change count reports one cell in one row.
6. Revert All restores the original grid value and removes the marker.
7. Revert All does not change the Notepad++ editor text.
8. Clean Edit mode can be exited normally.

## Test B — Apply, modified marker, undo, redo, and Save ownership

**Result: PASS**

Validated behavior:

1. Apply succeeds for a UTF-8 editor buffer.
2. The Notepad++ editor text changes immediately.
3. Notepad++ displays its modified-document marker.
4. The plugin leaves Edit mode and rebuilds the table from the editor.
5. One Ctrl+Z restores the complete pre-Apply CSV.
6. One Ctrl+Y restores the complete applied CSV.
7. Normal Notepad++ Save clears the modified marker.
8. The plugin performs no direct disk save.

## Test C — structural CSV serialization

**Result: PASS**

Use a UTF-8 document:

```csv
Name,Note,City
Alice,plain,Budapest
Bob,simple,Szeged
```

Validated edited values included:

- a delimiter-containing value;
- quotes;
- an actual multiline value;
- Unicode text including `東京 🗼`;
- leading and trailing spaces.

Validated behavior:

- delimiter-containing values are quoted;
- quotes are doubled;
- multiline values remain one logical CSV field;
- Unicode is preserved;
- boundary spaces are preserved through quoting;
- Refresh reparses the result without errors;
- one Ctrl+Z restores the exact original text.

## Test D — inconsistent-width preservation

**Result: PASS**

Use a UTF-8 document:

```csv
Name,Age
Alice,30
Bob
Charlie,42,Extra
```

Validated behavior:

- `CSV004` warnings identify the inconsistent records;
- editing Bob's padded Age cell produces `Bob,40`;
- Charlie remains `Charlie,42,Extra`;
- extra data is not discarded;
- one undo restores the exact original inconsistent-width text.

## Test E — content conflict

**Result: PASS**

Validated behavior:

- a direct editor-buffer change after Edit mode starts blocks Apply;
- the status reports that the editor buffer changed;
- the direct editor change is not overwritten;
- the dirty grid session remains available for review or Revert All;
- no blind replacement occurs.

## Test F — wrong active document conflict

**Result: PASS**

Validated behavior:

- a dirty session started in document A cannot be applied while document B is active;
- document B remains unchanged;
- returning to document A allows the session to be handled safely.

## Test G — lifecycle and locked transitions

**Result: PASS**

Validated behavior while a dirty Edit session exists:

- global Refresh Table is refused;
- hiding and reopening the panel retains the session;
- leaving Edit mode requires Apply or Revert All;
- dark mode remains readable;
- no automatic Apply occurs.

Validated behavior after Revert All or successful Apply:

- panel hide/reopen works;
- active-tab refresh works normally;
- restart loads the editor text normally;
- pending grid-only changes are never silently written.

## Test H — no-change behavior

**Result: PASS**

Validated behavior:

- Apply remains disabled without a cell change;
- clean Edit mode exits normally;
- editor and disk content remain unchanged.

## Test I — non-UTF-8 Apply refusal

**Result: PASS**

Validated with an ANSI/non-UTF-8 synthetic document:

- Edit mode and dirty tracking remain visible;
- Apply displays the UTF-8/Scintilla code page 65001 warning;
- the editor buffer remains unchanged;
- the disk file remains unchanged;
- the dirty grid session remains available for Revert All;
- converting to UTF-8 and reopening Edit mode allows the supported Apply path.

## Automated evidence

```text
Public branch: agent/editable-grid-apply
Final implementation head before acceptance documentation: 62313931677e77c66d3c2e7f375caf969ba62511
CI run: 29916768850
xUnit: 122 total, 0 failed, 0 skipped
Strict core build: PASS
Bootstrap smoke: PASS
Native AOT serializer/edit session/Apply coordinator/conflict smoke: PASS
Full win-x64 Native AOT plugin publish: PASS
Installable 0.7.0-alpha package: PASS
```

## Acceptance conclusion

Milestone 0.7 satisfies its defined host boundary: safe UTF-8 cell editing, deterministic minimal-difference replacement, explicit Revert/Apply behavior, single-step host undo/redo, Notepad++ Save ownership, conflict blocking, and non-UTF-8 write refusal. The milestone is approved for merge.
