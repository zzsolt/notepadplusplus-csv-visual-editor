param([string]$LibraryPath)
$ErrorActionPreference='Stop'
$source=Get-Content tests/host-review/Invoke-HostReview.ps1 -Raw
$prefix=$source.Substring(0,$source.IndexOf('$process=$null'))
$helper=Join-Path $env:RUNNER_TEMP 'keyboard-common.ps1'
[IO.File]::WriteAllText($helper,$prefix)
. $helper -LibraryPath $LibraryPath -OutputPath 'keyboard-evidence'
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class KeyboardProbe {
 [StructLayout(LayoutKind.Sequential)] public struct Info { public int cbSize,flags;public IntPtr active,focus,capture,menu,move,caret;public int l,t,r,b; }
 [DllImport("user32.dll")] public static extern bool GetGUIThreadInfo(uint id,ref Info i);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 public static IntPtr Focus(IntPtr window){uint p;var id=GetWindowThreadProcessId(window,out p);var i=new Info();i.cbSize=Marshal.SizeOf(i);if(!GetGUIThreadInfo(id,ref i))throw new Exception("Focus query failed");return i.focus;}
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
'@
function Click-At([IntPtr]$h,[int]$x,[int]$y){$xy=[IntPtr](($y -shl 16)-bor $x);Send-Native $h 0x201 ([IntPtr]1) $xy|Out-Null;Send-Native $h 0x202 ([IntPtr]0) $xy|Out-Null;Start-Sleep -Milliseconds 180}
function Type-Key([int]$key){$h=[KeyboardProbe]::Focus($window);[CsvHostWindows]::PostMessage($h,0x100,[IntPtr]$key,[IntPtr]1)|Out-Null;[CsvHostWindows]::PostMessage($h,0x101,[IntPtr]$key,[IntPtr]0xC0000001L)|Out-Null;Start-Sleep -Milliseconds 90}
function Type-Chars([string]$text){foreach($c in $text.ToCharArray()){[CsvHostWindows]::PostMessage([KeyboardProbe]::Focus($window),0x102,[IntPtr][int]$c,[IntPtr]1)|Out-Null;Start-Sleep -Milliseconds 25}}
function Record([string]$label){$h=[KeyboardProbe]::Focus($window);[ordered]@{label=$label;focusClass=[CsvHostWindows]::Class($h);text=[CsvHostWindows]::ControlText($h)}|ConvertTo-Json -Compress|Add-Content (Join-Path $output 'keys.jsonl')}
$process=$null
try {
 $process=Start-Process $exe -ArgumentList @('-multiInst','-nosession',('"'+$csv+'"')) -PassThru
 $window=[IntPtr]::Zero
 for($i=0;$i -lt 100 -and $window -eq [IntPtr]::Zero;$i++){Start-Sleep -Milliseconds 100;$window=[CsvHostWindows]::FindProcess($process.Id)}
 if($window -eq [IntPtr]::Zero){throw 'No host'}
 [KeyboardProbe]::SetForegroundWindow($window)|Out-Null
 [CsvHostWindows]::MoveWindow($window,0,0,1000,740,$true)|Out-Null
 Start-Sleep -Milliseconds 500
 $command=[CsvHostWindows]::FindCommand([CsvHostWindows]::GetMenu($window),'Open Visual Table')
 Send-Native $window 0x111 ([IntPtr]$command) ([IntPtr]0)|Out-Null
 Start-Sleep -Milliseconds 1800
 $children=@([CsvHostWindows]::Children($window))
 $form=@($children|Where-Object {[CsvHostWindows]::Title($_) -eq 'CSV Visual Editor' -and [CsvHostWindows]::Class($_).StartsWith('WindowsForms10.')})[0]
 $f=[CsvHostWindows]::Bounds($form)
 $strip=@([CsvHostWindows]::Children($form)|Where-Object {$r=[CsvHostWindows]::Bounds($_);$r.Top -eq $f.Top+24 -and $r.Height -eq 37})[0]
 if(!$strip){throw 'Command strip missing'}
 Click-At $strip 185 18
 $details=Open-DataDialog 'Cell details' 'Cell details'
 Require-DialogText $details 'Changes remain pending'
 Click-DataButton $details 'Cancel'
 $page=@([CsvHostWindows]::Children($form)|Where-Object {[CsvHostWindows]::Title($_) -eq 'Table'})[0]
 $grid=@([CsvHostWindows]::Children($page)|Where-Object {[CsvHostWindows]::IsWindowVisible($_) -and [CsvHostWindows]::Class($_).StartsWith('WindowsForms10.Window.8')})[0]
 Click-At $grid 120 36
 Type-Key 0x71
 Record 'after-F2'
 $active=[KeyboardProbe]::Focus($window)
 Send-Native $active 0xB1 ([IntPtr]0) ([IntPtr](-1))|Out-Null
 Send-Native $active 0x303 ([IntPtr]0) ([IntPtr]0)|Out-Null
 Type-Key 0x41;Record 'A'
 Type-Key 0x20;Record 'A-space'
 Type-Key 0x42;Record 'A-space-B'
 Type-Key 0xDC;Record 'backslash'
 Type-Key 0x43;Record 'C'
 Save-Window $form 'typed-inline.png'
 $details=Open-DataDialog 'Cell details' 'Cell details'
 @([CsvHostWindows]::Children($details)|ForEach-Object {[CsvHostWindows]::ControlText($_)})|ConvertTo-Json|Set-Content (Join-Path $output 'inline-details.json')
 $ed=@([CsvHostWindows]::Children($details)|Where-Object {[CsvHostWindows]::Class($_).StartsWith('WindowsForms10.Edit.',[StringComparison]::OrdinalIgnoreCase) -and [CsvHostWindows]::IsWindowVisible($_)})[0]
 Click-At $ed 10 10
 Send-Native $ed 0xB1 ([IntPtr]0) ([IntPtr](-1))|Out-Null
 Send-Native $ed 0x303 ([IntPtr]0) ([IntPtr]0)|Out-Null
 Type-Key 0x41;Type-Key 0x20;Type-Key 0x42;Type-Key 0xDC;Type-Key 0x4E
 Start-Sleep -Milliseconds 350
 Record 'modal-A-space-B-backslash-n'
 @([CsvHostWindows]::Children($details)|ForEach-Object {[CsvHostWindows]::ControlText($_)})|ConvertTo-Json|Set-Content (Join-Path $output 'modal-details.json')
 Save-Window $details 'typed-modal.png'
 Click-DataButton $details 'Cancel'
} finally {if($window -and [CsvHostWindows]::IsWindowVisible($window)){Save-Window $window 'final.png'};if($process -and !$process.HasExited){$process.Kill();$process.WaitForExit()};if(Test-Path $homeDir){Remove-Item $homeDir -Recurse -Force}}
