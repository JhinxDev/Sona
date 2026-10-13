$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; $env:DOTNET_NOLOGO='1'
$taskRepo = Split-Path $PSScriptRoot -Parent
$taskRuntime='E:\DevelopmentTools\local-dictation'
$env:DOTNET_CLI_HOME=Join-Path $taskRuntime 'dotnet-home'; $env:NUGET_PACKAGES=Join-Path $taskRuntime 'nuget'
$taskHead = git -C $taskRepo rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Commit the reviewed source before preparing its install package.' }
if (git -C $taskRepo status --porcelain) { throw 'The source must be clean for a source-identified release package.' }
$taskVersion = ([xml](Get-Content -LiteralPath (Join-Path $taskRepo 'src\Murmur.App\Murmur.App.csproj'))).Project.PropertyGroup.Version
if ($taskVersion -notmatch '^0\.[0-9]+\.[0-9]+$') { throw 'Unrecognized private candidate version.' }
$taskPackage = Join-Path $taskRepo ('artifacts\' + $taskVersion + '-' + $taskHead.Substring(0,8))
if (Test-Path -LiteralPath $taskPackage) { throw 'This candidate package already exists; inspect its receipt instead of overwriting it.' }
& (Join-Path $taskRuntime 'sdk\dotnet.exe') publish (Join-Path $taskRepo 'src\Murmur.App\Murmur.App.csproj') -c Release --self-contained false -o $taskPackage --disable-build-servers -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Native publish failed.' }
& (Join-Path $taskRuntime 'sdk\dotnet.exe') publish (Join-Path $taskRepo 'src\Sona.Agent\Sona.Agent.csproj') -c Release --self-contained false -o $taskPackage --disable-build-servers -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Agent interface publish failed.' }
Copy-Item -LiteralPath (Join-Path $taskRepo 'LICENSE') -Destination $taskPackage
Copy-Item -LiteralPath (Join-Path $taskRepo 'THIRD-PARTY-NOTICES.md') -Destination $taskPackage
Copy-Item -LiteralPath (Join-Path $taskRepo 'licenses') -Destination $taskPackage -Recurse
$taskFiles = @(Get-ChildItem -LiteralPath $taskPackage -File -Recurse | ForEach-Object { @{Path=[IO.Path]::GetRelativePath($taskPackage,$_.FullName); Bytes=$_.Length; SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()} })
@{Product='Sona'; Version=$taskVersion; SourceHead=$taskHead; Files=$taskFiles} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskPackage 'package.json')
Write-Output $taskPackage
