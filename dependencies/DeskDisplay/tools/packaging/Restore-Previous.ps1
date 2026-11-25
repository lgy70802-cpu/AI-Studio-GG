param([Parameter(Mandatory=$true)][string]$BackupName,[string]$InstallRoot=(Join-Path $env:LOCALAPPDATA 'D92Studio'))
$ErrorActionPreference='Stop'
$base=[IO.Path]::GetFullPath($InstallRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
if($base -eq [IO.Path]::GetPathRoot($base) -or $BackupName -notmatch '^backup-[a-fA-F0-9]{32}$'){throw 'Specify an existing backup name under the managed install root.'}
$backup=[IO.Path]::GetFullPath((Join-Path $base $BackupName));$current=Join-Path $base 'app'
foreach($path in @($base,$backup,$current)){
    if(-not (Test-Path -LiteralPath $path) -or ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Managed non-reparse installation and backup required.'}
}
if((Split-Path $backup) -ne $base -or (Split-Path $current) -ne $base){throw 'Rollback target escapes install root.'}
foreach($directory in @($backup,$current)){
    if((Get-Content -LiteralPath (Join-Path $directory 'd92-install.json') -Raw -Encoding UTF8|ConvertFrom-Json).Product -ne 'D92 Studio'){throw 'Unmanaged app directory rejected.'}
}
if(Get-Process DeskDisplay.App -ErrorAction SilentlyContinue|Where-Object {$_.Path -and $_.Path.StartsWith($current+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)}){throw 'Close D92 Studio before rollback.'}
$manifestFile=Join-Path $backup 'd92-files.json'
if(-not (Test-Path -LiteralPath $manifestFile)){throw 'Old backup has no verification manifest. Reinstall the original release package instead.'}
$manifest=@(Get-Content -LiteralPath $manifestFile -Raw -Encoding UTF8|ConvertFrom-Json)
if($manifest.Count -eq 0){throw 'Empty backup manifest.'}
$seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$files=@(Get-ChildItem -LiteralPath $backup -Recurse -Force)
if($files|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}){throw 'Backup reparse entry rejected.'}
foreach($entry in $manifest){
    if(-not $entry.Path -or [IO.Path]::IsPathRooted($entry.Path) -or $entry.Path -match '(^|[\\/])\.\.([\\/]|$)|:' -or $entry.SHA256 -notmatch '^[a-fA-F0-9]{64}$' -or -not $seen.Add($entry.Path)){throw 'Invalid backup manifest.'}
    $path=[IO.Path]::GetFullPath((Join-Path $backup $entry.Path))
    if(-not $path.StartsWith($backup+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Backup path escapes directory.'}
    if((Get-Item -LiteralPath $path).Length -ne $entry.Bytes -or (Get-FileHash -LiteralPath $path).Hash -ne $entry.SHA256){throw 'Backup payload verification failed.'}
}
if(@($files|Where-Object {-not $_.PSIsContainer}).Count -ne $manifest.Count+2 -or -not (Test-Path -LiteralPath (Join-Path $backup 'DeskDisplay.App.exe'))){throw 'Unlisted or missing backup files.'}
$retained=[IO.Path]::GetFullPath((Join-Path $base ('backup-'+[Guid]::NewGuid().ToString('N'))))
if((Split-Path $retained) -ne $base){throw 'Invalid retained app path.'}
Move-Item -LiteralPath $current -Destination $retained
try{Move-Item -LiteralPath $backup -Destination $current}
catch{if(-not (Test-Path -LiteralPath $current)){Move-Item -LiteralPath $retained -Destination $current};throw}
Write-Output "Restored verified $BackupName; previous app retained at $retained. User data unchanged."
