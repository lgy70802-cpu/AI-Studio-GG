param([int]$Seconds = 300, [string]$OutputFile = "$PSScriptRoot/artifacts/video-qa/resources-extended.json", [int]$HostProcessId = 0, [switch]$KeepSystemAwake)
$ErrorActionPreference = 'Stop'
if ($KeepSystemAwake) {
    Add-Type 'using System; using System.Runtime.InteropServices; public static class MiraBoxTestPower { [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint state); }'
    [void][MiraBoxTestPower]::SetThreadExecutionState(2147483649)
}
$samples = @()
$cores = [Environment]::ProcessorCount
$clock = [Diagnostics.Stopwatch]::StartNew()
$seenCpu = @{}
$lastCheckpoint = 0
while ($clock.Elapsed.TotalSeconds -lt $Seconds) {
    $hostProcess = if ($HostProcessId) { Get-Process -Id $HostProcessId -ErrorAction SilentlyContinue } else { Get-Process MiraBox.App | Where-Object Path -Like '*MiraBoxStudio.Next*' }
    if (@($hostProcess).Count -gt 1) { throw 'Multiple hosts found; pass -HostProcessId explicitly' }
    if (!$hostProcess) { throw 'MiraBoxStudio.Next exited during measurement' }
    $treeIds = [Collections.Generic.HashSet[int]]::new()
    [void]$treeIds.Add($hostProcess.Id)
    # Parent PIDs can be reused by short-lived renderer processes. Identify this
    # app's WebView2 processes by their actual profile instead of following stale parents.
    $inventory = Get-CimInstance Win32_Process -Filter "Name = 'msedgewebview2.exe'" |
        Where-Object { $_.CommandLine -like '*\MiraBoxStudio.Next\WebView2*' }
    foreach ($entry in $inventory) { [void]$treeIds.Add([int]$entry.ProcessId) }
    $family = Get-Process -Id @($treeIds) -ErrorAction SilentlyContinue
    foreach ($member in $family) {
        try { $seenCpu["$($member.Id):$($member.StartTime.ToFileTimeUtc())"] = $member.CPU } catch { }
    }
    $samples += [pscustomobject]@{
        seconds = $clock.Elapsed.TotalSeconds
        hostCpuSeconds = $hostProcess.CPU
        hostPrivateMiB = $hostProcess.PrivateMemorySize64 / 1MB
        treeCpuSeconds = ($seenCpu.Values | Measure-Object -Sum).Sum
        treePrivateMiB = ($family.PrivateMemorySize64 | Measure-Object -Sum).Sum / 1MB
        processCount = $family.Count
        hostThreads = $hostProcess.Threads.Count
        hostHandles = $hostProcess.HandleCount
    }
    if ($clock.Elapsed.TotalSeconds - $lastCheckpoint -ge 60) {
        $lastCheckpoint = $clock.Elapsed.TotalSeconds
        $firstSample = $samples[0]; $latestSample = $samples[-1]
        $checkpoint = @{ elapsedSeconds=$clock.Elapsed.TotalSeconds; sampleCount=$samples.Count; hostCpuPercent=100*($latestSample.hostCpuSeconds-$firstSample.hostCpuSeconds)/($latestSample.seconds-$firstSample.seconds)/$cores; treeCpuPercent=100*($latestSample.treeCpuSeconds-$firstSample.treeCpuSeconds)/($latestSample.seconds-$firstSample.seconds)/$cores; hostPrivateMiB=$latestSample.hostPrivateMiB; treePrivateMiB=$latestSample.treePrivateMiB; hostThreads=$latestSample.hostThreads; hostHandles=$latestSample.hostHandles }
        $checkpoint | ConvertTo-Json | Set-Content -LiteralPath ($OutputFile+'.progress.tmp')
        Move-Item -LiteralPath ($OutputFile+'.progress.tmp') -Destination ($OutputFile+'.progress.json') -Force
    }
    Start-Sleep -Seconds 3
}
$first = $samples[0]
$last = $samples[-1]
$result = [ordered]@{
    logicalProcessors = $cores
    durationSeconds = $last.seconds - $first.seconds
    hostCpuPercent = 100 * ($last.hostCpuSeconds - $first.hostCpuSeconds) / ($last.seconds - $first.seconds) / $cores
    treeCpuPercent = 100 * ($last.treeCpuSeconds - $first.treeCpuSeconds) / ($last.seconds - $first.seconds) / $cores
    firstHostPrivateMiB = $first.hostPrivateMiB
    lastHostPrivateMiB = $last.hostPrivateMiB
    peakHostPrivateMiB = ($samples.hostPrivateMiB | Measure-Object -Maximum).Maximum
    firstTreePrivateMiB = $first.treePrivateMiB
    lastTreePrivateMiB = $last.treePrivateMiB
    peakTreePrivateMiB = ($samples.treePrivateMiB | Measure-Object -Maximum).Maximum
    samples = $samples
}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputFile
[pscustomobject]$result | Select-Object -Property * -ExcludeProperty samples | ConvertTo-Json
if ($KeepSystemAwake) { [void][MiraBoxTestPower]::SetThreadExecutionState(2147483648) }
