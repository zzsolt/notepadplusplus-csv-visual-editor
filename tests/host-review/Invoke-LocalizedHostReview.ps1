param([Parameter(Mandatory)][string]$LibraryPath, [string]$OutputPath = 'artifacts/localized-host-review')
$ErrorActionPreference = 'Stop'
$library = (Resolve-Path $LibraryPath).Path
New-Item -ItemType Directory -Force $OutputPath | Out-Null
$output = (Resolve-Path $OutputPath).Path
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('csv-languages-' + [guid]::NewGuid())
New-Item -ItemType Directory $temporary | Out-Null
$inventory = Get-Content 'src/CsvVisualEditor.Localization/languages.json' -Raw | ConvertFrom-Json -AsHashtable
$translationPolicy = Get-Content 'src/CsvVisualEditor.Localization/translations.json' -Raw | ConvertFrom-Json -AsHashtable
$translatedCodes = @($translationPolicy.translatedCodes)
$upstream = $inventory.upstreamCommit
if (!$upstream) { throw 'Missing pinned language inventory revision' }
if ($translationPolicy.fallback -ne 'en' -or $translatedCodes.Count -ne 7 -or !($translatedCodes -contains 'en')) { throw 'Unexpected translated-language policy' }
Add-Type -AssemblyName System.Drawing
# Reuse the existing bounded window/message implementation, not a second plugin bridge.
if (!('CsvHostWindows' -as [type])) {
    $harness = Get-Content (Join-Path $PSScriptRoot 'Invoke-HostReview.ps1') -Raw
    $definition = [regex]::Match($harness, "(?s)Add-Type -TypeDefinition @'\r?\n(.*?)\r?\n'@")
    if (!$definition.Success) { throw 'Cannot find the shared host window helper' }
    Add-Type -TypeDefinition $definition.Groups[1].Value
}
function Send-Host([IntPtr]$handle, [uint32]$message, [IntPtr]$w, [IntPtr]$l) {
    $result = [IntPtr]::Zero
    if ([CsvHostWindows]::Send($handle,$message,$w,$l,2,5000,[ref]$result) -eq [IntPtr]::Zero) { throw 'Localized host message timed out' }
    return $result
}
function Capture([IntPtr]$handle, [string]$name) {
    $bounds = [CsvHostWindows]::Bounds($handle)
    if ($bounds.Width -lt 10 -or $bounds.Height -lt 10) { throw 'Invalid localized capture bounds' }
    $bitmap = [Drawing.Bitmap]::new($bounds.Width,$bounds.Height)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $dc = $graphics.GetHdc()
        try { $ok = [CsvHostWindows]::PrintWindow($handle,$dc,2) } finally { $graphics.ReleaseHdc($dc) }
        if (!$ok) { throw 'Localized capture failed' }
        $bitmap.Save((Join-Path $output $name),[Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}
function Caption([string]$key) {
    if (!$messages.ContainsKey($key)) { throw "Missing localized host expectation: $key" }
    return $messages[$key].text
}
function Open-Dialog([string]$commandKey, [string]$titleKey) {
    $id = [CsvHostWindows]::FindCommand([CsvHostWindows]::GetMenu($window),(Caption $commandKey))
    if (!$id) { throw "Localized native command missing: $commandKey ($code)" }
    [CsvHostWindows]::PostMessage($window,0x111,[IntPtr]$id,[IntPtr]::Zero) | Out-Null
    $dialog = [IntPtr]::Zero
    for ($i=0; $i -lt 50 -and $dialog -eq [IntPtr]::Zero; $i++) {
        Start-Sleep -Milliseconds 100
        $dialog = [CsvHostWindows]::FindNamedWindow($process.Id,(Caption $titleKey))
    }
    if ($dialog -eq [IntPtr]::Zero) { throw "Localized dialog missing: $titleKey ($code)" }
    return $dialog
}
function Click-Button([IntPtr]$dialog,[string]$key) {
    $caption = Caption $key
    $buttons = @([CsvHostWindows]::Children($dialog) | Where-Object {
        [CsvHostWindows]::Class($_).StartsWith('WindowsForms10.BUTTON.',[StringComparison]::OrdinalIgnoreCase) -and
        [CsvHostWindows]::IsWindowVisible($_) -and [CsvHostWindows]::ControlText($_) -ceq $caption
    })
    if ($buttons.Count -ne 1) { throw "Localized button not uniquely identified: $key ($code)" }
    Send-Host $buttons[0] 0xF5 ([IntPtr]::Zero) ([IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 100
}
function All-Text([IntPtr]$dialog) {
    return @([CsvHostWindows]::Children($dialog) | ForEach-Object { [CsvHostWindows]::ControlText($_) }) -join "`n"
}
$process = $null
$evidence = @()
try {
    $archive = Join-Path $temporary 'host.7z'
    Invoke-WebRequest 'https://github.com/notepad-plus-plus/notepad-plus-plus/releases/download/v8.9.8/npp.8.9.8.portable.minimalist.x64.7z' -OutFile $archive
    if ((Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne '624579cee4d9082f5f3945ceb2474ab5aeb222c2003b503b3df8f1dde60e4def') { throw 'Pinned host digest mismatch' }
    & 7z x $archive "-o$temporary/base" -y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot extract isolated host' }
    $cases = @($translatedCodes) + @('de','ja','unknown')
    foreach ($case in $cases) {
        $code = if ($case -eq 'unknown' -or !($translatedCodes -contains $case)) { 'en' } else { $case }
        $messages = (Get-Content "src/CsvVisualEditor.Localization/Catalogs/$code.json" -Raw | ConvertFrom-Json -AsHashtable).messages
        $hostDir = Join-Path $temporary $case
        Copy-Item (Join-Path $temporary 'base') $hostDir -Recurse
        New-Item -ItemType File (Join-Path $hostDir 'doLocalConf.xml') -Force | Out-Null
        New-Item -ItemType Directory (Join-Path $hostDir 'plugins/CsvVisualEditor') -Force | Out-Null
        Copy-Item $library (Join-Path $hostDir 'plugins/CsvVisualEditor/CsvVisualEditor.dll')
        $nativeFile = ''
        if ($case -ne 'en') {
            $lookup = if ($case -eq 'unknown') { 'hu' } else { $case }
            $nativeFile = @($inventory.languages | Where-Object { $_.code -eq $lookup })[0].file
            Invoke-WebRequest "https://raw.githubusercontent.com/notepad-plus-plus/notepad-plus-plus/$upstream/PowerEditor/installer/nativeLang/$nativeFile" -OutFile (Join-Path $hostDir 'nativeLang.xml')
            if ($case -eq 'unknown') {
                $path = Join-Path $hostDir 'nativeLang.xml'
                [xml]$xml = Get-Content $path -Raw
                $xml.NotepadPlus.'Native-Langue'.SetAttribute('filename','csv-custom-unknown.xml')
                $xml.Save($path)
                $nativeFile = 'csv-custom-unknown.xml'
            }
        }
        $fixture = "Value,Amount`r`n`"  alpha  `",12.5`r`n`"   `",-12.5`r`n`"`",2"
        $csv = Join-Path $hostDir 'language-fixture.csv'
        [IO.File]::WriteAllText($csv,$fixture,[Text.UTF8Encoding]::new($false))
        $exe = Join-Path $hostDir 'notepad++.exe'
        $process = Start-Process $exe -ArgumentList @('-multiInst','-nosession',('"'+$csv+'"')) -PassThru
        $window = [IntPtr]::Zero
        for ($i=0; $i -lt 100 -and $window -eq [IntPtr]::Zero; $i++) { Start-Sleep -Milliseconds 100; $window=[CsvHostWindows]::FindProcess($process.Id) }
        if ($window -eq [IntPtr]::Zero) { throw 'Localized Notepad++ did not start' }
        [CsvHostWindows]::ShowWindow($window,9) | Out-Null
        [CsvHostWindows]::MoveWindow($window,0,0,1420,880,$true) | Out-Null
        Start-Sleep -Milliseconds 750
        $open = [CsvHostWindows]::FindCommand([CsvHostWindows]::GetMenu($window),(Caption 'Native.OpenTable'))
        if (!$open) { Capture $window "$case-native-menu-error.png"; throw "Startup language command missing: $case" }
        Send-Host $window 0x111 ([IntPtr]$open) ([IntPtr]::Zero) | Out-Null
        Start-Sleep -Milliseconds 1000
        $children = @([CsvHostWindows]::Children($window))
        $forms = @($children | Where-Object { [CsvHostWindows]::Class($_).StartsWith('WindowsForms10.') -and [CsvHostWindows]::Title($_) -eq 'CSV Visual Editor' })
        if ($forms.Count -ne 1) { throw "Localized production panel missing: $case" }
        $form = $forms[0]
        $editors = @($children | Where-Object { [CsvHostWindows]::Class($_) -eq 'Scintilla' -and [CsvHostWindows]::IsWindowVisible($_) })
        if ($editors.Count -ne 1 -or [CsvHostWindows]::ControlText($editors[0]) -cne $fixture) { throw 'Initial source buffer differs' }
        Capture $window "$case-host.png"
        Capture $form "$case-panel.png"
        $rules = Open-Dialog 'Native.FilterSort' 'Filter.Title'
        Click-Button $rules 'Filter.AddCondition'
        $editInputs = @([CsvHostWindows]::Children($rules) | Where-Object { [CsvHostWindows]::Class($_).StartsWith('WindowsForms10.EDIT.',[StringComparison]::OrdinalIgnoreCase) -and [CsvHostWindows]::IsWindowVisible($_) })
        if ($editInputs.Count -ne 1) { throw 'Localized filter input is not unique' }
        $result=[IntPtr]::Zero
        if ([CsvHostWindows]::Text($editInputs[0],0xC,[IntPtr]::Zero,'alpha',2,5000,[ref]$result) -eq [IntPtr]::Zero) { throw 'Localized filter input timed out' }
        Click-Button $rules 'Common.Preview'
        $formatCulture = if ($code -eq 'en') { [Globalization.CultureInfo]::InvariantCulture } else { [Globalization.CultureInfo]::GetCultureInfo($code) }
        $expectedPreview = [string]::Format($formatCulture,(Caption 'Filter.PreviewOfDisplayedRowsCSVUnchanged'),[object[]]@(1,3))
        if (!(All-Text $rules).Contains($expectedPreview)) { throw "Localized preview count mismatch: $case" }
        Capture $rules "$case-filter.png"
        Click-Button $rules 'Filter.ApplyView'
        $summary = Open-Dialog 'Native.ColumnSummary' 'Summary.Title'
        # Current-view numeric semantics are already asserted by Invoke-HostReview.ps1.
        # Here the production host must expose content from the selected localized catalog.
        $expectedSummaryText = Caption 'Summary.DistinctValuesUseExactCaseSensitiveTextIncluding'
        if (!(All-Text $summary).Contains($expectedSummaryText)) { throw "Localized summary content mismatch: $case" }
        Capture $summary "$case-summary.png"
        Click-Button $summary 'Common.Close'
        $about = Open-Dialog 'Common.About' 'About.AboutCSVVisualEditor'
        $aboutText = All-Text $about
        $languageName = @($inventory.languages | Where-Object { $_.code -eq $code })[0].name
        $expectedLanguage = [string]::Format($formatCulture,(Caption 'About.InterfaceLanguage'),[object[]]@($languageName))
        if (!$aboutText.Contains($expectedLanguage) -or !$aboutText.Contains('Zolnai Zsolt') -or !$aboutText.Contains('zzsolt@gmail.com') -or !$aboutText.Contains($env:PACKAGE_VERSION)) { throw "Actual startup language/About mismatch: $case" }
        Capture $about "$case-about.png"
        Send-Host $about 0x10 ([IntPtr]::Zero) ([IntPtr]::Zero) | Out-Null
        if ([CsvHostWindows]::ControlText($editors[0]) -cne $fixture) { throw 'Localized commands modified the CSV source' }
        $evidence += [ordered]@{ Case=$case; ExpectedLanguage=$code; NativeFilename=$nativeFile; DisplayLanguageObserved=$true; LocalizedNativeMenu=$true; FilterPreviewAndApply=$true; ColumnSummary=$true; About=$true; SourceUnchanged=$true; Windows=[Environment]::OSVersion.VersionString; NotepadVersion=(Get-Item $exe).VersionInfo.ProductVersion; Dpi=[CsvHostWindows]::GetDpiForWindow($form); DllSha256=(Get-FileHash $library -Algorithm SHA256).Hash; PackageVersion=$env:PACKAGE_VERSION }
        $evidence | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $output 'language-evidence.json')
        $process.Kill(); $process.WaitForExit(); $process=$null
    }
    Write-Host 'Localized production-host startup, commands, view tools, source preservation and English fallback passed for all maintained languages plus unsupported-host cases.'
} finally {
    if ($process -and !$process.HasExited) { $process.Kill(); $process.WaitForExit() }
    Remove-Item -LiteralPath $temporary -Recurse -Force
}
