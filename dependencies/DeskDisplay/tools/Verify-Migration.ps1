param([switch]$Build, [switch]$RunTests, [switch]$SkipIntegrityCheck)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if (!$SkipIntegrityCheck) {
    $manifestPath = Join-Path $repo '.migration-manifest.json'
    if (!(Test-Path -LiteralPath $manifestPath)) { throw 'Migration manifest missing. Run this script from the extracted migration package.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $prefix = $repo.TrimEnd('\') + '\'
    foreach ($entry in $manifest.Files) {
        $path = [IO.Path]::GetFullPath((Join-Path $repo $entry.Path))
        if (!$path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest path escapes project directory' }
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing file: $($entry.Path)" }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Sha256) { throw "Hash mismatch: $($entry.Path)" }
    }
    Write-Output "PASS: $($manifest.Files.Count) migrated files match SHA256 manifest."
} else {
    Write-Output 'SKIP: migration integrity check explicitly skipped for a modified development tree.'
}
if (!$Build -and !$RunTests) { return }
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnet = if ($dotnetCommand) { $dotnetCommand.Source } else { $null }
$sdks = if ($dotnet) { & $dotnet --list-sdks }
if (!($sdks -match '^8\.')) {
    $localDotnet = Join-Path $env:LOCALAPPDATA 'DeskDisplayBuild\dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $localDotnet -PathType Leaf) {
        $dotnet = $localDotnet
        $sdks = & $dotnet --list-sdks
    }
}
if (!($sdks -match '^8\.')) { throw 'Install a Windows .NET 8 SDK, then retry.' }
Push-Location $repo
try {
    & $dotnet build DeskDisplay.sln -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if ($RunTests) {
        & $dotnet 'tests/DeskDisplay.SmokeTests/bin/Release/net8.0/DeskDisplay.SmokeTests.dll'
        if ($LASTEXITCODE -ne 0) { throw 'Configuration smoke checks failed.' }
        & './tests/DeskDisplay.DisplayTests/bin/Release/net8.0-windows10.0.19041.0/DeskDisplay.DisplayTests.exe'
        if ($LASTEXITCODE -ne 0) { throw 'Display regression checks failed; inspect the output and local display environment.' }
    }
} finally { Pop-Location }
