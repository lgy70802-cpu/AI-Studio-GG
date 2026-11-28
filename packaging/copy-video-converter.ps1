param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'

$name = 'ffmpeg-n8.1.3-1004-g8524289d26-win64-lgpl-shared-8.1'
$release = 'https://github.com/FFmpeg-Builds/FFmpeg-Builds/releases/download/autobuild-2026-11-28-12-43'
$sha256 = 'dee39f610313d8b2b56bc81ba5bbc9ca254916f523c7762b6ce0848e29eaeb18'
$cache = New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot '../artifacts/packaging/video-converter')
$archive = Join-Path $cache.FullName ($name + '.zip')
if (!(Test-Path -LiteralPath $archive)) {
    $partial = $archive + '.tmp'
    try {
        curl.exe --fail --location --retry 2 --max-time 300 --silent --show-error "$release/$name.zip" --output $partial
        if ($LASTEXITCODE) { throw 'Video converter download failed.' }
        if ((Get-FileHash -LiteralPath $partial).Hash -ne $sha256) { throw 'Video converter checksum mismatch.' }
        Move-Item -LiteralPath $partial -Destination $archive
    }
    finally {
        if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial -Force }
    }
}
if ((Get-FileHash -LiteralPath $archive).Hash -ne $sha256) { throw 'Cached video converter checksum mismatch.' }

$stage = Join-Path $cache.FullName ([Guid]::NewGuid().ToString('N'))
try {
    Expand-Archive -LiteralPath $archive -DestinationPath $stage
    $package = Join-Path $stage $name
    $bin = Join-Path $package 'bin'
    if (!(Test-Path -LiteralPath (Join-Path $bin 'ffmpeg.exe'))) { throw 'Video converter executable missing from package.' }
    New-Item -ItemType Directory -Force $Destination | Out-Null
    Copy-Item -LiteralPath (Join-Path $bin 'ffmpeg.exe'),(Join-Path $bin 'ffprobe.exe') -Destination $Destination -Force
    Get-ChildItem -LiteralPath $bin -Filter '*.dll' -File | Copy-Item -Destination $Destination -Force
    Get-ChildItem -LiteralPath $package -File | Copy-Item -Destination $Destination -Force
    "FFmpeg Windows build: $release/$name.zip" | Set-Content -LiteralPath (Join-Path $Destination 'UPSTREAM.txt') -Encoding utf8
    "FFmpeg source: https://github.com/FFmpeg/FFmpeg/tree/8524289d26" | Add-Content -LiteralPath (Join-Path $Destination 'UPSTREAM.txt')
} finally {
    if (Test-Path -LiteralPath $stage) {
        $resolved = (Resolve-Path -LiteralPath $stage).Path
        if ([IO.Path]::GetDirectoryName($resolved) -eq $cache.FullName) {
            Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
