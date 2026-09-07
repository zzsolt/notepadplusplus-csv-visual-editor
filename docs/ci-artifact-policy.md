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

1. the candidate commit message contains the literal marker `[test-package]`; or
2. the workflow is started manually and the `publish_test_package` input is enabled.

The installable candidate artifact is retained for 7 days.

For pull requests, the candidate is the exact event's PR head commit, not GitHub's
synthetic merge commit. Checkout retains the merge result for the full build and
fetches two levels of history so the head message can be read locally. Push and
manual-dispatch runs read the checked-out commit. A missing PR head identity or
unreadable candidate commit fails the policy step rather than guessing.

`.github/scripts/Test-TestPackagePolicy.ps1` exercises a synthetic shallow merge
checkout, marked and ordinary candidates, dispatch on/off, missing PR identity,
and preservation of the build checkout. These checks do not build or upload ZIPs.

Diagnostic artifacts remain available for 3 days so failed CI runs can still be investigated without retaining logs indefinitely.

## Development convention

Use ordinary commit messages during implementation. When a real Notepad++ host package is required, make the candidate commit with `[test-package]` in its commit message. The resulting workflow run must still pass every normal CI gate before its package is treated as a test candidate.

Deleting an Actions artifact does not alter Git history, source code, CI status, or documented test evidence.

## Unique test build names

Every CI run now uses version `0.12.4-alpha.<run_number>.<run_attempt>` for the published DLL and both the inner ZIP and GitHub artifact name, for example `CsvVisualEditor-0.12.4-alpha.335.1-win-x64.zip`. The run number increases for each new workflow invocation; the attempt increases on reruns. Small fixes therefore cannot reuse a previous download name. Local builds use base version `0.12.4-alpha`. The DLL remains `CsvVisualEditor/CsvVisualEditor.dll` for Notepad++ compatibility. Candidate CI logs record inner ZIP and DLL SHA-256; retention and candidate-only upload policy are unchanged.

