# 0.13 - Data exploration

## Accepted baseline and plan

Milestone 0.12 was owner-accepted on 2026-09-08 after delivery of 0.12.6-alpha.357.1. Public PR #12 is merged at c00a1a106fcd7bbb97e94e07cebb57de3854c954. The approved whitespace renderer and main visual layout remain unchanged in this phase.

0.13 adds view-only tools: up to eight column conditions combined with ALL/ANY, literal text comparisons with optional case sensitivity, exact empty/whitespace predicates and explicit invariant-decimal comparisons; up to three stable text/numeric sort keys; and a current-view column summary (empty/whitespace/distinct/repeated/numeric counts, numeric range and most frequent values).

One Filter and sort dialog owns the rule draft, Preview, Apply view and Cancel. It composes with the existing inline search. Column summary is read-only. Both tools are available from icons and text menus. Reset view clears the complete view state; Edit starts from the full source-order table and disables view tools. Document/dialect/header changes clear schema-dependent rules.

Rules and summaries operate on the bounded projected table, never silently on undisplayed source rows. Stable parser row identity is retained for Source and clipboard operations. No files are written, rows deleted or values normalized by these tools. Pending editing/Apply/encoding/one-undo behavior is unchanged. No saved presets, regex, arbitrary formulas or new dependencies are introduced.

## Validation plan

Core: ALL/ANY, every predicate, exact spaces/Unicode/case, numeric precision and rejected formats, multiple sort priorities and deterministic ties, missing/nonnumeric placement, search composition, immutable options, invalid bounds, source identity and column statistics. Measure a 10,000-row / 250,000-cell view without speculative paging.

Native AOT: real dialog controls, validation/cancel/apply semantics, theme/resize, original renderer regression suite. Production Notepad++: load the exact DLL, exercise the new dialog and summary, filter and reset, preserve source text, and retain search/docking/About checks. Package only after all required gates pass.

Status at planning commit: implementation and new tests not yet completed. The final implementation, measured results and Hungarian usage notes will replace this planning status before delivery. Owner acceptance of 0.13 is separate from the accepted 0.12 baseline.
