param([Parameter(Mandatory=$true)][string]$ReleasePath,[Parameter(Mandatory=$true)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$release=[IO.Path]::GetFullPath($ReleasePath);$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
if(Test-Path -LiteralPath $evidence){throw 'Choose a new evidence directory.'}
New-Item -ItemType Directory -Path $evidence|Out-Null
$install=Join-Path $evidence '隔离安装 RC';$data=Join-Path $evidence '新用户 数据'
$realSettings=Join-Path $env:APPDATA 'DeskDisplay\settings.json'
$before=if(Test-Path -LiteralPath $realSettings){(Get-FileHash -LiteralPath $realSettings).Hash}else{$null}
$results=New-Object 'System.Collections.Generic.List[string]'
function Check($condition,[string]$name){if(-not $condition){throw $name};$results.Add($name);Write-Output "PASS: $name"}
try{
    & (Join-Path $release 'Install.ps1') -NoShortcut -InstallRoot $install
    $app=Join-Path $install 'app';$exe=Join-Path $app 'DeskDisplay.App.exe'
    foreach($name in @('hostfxr.dll','hostpolicy.dll','coreclr.dll','DeskDisplay.App.runtimeconfig.json')){Check (Test-Path -LiteralPath (Join-Path $app $name)) "Bundled runtime file present: $name"}
    $start=New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName=$exe;$start.WorkingDirectory=$app;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WindowStyle='Hidden'
    $start.EnvironmentVariables['D92_STUDIO_DATA_ROOT']=$data
    $start.EnvironmentVariables['D92_STUDIO_FIRST_START_SMOKE']='1'
    $start.EnvironmentVariables['DOTNET_ROOT']=Join-Path $evidence 'no-external-runtime'
    $start.EnvironmentVariables['DOTNET_ROOT_X64']=$start.EnvironmentVariables['DOTNET_ROOT']
    $process=[Diagnostics.Process]::Start($start)
    if(-not $process.WaitForExit(60000)){throw "Isolated first-start timed out. Process $($process.Id) left intact; inspect its logs. No force kill performed."}
    Check ($process.ExitCode -eq 0) 'Actual self-contained app starts and exits normally with isolated fresh data'
    $proof=Get-Content -LiteralPath (Join-Path $data 'first-start-smoke.json') -Raw -Encoding UTF8|ConvertFrom-Json
    Check ($proof.Success -and $proof.MainWindowLoaded -and $proof.DataRoot -eq $data) 'First-start proof confirms a loaded window and isolated data root'
    Check (-not $proof.StandaloneOutputRunning -and $proof.ManagedOutputRecords -eq 0 -and -not $proof.AutoDisplayOnStartup -and -not $proof.RestoreD92OnStartup -and -not $proof.LaunchAtLogin) 'Fresh profile never automatically outputs or registers login startup'
    Check ($proof.RuntimeDirectory.StartsWith($app+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) 'Actual runtime loaded from the installed payload rather than the global SDK'
    $settings=Join-Path $data 'settings.json';$dataHash=(Get-FileHash -LiteralPath $settings).Hash
    & (Join-Path $release 'Install.ps1') -NoShortcut -InstallRoot $install
    Check ((Get-FileHash -LiteralPath $settings).Hash -eq $dataHash) 'Actual package update preserves isolated user settings'
    $backup=(Get-ChildItem -LiteralPath $install -Directory -Filter 'backup-*'|Select-Object -First 1).Name
    & (Join-Path $release 'app\Release\Restore-Previous.ps1') -InstallRoot $install -BackupName $backup
    Check ((Get-FileHash -LiteralPath $settings).Hash -eq $dataHash) 'Actual verified rollback preserves isolated user settings'
    & (Join-Path $release 'Uninstall.ps1') -InstallRoot $install -NoShortcut
    Check (-not (Test-Path -LiteralPath $app) -and (Get-FileHash -LiteralPath $settings).Hash -eq $dataHash) 'Actual isolated uninstall preserves its user data'
    $after=if(Test-Path -LiteralPath $realSettings){(Get-FileHash -LiteralPath $realSettings).Hash}else{$null}
    Check ($before -eq $after) 'Real user settings remain untouched throughout release acceptance'
    [pscustomobject]@{Success=$true;Environment='Same Windows host, isolated data; not a clean OS or standard-user VM';Checks=$results;SettingsBefore=$before;SettingsAfter=$after;FirstStart=$proof}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $evidence 'result.json') -Encoding UTF8
}catch{
    [pscustomobject]@{Success=$false;Error=$_.Exception.Message;Checks=$results;Environment='Same Windows host, isolated data; not a clean OS'}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $evidence 'result.json') -Encoding UTF8
    throw
}
