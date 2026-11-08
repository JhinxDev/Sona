# Builds the Sona per-user Windows installer.
#
# The payload is a self-contained win-x64 publish of the app plus the CLI, so the
# installed app needs no .NET runtime. The pinned Whisper CPU engine is bundled as
# an optional component; model weights are downloaded in-app by design.
#
# Usage:
#   pwsh -File tools/installer/build-installer.ps1
#   pwsh -File tools/installer/build-installer.ps1 -Rehearsal
#
# -Rehearsal builds an isolated test installer, installs it, verifies the
# installed app/receipt/engine, uninstalls it, then builds the real artifact.
param(
    [switch]$Rehearsal,
    [switch]$SkipPayload,
    [string]$OutputRoot,
    [string]$IsccPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$runtimeRoot = if ($env:SONA_RUNTIME) { $env:SONA_RUNTIME } else { 'E:\DevelopmentTools\local-dictation' }
$sdk = Join-Path $runtimeRoot 'sdk'
if (Test-Path (Join-Path $sdk 'dotnet.exe')) {
    $env:DOTNET_ROOT = $sdk
    $env:DOTNET_CLI_HOME = Join-Path $runtimeRoot 'dotnet-home'
    $env:NUGET_PACKAGES = 'E:\DevelopmentCaches\nuget'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $dotnet = Join-Path $sdk 'dotnet.exe'
} else {
    $dotnet = 'dotnet'
}

$versionMatch = Select-String -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Pattern '<Version>([0-9]+\.[0-9]+\.[0-9]+)</Version>'
if (-not $versionMatch) { throw 'Could not read the app version from Directory.Build.props.' }
$version = $versionMatch.Matches[0].Groups[1].Value
$head = (git -C $repoRoot rev-parse HEAD).Trim()
if ($head -notmatch '^[a-f0-9]{40}$') { throw 'Could not resolve the source head.' }

if (-not $OutputRoot) { $OutputRoot = Join-Path $repoRoot 'artifacts\installer' }
$payload = Join-Path $OutputRoot 'payload'
$engineRoot = Join-Path $OutputRoot 'payload-engine'
$engineSource = Join-Path $engineRoot 'whisper\Release'
$iss = Join-Path $repoRoot 'installer\Sona.iss'

if (-not $IsccPath) {
    $candidates = @(
        'E:\DevelopmentTools\SonaInstaller\InnoSetup7\ISCC.exe',
        'C:\Program Files\Inno Setup 7\ISCC.exe',
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe'
    )
    $IsccPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $IsccPath -or -not (Test-Path $IsccPath)) { throw 'Inno Setup compiler (ISCC.exe) was not found. Pass -IsccPath.' }

# The engine is pinned to the same whisper.cpp release and hashes as
# tools/setup-runtime.ps1 / tools/runtime-manifest.json.
$engineZipUrl = 'https://github.com/ggml-org/whisper.cpp/releases/download/b5454/whisper-bin-x64.zip'
$engineZipSha = '6ba69e3482d7826214f90a6a9c84ca07782aec1e1d0c6a7c30c994fd5d816ccb'
$cache = Join-Path $OutputRoot 'cache'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
$engineZip = Join-Path $cache 'whisper-bin-x64.zip'

if (-not $SkipPayload) {
    Write-Output "Publishing Sona $version ($($head.Substring(0,8))) payload..."
    if (Test-Path $payload) { Remove-Item -LiteralPath $payload -Recurse -Force }
    & $dotnet publish (Join-Path $repoRoot 'src\Sona.App\Sona.App.csproj') -c Release -r win-x64 --self-contained true -o $payload --disable-build-servers -p:UseSharedCompilation=false | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'App publish failed.' }
    # Self-contained CLI into the same folder: it reuses the app's runtime files
    # and only adds the agent binaries.
    & $dotnet publish (Join-Path $repoRoot 'src\Sona.Agent\Sona.Agent.csproj') -c Release -r win-x64 --self-contained true -o $payload --disable-build-servers -p:UseSharedCompilation=false | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Agent publish failed.' }
    # A win-x64 publish adds runtime entries to the checked-in packages.lock.json
    # files. Restore the normal source lock state so the checkout stays clean.
    foreach ($project in @('src\Sona.App\Sona.App.csproj', 'src\Sona.Agent\Sona.Agent.csproj')) {
        & $dotnet restore (Join-Path $repoRoot $project) | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Lock-file normalization restore failed for $project." }
    }
}

if (-not (Test-Path (Join-Path $payload 'Sona.exe'))) { throw "Payload is missing Sona.exe at $payload." }
if (-not (Test-Path (Join-Path $payload 'sona-agent.exe'))) { throw "Payload is missing sona-agent.exe at $payload." }

# Prepare the pinned engine exactly once per output root.
if (-not (Test-Path $engineZip)) {
    Write-Output 'Downloading pinned Whisper CPU engine...'
    Invoke-WebRequest -Uri $engineZipUrl -OutFile $engineZip -UseBasicParsing -TimeoutSec 300
}
$actualZip = (Get-FileHash -LiteralPath $engineZip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualZip -ne $engineZipSha) { throw "Whisper engine archive hash mismatch: $actualZip" }
if (-not (Test-Path $engineSource)) {
    $temp = Join-Path $cache ('extract-' + [Guid]::NewGuid().ToString('N'))
    Expand-Archive -LiteralPath $engineZip -DestinationPath $temp -Force
    if (Test-Path $engineRoot) { Remove-Item -LiteralPath $engineRoot -Recurse -Force }
    New-Item -ItemType Directory -Path (Join-Path $engineRoot 'whisper') -Force | Out-Null
    Move-Item -LiteralPath (Join-Path $temp 'Release') -Destination $engineSource
    Remove-Item -LiteralPath $temp -Recurse -Force
}
$manifest = Get-Content -LiteralPath (Join-Path $repoRoot 'tools\runtime-manifest.json') -Raw | ConvertFrom-Json
$mismatches = 0
foreach ($file in $manifest.EngineFiles) {
    $path = Join-Path $engineSource $file.Name
    if (-not (Test-Path $path)) { $mismatches++; continue }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.SHA256) { $mismatches++ }
}
if ($mismatches -ne 0) { throw "Bundled engine does not match the pinned runtime manifest ($mismatches mismatches)." }

$exeSha = (Get-FileHash -LiteralPath (Join-Path $payload 'Sona.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

function Invoke-Compile([string[]]$defines, [string]$logName, [string]$expectedInstaller) {
    $arguments = @('/Q') + $defines + @($iss)
    & $IsccPath @arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed ($logName)." }
    if (-not (Test-Path $expectedInstaller)) { throw "Inno Setup did not produce $expectedInstaller." }
    return $expectedInstaller
}

$baseDefines = @(
    "/DMyAppVersion=$version",
    "/DMySourceHead=$head",
    "/DMyExeSHA256=$exeSha",
    "/DMyRepoRoot=$repoRoot",
    "/DMySourceDir=$payload",
    "/DMyEngineSourceDir=$engineSource",
    "/DMyOutputDir=$OutputRoot"
)

if ($Rehearsal) {
    Write-Output 'Building isolated rehearsal installer...'
    $testRoot = Join-Path $env:TEMP ('sona-installer-rehearsal-' + [Guid]::NewGuid().ToString('N'))
    $testInstall = Join-Path $testRoot 'app'
    $testEngine = Join-Path $testRoot 'runtime\whisper\Release'
    $rehearsalDefines = $baseDefines + @(
        '/DRehearsal=1',
        "/DMyDefaultDir=$testInstall",
        "/DMyEngineTargetDir=$testEngine"
    )
    $testInstaller = Invoke-Compile $rehearsalDefines 'rehearsal' (Join-Path $OutputRoot 'Sona-Setup-Test.exe')
    Write-Output "Rehearsal installer: $testInstaller"
    try {
        & pwsh -NoProfile -File (Join-Path $repoRoot 'tools\installer\rehearse-installer.ps1') `
            -InstallerPath $testInstaller -InstallDir $testInstall -EngineDir $testEngine -ReportPath (Join-Path $OutputRoot 'rehearsal-report.json') -ExpectedVersion $version | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'Installer rehearsal failed.' }
    } finally {
        if (Test-Path $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue }
        $uninstallRoot = Join-Path $env:TEMP '.sona-installer-uninstall-state.json'
        if (Test-Path $uninstallRoot) { Remove-Item -LiteralPath $uninstallRoot -Force -ErrorAction SilentlyContinue }
    }
}

Write-Output 'Building final installer...'
$final = Invoke-Compile $baseDefines 'final' (Join-Path $OutputRoot "Sona-Setup-$version.exe")
$finalSha = (Get-FileHash -LiteralPath $final -Algorithm SHA256).Hash.ToLowerInvariant()
$finalMb = [math]::Round((Get-Item -LiteralPath $final).Length / 1MB, 1)
$buildInfo = [ordered]@{
    Product = 'Sona'
    Version = $version
    SourceHead = $head
    PayloadExeSHA256 = $exeSha
    Installer = (Split-Path -Leaf $final)
    InstallerSHA256 = $finalSha
    InstallerBytes = (Get-Item -LiteralPath $final).Length
    BuiltAt = (Get-Date -Format o)
    EnginePinned = 'whisper.cpp b5454 CPU'
}
$buildInfo | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputRoot 'installer-build-info.json') -Encoding utf8
Write-Output "Final installer: $final"
Write-Output "Size: $finalMb MB"
Write-Output "SHA256: $finalSha"
