# Milestone 0.8 host acceptance

## Result

**PASS — complete owner acceptance in Notepad++ 8.9.7 x64 on 2026-07-23.**

## Environment

```text
Host: Notepad++ 8.9.7 x64
Platform: Windows x64
Plugin: CSV Visual Editor 0.8.0-alpha
Final public branch head before acceptance closure: d677b0c5496fb04e02fe96d4e769acd93fb769e4
Final CI: 30002810651 — PASS
Core xUnit: 159/159 PASS
```

## Accepted functional coverage

The owner confirmed that the complete 0.8 row-operation workflow works as intended:

- explicit Edit mode;
- cell editing;
- Add Row placement;
- unique synthetic inserted-row identities;
- Delete Row for source rows;
- deleting an inserted row cancels the insertion;
- Revert All restores cell and structural state without changing the editor buffer;
- combined cell edit, insertion, and deletion Apply;
- one-step Ctrl+Z and Ctrl+Y;
- normal Notepad++ modified marker and Save ownership;
- stable source identities after view sorting;
- header-only CSV receiving its first data row;
- quoted delimiters, quotes, multiline fields, Unicode, and emoji;
- mixed CRLF/LF and terminal-newline preservation;
- inconsistent-width record preservation;
- content-change and active-document conflict blocking;
- non-UTF-8 Apply refusal;
- panel lifecycle and light/dark mode behavior.

## Row-header correction and targeted retest

The initial host pass found two usability defects:

1. `new:1 *` was truncated to `new:`;
2. clicking the left row header did not select the complete row.

The correction added bootstrap-safe table discovery, a 112-pixel row header, `RowHeaderSelect`, explicit complete-row selection on left row-header click, and policy reapplication after table reconstruction.

The final targeted owner retest confirmed:

- the complete `new:1 *` marker is visible;
- ordinary cell clicks remain cell selections;
- clicking the left row header selects the complete row;
- Delete Row targets the selected row;
- the behavior remains correct after reconstruction/reopen;
- light and dark mode remain readable and functional.

## Automated evidence

```text
Final CI: 30002810651 — PASS
159/159 xUnit tests: PASS
Native AOT bootstrap-time grid discovery: PASS
Native AOT preferred row-header width: PASS
Native AOT RowHeaderSelect behavior: PASS
Native AOT complete-row selection: PASS
Native AOT reconstruction reapplication: PASS
Native AOT structural preview/Apply/conflict/Revert: PASS
Full win-x64 plugin Native AOT publish/package: PASS
```

## Accepted package evidence

```text
Artifact ID: 8561781245
GitHub artifact digest: sha256:239a1645e73f33ef2d50a8e9449971add1cbe217dc4d3a3fe616a2854c016cb3
Inner ZIP SHA-256: a661d87ffa64cd40765315859815ac348ace90f6fa0a59a07179794df562a745
DLL SHA-256: 58ebf8ec05d0b435500fdb9da08db9f2731b06a9431fc3f5a5f8679980a471c1
Layout: CsvVisualEditor/CsvVisualEditor.dll
```

## Conclusion

Milestone 0.8 satisfies its automated and real-host acceptance gates. Public PR #8 may be squash-merged before the synchronized internal PR #8.
