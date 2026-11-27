param([string]$InstallRoot=(Join-Path $env:LOCALAPPDATA 'D92Studio'),[switch]$NoShortcut)
$ErrorActionPreference = 'Stop'
$base = [IO.Path]::GetFullPath($InstallRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
if($base -eq [IO.Path]::GetPathRoot($base) -or (Test-Path -LiteralPath $base) -and ((Get-Item -LiteralPath $base).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Invalid uninstall root'}
$destination = [IO.Path]::GetFullPath((Join-Path $base 'app'))
if ((Split-Path $destination) -ne $base -or (Split-Path $destination -Leaf) -ne 'app') { throw 'Invalid uninstall target' }
$marker = Join-Path $destination 'd92-install.json'
if (-not (Test-Path -LiteralPath $marker)) { throw 'D92 Studio installation marker missing' }
if ((Get-Content -LiteralPath $marker -Raw -Encoding UTF8 | ConvertFrom-Json).Product -ne 'D92 Studio') { throw 'Invalid installation marker' }
if((Get-Item -LiteralPath $destination).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Uninstall reparse target rejected'}
if (Get-Process DeskDisplay.App -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($destination, [StringComparison]::OrdinalIgnoreCase) }) { throw 'Close D92 Studio before uninstalling.' }
Remove-Item -LiteralPath $destination -Recurse -Force
$shortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'D92 Studio.lnk'
if (-not $NoShortcut -and (Test-Path -LiteralPath $shortcut)) {
    $shell = New-Object -ComObject WScript.Shell
    if ($shell.CreateShortcut($shortcut).TargetPath -eq (Join-Path $destination 'DeskDisplay.App.exe')) { Remove-Item -LiteralPath $shortcut }
}
Write-Output 'Application removed. APPDATA\DeskDisplay settings, downloaded resources and library were preserved.'
