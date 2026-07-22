[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$LibraryPath,

    [Parameter(Mandatory)]
    [string]$LogPath
)

$ErrorActionPreference = 'Stop'

$resolvedLibrary = (Resolve-Path -LiteralPath $LibraryPath).Path
$resolvedLog = [System.IO.Path]::GetFullPath($LogPath)
$env:CSV_VISUAL_EDITOR_SMOKE_LOG = $resolvedLog

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate int CsvVisualEditorSmokeDelegate();
'@

$handle = [System.Runtime.InteropServices.NativeLibrary]::Load($resolvedLibrary)
try {
    $entryPoint = [System.Runtime.InteropServices.NativeLibrary]::GetExport(
        $handle,
        'RunCsvVisualTableSmoke')
    $delegate = [System.Runtime.InteropServices.Marshal]::GetDelegateForFunctionPointer(
        $entryPoint,
        [CsvVisualEditorSmokeDelegate])
    $result = $delegate.Invoke()
}
finally {
    [System.Runtime.InteropServices.NativeLibrary]::Free($handle)
}

if (Test-Path -LiteralPath $resolvedLog) {
    Get-Content -LiteralPath $resolvedLog
}

if ($result -ne 0) {
    throw "Native AOT CSV table runtime smoke returned exit code $result."
}
