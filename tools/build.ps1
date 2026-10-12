$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$taskRuntime = 'E:\DevelopmentTools\local-dictation'
$env:DOTNET_CLI_HOME = "$taskRuntime\dotnet-home"
$env:NUGET_PACKAGES = "$taskRuntime\nuget"
$taskRepo = Split-Path $PSScriptRoot -Parent
& "$taskRuntime\sdk\dotnet.exe" build "$taskRepo\src\Murmur.App\Murmur.App.csproj" -c Release --disable-build-servers -p:UseSharedCompilation=false -p:RestorePackagesWithLockFile=true
if ($LASTEXITCODE -ne 0) { throw 'App build failed' }
& "$taskRuntime\sdk\dotnet.exe" run --project "$taskRepo\tests\Murmur.Tests" -c Release --disable-build-servers -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Safety regressions failed' }
& "$taskRuntime\sdk\dotnet.exe" run --project "$taskRepo\tests\Murmur.WindowsTests" -c Release --disable-build-servers -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Windows lifecycle regressions failed' }
& "$taskRuntime\sdk\dotnet.exe" list "$taskRepo\src\Murmur.App\Murmur.App.csproj" package --vulnerable --include-transitive
if ($LASTEXITCODE -ne 0) { throw 'Dependency audit command failed' }
