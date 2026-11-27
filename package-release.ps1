param([string]$Version = '0.2.16', [string]$Iscc, [string]$UpdateBaseUrl, [string]$ReleaseNotesFile)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must have the form 0.2.0' }
Push-Location $PSScriptRoot
try {
    npm --prefix d-studio run lint
    if ($LASTEXITCODE) { throw 'TypeScript check failed' }
    npm --prefix d-studio run build
    if ($LASTEXITCODE) { throw 'Frontend build failed' }
    $release = Join-Path $PSScriptRoot "artifacts/release/$Version"
    $app = Join-Path $release 'app'
    if (Get-Process -Name 'MiraBox.App' -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($app + '\', [StringComparison]::OrdinalIgnoreCase) }) {
        throw 'Close the application running from the release build directory before packaging'
    }
    if (Test-Path $app) {
        $resolved = (Resolve-Path $app).Path
        if ($resolved -ne [IO.Path]::GetFullPath($app)) { throw 'Unexpected publish directory' }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
    dotnet publish src/MiraBox.App/MiraBox.App.csproj -c Release -r win-x64 --self-contained true "-p:Version=$Version" -o $app
    if ($LASTEXITCODE) { throw 'Desktop publish failed' }
    New-Item -ItemType Directory -Force (Join-Path $app 'Web') | Out-Null
    Copy-Item d-studio/dist/* (Join-Path $app 'Web') -Recurse -Force
    $bootstrap = Join-Path $PSScriptRoot 'artifacts/packaging/MicrosoftEdgeWebview2Setup.exe'
    if (!(Test-Path $bootstrap)) {
        New-Item -ItemType Directory -Force (Split-Path $bootstrap) | Out-Null
        Invoke-WebRequest 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $bootstrap
    }
    $signature = Get-AuthenticodeSignature $bootstrap
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') { throw 'WebView2 bootstrapper signature check failed' }
    $vcRuntime = Join-Path $PSScriptRoot 'artifacts/packaging/VC_redist.x64.exe'
    if (!(Test-Path $vcRuntime)) {
        Invoke-WebRequest 'https://aka.ms/vs/17/release/vc_redist.x64.exe' -OutFile $vcRuntime
    }
    $vcSignature = Get-AuthenticodeSignature $vcRuntime
    if ($vcSignature.Status -ne 'Valid' -or $vcSignature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') { throw 'VC runtime signature check failed' }
    if (!$Iscc) {
        $Iscc = @("$env:LOCALAPPDATA/Programs/Inno Setup 6/ISCC.exe", "${env:ProgramFiles(x86)}/Inno Setup 6/ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
    if (!$Iscc) { throw 'Install Inno Setup 6 or provide -Iscc' }
    & $Iscc "/DAppVersion=$Version" packaging/MiraBoxStudio.iss
    if ($LASTEXITCODE) { throw 'Installer build failed' }
    $setup = Join-Path $release "MiraBoxStudioNext-$Version-win-x64-Setup.exe"
    $hash = Get-FileHash $setup
    $fileName = [IO.Path]::GetFileName($setup)
    $baseUrl = if ($UpdateBaseUrl) { $UpdateBaseUrl.TrimEnd('/') } else { 'https://updates.example.com/mirabox' }
    $uri = [Uri]$baseUrl
    if (!$uri.IsAbsoluteUri -or $uri.Scheme -ne 'https' -or $uri.UserInfo) { throw 'UpdateBaseUrl must use HTTPS without embedded credentials' }
    $notes = if ($ReleaseNotesFile) { Get-Content -LiteralPath $ReleaseNotesFile -Raw -Encoding utf8 } else { "MiraBox Studio Next $Version 更新" }
    if ($notes.Length -gt 32000) { throw 'Release notes exceed the update protocol limit' }
    $manifest = @{ version = $Version; notes = $notes; url = "$baseUrl/$fileName"; sha256 = $hash.Hash; bytes = (Get-Item $setup).Length }
    $manifestName = if ($UpdateBaseUrl) { 'update.json' } else { 'update-template.json' }
    $manifest | ConvertTo-Json | Set-Content (Join-Path $release $manifestName) -Encoding utf8NoBOM
    "$($hash.Hash)  $fileName" | Set-Content ($setup + '.sha256') -Encoding ascii
    $hash | Format-List
} finally { Pop-Location }
