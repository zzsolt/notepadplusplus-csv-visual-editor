# Milestone 0.9 owner acceptance

## Decision

Milestone 0.9 is accepted for merge. On 2026-07-29 the owner reported that the final package was “tökéletes lett” in Notepad++ 8.9.7 x64 and explicitly instructed the project to close 0.9 and begin 0.10.

## Accepted implementation

- stable-ID synchronized checkbox, native-row-header, and `#`-column complete-row selection;
- plain, Ctrl, Shift, and Ctrl+Shift row gestures;
- ordinary-cell clearing and no current-cell deletion fallback;
- atomic multi-row deletion and exact Revert All;
- glyph-only native row header plus aligned, content-sized `#` indicator column;
- unchanged conflict blocking, one-undo Apply, UTF-8 gate, and Notepad++ Save ownership.

## Automated evidence

```text
Public implementation head: ee686b75e65800dd1e6e959e477d60fd1d2234d1
CI run: 30387337728 — PASS
CI job: 90369811550 — PASS
Core xUnit: 168/168 PASS
Native AOT runtime smoke: PASS
win-x64 Native AOT publish/package: PASS
Artifact ID: 8699579177
Inner ZIP SHA-256: d79e0d28a3d41b18476418600a2ee59e1979eecae404db71fe0218459733141f
DLL SHA-256: b32aae7a57affbe2bb1a38d9321539919df6de9b1d056c2b640c8913131dec28
```

## Evidence boundary

No separate line-by-line A–N result transcript was supplied. This record therefore represents the owner’s final-package acceptance and explicit authorization to close the milestone; it does not fabricate unreported observations.

## Merge order

Merge public PR #9 first. Record its actual merge SHA in the internal repository, synchronize the internal acceptance state, and merge internal PR #9 second.
