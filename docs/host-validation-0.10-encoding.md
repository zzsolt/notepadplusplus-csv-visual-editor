# Milestone 0.10 encoding host-validation plan

## Current package scope

The first 0.10 package keeps production Apply UTF-8-only. It may be installed to regression-test accepted UTF-8 behavior and to verify that legacy-code-page documents are blocked safely. It does not yet claim successful non-UTF-8 writes.

## Target environment

- Notepad++ 8.9.7 x64;
- Windows x64;
- CSV Visual Editor 0.10.0-alpha;
- synthetic files only.

## Test A — UTF-8 regression

1. Open a synthetic UTF-8 CSV containing Hungarian text, CJK, and emoji.
2. Enter Edit mode.
3. Change one cell, add one row, and delete two selected rows.
4. Apply.
5. Confirm one Ctrl+Z restores the exact original and one Ctrl+Y reapplies.
6. Save through Notepad++ and reopen.
7. Confirm all Unicode text remains exact.

Expected: unchanged accepted 0.9 write behavior.

## Test B — legacy production block

For controlled Windows-1250 and Windows-1252 CSV files:

1. confirm Notepad++ reports the intended encoding;
2. enter Edit mode and make a representable pending edit;
3. select Apply;
4. confirm the message states that host writing is not yet enabled for the current code page;
5. confirm editor text and modified state are unchanged;
6. confirm Revert All remains available.

Expected: no host replacement and no implicit conversion.

## Test C — code-page conflict

1. open a UTF-8 CSV and enter Edit mode;
2. make a pending change;
3. change the document encoding in Notepad++;
4. select Apply.

Expected: code-page conflict, zero replacement, pending session retained.

## Future enablement matrix

Before Windows-1250 or Windows-1252 is production-authorized, repeat with a specially enabled test build and prove:

- representable Hungarian/Western-European content;
- unrepresentable CJK and emoji blocking;
- code-page retention after Apply;
- one-step undo/redo;
- Save and close/reopen;
- byte-level expected output;
- BOM and no-BOM variants;
- CRLF, LF, mixed EOL, and terminal-newline variants;
- cell-only, insertion, deletion, and combined edits.

Record exact PASS/FAIL evidence. Do not enable or merge legacy production writes from automated .NET evidence alone.
