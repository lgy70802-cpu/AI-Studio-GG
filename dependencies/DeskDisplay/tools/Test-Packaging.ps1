$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('d92-package-' + [Guid]::NewGuid().ToString('N'))
$package = Join-Path $fixture 'release'; $target = Join-Path $fixture 'installed'
function Check($value, [string]$label) { if (-not $value) { throw $label }; Write-Output "PASS: $label" }
function Manifest {
    $entries = @(Get-ChildItem -LiteralPath (Join-Path $package 'app') -File | ForEach-Object { @{ Path=$_.Name; Bytes=$_.Length; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
    ConvertTo-Json -InputObject $entries | Set-Content -LiteralPath (Join-Path $package 'files.json')
}
function Metadata([string]$version) { @{Product='D92 Studio';Architecture='win-x64';Version=$version} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'release.json') }
function Install { & (Join-Path $package 'Install.ps1') -NoShortcut -InstallRoot $target }
try {
    New-Item -ItemType Directory -Path (Join-Path $package 'app') -Force | Out-Null
    foreach ($name in @('Install.ps1','Install-Staged.ps1','Uninstall.ps1','Restore-Previous.ps1')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot "packaging/$name") -Destination $package }
    # Non-executed fixture bytes test file transactions only, not application startup.
    Set-Content -LiteralPath (Join-Path $package 'app/DeskDisplay.App.exe') -Value 'non-executed installer fixture one'
    Set-Content -LiteralPath (Join-Path $package 'app/obsolete.txt') -Value 'old'
    Metadata '0.1.0-test.1'; Manifest; Install
    Check ((Get-Content -LiteralPath (Join-Path $target 'app/d92-install.json') -Raw | ConvertFrom-Json).Version -eq '0.1.0-test.1') 'Verified installation writes its version marker'
    New-Item -ItemType Directory -Path (Join-Path $target 'UserData') | Out-Null
    Set-Content -LiteralPath (Join-Path $target 'UserData/keep.txt') -Value 'preserve'
    $obsolete = [IO.Path]::GetFullPath((Join-Path $package 'app/obsolete.txt'))
    if (-not $obsolete.StartsWith([IO.Path]::GetFullPath($fixture) + [IO.Path]::DirectorySeparatorChar)) { throw 'Bad fixture scope' }
    Remove-Item -LiteralPath $obsolete
    Set-Content -LiteralPath (Join-Path $package 'app/DeskDisplay.App.exe') -Value 'non-executed installer fixture two'
    Metadata '0.1.0-test.2'; Manifest; Install
    Check (-not (Test-Path -LiteralPath (Join-Path $target 'app/obsolete.txt'))) 'Update swaps the folder without retaining stale files'
    Check ((Get-ChildItem -LiteralPath $target -Directory -Filter 'backup-*').Count -eq 1) 'Previous installed app is retained for recovery'
    Check ((Get-Content -LiteralPath (Join-Path $target 'UserData/keep.txt')) -eq 'preserve') 'Unrelated user data remains untouched'
    $hash = (Get-FileHash -LiteralPath (Join-Path $target 'app/DeskDisplay.App.exe')).Hash
    $originalBackup=(Get-ChildItem -LiteralPath $target -Directory -Filter 'backup-*').Name
    $rejected=$false;try{& (Join-Path $package 'Restore-Previous.ps1') -InstallRoot $target -BackupName '../outside'}catch{$rejected=$true}
    Check ($rejected -and (Get-FileHash -LiteralPath (Join-Path $target 'app/DeskDisplay.App.exe')).Hash -eq $hash) 'Rollback rejects an outside backup before moving the current app'
    & (Join-Path $package 'Restore-Previous.ps1') -InstallRoot $target -BackupName $originalBackup
    Check ((Get-Content -LiteralPath (Join-Path $target 'app/d92-install.json') -Raw|ConvertFrom-Json).Version -eq '0.1.0-test.1') 'Verified rollback restores the prior version'
    Check ((Get-Content -LiteralPath (Join-Path $target 'UserData/keep.txt')) -eq 'preserve') 'Rollback leaves user data untouched'
    Install
    $hash = (Get-FileHash -LiteralPath (Join-Path $target 'app/DeskDisplay.App.exe')).Hash
    $badBackup=Get-ChildItem -LiteralPath $target -Directory -Filter 'backup-*'|Select-Object -First 1
    Set-Content -LiteralPath (Join-Path $badBackup.FullName 'DeskDisplay.App.exe') 'damaged backup'
    $rejected=$false;try{& (Join-Path $package 'Restore-Previous.ps1') -InstallRoot $target -BackupName $badBackup.Name}catch{$rejected=$true}
    Check ($rejected -and (Get-FileHash -LiteralPath (Join-Path $target 'app/DeskDisplay.App.exe')).Hash -eq $hash) 'Corrupt backup cannot replace a working installation'
    Set-Content -LiteralPath (Join-Path $package 'app/DeskDisplay.App.exe') -Value 'tampered'
    $rejected = $false; try { Install } catch { $rejected = $true }
    Check ($rejected -and (Get-FileHash -LiteralPath (Join-Path $target 'app/DeskDisplay.App.exe')).Hash -eq $hash) 'Corrupt update preserves the working installed payload'
    Manifest
    Set-Content -LiteralPath (Join-Path $package 'app/unlisted.exe') -Value 'unlisted'
    $rejected = $false; try { Install } catch { $rejected = $true }
    Check $rejected 'Unlisted payload files are rejected'
    @(@{Path='../escape.exe';Bytes=1;SHA256=('a' * 64)}) | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'files.json')
    $rejected = $false; try { Install } catch { $rejected = $true }
    Check $rejected 'Manifest traversal is rejected before destination changes'
    & (Join-Path $package 'Uninstall.ps1') -InstallRoot $target -NoShortcut
    Check (-not (Test-Path -LiteralPath (Join-Path $target 'app')) -and (Test-Path -LiteralPath (Join-Path $target 'UserData/keep.txt'))) 'Isolated uninstall removes only app files and preserves user data'
    Check (@(Get-ChildItem -LiteralPath $target -Directory -Filter 'backup-*').Count -gt 0) 'Uninstall retains recovery backups'
} finally {
    $resolved = [IO.Path]::GetFullPath($fixture)
    if (-not $resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolved -Leaf) -notlike 'd92-package-*') { throw 'Unsafe fixture cleanup path' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
