param([Parameter(Mandatory)][string]$PackageDirectory, [switch]$Launch, [string]$RehearsalRoot, [switch]$NextLaunch)
$ErrorActionPreference='Stop'
if($NextLaunch -and $Launch){throw 'NextLaunch never starts or restarts Sona.'}
$taskPackage=[IO.Path]::GetFullPath($PackageDirectory)
$taskReceipt=Get-Content -LiteralPath (Join-Path $taskPackage 'package.json') | ConvertFrom-Json
if ($taskReceipt.Version -notmatch '^0\.[0-9]+\.[0-9]+$' -or $taskReceipt.SourceHead -notmatch '^[a-f0-9]{40}$') { throw 'The package receipt is not a recognized candidate.' }
$taskProgramRoot=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\Sona'))
if($RehearsalRoot){
    $RehearsalRoot=[IO.Path]::GetFullPath($RehearsalRoot)
    if((Split-Path $RehearsalRoot -Leaf)-notmatch'^sona-upgrade-rehearsal-[a-f0-9]{32}$'-or!(Test-Path -LiteralPath (Join-Path $RehearsalRoot '.sona-rehearsal'))){throw 'Unrecognized isolated rehearsal root'}
    $taskProgramRoot=Join-Path $RehearsalRoot 'Programs\Sona'
}
$taskRunning=@(Get-Process -Name Murmur,Sona -ErrorAction SilentlyContinue)
if($RehearsalRoot){$taskRunning=@($taskRunning|Where-Object{$_.Path-and$_.Path.StartsWith($taskProgramRoot+'\',[StringComparison]::OrdinalIgnoreCase)})}
if($taskRunning.Count -and !$NextLaunch){throw 'Quit the existing dictation instance before installing this candidate, or use NextLaunch to preserve it.'}
$taskInstall = Join-Path $taskProgramRoot ($taskReceipt.Version + '-' + $taskReceipt.SourceHead.Substring(0,8))
$taskPreservedProcesses=@()
if($NextLaunch){
    foreach($taskProcess in $taskRunning){
        $taskRunningPath=$taskProcess.Path
        if(!$taskRunningPath -or !$taskRunningPath.StartsWith($taskProgramRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'NextLaunch preserves an unrecognized running instance; installation aborted.'}
        if((Split-Path $taskRunningPath -Parent).Equals($taskInstall,[StringComparison]::OrdinalIgnoreCase)){throw 'NextLaunch never writes into a running version directory.'}
        $taskRunningReceipt=Join-Path (Split-Path $taskRunningPath -Parent) 'installation.json'
        if(!(Test-Path -LiteralPath $taskRunningReceipt)){throw 'Running instance has no ownership receipt.'}
        $taskRunningOwned=Get-Content -LiteralPath $taskRunningReceipt -Raw|ConvertFrom-Json
        if($taskRunningOwned.Path -ne $taskRunningPath -or (Get-FileHash -LiteralPath $taskRunningPath).Hash.ToLowerInvariant() -ne $taskRunningOwned.SHA256){throw 'Running instance ownership differs; preserved.'}
        $taskPreservedProcesses+=@{Id=$taskProcess.Id;Path=$taskRunningPath;Started=$taskProcess.StartTime.ToUniversalTime().ToString('o')}
    }
}

foreach ($taskFile in $taskReceipt.Files) {
    $taskSource = [IO.Path]::GetFullPath((Join-Path $taskPackage $taskFile.Path))
    if (!$taskSource.StartsWith($taskPackage + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Package path escapes its root.' }
    if ((Get-FileHash -LiteralPath $taskSource).Hash.ToLowerInvariant() -ne $taskFile.SHA256) { throw "Package file hash differs: $($taskFile.Path)" }
}
foreach ($taskFile in $taskReceipt.Files) {
    $taskDestination = [IO.Path]::GetFullPath((Join-Path $taskInstall $taskFile.Path))
    if (!$taskDestination.StartsWith($taskInstall + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Install path escapes its version directory.' }
    New-Item -ItemType Directory -Path (Split-Path $taskDestination -Parent) -Force | Out-Null
    if ((Test-Path -LiteralPath $taskDestination) -and (Get-FileHash -LiteralPath $taskDestination).Hash.ToLowerInvariant() -ne $taskFile.SHA256) { throw "Preserving a different existing version file: $taskDestination" }
    Copy-Item -LiteralPath (Join-Path $taskPackage $taskFile.Path) -Destination $taskDestination -Force
    if ((Get-FileHash -LiteralPath $taskDestination).Hash.ToLowerInvariant() -ne $taskFile.SHA256) { throw 'Installed file verification failed.' }
}
$taskMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Sona.lnk'
if($RehearsalRoot){$taskMenu=Join-Path $RehearsalRoot 'Sona.lnk'}
$taskShell=New-Object -ComObject WScript.Shell
if (Test-Path -LiteralPath $taskMenu) { $taskExisting=$taskShell.CreateShortcut($taskMenu); if (!$taskExisting.TargetPath.StartsWith($taskProgramRoot + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'An unrelated Sona shortcut was preserved. Choose a different shortcut name.' } }
$taskShortcut=$taskShell.CreateShortcut($taskMenu); $taskShortcut.TargetPath=Join-Path $taskInstall 'Sona.exe'; $taskShortcut.WorkingDirectory=$taskInstall; $taskShortcut.IconLocation=(Join-Path $taskInstall 'Sona.exe') + ',0'; $taskShortcut.Description='Intentional local dictation for an unsent draft'; $taskShortcut.Save()
# Remove only the legacy shortcut verified by its own installed receipt. Keep
# versioned recovery binaries and the existing preferences directory intact.
$taskLegacyMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Murmur.lnk'
if (!$NextLaunch -and !$RehearsalRoot -and (Test-Path -LiteralPath $taskLegacyMenu)) {
    $taskLegacy = $taskShell.CreateShortcut($taskLegacyMenu)
    $taskLegacyRoot = (Join-Path $env:LOCALAPPDATA 'Programs\Murmur') + '\'
    if ($taskLegacy.TargetPath.StartsWith($taskLegacyRoot,[StringComparison]::OrdinalIgnoreCase)) {
        $taskLegacyReceiptPath = Join-Path (Split-Path $taskLegacy.TargetPath -Parent) 'installation.json'
        if (Test-Path -LiteralPath $taskLegacyReceiptPath) {
            $taskLegacyReceipt = Get-Content -LiteralPath $taskLegacyReceiptPath -Raw | ConvertFrom-Json
            if ($taskLegacyReceipt.Path -eq $taskLegacy.TargetPath -and (Test-Path -LiteralPath $taskLegacy.TargetPath) -and (Get-FileHash -LiteralPath $taskLegacy.TargetPath).Hash.ToLowerInvariant() -eq $taskLegacyReceipt.SHA256) { Remove-Item -LiteralPath $taskLegacyMenu }
        }
    }
}
$taskInstalled=@{Version=$taskReceipt.Version; SourceHead=$taskReceipt.SourceHead; Path=(Join-Path $taskInstall 'Sona.exe'); SHA256=(Get-FileHash -LiteralPath (Join-Path $taskInstall 'Sona.exe')).Hash.ToLowerInvariant(); StartMenu=$taskMenu; StartupChanged=$false; InstalledAt=[DateTimeOffset]::Now.ToString('o'); Files=$taskReceipt.Files}
$taskInstalled | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskInstall 'installation.json')
if(Test-Path -LiteralPath (Join-Path $taskInstall 'sona-agent.exe')) {
    $taskCurrent=Join-Path $taskProgramRoot 'current'
    if(Test-Path -LiteralPath $taskCurrent) {
        $taskCurrentItem=Get-Item -LiteralPath $taskCurrent
        if($taskCurrentItem.LinkType -ne 'Junction' -or -not $taskCurrentItem.Target) { throw 'An unrelated current directory was preserved.' }
        $taskCurrentTargets=@($taskCurrentItem.Target)
        $taskOldTarget=[IO.Path]::GetFullPath([string]$taskCurrentTargets[0])
        if(-not $taskOldTarget.StartsWith($taskProgramRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'An unrelated current junction was preserved.' }
        $taskOldReceipt=Join-Path $taskOldTarget 'installation.json'
        if(-not(Test-Path -LiteralPath $taskOldReceipt)){throw 'Unreceipted current junction was preserved.'}
        $taskOwnedReceipt=Get-Content -LiteralPath $taskOldReceipt -Raw | ConvertFrom-Json
        if($taskOwnedReceipt.Path -ne (Join-Path $taskOldTarget 'Sona.exe') -or (Get-FileHash -LiteralPath $taskOwnedReceipt.Path).Hash.ToLowerInvariant() -ne $taskOwnedReceipt.SHA256){throw 'Current junction ownership did not verify.'}
        # Nonrecursive removal of the verified junction only; its version directory remains.
        Remove-Item -LiteralPath $taskCurrent -Force
    }
    if(-not [IO.Path]::GetFullPath($taskInstall).StartsWith($taskProgramRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'CLI alias target escapes the Sona installation.'}
    New-Item -ItemType Junction -Path $taskCurrent -Value $taskInstall | Out-Null
    if(!$RehearsalRoot){
    $taskUserPath=[Environment]::GetEnvironmentVariable('Path','User')
    $taskPathEntries=@($taskUserPath -split ';' | Where-Object{$_})
    if(-not @($taskPathEntries | Where-Object{$_.TrimEnd('\').Equals($taskCurrent,[StringComparison]::OrdinalIgnoreCase)}).Count){[Environment]::SetEnvironmentVariable('Path',(($taskPathEntries+$taskCurrent)-join ';'),'User')}
    }
}
if ($Launch -and $RehearsalRoot) { throw 'Launch an isolated test with its explicit profile and --test-ui instead.' }
if ($Launch) { Start-Process -FilePath (Join-Path $taskInstall 'Sona.exe') -ArgumentList '--start-hidden' -WindowStyle Hidden }
if($NextLaunch){
    foreach($taskPreserved in $taskPreservedProcesses){
        $taskStillRunning=Get-Process -Id $taskPreserved.Id -ErrorAction Stop
        if($taskStillRunning.Path -ne $taskPreserved.Path -or $taskStillRunning.StartTime.ToUniversalTime().ToString('o') -ne $taskPreserved.Started){throw 'Running instance changed during staging; inspect before launch.'}
    }
    $taskInstalled.NextLaunchOnly=$true
    $taskInstalled.PreservedProcesses=$taskPreservedProcesses
}
$taskInstalled | ConvertTo-Json -Depth 2
