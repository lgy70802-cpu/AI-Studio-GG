param([switch]$NoShortcut, [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'D92Studio'))
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'app'))
$base = [IO.Path]::GetFullPath($InstallRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
$destination = Join-Path $base 'app'
if ($base -eq [IO.Path]::GetPathRoot($base) -or $base.StartsWith($source, [StringComparison]::OrdinalIgnoreCase) -or $source.StartsWith($base + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use a separate dedicated install root.' }
$release = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'release.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($release.Product -ne 'D92 Studio' -or $release.Architecture -ne 'win-x64' -or $release.Version -notmatch '^\d+\.\d+\.\d+(?:-[a-zA-Z0-9.]+)?$') { throw 'Invalid release metadata' }
$manifest = @(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'files.json') -Raw -Encoding UTF8 | ConvertFrom-Json)
function Assert-Child([string]$path) { if (-not [IO.Path]::GetFullPath($path).StartsWith($base + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Install path escapes root' } }
function Payload-Files([string]$directory) {
    foreach ($item in Get-ChildItem -LiteralPath $directory -Force) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse point rejected' }
        if ($item.PSIsContainer) { Payload-Files $item.FullName } else { $item }
    }
}
function Verify-Payload([string]$directory) {
    if ((Get-Item -LiteralPath $directory).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Payload reparse root rejected' }
    if ($manifest.Count -eq 0) { throw 'Empty payload manifest' }
    $files = @(Payload-Files $directory)
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $manifest) {
        if (-not $entry.Path -or [IO.Path]::IsPathRooted($entry.Path) -or $entry.Path -match '(^|[\\/])\.\.([\\/]|$)|:' -or $entry.SHA256 -notmatch '^[a-fA-F0-9]{64}$' -or -not $seen.Add($entry.Path)) { throw 'Invalid manifest entry' }
        $path = [IO.Path]::GetFullPath((Join-Path $directory $entry.Path))
        if (-not $path.StartsWith([IO.Path]::GetFullPath($directory) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Payload escapes root' }
        if ((Get-Item -LiteralPath $path).Length -ne $entry.Bytes -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.SHA256) { throw "Payload mismatch: $($entry.Path)" }
    }
    if ($files.Count -ne $manifest.Count -or -not (Test-Path -LiteralPath (Join-Path $directory 'DeskDisplay.App.exe'))) { throw 'Unlisted or missing payload files' }
}
Verify-Payload $source
if ((Test-Path -LiteralPath $base) -and ((Get-Item -LiteralPath $base).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Install root is a reparse point' }
Assert-Child $destination
if (Test-Path -LiteralPath $destination) {
    $marker = Join-Path $destination 'd92-install.json'
    if (((Get-Item -LiteralPath $destination).Attributes -band [IO.FileAttributes]::ReparsePoint) -or -not (Test-Path -LiteralPath $marker) -or (Get-Content -LiteralPath $marker -Raw -Encoding UTF8 | ConvertFrom-Json).Product -ne 'D92 Studio') { throw 'Existing app directory is not a managed installation; preserving it.' }
}
if (Get-Process DeskDisplay.App -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($destination + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) }) { throw 'Close installed D92 Studio before updating.' }
New-Item -ItemType Directory -Force -Path $base | Out-Null
$stage = Join-Path $base ('staging-' + [Guid]::NewGuid().ToString('N')); $backup = Join-Path $base ('backup-' + [Guid]::NewGuid().ToString('N'))
Assert-Child $stage; Assert-Child $backup
New-Item -ItemType Directory -Path $stage | Out-Null
$moved = $false
try {
    foreach ($item in Get-ChildItem -LiteralPath $source -Force) { Copy-Item -LiteralPath $item.FullName -Destination $stage -Recurse -Force }
    Verify-Payload $stage
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'release.json') -Destination (Join-Path $stage 'd92-install.json')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'files.json') -Destination (Join-Path $stage 'd92-files.json')
    if (Test-Path -LiteralPath $destination) { Move-Item -LiteralPath $destination -Destination $backup; $moved = $true }
    try { Move-Item -LiteralPath $stage -Destination $destination }
    catch { if ($moved -and -not (Test-Path -LiteralPath $destination)) { Move-Item -LiteralPath $backup -Destination $destination }; throw }
} finally { Assert-Child $stage; if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force } }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall.ps1') -Destination (Join-Path $base 'Uninstall.ps1') -Force
$restoreTool=Join-Path $source 'Release\Restore-Previous.ps1'
if(-not (Test-Path -LiteralPath $restoreTool)){$restoreTool=Join-Path $PSScriptRoot 'Restore-Previous.ps1'}
if(Test-Path -LiteralPath $restoreTool) { Copy-Item -LiteralPath $restoreTool -Destination (Join-Path $base 'Restore-Previous.ps1') -Force }
if (-not $NoShortcut) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'D92 Studio.lnk'))
    $shortcut.TargetPath = Join-Path $destination 'DeskDisplay.App.exe'; $shortcut.WorkingDirectory = $destination; $shortcut.Save()
}
Write-Output "Installed $($release.Version): $destination"
if ($moved) { Write-Output "Previous app retained: $backup" }
Write-Output 'APPDATA\DeskDisplay settings, library and downloaded resources were preserved.'
