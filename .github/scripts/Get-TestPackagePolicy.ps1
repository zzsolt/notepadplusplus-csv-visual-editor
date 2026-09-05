param(
    [Parameter(Mandatory)]
    [ValidateSet('pull_request', 'push', 'workflow_dispatch')]
    [string] $EventName,
    [string] $PullRequestHeadSha = '',
    [string] $PublishTestPackage = 'false'
)

$ErrorActionPreference = 'Stop'

# PR checkout remains the merge result; only the marker comes from its head parent.
$candidateCommit = 'HEAD'
if ($EventName -eq 'pull_request') {
    if ($PullRequestHeadSha -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'A pull-request head commit SHA is required for package policy.'
    }
    $candidateCommit = $PullRequestHeadSha
}

$commitMessage = git log -1 --format=%B $candidateCommit --
if ($LASTEXITCODE -ne 0) {
    throw 'Cannot read the candidate commit for package policy.'
}

$requestedByCommit = ($commitMessage -join "`n").Contains('[test-package]')
$requestedByDispatch = ($EventName -eq 'workflow_dispatch') -and ($PublishTestPackage -eq 'true')
return ($requestedByCommit -or $requestedByDispatch)
