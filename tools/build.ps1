$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$taskRuntime = if ($env:SONA_RUNTIME) { $env:SONA_RUNTIME } else { 'E:\DevelopmentTools\local-dictation' }
$sdkDotnet = Join-Path $taskRuntime 'sdk\dotnet.exe'
if (Test-Path -LiteralPath $sdkDotnet) {
    if (-not $env:DOTNET_ROOT) { $env:DOTNET_ROOT = Join-Path $taskRuntime 'sdk' }
    if (-not $env:DOTNET_CLI_HOME) { $env:DOTNET_CLI_HOME = Join-Path $taskRuntime 'dotnet-home' }
    if (-not $env:NUGET_PACKAGES) { $env:NUGET_PACKAGES = Join-Path $taskRuntime 'nuget' }
    $dotnet = $sdkDotnet
} else {
    $dotnet = 'dotnet'
}
$taskRepo = Split-Path $PSScriptRoot -Parent
& $dotnet build "$taskRepo\src\Sona.App\Sona.App.csproj" -c Release --disable-build-servers -p:UseSharedCompilation=false -p:RestorePackagesWithLockFile=true
if ($LASTEXITCODE -ne 0) { throw 'App build failed' }
& $dotnet build "$taskRepo\src\Sona.Agent\Sona.Agent.csproj" -c Release --disable-build-servers -p:UseSharedCompilation=false -p:RestorePackagesWithLockFile=true
if ($LASTEXITCODE -ne 0) { throw 'Agent interface build failed' }
& $dotnet run --project "$taskRepo\tests\Sona.Tests" -c Release --disable-build-servers -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Safety regressions failed' }
& $dotnet run --project "$taskRepo\tests\Sona.WindowsTests" -c Release --disable-build-servers -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Windows lifecycle regressions failed' }
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --capture-policy-checks
if ($LASTEXITCODE -ne 0) { throw 'Capture policy regressions failed' }
$taskCaptureEvidence = Join-Path ([IO.Path]::GetTempPath()) ('sona-capture-status-' + [Guid]::NewGuid().ToString('N'))
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --capture-status-fixture $taskCaptureEvidence
if ($LASTEXITCODE -ne 0) { throw 'Capture status regressions failed' }
foreach ($taskFixture in @('capture-readiness', 'retained-notice')) {
    $taskEvidence = Join-Path ([IO.Path]::GetTempPath()) ('sona-' + $taskFixture + '-' + [Guid]::NewGuid().ToString('N'))
    $taskInfo = [Diagnostics.ProcessStartInfo]::new("$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe")
    $taskInfo.UseShellExecute = $false; $taskInfo.CreateNoWindow = $true
    $taskInfo.Arguments = '--' + $taskFixture + '-fixture "' + $taskEvidence + '"'
    $taskProcess = [Diagnostics.Process]::Start($taskInfo)
    if (!$taskProcess.WaitForExit(45000)) { $taskProcess.Kill(); throw ($taskFixture + ' fixture exceeded its bound') }
    if ($taskProcess.ExitCode -ne 0) { throw ($taskFixture + ' regressions failed') }
    $taskProcess.Dispose()
}
$taskHistoryEvidence = Join-Path ([IO.Path]::GetTempPath()) ('sona-history-controls-' + [Guid]::NewGuid().ToString('N'))
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --history-controls-fixture $taskHistoryEvidence
if ($LASTEXITCODE -ne 0) { throw 'History control regressions failed' }
$taskHistoryOffEvidence = Join-Path ([IO.Path]::GetTempPath()) ('sona-history-controls-off-' + [Guid]::NewGuid().ToString('N'))
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --history-controls-no-retention-fixture $taskHistoryOffEvidence
if ($LASTEXITCODE -ne 0) { throw 'History controls with retention off failed' }
$taskProtectionEvidence = Join-Path ([IO.Path]::GetTempPath()) ('sona-protection-controls-' + [Guid]::NewGuid().ToString('N'))
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --protection-controls-fixture $taskProtectionEvidence
if ($LASTEXITCODE -ne 0) { throw 'Isolated protection switch regressions failed' }
$taskSpeedEvidence = Join-Path ([IO.Path]::GetTempPath()) ('sona-responsiveness-' + [Guid]::NewGuid().ToString('N'))
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --responsiveness-fixture $taskSpeedEvidence
if ($LASTEXITCODE -ne 0) { throw 'Isolated responsiveness regressions failed' }
$taskEnterEvidence = Join-Path ([IO.Path]::GetTempPath()) ('sona-enter-delivery-' + [Guid]::NewGuid().ToString('N'))
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --enter-delivery-fixture $taskEnterEvidence
if ($LASTEXITCODE -ne 0) { throw 'Isolated Enter delivery regressions failed' }
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --completed-paste-fixture
if ($LASTEXITCODE -ne 0) { throw 'Completed-paste verification regressions failed' }
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --owned-clipboard-checks
if ($LASTEXITCODE -ne 0) { throw 'Clipboard ownership regressions failed' }
$taskPersonalEvidence = Join-Path ([IO.Path]::GetTempPath()) ('sona-personalization-' + [Guid]::NewGuid().ToString('N'))
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --personalization-fixture $taskPersonalEvidence
if ($LASTEXITCODE -ne 0) { throw 'Per-app delivery regressions failed' }
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --personalization-reopen $taskPersonalEvidence
if ($LASTEXITCODE -ne 0) { throw 'Saved draft reopen regressions failed' }
$taskPolishEvidence = Join-Path ([IO.Path]::GetTempPath()) ('sona-professional-polish-' + [Guid]::NewGuid().ToString('N'))
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --professional-polish-fixture $taskPolishEvidence
if ($LASTEXITCODE -ne 0) { throw 'Professional polish fixture failed' }
$taskSetupEvidence = Join-Path ([IO.Path]::GetTempPath()) ('sona-professional-setup-' + [Guid]::NewGuid().ToString('N'))
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --professional-polish-setup-fixture $taskSetupEvidence
if ($LASTEXITCODE -ne 0) { throw 'Setup guidance fixture failed' }
$taskLoadingEvidence = Join-Path ([IO.Path]::GetTempPath()) ('sona-model-loading-' + [Guid]::NewGuid().ToString('N'))
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --model-loading-fixture $taskLoadingEvidence
if ($LASTEXITCODE -ne 0) { throw 'Model loading feedback regressions failed' }
$taskLiveEvidence = Join-Path ([IO.Path]::GetTempPath()) ('sona-live-draft-' + [Guid]::NewGuid().ToString('N'))
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --live-draft-fixture $taskLiveEvidence
if ($LASTEXITCODE -ne 0) { throw 'Live preview completion regressions failed' }
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --active-app-state-fixture
if ($LASTEXITCODE -ne 0) { throw 'Active app state regressions failed' }
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --theme-material-fixture
if ($LASTEXITCODE -ne 0) { throw 'Theme material regressions failed' }
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --presentation-fixture
if ($LASTEXITCODE -ne 0) { throw 'Presentation regressions failed' }
& (Join-Path $taskRepo 'tools\install-staging-checks.ps1') -OutputRoot (Join-Path ([IO.Path]::GetTempPath()) ('sona-upgrade-rehearsal-' + [Guid]::NewGuid().ToString('N')))
if (!$?) { throw 'Installer staging regressions failed' }
& (Join-Path $taskRepo 'tools\session-readiness-checks.ps1')
if (!$?) { throw 'Updater status regressions failed' }
& (Join-Path $taskRepo 'tools\owned-startup-checks.ps1')
if (!$?) { throw 'Owned startup readiness regressions failed' }
& $dotnet list "$taskRepo\src\Sona.App\Sona.App.csproj" package --vulnerable --include-transitive
if ($LASTEXITCODE -ne 0) { throw 'Dependency audit command failed' }
