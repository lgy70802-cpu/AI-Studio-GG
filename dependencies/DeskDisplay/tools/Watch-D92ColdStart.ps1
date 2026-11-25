# Observe an actual detach/reattach; never toggle USB power or launch Craft.
param(
    [Parameter(Mandatory)][string]$PlanPath,
    [Parameter(Mandatory)][string]$LogPath,
    [string]$ProductionPlanPath,
    [ValidateRange(30,600)][int]$TimeoutSeconds = 300
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$logFile = [IO.Path]::GetFullPath($LogPath)
$planFile = [IO.Path]::GetFullPath($PlanPath)
$clock = [Diagnostics.Stopwatch]::StartNew()
$waitUntil = $TimeoutSeconds
function Write-Event($kind, $details) {
    $event = @{ time = (Get-Date -Format o); kind = $kind; details = $details } | ConvertTo-Json -Compress -Depth 5
    Add-Content -LiteralPath $logFile -Value $event -Encoding utf8
    Write-Output $event
}
function Assert-CraftStopped {
    if (Get-Process -Name 'MiraBox Craft' -ErrorAction SilentlyContinue) { throw 'Craft started during cold-start observation; experiment invalidated' }
}
function Get-D92 {
    @(Get-PnpDevice -PresentOnly | Where-Object { $_.InstanceId -like 'USB\VID_5548&PID_1011\*' })
}
try {
    Assert-CraftStopped
    $initial = @(Get-D92)
    if ($initial.Count -ne 1) { throw 'Expected exactly one connected D92 before disconnect observation' }
    $originalId = $initial[0].InstanceId
    Write-Event 'waiting_for_disconnect' @{ craftRunning = $false; timeoutSeconds = $TimeoutSeconds }
    $detached = $false
    while ($clock.Elapsed.TotalSeconds -lt $waitUntil) {
        Assert-CraftStopped
        $devices = @(Get-D92)
        if ($devices.Count -gt 1) { throw 'More than one D92; target is ambiguous' }
        if (!$detached -and $devices.Count -eq 0) {
            $detached = $true
            $detachedAt = $clock.Elapsed.TotalSeconds
            $waitUntil = $detachedAt + $TimeoutSeconds
            Write-Event 'disconnect_observed' @{}
        } elseif ($detached -and $devices.Count -eq 1) {
            if ($devices[0].InstanceId -ne $originalId) { throw 'A different device instance reconnected; do not guess its identity' }
            $absentSeconds = $clock.Elapsed.TotalSeconds - $detachedAt
            if ($absentSeconds -lt 3) { throw 'Disconnect interval too short; repeat with all power removed for at least 5 seconds' }
            Write-Event 'reconnect_observed' @{ absentSeconds = [Math]::Round($absentSeconds,1) }
            Start-Sleep -Seconds 2
            Assert-CraftStopped
            $interfaces = @(& (Join-Path $PSScriptRoot 'Inspect-D92Hid.ps1') | ConvertFrom-Json)
            if ($interfaces.Count -ne 1) { throw 'Expected one freshly enumerated D92 HID interface' }
            $plan = Get-Content -LiteralPath $planFile -Raw | ConvertFrom-Json
            $plan.DevicePath = $interfaces[0].Path
            $plan | ConvertTo-Json | Set-Content -LiteralPath $planFile -Encoding utf8
            Write-Event 'sending_after_reconnect' @{ reportBytes = $interfaces[0].OutputReportBytes; craftRunning = $false }
            $dotnet = Join-Path $env:LOCALAPPDATA 'DeskDisplayBuild\dotnet\dotnet.exe'
            $probe = Join-Path $repo 'tools\D92.ProtocolProbe\bin\Release\net8.0-windows10.0.19041.0\D92.ProtocolProbe.dll'
            if ($ProductionPlanPath) {
                $productionPlan = Get-Content -LiteralPath $ProductionPlanPath -Raw | ConvertFrom-Json
                if (($originalId -split '\\')[-1] -ne $productionPlan.SerialNumber) { throw 'Production plan serial does not match the observed device' }
                $demo = Join-Path $repo 'tools\DeskDisplay.UsbDemo\bin\Release\net8.0-windows10.0.19041.0\DeskDisplay.UsbDemo.dll'
                & $dotnet $demo --send-static ([IO.Path]::GetFullPath($ProductionPlanPath)) $plan.ImagePath 2>&1 | ForEach-Object { Write-Event 'sender' @{ output = "$_" } }
            } else {
                & $dotnet $probe --send $planFile 2>&1 | ForEach-Object { Write-Event 'sender' @{ output = "$_" } }
            }
            if ($LASTEXITCODE -ne 0) { throw "Sender exited with code $LASTEXITCODE" }
            Assert-CraftStopped
            Write-Event 'completed' @{ visualConfirmation = 'pending'; physicalPowerRemoval = 'requires_user_confirmation' }
            exit 0
        }
        Start-Sleep -Seconds 1
    }
    throw 'Timed out waiting for observed USB disconnect and reconnect'
} catch {
    Write-Event 'failed' @{ error = $_.Exception.Message }
    exit 1
}
