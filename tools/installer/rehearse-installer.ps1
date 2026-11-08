# Installs the isolated rehearsal installer, verifies the result, then uninstalls
# it. Called by build-installer.ps1 -Rehearsal. Never touches the owner's Sona
# installation, profile or microphone.
param(
    [Parameter(Mandatory)] [string]$InstallerPath,
    [Parameter(Mandatory)] [string]$InstallDir,
    [Parameter(Mandatory)] [string]$EngineDir,
    [Parameter(Mandatory)] [string]$ReportPath,
    [Parameter(Mandatory)] [string]$ExpectedVersion
)

$ErrorActionPreference = 'Stop'
$checks = [System.Collections.Generic.List[string]]::new()
$failures = [System.Collections.Generic.List[string]]::new()
function Check([bool]$ok, [string]$name) { if ($ok) { $checks.Add($name) } else { $failures.Add($name) } }

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$exe = Join-Path $InstallDir 'Sona.exe'
$agent = Join-Path $InstallDir 'sona-agent.exe'
$receiptPath = Join-Path $InstallDir 'installation.json'
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Sona Installer Test.lnk'
$uninstallRoot = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall'
$profileRoot = Join-Path $env:TEMP ('sona-installer-profile-' + [Guid]::NewGuid().ToString('N'))
$report = [ordered]@{
    Timestamp = (Get-Date -Format o)
    InstallerPath = $InstallerPath
    InstallerSHA256 = (Get-FileHash -LiteralPath $InstallerPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Checks = $checks
    Failures = $failures
    MicrophoneOpened = $false
    GlobalInputSent = $false
    OwnerInstallTouched = $false
}

try {
    New-Item -ItemType Directory -Path $profileRoot -Force | Out-Null
    Write-Output "Installing rehearsal artifact into $InstallDir ..."
    $install = Start-Process -FilePath $InstallerPath -Wait -PassThru -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/TASKS="engine"', "/DIR=$InstallDir"
    )
    Check ($install.ExitCode -eq 0) 'Silent per-user installation exits successfully'

    Check (Test-Path -LiteralPath $exe) 'Sona.exe is installed'
    Check (Test-Path -LiteralPath $agent) 'The sona-agent CLI is installed beside the app'
    Check (Test-Path -LiteralPath (Join-Path $InstallDir 'THIRD-PARTY-NOTICES.md')) 'Third-party notices are installed'
    Check (Test-Path -LiteralPath (Join-Path $InstallDir 'licenses\whisper.cpp.txt')) 'Upstream licenses are installed'

    $receipt = $null
    if (Test-Path -LiteralPath $receiptPath) { $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json }
    $exeHash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
    Check ($null -ne $receipt) 'Installation receipt is written'
    if ($receipt) {
        Check ($receipt.Version -eq $ExpectedVersion) 'Receipt version matches the packaged version'
        Check ($receipt.SourceHead -match '^[a-f0-9]{40}$') 'Receipt carries the source head'
        Check ($receipt.Path -eq $exe -and $receipt.SHA256 -eq $exeHash) 'Receipt Path and SHA256 match the installed executable'
    }

    Check (Test-Path -LiteralPath (Join-Path $EngineDir 'whisper-server.exe')) 'Pinned Whisper CPU engine is installed to the runtime root'
    $manifest = Get-Content -LiteralPath (Join-Path $repoRoot 'tools\runtime-manifest.json') -Raw | ConvertFrom-Json
    $engineMismatches = 0
    foreach ($file in $manifest.EngineFiles) {
        $filePath = Join-Path $EngineDir $file.Name
        if (-not (Test-Path -LiteralPath $filePath) -or
            (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.SHA256) { $engineMismatches++ }
    }
    Check ($engineMismatches -eq 0) 'Installed engine files match the pinned runtime manifest'

    Check (Test-Path -LiteralPath $startMenu) 'Start Menu shortcut is created'
    Check ($null -ne (Get-ItemProperty -LiteralPath (Join-Path $InstallDir 'Sona.exe') -ErrorAction SilentlyContinue)) 'Installed executable has usable version metadata'

    # Run the installed binaries without a microphone or input. The isolated
    # profile keeps the owner's data out of reach.
    $env:SONA_DATA = $profileRoot
    $app = Start-Process -FilePath $exe -PassThru -ArgumentList '--test-ui'
    Start-Sleep -Milliseconds 2500
    Check (-not $app.HasExited) 'Installed self-contained app starts and stays running'
    $agentOutput = ''
    try { $agentOutput = (& $agent status 2>&1 | Select-Object -First 1) } catch { $agentOutput = '' }
    Check ($agentOutput -match '^\s*\{') 'Installed CLI starts without a separately installed .NET runtime'
    if (-not $app.HasExited) {
        $null = $app.CloseMainWindow()
        if (-not $app.WaitForExit(10000)) { Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue; $null = $app.WaitForExit(5000) }
    }
    Remove-Item Env:\SONA_DATA -ErrorAction SilentlyContinue
    Check ($app.HasExited) 'Installed app exits without leaving a process behind'

    Set-Content -LiteralPath (Join-Path $profileRoot 'uninstall-marker.txt') -Value 'survives uninstall' -Encoding utf8
    $uninstaller = Join-Path $InstallDir 'unins000.exe'
    Check (Test-Path -LiteralPath $uninstaller) 'Uninstaller is installed'
    if (Test-Path -LiteralPath $uninstaller) {
        $uninstall = Start-Process -FilePath $uninstaller -Wait -PassThru -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
        Check ($uninstall.ExitCode -eq 0) 'Silent uninstall exits successfully'
    }
    Check (-not (Test-Path -LiteralPath $exe)) 'Uninstall removes the installed app files'
    Check (-not (Test-Path -LiteralPath $startMenu)) 'Uninstall removes the Start Menu shortcut'
    Check (-not (Get-ChildItem -LiteralPath $uninstallRoot -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -like '*4D2B8E6A*' })) 'Uninstall removes its uninstall registration'
    Check (Test-Path -LiteralPath (Join-Path $profileRoot 'uninstall-marker.txt')) 'Uninstall preserves user profile data outside the app folder'
    Check (-not (Test-Path -LiteralPath (Join-Path $EngineDir 'whisper-server.exe'))) 'Uninstall removes the installer-owned engine it placed in the runtime folder'
}
catch {
    $failures.Add("Rehearsal threw: $($_.Exception.Message)")
}
finally {
    Remove-Item Env:\SONA_DATA -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $profileRoot) { Remove-Item -LiteralPath $profileRoot -Recurse -Force -ErrorAction SilentlyContinue }
    New-Item -ItemType Directory -Path (Split-Path -Parent $ReportPath) -Force | Out-Null
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ReportPath -Encoding utf8
    Write-Output "Rehearsal checks passed: $($checks.Count); failures: $($failures.Count)"
    foreach ($failure in $failures) { Write-Output "FAIL: $failure" }
    if ($failures.Count -ne 0) { exit 1 }
}
