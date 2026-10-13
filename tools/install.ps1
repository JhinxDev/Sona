param([Parameter(Mandatory)][string]$PackageDirectory, [switch]$Launch)
$ErrorActionPreference='Stop'
$taskPackage=[IO.Path]::GetFullPath($PackageDirectory)
$taskReceipt=Get-Content -LiteralPath (Join-Path $taskPackage 'package.json') | ConvertFrom-Json
if ($taskReceipt.Version -notmatch '^0\.1\.[0-9]+$' -or $taskReceipt.SourceHead -notmatch '^[a-f0-9]{40}$') { throw 'The package receipt is not a recognized candidate.' }
if (Get-Process -Name Murmur,Sona -ErrorAction SilentlyContinue) { throw 'Quit the existing dictation instance before installing this candidate.' }
$taskInstall = Join-Path (Join-Path $env:LOCALAPPDATA 'Programs\Sona') ($taskReceipt.Version + '-' + $taskReceipt.SourceHead.Substring(0,8))
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
$taskShell=New-Object -ComObject WScript.Shell
if (Test-Path -LiteralPath $taskMenu) { $taskExisting=$taskShell.CreateShortcut($taskMenu); if (!$taskExisting.TargetPath.StartsWith((Join-Path $env:LOCALAPPDATA 'Programs\Sona') + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'An unrelated Sona shortcut was preserved. Choose a different shortcut name.' } }
$taskShortcut=$taskShell.CreateShortcut($taskMenu); $taskShortcut.TargetPath=Join-Path $taskInstall 'Sona.exe'; $taskShortcut.WorkingDirectory=$taskInstall; $taskShortcut.IconLocation=(Join-Path $taskInstall 'Sona.exe') + ',0'; $taskShortcut.Description='Intentional local dictation for an unsent draft'; $taskShortcut.Save()
# Remove only the legacy shortcut verified by its own installed receipt. Keep
# versioned recovery binaries and the existing preferences directory intact.
$taskLegacyMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Murmur.lnk'
if (Test-Path -LiteralPath $taskLegacyMenu) {
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
if ($Launch) { Start-Process -FilePath (Join-Path $taskInstall 'Sona.exe') -ArgumentList '--start-hidden' -WindowStyle Hidden }
$taskInstalled | ConvertTo-Json -Depth 2
