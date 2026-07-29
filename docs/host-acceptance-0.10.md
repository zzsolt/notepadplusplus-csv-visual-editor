# Milestone 0.10 owner acceptance

## Decision

Milestone 0.10 is accepted for merge. On 2026-07-29 the owner confirmed in the real Notepad++ host that the package behaved correctly and specifically verified that Apply works for a Windows-1250 document. The owner then explicitly authorized proceeding to the next development phase.

## Accepted behavior

- UTF-8 Apply remains functional.
- Windows-1250 Apply works when the complete replacement text is representable in Windows-1250.
- Strict encoding representability remains the safety boundary: unsupported or non-representable replacement text must be rejected before any editor write.
- Existing complete-row selection, checkbox, Ctrl/Shift, batch Delete, Revert, conflict detection, one-step undo transaction, and Notepad++ Save ownership remain preserved.

## Clarified policy

The correct long-term rule is not a blanket UTF-8-only restriction. A supported legacy code page may be written when strict encode/decode round-trip validation proves that the complete replacement text is lossless. The owner-observed Windows-1250 result is consistent with the audited Npp.DotNet/Scintilla path, which encodes replacement text using the active document code page.

## Automated evidence

```text
Public implementation head before acceptance record:
e2fb1299adcb1f613ff8ecd80d10899742e81812

CI run: 30448538688 — PASS
CI job: 90564745147 — PASS
Core xUnit: 200/200 PASS
Native AOT runtime smoke: PASS
win-x64 Native AOT publish/package: PASS
Artifact ID: 8722576173
Inner install ZIP SHA-256:
515ad527d4a2db687ba72cd58345a15642f906ea2288733eb063e6dff655d14a
DLL SHA-256:
a0bc517a6399600ccd1b1ea4d8253fab2e56804ab00124fbb8ab7c95a2181740
```

## Evidence boundary

The owner supplied a final positive host result and explicit milestone-closure authorization, including direct Windows-1250 Apply confirmation. A separate byte-for-byte matrix for every supported encoding was not supplied, so this record does not invent unreported observations.

## Merge order

Merge public PR #10 first. Record its actual merge SHA in the internal repository, synchronize the internal handoff, and merge internal PR #10 second.
