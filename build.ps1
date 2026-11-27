param()
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Push-Location (Join-Path $root 'd-studio')
try {
    npm run build
    if ($LASTEXITCODE) { throw '前端构建失败' }
} finally { Pop-Location }
dotnet build (Join-Path $root 'src/MiraBox.App/MiraBox.App.csproj') -c Release -o (Join-Path $root 'artifacts/current')
if ($LASTEXITCODE) { throw '桌面构建失败' }
$web = Join-Path $root 'artifacts/current/Web'
New-Item -ItemType Directory -Force $web | Out-Null
Copy-Item (Join-Path $root 'd-studio/dist/*') $web -Recurse -Force
Get-ChildItem -LiteralPath $web -Recurse -File -Filter '*1123*.html' | Remove-Item
