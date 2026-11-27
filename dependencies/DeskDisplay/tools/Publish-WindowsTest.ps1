param([string]$Version = '0.1.0-preview.20261024.1')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[a-zA-Z0-9.]+)?$') { throw 'Invalid version' }
$projectRoot = Split-Path $PSScriptRoot
$releaseRoot = Join-Path $projectRoot "artifacts\releases\D92Studio-$Version-win-x64"
$payload = Join-Path $releaseRoot 'app'
if (Test-Path -LiteralPath $releaseRoot) { throw 'Release already exists; choose a new version.' }
New-Item -ItemType Directory -Force -Path $payload | Out-Null
dotnet publish (Join-Path $projectRoot 'src\DeskDisplay.App\DeskDisplay.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:Version=$Version -o $payload
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
$support=Join-Path $payload 'Release'
New-Item -ItemType Directory -Path $support -Force|Out-Null
foreach($name in @('RELEASE_NOTES.md','KNOWN_ISSUES.md','ACCEPTANCE.md','Install.cmd','Restore-Previous.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "packaging\$name") -Destination $support
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Test-Release.ps1') -Destination $support
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'packaging\Install.ps1') -Destination $releaseRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'packaging\Install-Staged.ps1') -Destination $releaseRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'packaging\Uninstall.ps1') -Destination $releaseRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'packaging\README.txt') -Destination $releaseRoot
@{ Product = 'D92 Studio'; Version = $Version; Architecture = 'win-x64'; SelfContained = $true } | ConvertTo-Json | Set-Content (Join-Path $releaseRoot 'release.json') -Encoding UTF8
$manifest = Get-ChildItem -LiteralPath $payload -File -Recurse | ForEach-Object {
    [pscustomobject]@{ Path = [IO.Path]::GetRelativePath($payload, $_.FullName); SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash; Bytes = $_.Length }
}
$manifest | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $releaseRoot 'files.json') -Encoding UTF8
$zip = "$releaseRoot.zip"
Compress-Archive -Path (Join-Path $releaseRoot '*') -DestinationPath $zip -Force
Get-FileHash -LiteralPath $zip -Algorithm SHA256 | Format-List
Write-Output "Release folder: $releaseRoot"
