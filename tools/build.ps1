$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$taskRuntime = 'E:\DevelopmentTools\local-dictation'
$env:DOTNET_CLI_HOME = "$taskRuntime\dotnet-home"
$env:NUGET_PACKAGES = "$taskRuntime\nuget"
$taskRepo = Split-Path $PSScriptRoot -Parent
& "$taskRuntime\sdk\dotnet.exe" build "$taskRepo\src\Sona.App\Sona.App.csproj" -c Release --disable-build-servers -p:UseSharedCompilation=false -p:RestorePackagesWithLockFile=true
if ($LASTEXITCODE -ne 0) { throw 'App build failed' }
& "$taskRuntime\sdk\dotnet.exe" build "$taskRepo\src\Sona.Agent\Sona.Agent.csproj" -c Release --disable-build-servers -p:UseSharedCompilation=false -p:RestorePackagesWithLockFile=true
if ($LASTEXITCODE -ne 0) { throw 'Agent interface build failed' }
& "$taskRuntime\sdk\dotnet.exe" run --project "$taskRepo\tests\Sona.Tests" -c Release --disable-build-servers -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Safety regressions failed' }
& "$taskRuntime\sdk\dotnet.exe" run --project "$taskRepo\tests\Sona.WindowsTests" -c Release --disable-build-servers -p:UseSharedCompilation=false
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
& (Join-Path $taskRepo 'tools\session-readiness-checks.ps1')
if (!$?) { throw 'Updater status regressions failed' }
& (Join-Path $taskRepo 'tools\owned-startup-checks.ps1')
if (!$?) { throw 'Owned startup readiness regressions failed' }
& "$taskRuntime\sdk\dotnet.exe" list "$taskRepo\src\Sona.App\Sona.App.csproj" package --vulnerable --include-transitive
if ($LASTEXITCODE -ne 0) { throw 'Dependency audit command failed' }
