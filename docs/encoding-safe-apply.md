# Milestone 0.10 — Encoding-safe Apply

## Status

Milestone 0.10 starts from accepted public main `b851a8b83313afc41a37c845533a069747ae63aa`.

The first increment is a host-independent, strict encoding-profile and representability foundation. It does **not** enable non-UTF-8 editor replacement yet.

## Problem

The writable plugin path currently permits Apply only when both the edit-session baseline and the fresh Notepad++ snapshot report Scintilla code page `65001` (UTF-8). Viewing other readable documents remains supported, but Apply is blocked before the host replacement target is constructed.

Removing that gate safely requires more than selecting a .NET `Encoding` object. A candidate replacement must be representable without replacement fallbacks, and the actual Npp.DotNet/Scintilla bridge must later be proven to preserve the editor code page and text through Apply, undo/redo, Save, and reopen.

## First-increment goals

- define explicit supported Scintilla code-page profiles;
- use strict encoder and decoder exception fallbacks;
- encode and decode the complete replacement text;
- require character-for-character round-trip equality;
- classify unsupported code pages separately from unrepresentable text;
- expose safe, generic diagnostics without logging CSV values;
- preserve the accepted UTF-8 path unchanged;
- keep non-UTF-8 host replacement disabled;
- provide xUnit and Native AOT coverage for the policy.

## Initial code-page scope

The first host-independent profile set evaluates:

- `65001` — UTF-8;
- `20127` — US-ASCII;
- `1250` — Windows Central European;
- `1252` — Windows Western European.

Additional Scintilla code pages require an explicit decision and test matrix; there is no implicit fallback to the process default or current culture.

## Required result model

The representability check must distinguish at least:

- supported and exact round trip;
- unsupported code page;
- encoder rejection because one or more characters are not representable;
- decoder rejection or unexpected non-identical round trip.

No result may include document values, rejected characters, full replacement text, or local paths.

## Safety invariants

- no replacement-character fallback;
- no direct disk write;
- no automatic encoding conversion;
- no host method call for unsupported or lossy candidates;
- document, code-page, and content conflicts remain authoritative;
- Apply remains one full editor-buffer replacement in one undo action;
- Notepad++ remains responsible for modified state and Save;
- BOM presence, decoded string identity, Scintilla code page, and saved file bytes remain separate concepts.

## Test matrix

Automated coverage must include:

- UTF-8 ASCII, Hungarian, CJK, and emoji;
- strict US-ASCII success and non-ASCII rejection;
- Windows-1250 Hungarian success and CJK/emoji rejection;
- Windows-1252 Western-European success and unsupported-character rejection;
- unsupported code-page handling;
- empty text, CRLF/LF/CR, embedded CSV newlines, quotes, insertion, deletion, and combined preview text;
- deterministic repeated evaluation;
- Native AOT execution.

## Host gate before enabling non-UTF-8 Apply

A later increment must audit and test the real Npp.DotNet/Scintilla replacement path in Notepad++ 8.9.7 x64. For each enabled legacy code page it must prove:

1. editor code page remains unchanged;
2. Apply performs one replacement and one undo transaction;
3. one Ctrl+Z and one Ctrl+Y restore/reapply the complete change;
4. Save preserves the intended encoding;
5. reopening the file preserves exact text;
6. an unrepresentable candidate is blocked with zero host writes.

Until that host gate passes, the current UTF-8-only host-write restriction remains in force.
