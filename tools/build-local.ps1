param(
    [string] $PackageVersion = ("1.0.1-local." + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'Run the complete Native AOT and Notepad++ gate on Windows with PowerShell 7.' }
if ($PackageVersion -notmatch '^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$') { throw 'Invalid package version.' }
$root = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $root "artifacts/local/$PackageVersion"
if (Test-Path $destination) { throw 'This candidate directory already exists; use a new version.' }
New-Item -ItemType Directory $destination | Out-Null

function Invoke-CheckedNative {
    param([string] $Command, [string[]] $Arguments, [string] $Log)
    & $Command @Arguments *>&1 | Tee-Object -FilePath (Join-Path $destination $Log)
    if ($LASTEXITCODE -ne 0) { throw "$Command failed with exit code $LASTEXITCODE. See $Log." }
}

Push-Location $root
try {
    # Fail before any expensive compilation when translations are missing or stale.
    Invoke-CheckedNative 'python' @('-m', 'unittest', 'discover', '-s', 'tests/ci', '-p', 'test_*.py') 'ci-policy.log'
    Invoke-CheckedNative 'python' @('-m', 'unittest', 'discover', '-s', 'tests/localization', '-p', 'test_*.py') 'localization-tests.log'
    Invoke-CheckedNative 'python' @('-m', 'unittest', 'discover', '-s', 'tests/release', '-p', 'test_*.py') 'release-tests.log'
    Invoke-CheckedNative 'python' @('tools/localization/verify.py', '--check-upstream') 'localization.log'
    Invoke-CheckedNative 'pwsh' @('-NoProfile', '-File', '.github/scripts/Test-TestPackagePolicy.ps1') 'package-policy.log'
    Invoke-CheckedNative 'dotnet' @('build', 'src/CsvVisualEditor.Core/CsvVisualEditor.Core.csproj', '-c', 'Release') 'core-build.log'
    Invoke-CheckedNative 'dotnet' @('run', '--project', 'tests/CsvVisualEditor.Core.SmokeTests', '-c', 'Release') 'core-smoke.log'
    Invoke-CheckedNative 'dotnet' @('run', '--project', 'tests/CsvVisualEditor.Core.Tests', '-c', 'Release') 'core-tests.log'
    $native = Join-Path $destination 'native-smoke'
    Invoke-CheckedNative 'dotnet' @('publish', 'tests/CsvVisualEditor.NativeAot.SmokeTests/CsvVisualEditor.NativeAot.SmokeTests.csproj', '-c', 'Release', '-f', 'net10.0-windows', '-r', 'win-x64', '-o', $native) 'native-publish.log'
    # Native screenshot fixtures run in a fresh candidate directory, not a stale
    # artifacts/ui-review folder from another build.
    Push-Location $destination
    try {
        Invoke-CheckedNative 'pwsh' @('-NoProfile', '-File', (Join-Path $root 'tests/CsvVisualEditor.NativeAot.SmokeTests/Invoke-Smoke.ps1'), '-LibraryPath', (Join-Path $native 'CsvVisualEditor.NativeAot.SmokeTests.dll'), '-LogPath', (Join-Path $destination 'native-execution.log')) 'native-smoke.log'
    }
    finally { Pop-Location }
    $publish = Join-Path $destination 'publish'
    Invoke-CheckedNative 'dotnet' @('publish', 'src/CsvVisualEditor/CsvVisualEditor.csproj', '-c', 'Release', '-f', 'net10.0-windows', '-r', 'win-x64', "-p:Version=$PackageVersion", '-o', $publish) 'publish.log'
    $dll = Join-Path $publish 'CsvVisualEditor.dll'
    Invoke-CheckedNative 'pwsh' @('-NoProfile', '-File', 'tests/host-review/Invoke-HostReview.ps1', '-LibraryPath', $dll, '-OutputPath', (Join-Path $destination 'host-review')) 'host-review.log'
    # Reproduce direct typing in the real Notepad++ host.
    Invoke-CheckedNative 'pwsh' @('-NoProfile', '-File', 'tests/host-review/Invoke-KeyboardInputReview.ps1', '-LibraryPath', $dll, '-OutputPath', (Join-Path $destination 'keyboard-input-review')) 'keyboard-input-review.log'
    Invoke-CheckedNative 'pwsh' @('-NoProfile', '-File', 'tests/host-review/Invoke-ContextMenuReview.ps1', '-LibraryPath', $dll, '-OutputPath', (Join-Path $destination 'context-menu-review')) 'context-menu-review.log'
    Invoke-CheckedNative 'pwsh' @('-NoProfile', '-File', 'tests/host-review/Invoke-LocalizedHostReview.ps1', '-LibraryPath', $dll, '-OutputPath', (Join-Path $destination 'localized-host-review')) 'localized-host-review.log'
    # Plugins Admin requires the plugin DLL at the ZIP root. Keep the exact
    # production DLL that passed every host gate above and ship its notices.
    $layout = Join-Path $destination 'package'
    New-Item -ItemType Directory $layout -Force | Out-Null
    Copy-Item $dll (Join-Path $layout 'CsvVisualEditor.dll')
    Copy-Item (Join-Path $root 'LICENSE') (Join-Path $layout 'LICENSE.txt')
    Copy-Item (Join-Path $root 'THIRD_PARTY_NOTICES.txt') (Join-Path $layout 'THIRD_PARTY_NOTICES.txt')
    $zip = Join-Path $destination "CsvVisualEditor-$PackageVersion-win-x64.zip"
    Compress-Archive -Path (Join-Path $layout '*') -DestinationPath $zip -CompressionLevel Optimal
    Get-FileHash $zip, $dll -Algorithm SHA256 | Format-List | Out-File (Join-Path $destination 'SHA256.txt')

    if ($PackageVersion -match '^\d+\.\d+\.\d+$') {
        $expectedFileVersion = "$PackageVersion.0"
        $actualFileVersion = (Get-Item $dll).VersionInfo.FileVersion
        if ($actualFileVersion -ne $expectedFileVersion) {
            throw "Plugins Admin version mismatch: DLL file version $actualFileVersion, expected $expectedFileVersion."
        }
        Invoke-CheckedNative 'python' @(
            'tools/release/npp_plugin_package.py',
            '--zip', $zip,
            '--dll', $dll,
            '--version', $PackageVersion,
            '--entry-out', (Join-Path $destination 'nppPluginList-entry.x64.json'),
            '--manifest-out', (Join-Path $destination 'release-manifest.json')
        ) 'plugin-admin-package.log'
    }
    Write-Host "Validated candidate: $zip"
}
finally {
    Pop-Location
}
