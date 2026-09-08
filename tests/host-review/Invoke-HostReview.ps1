param([Parameter(Mandatory)][string]$LibraryPath, [string]$OutputPath = 'artifacts/host-review')
$ErrorActionPreference = 'Stop'
$library = (Resolve-Path $LibraryPath).Path
New-Item -ItemType Directory -Force $OutputPath | Out-Null
$output = (Resolve-Path $OutputPath).Path
$homeDir = Join-Path ([System.IO.Path]::GetTempPath()) ('csv-host-' + [guid]::NewGuid())
New-Item -ItemType Directory $homeDir | Out-Null
$archive = Join-Path $homeDir 'host.7z'
# Pinned official upstream asset. No update service, installer, or user profile.
Invoke-WebRequest 'https://github.com/notepad-plus-plus/notepad-plus-plus/releases/download/v8.9.8/npp.8.9.8.portable.minimalist.x64.7z' -OutFile $archive
$expected = '624579cee4d9082f5f3945ceb2474ab5aeb222c2003b503b3df8f1dde60e4def'
if ((Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'Host archive digest mismatch' }
& 7z x $archive "-o$homeDir/host" -y | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Cannot extract pinned host' }
$hostDir = Join-Path $homeDir 'host'
$exe = Join-Path $hostDir 'notepad++.exe'
if (!(Test-Path $exe)) { throw 'Pinned host layout changed' }
New-Item -ItemType File (Join-Path $hostDir 'doLocalConf.xml') -Force | Out-Null
New-Item -ItemType Directory (Join-Path $hostDir 'plugins/CsvVisualEditor') -Force | Out-Null
Copy-Item $library (Join-Path $hostDir 'plugins/CsvVisualEditor/CsvVisualEditor.dll')
$csv = Join-Path $homeDir 'visual-regression.csv'
[IO.File]::WriteAllText($csv, "Value,Note`r`n`"  alpha  `",`"a b`"`r`n`"   `",`"x  y`"`r`n`"`",`"end`"", [Text.UTF8Encoding]::new($false))
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class CsvHostWindows {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; public int Width {get{return Right-Left;}} public int Height {get{return Bottom-Top;}} }
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out Rect r);
    [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int t,bool repaint);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int command);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetMenu(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetSubMenu(IntPtr h,int p);
    [DllImport("user32.dll")] public static extern int GetMenuItemCount(IntPtr h);
    [DllImport("user32.dll")] public static extern uint GetMenuItemID(IntPtr h,int p);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetMenuString(IntPtr h,uint p,StringBuilder s,int n,uint flags);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
    [DllImport("user32.dll", EntryPoint="SendMessageTimeoutW")] public static extern IntPtr Send(IntPtr h,uint m,IntPtr w,IntPtr l,uint f,uint timeout,out IntPtr result);
    [DllImport("user32.dll", EntryPoint="SendMessageTimeoutW", CharSet=CharSet.Unicode)] public static extern IntPtr Text(IntPtr h,uint m,IntPtr w,string l,uint f,uint timeout,out IntPtr result);
    public static string Title(IntPtr h) {var b=new StringBuilder(512);GetWindowText(h,b,b.Capacity);return b.ToString();}
    public static string Class(IntPtr h) {var b=new StringBuilder(512);GetClassName(h,b,b.Capacity);return b.ToString();}
    public static Rect Bounds(IntPtr h) {Rect r;GetWindowRect(h,out r);return r;}
    public static IntPtr[] Children(IntPtr h) {var a=new List<IntPtr>();EnumChildWindows(h,(w,l)=>{a.Add(w);return true;},IntPtr.Zero);return a.ToArray();}
    public static IntPtr FindProcess(uint pid) {IntPtr found=IntPtr.Zero;EnumWindows((w,l)=>{uint p;GetWindowThreadProcessId(w,out p);if(p==pid && Class(w)=="Notepad++")found=w;return true;},IntPtr.Zero);return found;}
    public static IntPtr FindNamedWindow(uint pid, string title) {IntPtr found=IntPtr.Zero;EnumWindows((w,l)=>{uint p;GetWindowThreadProcessId(w,out p);if(p==pid && Title(w)==title && IsWindowVisible(w))found=w;return true;},IntPtr.Zero);return found;}
    public static uint FindCommand(IntPtr menu,string name) {
        for(int i=0;i<GetMenuItemCount(menu);i++) { var b=new StringBuilder(512);GetMenuString(menu,(uint)i,b,b.Capacity,0x400);var s=b.ToString().Replace("&","");
            if(s.Split('\t')[0]==name)return GetMenuItemID(menu,i);
            var sub=GetSubMenu(menu,i);if(sub!=IntPtr.Zero){var id=FindCommand(sub,name);if(id!=0)return id;}}
        return 0;
    }
}
'@
function Send-Native([IntPtr]$handle,[uint32]$message,[IntPtr]$w,[IntPtr]$l) {
    $result = [IntPtr]::Zero
    if ([CsvHostWindows]::Send($handle,$message,$w,$l,2,5000,[ref]$result) -eq [IntPtr]::Zero) { throw 'Host message timed out' }
    return $result
}
function Save-Window([IntPtr]$handle,[string]$name) {
    $rect = [CsvHostWindows]::Bounds($handle)
    if ($rect.Width -lt 10 -or $rect.Height -lt 10) { throw 'Invalid capture dimensions' }
    $bmp = [Drawing.Bitmap]::new($rect.Width,$rect.Height)
    $g = [Drawing.Graphics]::FromImage($bmp)
    try {
        $dc=$g.GetHdc()
        try { $ok=[CsvHostWindows]::PrintWindow($handle,$dc,2) } finally { $g.ReleaseHdc($dc) }
        if (!$ok) { throw 'PrintWindow failed' }
        $bmp.Save((Join-Path $output $name), [Drawing.Imaging.ImageFormat]::Png)
    } finally { $g.Dispose(); $bmp.Dispose() }
}
$process=$null
try {
    $process=Start-Process $exe -ArgumentList @('-multiInst','-nosession',('"'+$csv+'"')) -PassThru
    $window=[IntPtr]::Zero
    for($i=0;$i -lt 100 -and $window -eq [IntPtr]::Zero;$i++) { Start-Sleep -Milliseconds 100; $window=[CsvHostWindows]::FindProcess($process.Id) }
    if($window -eq [IntPtr]::Zero) {throw 'Notepad++ did not create its window'}
    [CsvHostWindows]::ShowWindow($window,9) | Out-Null
    [CsvHostWindows]::MoveWindow($window,0,0,1420,880,$true) | Out-Null
    Start-Sleep -Milliseconds 1000
    $command=[CsvHostWindows]::FindCommand([CsvHostWindows]::GetMenu($window),'Open Visual Table')
    if($command -eq 0) { Save-Window $window 'load-error.png'; throw 'Production DLL did not register Open Visual Table' }
    Send-Native $window 0x111 ([IntPtr]$command) ([IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 2000
    $children=@([CsvHostWindows]::Children($window))
    @($children | ForEach-Object { [ordered]@{ Class=[CsvHostWindows]::Class($_); Title=[CsvHostWindows]::Title($_); Visible=[CsvHostWindows]::IsWindowVisible($_); Bounds=[CsvHostWindows]::Bounds($_) } }) | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $output 'window-layout.json')
    $dock=@($children | Where-Object { [CsvHostWindows]::Class($_).StartsWith('WindowsForms10.') -and [CsvHostWindows]::Title($_) -eq 'CSV Visual Editor' })
    if($dock.Count -ne 1) { Save-Window $window 'dock-error.png'; throw 'Cannot identify the production docking form' }
    $form=$dock[0]
    Save-Window $window 'host-full.png'
    Save-Window $form 'panel-initial.png'
    $query=@([CsvHostWindows]::Children($form) | Where-Object { [CsvHostWindows]::Class($_).StartsWith('WindowsForms10.Edit.', [StringComparison]::OrdinalIgnoreCase) -and [CsvHostWindows]::IsWindowVisible($_) })
    if($query.Count -ne 1) {throw 'The panel must contain exactly one visible query editor'}
    $result=[IntPtr]::Zero
    if([CsvHostWindows]::Text($query[0],0xC,[IntPtr]::Zero,'alpha',2,5000,[ref]$result) -eq [IntPtr]::Zero){throw 'Search text message timed out'}
    Start-Sleep -Milliseconds 800
    Save-Window $form 'panel-search.png'
    $labels=@([CsvHostWindows]::Children($form) | ForEach-Object { [CsvHostWindows]::Title($_) })
    if(!($labels -match '1 / 1')) {throw 'Production search did not expose its matching-cell count'}
    # Verify the real dock, not merely the standalone search control, can shrink.
    $manager=@($children | Where-Object {[CsvHostWindows]::Class($_) -eq 'dockingManager'}) | Select-Object -First 1
    $container=[CsvHostWindows]::GetParent([CsvHostWindows]::GetParent($form))
    if (!$manager) { throw 'Docking manager not found; resize verification cannot be skipped' }
    $resizes=@()
    # Leave room for the editor even on a 1024px CI desktop; 750 tests the wide layout.
    foreach($width in @(420,750)) {
        $box=[CsvHostWindows]::Bounds($container)
        $splitter=@($children | Where-Object {[CsvHostWindows]::Class($_) -eq 'wedockspliter' -and [CsvHostWindows]::IsWindowVisible($_)} | Sort-Object { [Math]::Abs([CsvHostWindows]::Bounds($_).Right-$box.Left) }) | Select-Object -First 1
        if (!$splitter) { throw 'Visible dock splitter not found; resize verification cannot be skipped' }
        Send-Native $manager 0x500B ([IntPtr]($width-$box.Width)) $splitter | Out-Null
        Start-Sleep -Milliseconds 500
        Save-Window $form ("panel-width-$width.png")
        $actual=[CsvHostWindows]::Bounds($container)
        $panel=[CsvHostWindows]::Bounds($form)
        $resizes += [ordered]@{ Requested=$width; ActualContainer=$actual.Width; ActualPanel=$panel.Width }
        $resizes | ConvertTo-Json | Set-Content (Join-Path $output 'resize-evidence.json')
        if ([Math]::Abs($actual.Width-$width) -gt 20 -or $panel.Width -gt $actual.Width) { throw 'Production dock did not honor the requested width' }
    }
    # Clearing the actual query must restore the unfiltered table.
    if([CsvHostWindows]::Text($query[0],0xC,[IntPtr]::Zero,'',2,5000,[ref]$result) -eq [IntPtr]::Zero){throw 'Clear search message timed out'}
    Start-Sleep -Milliseconds 600
    Save-Window $form 'panel-clear.png'
    $labels=@([CsvHostWindows]::Children($form) | ForEach-Object { [CsvHostWindows]::Title($_) })
    if($labels -match '1 / 1') {throw 'Clear left a stale result counter'}
    # Open the real plugin About command without blocking on its modal dialog.
    $aboutCommand=[CsvHostWindows]::FindCommand([CsvHostWindows]::GetMenu($window),'About')
    if($aboutCommand -eq 0) {throw 'Plugin About command missing'}
    [CsvHostWindows]::PostMessage($window,0x111,[IntPtr]$aboutCommand,[IntPtr]::Zero) | Out-Null
    $about=[IntPtr]::Zero
    for($i=0;$i -lt 50 -and $about -eq [IntPtr]::Zero;$i++) {Start-Sleep -Milliseconds 100; $about=[CsvHostWindows]::FindNamedWindow($process.Id,'About CSV Visual Editor')}
    if($about -eq [IntPtr]::Zero) {throw 'Production About dialog did not open'}
    Save-Window $about 'about-production.png'
    $aboutText=@([CsvHostWindows]::Children($about) | ForEach-Object { [CsvHostWindows]::Title($_) }) -join "`n"
    if(!$aboutText.Contains('Zolnai Zsolt') -or !$aboutText.Contains('zzsolt@gmail.com') -or !$aboutText.Contains($env:PACKAGE_VERSION)) {throw 'Production About content or package version mismatch'}
    Send-Native $about 0x10 ([IntPtr]::Zero) ([IntPtr]::Zero) | Out-Null
    [ordered]@{ NotepadVersion=(Get-Item $exe).VersionInfo.ProductVersion; Windows=[Environment]::OSVersion.VersionString; Dpi=[CsvHostWindows]::GetDpiForWindow($form); DllSha256=(Get-FileHash $library -Algorithm SHA256).Hash; ProductionDllLoaded=$true; SearchCountObserved=$true; SearchClearObserved=$true; AboutObserved=$true; PackageVersion=$env:PACKAGE_VERSION; DockWidths=$resizes; Scope='Automated native-host load, render, search, clear, resize and About only; not a manual acceptance or Apply test.' } | ConvertTo-Json | Set-Content (Join-Path $output 'host-evidence.json')
    Write-Host 'Real Notepad++ production-DLL load/render/search review completed.'
} finally {
    if($process -and !$process.HasExited) { $process.Kill(); $process.WaitForExit() }
    Remove-Item -LiteralPath $homeDir -Recurse -Force
}
