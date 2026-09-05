$ErrorActionPreference = 'Stop'
$policyScript = Join-Path $PSScriptRoot 'Get-TestPackagePolicy.ps1'
$testDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('csv-package-policy-' + [guid]::NewGuid())
$sourceDirectory = Join-Path $testDirectory 'source'
$checkoutDirectory = Join-Path $testDirectory 'checkout'

function Invoke-TestGit {
    $result = & git @args
    if ($LASTEXITCODE -ne 0) { throw 'Package-policy Git fixture failed.' }
    return $result
}

function Assert-Policy([bool] $Expected, [hashtable] $Arguments, [string] $Case) {
    $actual = & $policyScript @Arguments
    if ($actual -isnot [bool] -or $actual -ne $Expected) {
        throw "Package-policy regression: $Case"
    }
    Write-Host "PASS: $Case"
}

New-Item -ItemType Directory -Path $sourceDirectory -Force | Out-Null
Push-Location $sourceDirectory
try {
    Invoke-TestGit init --initial-branch=main | Out-Null
    Invoke-TestGit config user.name 'Synthetic policy test'
    Invoke-TestGit config user.email 'policy-test@example.invalid'
    Invoke-TestGit -c commit.gpgsign=false commit --allow-empty -m 'baseline' | Out-Null
    Invoke-TestGit checkout -b candidate | Out-Null
    Invoke-TestGit -c commit.gpgsign=false commit --allow-empty -m 'candidate' -m '[test-package]' | Out-Null
    $candidateSha = Invoke-TestGit rev-parse HEAD
    Invoke-TestGit checkout main | Out-Null
    Invoke-TestGit -c commit.gpgsign=false commit --allow-empty -m 'ordinary base update' | Out-Null
    $ordinarySha = Invoke-TestGit rev-parse HEAD
    Invoke-TestGit -c commit.gpgsign=false merge --no-ff candidate -m 'Synthetic PR merge without candidate marker' | Out-Null
    $mergeSha = Invoke-TestGit rev-parse HEAD

    # --no-local makes depth effective, reproducing actions/checkout fetch-depth: 2.
    Invoke-TestGit clone --no-local --depth=2 $sourceDirectory $checkoutDirectory | Out-Null
    Push-Location $checkoutDirectory
    try {
        if ((Invoke-TestGit rev-parse --is-shallow-repository) -ne 'true') {
            throw 'Package-policy fixture must be a shallow checkout.'
        }
        Assert-Policy $true @{ EventName = 'pull_request'; PullRequestHeadSha = $candidateSha } 'PR head marker survives synthetic merge and shallow checkout'
        Assert-Policy $false @{ EventName = 'pull_request'; PullRequestHeadSha = $ordinarySha } 'ordinary PR head does not inherit another parent marker'
        Assert-Policy $false @{ EventName = 'push' } 'ordinary checked-out commit produces no package'
        Assert-Policy $false @{ EventName = 'workflow_dispatch'; PublishTestPackage = 'false' } 'manual dispatch defaults to no package'
        Assert-Policy $true @{ EventName = 'workflow_dispatch'; PublishTestPackage = 'true' } 'manual dispatch can request a package'
        Assert-Policy $false @{ EventName = 'push'; PublishTestPackage = 'true' } 'dispatch input cannot enable a push package'
        $rejected = $false
        try { $null = & $policyScript -EventName pull_request }
        catch { $rejected = $true }
        if (-not $rejected) { throw 'Missing PR identity must fail closed.' }
        Write-Host 'PASS: missing PR identity fails closed'
        if ((Invoke-TestGit rev-parse HEAD) -ne $mergeSha) {
            throw 'Policy evaluation changed the build checkout.'
        }
        Write-Host 'PASS: policy preserves merge checkout'

        Invoke-TestGit checkout --detach $candidateSha | Out-Null
        Assert-Policy $true @{ EventName = 'push' } 'push reads the checked-out candidate marker'
    }
    finally { Pop-Location }
}
finally {
    Pop-Location
    Remove-Item -LiteralPath $testDirectory -Recurse -Force
}

Write-Host 'All 9 test-package policy checks passed.'
