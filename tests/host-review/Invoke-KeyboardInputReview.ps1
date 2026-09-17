param([Parameter(Mandatory)][string]$LibraryPath, [string]$OutputPath = 'artifacts/keyboard-input-review')
$ErrorActionPreference='Stop'
$OutputPath=[IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force $OutputPath|Out-Null

# Reuse the pinned host/bootstrap and native-window helpers from production review.
$source=Get-Content (Join-Path $PSScriptRoot 'Invoke-HostReview.ps1') -Raw
$marker='$process=$null'
$cut=$source.IndexOf($marker,[StringComparison]::Ordinal)
if($cut -lt 0){throw 'Production host bootstrap boundary missing'}
$prefix=$source.Substring(0,$cut)
$helper=Join-Path ([IO.Path]::GetTempPath()) ('csv-keyboard-common-' + [guid]::NewGuid().ToString('N') + '.ps1')
[IO.File]::WriteAllText($helper,$prefix)
. $helper -LibraryPath $LibraryPath -OutputPath $OutputPath

Add-Type @'
using System;using System.Runtime.InteropServices;
public static class CsvKeyboardReview {
 [StructLayout(LayoutKind.Sequential)] public struct Info { public int cbSize,flags;public IntPtr active,focus,capture,menu,move,caret;public int l,t,r,b; }
 [DllImport("user32.dll")] public static extern bool GetGUIThreadInfo(uint id,ref Info i);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 public static IntPtr Focus(IntPtr window){uint p;var id=GetWindowThreadProcessId(window,out p);var i=new Info();i.cbSize=Marshal.SizeOf(i);if(!GetGUIThreadInfo(id,ref i))throw new Exception("Focus query failed");return i.focus;}
}
'@
function Click-At([IntPtr]$h,[int]$x,[int]$y){$xy=[IntPtr](($y -shl 16)-bor $x);Send-Native $h 0x201 ([IntPtr]1) $xy|Out-Null;Send-Native $h 0x202 ([IntPtr]0) $xy|Out-Null;Start-Sleep -Milliseconds 180}
function Type-Key([int]$key){
    $h=[CsvKeyboardReview]::Focus($window)
    if(![CsvHostWindows]::PostMessage($h,0x100,[IntPtr]$key,[IntPtr]1)){throw 'Keydown delivery failed'}
    if(![CsvHostWindows]::PostMessage($h,0x101,[IntPtr]$key,[IntPtr]0xC0000001L)){throw 'Keyup delivery failed'}
    Start-Sleep -Milliseconds 90
}
function Clear-FocusedEditor { $h=[CsvKeyboardReview]::Focus($window); Send-Native $h 0xB1 ([IntPtr]0) ([IntPtr](-1))|Out-Null; Send-Native $h 0x303 ([IntPtr]0) ([IntPtr]0)|Out-Null }
function Get-VisibleTextEditor([IntPtr]$parent) {
    $editors=@([CsvHostWindows]::Children($parent)|Where-Object {[CsvHostWindows]::Class($_).StartsWith('WindowsForms10.Edit.',[StringComparison]::OrdinalIgnoreCase) -and [CsvHostWindows]::IsWindowVisible($_)})
    if($editors.Count -ne 1){throw "Expected one visible text editor, found $($editors.Count)"}
    return $editors[0]
}

$process=$null
$window=[IntPtr]::Zero
$evidence=[ordered]@{DllSha256=(Get-FileHash $library -Algorithm SHA256).Hash.ToLowerInvariant();Passed=$false}
try {
    $process=Start-Process $exe -ArgumentList @('-multiInst','-nosession',('"'+$csv+'"')) -PassThru
    for($i=0;$i -lt 100 -and $window -eq [IntPtr]::Zero;$i++){Start-Sleep -Milliseconds 100;$window=[CsvHostWindows]::FindProcess($process.Id)}
    if($window -eq [IntPtr]::Zero){throw 'Notepad++ did not create its window'}
    [CsvKeyboardReview]::SetForegroundWindow($window)|Out-Null
    [CsvHostWindows]::MoveWindow($window,0,0,1000,740,$true)|Out-Null
    Start-Sleep -Milliseconds 500
    $command=[CsvHostWindows]::FindCommand([CsvHostWindows]::GetMenu($window),'Open Visual Table')
    if($command -eq 0){throw 'Production DLL did not register Open Visual Table'}
    Send-Native $window 0x111 ([IntPtr]$command) ([IntPtr]0)|Out-Null
    Start-Sleep -Milliseconds 1800

    $children=@([CsvHostWindows]::Children($window))
    $form=@($children|Where-Object {[CsvHostWindows]::Title($_) -eq 'CSV Visual Editor' -and [CsvHostWindows]::Class($_).StartsWith('WindowsForms10.')})[0]
    if(!$form){throw 'CSV Visual Editor dock not found'}
    $f=[CsvHostWindows]::Bounds($form)
    $strip=@([CsvHostWindows]::Children($form)|Where-Object {$r=[CsvHostWindows]::Bounds($_);$r.Top -eq $f.Top+24 -and $r.Height -eq 37})[0]
    if(!$strip){throw 'Command strip missing'}
    Click-At $strip 160 18
    $page=@([CsvHostWindows]::Children($form)|Where-Object {[CsvHostWindows]::Title($_) -eq 'Table'})[0]
    $grid=@([CsvHostWindows]::Children($page)|Where-Object {[CsvHostWindows]::IsWindowVisible($_) -and [CsvHostWindows]::Class($_).StartsWith('WindowsForms10.Window.8')})[0]
    if(!$grid){throw 'Primary table grid missing'}
    Click-At $grid 120 36
    Type-Key 0x71 # F2
    Clear-FocusedEditor

    # Posted virtual keys pass through the real native host message loop.
    Type-Key 0x41 # a
    Type-Key 0x20 # Space
    Type-Key 0x42 # b
    Type-Key 0xDC # backslash on the runner keyboard
    Type-Key 0x43 # c
    $inline=[CsvHostWindows]::ControlText([CsvKeyboardReview]::Focus($window))
    $evidence.Inline=$inline
    if($inline -cne 'a b\c'){throw "Direct cell typing changed characters: [$inline]"}
    Type-Key 0x20
    Type-Key 0x20
    $edges=[CsvHostWindows]::ControlText([CsvKeyboardReview]::Focus($window))
    $evidence.RepeatedSpaces=$edges
    if($edges -cne 'a b\c  '){throw "Repeated Space changed characters: [$edges]"}
    Save-Window $window 'grid-keyboard-input.png'

    # Opening details commits only into the pending model, never the source.
    $details=Open-DataDialog 'Cell details' 'Cell details'
    $natural=Get-VisibleTextEditor $details
    $opened=[CsvHostWindows]::ControlText($natural)
    if($opened -cne $edges){throw "Natural cell editor did not receive exact grid text: [$opened]"}
    Click-At $natural 12 12
    Clear-FocusedEditor
    Type-Key 0x58 # x
    Type-Key 0x20 # Space
    Type-Key 0x59 # y
    Type-Key 0xDC # one literal backslash
    Type-Key 0x5A # z
    $naturalTyped=[CsvHostWindows]::ControlText([CsvKeyboardReview]::Focus($window))
    $evidence.Natural=$naturalTyped
    if($naturalTyped -cne 'x y\z'){throw "Natural editor changed characters: [$naturalTyped]"}
    Type-Key 0x0D # Enter must insert a real line break, not accept the dialog.
    Type-Key 0x4E # n
    $multiline=[CsvHostWindows]::ControlText([CsvKeyboardReview]::Focus($window))
    $evidence.Multiline=$multiline
    if($multiline -cne "x y\z`r`nn"){throw "Natural Enter did not insert one CRLF: [$multiline]"}
    Click-DataButton $details 'Accept changes'
    Start-Sleep -Milliseconds 250

    $details=Open-DataDialog 'Cell details' 'Cell details'
    $natural=Get-VisibleTextEditor $details
    $reopened=[CsvHostWindows]::ControlText($natural)
    $evidence.Reopened=$reopened
    if($reopened -cne $multiline){throw "Reopened pending cell changed characters: [$reopened]"}
    Save-Window $details 'cell-details-natural-edit.png'
    Click-DataButton $details 'Cancel'
    $editors=@([CsvHostWindows]::Children($window)|Where-Object {[CsvHostWindows]::Class($_) -eq 'Scintilla' -and [CsvHostWindows]::IsWindowVisible($_)})
    if($editors.Count -ne 1 -or [CsvHostWindows]::ControlText($editors[0]) -cne $fixture){throw 'Pending keyboard review changed the Notepad++ source before Apply'}
    $evidence.SourceUnchanged=$true
    $evidence.Passed=$true
    Write-Host 'Keyboard input review PASS: direct and repeated Space, literal backslash, natural Enter, reopen and pending-only source behavior.'
}
catch {
    $evidence.Failure=$_.Exception.Message
    if($window -ne [IntPtr]::Zero){try{Save-Window $window 'keyboard-failure.png'}catch{}}
    throw
}
finally {
    $evidence|ConvertTo-Json -Depth 4|Set-Content (Join-Path $output 'keyboard-input-evidence.json') -Encoding utf8
    if(Test-Path $helper){Remove-Item $helper -Force -ErrorAction SilentlyContinue}
    if($process -and !$process.HasExited){$process.Kill();$process.WaitForExit()}
    if(Test-Path $homeDir){Remove-Item $homeDir -Recurse -Force}
}
