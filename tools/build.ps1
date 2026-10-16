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
$taskHistoryEvidence = Join-Path ([IO.Path]::GetTempPath()) ('sona-history-controls-' + [Guid]::NewGuid().ToString('N'))
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --history-controls-fixture $taskHistoryEvidence
if ($LASTEXITCODE -ne 0) { throw 'History control regressions failed' }
$taskHistoryOffEvidence = Join-Path ([IO.Path]::GetTempPath()) ('sona-history-controls-off-' + [Guid]::NewGuid().ToString('N'))
& "$taskRepo\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe" --history-controls-no-retention-fixture $taskHistoryOffEvidence
if ($LASTEXITCODE -ne 0) { throw 'History controls with retention off failed' }
& "$taskRuntime\sdk\dotnet.exe" list "$taskRepo\src\Sona.App\Sona.App.csproj" package --vulnerable --include-transitive
if ($LASTEXITCODE -ne 0) { throw 'Dependency audit command failed' }
