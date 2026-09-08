# 0.12.5 source review and remaining validation

The review covered the source inventory and focused on the host Apply call, snapshot
lifecycle, parser/serialization and stable identities, clipboard bounds, search/view
state, native command dispatch, painting, themes, DPI and resource ownership.
It is not a proof that the application contains no other defects.

## Corrected issues

- The host Apply entry used the conservative default rather than the accepted
  Windows-1250 policy. An explicit UTF-8/1250 host coordinator now preserves strict
  whole-buffer preflight before target construction. Additional encodings remain blocked.
- The Notepad++ About entry displayed a hard-coded old version and differed from the
  new panel About. Both now share one version-aware, theme-aware dialog.
- A failed snapshot read did not retire an earlier asynchronous table build. Cancellation,
  generation invalidation and source-baseline clearing now precede the read attempt.
- Repeated command-surface installation could accumulate tooltip prefixes. Binding and
  disposal are idempotent; availability explanations are refreshed from current state.
- Invalid sort directions previously fell through to descending sort; invalid enum
  values and null queries now fail at the view-builder boundary.
- Clipboard row/column limits were checked after potentially very large split allocations.
  Text now has a 16 Mi UTF-16-unit limit checked before scanning; row limits are enforced
  before substring allocation, and column splitting creates at most 513 entries. Existing
  limits of 10,000 rows, 512 columns and 250,000 cells and terminal-newline semantics remain.
- Search uses one immutable, value-free cell index per rendered view. Painting/navigation
  no longer repeatedly scan full cell strings; mutation/rebuild invalidates the index.
- Space markers are separated solid dots; monospaced value cells make their spacing
  readable. Graphics clipping/focus order, GDI ownership and selected-cell contrast were reviewed.

## Validation boundary

Core and Windows Native AOT tests cover deterministic behavior and synthetic UI drawing.
Actual Notepad++ input routing, docking, DPI changes between monitors, clipboard locking,
failed native snapshot recovery and legacy-code-page host bytes still need host regression
checks. The two PRs stay draft and milestone 0.12 is not declared accepted.
See [the GUI checklist](gui-refresh-0.12.5.md) for the manual cases.
