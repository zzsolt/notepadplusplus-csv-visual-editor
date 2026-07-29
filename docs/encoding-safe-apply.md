# Milestone 0.10 — Encoding-safe Apply

## Status

Milestone 0.10 starts from owner-accepted public main `b851a8b83313afc41a37c845533a069747ae63aa`. The first increment is implemented and automated-green, but real non-UTF-8 Notepad++ replacement remains disabled.

```text
Public branch/PR: agent/encoding-safe-apply / #10 draft
Implementation head: 783e66773a6b7d405742ed21cdc78f32cd9c5d7a
CI run/job: 30447685517 / 90561981814 — PASS
Core xUnit: 200/200 PASS
Native AOT runtime: PASS
win-x64 plugin publish/package: PASS
Implementation artifact: 8722228088
```

## Problem

The writable plugin path historically permitted Apply only when both the edit-session baseline and the fresh Notepad++ snapshot reported Scintilla code page `65001` (UTF-8). Viewing other readable documents was supported, but Apply was blocked before the replacement adapter was created.

Removing that restriction safely requires two independent proofs:

1. **Host-independent representability:** the complete proposed replacement must encode and decode exactly in an explicit code-page profile without replacement fallback.
2. **Host behavior:** the pinned Npp.DotNet/Scintilla replacement path must preserve code page, text, one-step undo/redo, Save, and reopen behavior in real Notepad++.

The first increment completes the first proof and preserves the second as a merge-blocking host gate for any future legacy write enablement.

## Pinned bridge audit

The pinned `Npp.DotNet.Plugin` implementation was inspected at commit `7730aabd09fcadb9efbf5913ab552cbac4d3aa50`.

- `ScintillaGateway.CodePage` resolves the current document code page through `Encoding.GetEncoding`; code page zero maps to the system ANSI code page.
- .NET builds register `CodePagesEncodingProvider.Instance`.
- `ReplaceTarget(string)` delegates to the shared `SendEncodedBytes` path.
- `SendEncodedBytes` reads the current document encoding, calls `Encoding.GetBytes` on the complete string plus terminating NUL, and sends the counted byte buffer to `SCI_REPLACETARGET`.
- The gateway uses the encoding's normal fallback behavior.

Consequently, an unrepresentable .NET character could otherwise be converted through replacement fallback. The plugin-side strict preflight is necessary and must complete before host-target construction. It is not, by itself, sufficient evidence to authorize legacy writes.

## Explicit profile set

The first increment recognizes exactly:

- `65001` — UTF-8;
- `20127` — US-ASCII;
- `1250` — Windows Central European;
- `1252` — Windows Western European.

There is no implicit process-default or current-culture fallback. Scintilla code page zero and every other code page remain unsupported until explicitly designed and tested.

## Strict representability contract

For the complete replacement text the core layer:

1. resolves an explicit profile;
2. creates an encoding with encoder and decoder exception fallbacks;
3. encodes the entire string;
4. decodes the complete byte array;
5. requires ordinal string equality;
6. returns only status, code page, encoding web name, and successful encoded byte count.

The result never includes source text, rejected character values, replacement text, or local paths.

Statuses:

- `ExactRoundTrip`;
- `UnsupportedCodePage`;
- `TextNotRepresentable`;
- `RoundTripMismatch`.

## Support versus host-write authorization

`CsvEncodingApplyPolicy` separates a tested representability profile from a code page authorized for real host replacement.

The production policy remains:

```csharp
CsvEncodingApplyPolicy.Utf8Only
```

Unit tests may construct an explicit policy enabling Windows-1250 or Windows-1252 to prove the core gate and coordinator order. Production UI code does not enable those writes.

## Apply coordinator order

1. create a NoChanges, conflict, or Ready replacement plan;
2. return NoChanges/conflict without encoding or host work;
3. run strict encoding preflight for a Ready plan;
4. return a sanitized blocked result for disabled, unsupported, unrepresentable, or mismatched candidates;
5. only after successful preflight invoke the editor-target factory;
6. run the existing Begin/Replace/Selection/End transaction.

Thus blocked production cases cause both zero editor calls and zero host-adapter construction.

## Automated coverage

The 200-test suite and Native AOT smoke cover:

- UTF-8 with ASCII, Hungarian, CJK, and emoji;
- strict ASCII success and non-ASCII rejection;
- Windows-1250 Hungarian success and CJK/emoji rejection;
- Windows-1252 Western-European success and CJK rejection;
- unsupported code pages including 0 and 932;
- invalid UTF-16 input such as an unpaired surrogate;
- empty text and deterministic repeated evaluation;
- production legacy-write disablement;
- exact custom-policy legacy preflight;
- zero editor calls and zero target construction for blocked cases;
- one undo transaction for an explicitly authorized, exact test-policy candidate;
- Native AOT code-page provider and policy execution.

## CI findings corrected during the increment

- An explicit `System.Text.Encoding.CodePages` PackageReference produced .NET 10 `NU1510` warning-as-error because the framework already supplies it; the redundant reference was removed.
- Restore originally preceded diagnostic-directory creation, producing a secondary missing-log artifact error. CI now retains a dedicated `restore.log`, gates later stages on restore success, and avoids secondary missing-artifact failures.
- The target factory was initially delayed only for structural edits. Cell-only and structural Apply now share the same factory-delayed guarantee.

## Implementation package evidence

```text
Artifact ID: 8722228088
Outer artifact ZIP SHA-256:
4cac2aff2cbdc1e9c45db5220f29e87846a5a94cccd160f9fdf7d6c957093a28

Inner install ZIP size: 7621487 bytes
Inner install ZIP SHA-256:
fd4125845b2c8b0fd5fbdacdae7bb7f7b733e07f863f453089c7efc003448d4d

CsvVisualEditor.dll size: 20261376 bytes
CsvVisualEditor.dll SHA-256:
7f63e0aac359766516d6337e91a048fb58e0e0b266b3f179107fee4d48f55c68

Layout: CsvVisualEditor/CsvVisualEditor.dll
```

## Host gate before enabling non-UTF-8 Apply

For every candidate legacy code page, Notepad++ 8.9.7 x64 must prove:

1. the editor remains in the intended code page;
2. representable cell edit, insertion, and batch deletion Apply successfully;
3. one Ctrl+Z restores the exact source and one Ctrl+Y reapplies;
4. normal Notepad++ Save retains the intended encoding;
5. close/reopen preserves exact text;
6. controlled byte comparison matches expectation where feasible;
7. unrepresentable text is blocked before target construction with zero host writes;
8. code-page change during Edit mode remains a conflict;
9. BOM and no-BOM cases are tested separately.

Until that gate passes, non-UTF-8 production host writes remain disabled.
