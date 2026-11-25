param([switch]$NoShortcut, [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'D92Studio'))
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Install-Staged.ps1') -NoShortcut:$NoShortcut -InstallRoot $InstallRoot