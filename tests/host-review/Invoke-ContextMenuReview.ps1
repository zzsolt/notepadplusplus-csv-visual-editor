param([Parameter(Mandatory)][string]$LibraryPath, [string]$OutputPath = 'artifacts/context-menu-review')
$ErrorActionPreference='Stop'

# Reuse the pinned portable host and source-preservation helpers.
$source=Get-Content (Join-Path $PSScriptRoot 'Invoke-HostReview.ps1') -Raw
$marker='$process=$null'
$prefix=$source.Substring(0,$source.IndexOf($marker,[StringComparison]::Ordinal))
$helper=Join-Path ([IO.Path]::GetTempPath()) ('csv-context-common-' + [guid]::NewGuid().ToString('N') + '.ps1')
[IO.File]::WriteAllText($helper,$prefix)
. $helper -LibraryPath $LibraryPath -OutputPath $OutputPath
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class CsvContextReview {
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
'@
function Click-At([IntPtr]$h,[int]$x,[int]$y) {
 $xy=[IntPtr](($y -shl 16)-bor $x)
 [CsvHostWindows]::PostMessage($h,0x201,[IntPtr]1,$xy)|Out-Null
 [CsvHostWindows]::PostMessage($h,0x202,[IntPtr]0,$xy)|Out-Null
 Start-Sleep -Milliseconds 250
}
function Find-Popup {
 for($i=0;$i -lt 40;$i++) {
  $h=[CsvHostWindows]::FindNamedWindow($process.Id,'Table')
  if($h -ne [IntPtr]::Zero) { return $h }
  Start-Sleep -Milliseconds 100
 }
 throw 'Data-cell context menu did not open in the actual host.'
}
function Open-CellMenu([int]$row=0,[int]$column=0) {
 # Data columns are equally sized in this two-column fixture; exclude # and selector.
 $x=120
 if($column -eq 1){$x=[Math]::Max(300,[int]([CsvHostWindows]::Bounds($grid).Width*0.72))}
 $y=36+24*$row
 $xy=[IntPtr](($y -shl 16)-bor $x)
 Send-Native $grid 0x204 ([IntPtr]2) $xy|Out-Null
 Send-Native $grid 0x205 ([IntPtr]0) $xy|Out-Null
 $script:popup=Find-Popup
 return $script:popup
}
function Menu-Items {
 $root=[System.Windows.Automation.AutomationElement]::FromHandle($script:popup)
 $condition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::MenuItem)
 $items=$root.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)
 return @($items | ForEach-Object { $_ })
}
function Find-Item([string]$caption) {
 $items=@(Menu-Items | Where-Object { $_.Current.Name.Replace('&','') -eq $caption })
 if($items.Count -ne 1){throw "Expected context command [$caption], found $($items.Count). Available: $((Menu-Items | ForEach-Object {$_.Current.Name}) -join ', ')"}
 return $items[0]
}
function Click-Item([string]$caption) {
 $item=Find-Item $caption
 if(!$item.Current.IsEnabled){throw "Context command is disabled: $caption"}
 $r=$item.Current.BoundingRectangle
 $p=[CsvHostWindows]::Bounds($script:popup)
 Click-At $script:popup ([int]($r.Left-$p.Left+$r.Width/2)) ([int]($r.Top-$p.Top+$r.Height/2))
}
function Close-Menu {
 [CsvHostWindows]::PostMessage($script:popup,0x100,[IntPtr]0x1B,[IntPtr]1)|Out-Null
 [CsvHostWindows]::PostMessage($script:popup,0x101,[IntPtr]0x1B,[IntPtr]0)|Out-Null
 Start-Sleep -Milliseconds 200
}
function Source-Text { return [CsvHostWindows]::ControlText($script:scintilla) }
function Assert-SourceUnchanged { if((Source-Text) -cne $fixture){throw 'Context operation changed the source before Apply.'} }

$process=$null
$window=[IntPtr]::Zero
$evidence=[ordered]@{Passed=$false; DllSha256=(Get-FileHash $library -Algorithm SHA256).Hash.ToLowerInvariant(); Version=[Diagnostics.FileVersionInfo]::GetVersionInfo($library).ProductVersion}
try {
 $process=Start-Process $exe -ArgumentList @('-multiInst','-nosession',('"'+$csv+'"')) -PassThru
 for($i=0;$i -lt 100 -and $window -eq [IntPtr]::Zero;$i++){Start-Sleep -Milliseconds 100;$window=[CsvHostWindows]::FindProcess($process.Id)}
 if($window -eq [IntPtr]::Zero){throw 'Notepad++ window missing.'}
 [CsvContextReview]::SetForegroundWindow($window)|Out-Null
 [CsvHostWindows]::MoveWindow($window,0,0,1100,850,$true)|Out-Null
 Start-Sleep -Milliseconds 500
 $id=[CsvHostWindows]::FindCommand([CsvHostWindows]::GetMenu($window),'Open Visual Table')
 if($id -eq 0){throw 'Production plugin did not register its command.'}
 Send-Native $window 0x111 ([IntPtr]$id) ([IntPtr]0)|Out-Null
 Start-Sleep -Milliseconds 1800
 $children=@([CsvHostWindows]::Children($window))
 $form=@($children|Where-Object {[CsvHostWindows]::Title($_) -eq 'CSV Visual Editor' -and [CsvHostWindows]::Class($_).StartsWith('WindowsForms10.')})[0]
 if(!$form){throw 'CSV dock missing.'}
 $page=@([CsvHostWindows]::Children($form)|Where-Object {[CsvHostWindows]::Title($_) -eq 'Table'})[0]
 $grid=@([CsvHostWindows]::Children($page)|Where-Object {[CsvHostWindows]::IsWindowVisible($_) -and [CsvHostWindows]::Class($_).StartsWith('WindowsForms10.Window.8')})[0]
 if(!$grid){throw 'Primary data grid missing.'}
 $script:scintilla=@($children|Where-Object {[CsvHostWindows]::Class($_) -eq 'Scintilla' -and [CsvHostWindows]::IsWindowVisible($_)})[0]
 Assert-SourceUnchanged

 # A right mouse click must target the second row, not the previously current first row.
 Open-CellMenu 1 0|Out-Null
 $readNames=@(Menu-Items|ForEach-Object {$_.Current.Name.Replace('&','')})
 if($readNames -contains 'Cut' -or $readNames -contains 'Paste' -or $readNames -contains 'Apply'){throw 'Read-only context exposed editing commands.'}
 Find-Item 'Cell details'|Out-Null
 Find-Item 'Column summary'|Out-Null
 Save-Window $script:popup 'context-menu-read-only.png'
 Click-Item 'Copy'
 $copied=Get-Clipboard -Raw
 if($copied -cne '   '){throw "Right-click Copy used the wrong cell: [$copied]"}
 $evidence.ReadOnlyCopy=$copied
 Assert-SourceUnchanged

 # Keyboard invocation reuses the current cell rather than the mouse location.
 Send-Native $grid 0x007B $grid ([IntPtr](-1))|Out-Null
 $script:popup=Find-Popup
 Click-Item 'Copy'
 if((Get-Clipboard -Raw) -cne '   '){throw 'Keyboard context menu lost the current-cell target.'}
 $evidence.KeyboardContext=$true

 $f=[CsvHostWindows]::Bounds($form)
 $strip=@([CsvHostWindows]::Children($form)|Where-Object {$r=[CsvHostWindows]::Bounds($_);$r.Top -eq $f.Top+24 -and $r.Height -eq 37})[0]
 if(!$strip){throw 'Command strip missing.'}
 Click-At $strip 160 18
 Open-CellMenu 0 0|Out-Null
 Find-Item 'Cut'|Out-Null; Find-Item 'Paste'|Out-Null
 if((Find-Item 'Apply').Current.IsEnabled){throw 'Apply must be disabled before a pending change.'}
 if((Find-Item 'Delete Row').Current.IsEnabled){throw 'A single data-cell target must not authorize whole-row deletion.'}
 Save-Window $script:popup 'context-menu-edit.png'
 # Paste the copied three spaces into the first row via the real existing command.
 Click-Item 'Paste'
 Assert-SourceUnchanged
 Open-CellMenu 0 0|Out-Null
 if(!(Find-Item 'Apply').Current.IsEnabled){throw 'Pending paste did not enable Apply.'}
 Click-Item 'Revert All'
 Assert-SourceUnchanged
 Open-CellMenu 0 0|Out-Null; Click-Item 'Copy'
 if((Get-Clipboard -Raw) -cne '  alpha  '){throw 'Context Revert All did not restore the original cell.'}
 $evidence.PasteAndRevert=$true

 # Whole-row deletion is available only after explicit row selection.
 Open-CellMenu 1 0|Out-Null
 Click-Item 'Select complete rows for deletion'
 Open-CellMenu 1 0|Out-Null
 if(!(Find-Item 'Delete Row').Current.IsEnabled){throw 'Explicit row selection did not enable Delete Row.'}
 Save-Window $script:popup 'context-menu-selected-row.png'
 Click-Item 'Delete Row'
 Assert-SourceUnchanged
 Open-CellMenu 0 0|Out-Null; Click-Item 'Revert All'
 Assert-SourceUnchanged
 $evidence.ExplicitRowDeleteAndRevert=$true

 # End-to-end pending edit -> context Apply -> one Scintilla Undo on the exact DLL.
 Open-CellMenu 1 0|Out-Null; Click-Item 'Copy'
 Open-CellMenu 0 0|Out-Null; Click-Item 'Paste'
 Assert-SourceUnchanged
 Open-CellMenu 0 0|Out-Null; Click-Item 'Apply'
 Start-Sleep -Milliseconds 500
 $applied=Source-Text
 $rows=@($applied|ConvertFrom-Csv)
 if($rows.Count -ne 3 -or $rows[0].Value -cne '   ' -or $rows[0].Note -cne 'a b' -or $rows[1].Value -cne '   '){throw 'Context Apply wrote unexpected CSV data.'}
 Send-Native $script:scintilla 2176 ([IntPtr]0) ([IntPtr]0)|Out-Null
 Assert-SourceUnchanged
 $evidence.ApplyAndSingleUndo=$true
 $evidence.SourceRestored=$true
 $evidence.Passed=$true
 Write-Host 'Context-menu host review PASS: mouse/keyboard targets, read/edit, Copy/Paste/Revert, explicit row delete, Apply and one Undo.'
}
catch { $evidence.Error=$_.Exception.Message; throw }
finally {
 $evidence|ConvertTo-Json -Depth 5|Set-Content (Join-Path $output 'context-menu-evidence.json')
 if(Test-Path $helper){Remove-Item $helper -Force -ErrorAction SilentlyContinue}
 if($process -and !$process.HasExited){$process.Kill();$process.WaitForExit()}
 if(Test-Path $homeDir){Remove-Item $homeDir -Recurse -Force}
}
