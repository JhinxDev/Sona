param([Parameter(Mandatory)][string]$PackageDirectory, [switch]$Launch)
$ErrorActionPreference='Stop'
$taskPackage=[IO.Path]::GetFullPath($PackageDirectory)
$taskReceipt=Get-Content -LiteralPath (Join-Path $taskPackage 'package.json') | ConvertFrom-Json
if ($taskReceipt.Version -ne '0.1.0' -or $taskReceipt.SourceHead -notmatch '^[a-f0-9]{40}$') { throw 'The package receipt is not a recognized candidate.' }
if (Get-Process -Name Murmur -ErrorAction SilentlyContinue) { throw 'Quit the existing Murmur instance before installing this candidate.' }
$taskInstall = Join-Path (Join-Path $env:LOCALAPPDATA 'Programs\Murmur') ($taskReceipt.Version + '-' + $taskReceipt.SourceHead.Substring(0,8))
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
$taskMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Murmur.lnk'
$taskShell=New-Object -ComObject WScript.Shell
if (Test-Path -LiteralPath $taskMenu) { $taskExisting=$taskShell.CreateShortcut($taskMenu); if (!$taskExisting.TargetPath.StartsWith((Join-Path $env:LOCALAPPDATA 'Programs\Murmur') + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'An unrelated Murmur shortcut was preserved. Choose a different shortcut name.' } }
$taskShortcut=$taskShell.CreateShortcut($taskMenu); $taskShortcut.TargetPath=Join-Path $taskInstall 'Murmur.exe'; $taskShortcut.WorkingDirectory=$taskInstall; $taskShortcut.IconLocation=(Join-Path $taskInstall 'Murmur.exe') + ',0'; $taskShortcut.Description='Intentional local dictation for an unsent draft'; $taskShortcut.Save()
$taskInstalled=@{Version=$taskReceipt.Version; SourceHead=$taskReceipt.SourceHead; Path=(Join-Path $taskInstall 'Murmur.exe'); SHA256=(Get-FileHash -LiteralPath (Join-Path $taskInstall 'Murmur.exe')).Hash.ToLowerInvariant(); StartMenu=$taskMenu; StartupChanged=$false; InstalledAt=[DateTimeOffset]::Now.ToString('o'); Files=$taskReceipt.Files}
$taskInstalled | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskInstall 'installation.json')
if ($Launch) { Start-Process -FilePath (Join-Path $taskInstall 'Murmur.exe') -ArgumentList '--start-hidden' -WindowStyle Hidden }
$taskInstalled | ConvertTo-Json -Depth 2
