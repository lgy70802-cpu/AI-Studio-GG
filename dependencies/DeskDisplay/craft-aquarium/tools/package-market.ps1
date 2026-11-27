$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectRoot = Split-Path $PSScriptRoot -Parent
$pluginName = 'com.mirabox.streamdock.aquarium.mPanelPlugin'
$sourcePath = Join-Path $projectRoot ('release/Aquarium-V1.1/' + $pluginName)
$manifest = Get-Content -LiteralPath (Join-Path $sourcePath 'manifest.json') -Raw | ConvertFrom-Json
if (-not $manifest.Name -or -not $manifest.Version -or -not $manifest.OS) { throw 'Incomplete manifest' }
foreach ($action in $manifest.Actions) {
    if ($action.UUID -notmatch '^com\.mirabox\.streamdock\.aquarium\.[a-z0-9.-]+$') { throw 'Unexpected Action UUID' }
}
$outputDirectory = Join-Path $projectRoot 'release/marketplace'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$temporaryPath = Join-Path $outputDirectory ([Guid]::NewGuid().ToString('N') + '.zip')
try {
    # ZIP container, with manifest at the archive root. No preview or source wrapper.
    [IO.Compression.ZipFile]::CreateFromDirectory($sourcePath, $temporaryPath, [IO.Compression.CompressionLevel]::Optimal, $false)
    $archive = [IO.Compression.ZipFile]::OpenRead($temporaryPath)
    try {
        $entry = $archive.GetEntry('manifest.json')
        if (-not $entry) { throw 'Missing root manifest.json' }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { $packed = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        $required = @($packed.CodePath, $packed.Icon, $packed.CategoryIcon)
        foreach ($action in $packed.Actions) { $required += @($action.Icon, $action.PropertyInspectorPath) }
        foreach ($file in $required) { if ($file -and -not $archive.GetEntry($file)) { throw "Missing entry: $file" } }
        foreach ($item in $archive.Entries) {
            if ($item.FullName -match '(^/|(^|/)\.\.(/|$)|preview\.html|README\.md)') { throw "Unexpected entry: $($item.FullName)" }
            $stream = $item.Open()
            try { $stream.CopyTo([IO.Stream]::Null) } finally { $stream.Dispose() }
        }
        Write-Host "Validated $($archive.Entries.Count) entries: $($packed.Name) $($packed.Version)"
        Write-Host ('Action UUID: ' + ($packed.Actions.UUID -join ', '))
    } finally { $archive.Dispose() }
    Copy-Item -LiteralPath $temporaryPath -Destination (Join-Path $outputDirectory $pluginName) -Force
    Copy-Item -LiteralPath $temporaryPath -Destination (Join-Path $outputDirectory ($pluginName + '.zip')) -Force
} finally {
    if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath }
}
Get-ChildItem -LiteralPath $outputDirectory -File | Select-Object Name, Length
