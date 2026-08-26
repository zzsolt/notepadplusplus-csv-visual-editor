# CI artifact policy

The CI always performs the full correctness gate for normal development commits:

- restore;
- strict Core build;
- bootstrap smoke tests;
- Core xUnit tests;
- Windows Native AOT runtime smoke;
- production win-x64 Native AOT plugin publish.

A normal commit does **not** create or upload the installable ZIP artifact. This avoids accumulating multi-megabyte packages for every intermediate development commit.

## Test-package candidates

The installable ZIP is created and uploaded only when either condition is true:

1. the checked-out commit message contains the literal marker `[test-package]`; or
2. the workflow is started manually and the `publish_test_package` input is enabled.

The installable candidate artifact is retained for 7 days.

Diagnostic artifacts remain available for 3 days so failed CI runs can still be investigated without retaining logs indefinitely.

## Development convention

Use ordinary commit messages during implementation. When a real Notepad++ host package is required, make the candidate commit with `[test-package]` in its commit message. The resulting workflow run must still pass every normal CI gate before its package is treated as a test candidate.

Deleting an Actions artifact does not alter Git history, source code, CI status, or documented test evidence.
