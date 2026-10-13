param([string]$RuntimeRoot = $(if($env:SONA_RUNTIME){$env:SONA_RUNTIME}elseif(Test-Path -LiteralPath 'E:\DevelopmentTools\local-dictation\whisper'){'E:\DevelopmentTools\local-dictation'}else{Join-Path $env:LOCALAPPDATA 'Sona\runtime'}), [switch]$BuildTools)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath($RuntimeRoot)
New-Item -ItemType Directory -Path $taskRoot -Force | Out-Null
function FetchChecked([string]$Url,[string]$Destination,[string]$Hash,[string]$Algorithm='SHA256') {
    if (Test-Path -LiteralPath $Destination) { if ((Get-FileHash -LiteralPath $Destination -Algorithm $Algorithm).Hash.ToLowerInvariant() -ne $Hash) { throw "Existing dependency hash differs: $Destination" }; return }
    New-Item -ItemType Directory -Path (Split-Path $Destination -Parent) -Force | Out-Null
    $taskPart = $Destination + '.part'
    Invoke-WebRequest -Uri $Url -OutFile $taskPart
    if ((Get-FileHash -LiteralPath $taskPart -Algorithm $Algorithm).Hash.ToLowerInvariant() -ne $Hash) { throw "Downloaded dependency hash differs: $Destination" }
    Move-Item -LiteralPath $taskPart -Destination $Destination
}
$taskArchive = Join-Path $taskRoot 'downloads\whisper-b5454-x64.zip'
$taskEngine = Join-Path $taskRoot 'whisper\Release\whisper-cli.exe'
if (!(Test-Path -LiteralPath $taskEngine)) {
    FetchChecked 'https://github.com/ggml-org/whisper.cpp/releases/download/b5454/whisper-bin-x64.zip' $taskArchive '6ba69e3482d7826214f90a6a9c84ca07782aec1e1d0c6a7c30c994fd5d816ccb'
    $taskExtraction = Join-Path $taskRoot ('extract-' + [guid]::NewGuid().ToString('N'))
    Expand-Archive -LiteralPath $taskArchive -DestinationPath $taskExtraction
    New-Item -ItemType Directory -Path (Join-Path $taskRoot 'whisper') -Force | Out-Null
    foreach ($taskPath in @((Join-Path $taskExtraction 'Release'),(Join-Path $taskRoot 'whisper\Release'))) { if (![IO.Path]::GetFullPath($taskPath).StartsWith($taskRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Engine move escapes the selected runtime root.' } }
    Move-Item -LiteralPath (Join-Path $taskExtraction 'Release') -Destination (Join-Path $taskRoot 'whisper\Release')
    Remove-Item -LiteralPath $taskExtraction
}
$taskManifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-manifest.json') | ConvertFrom-Json
foreach ($taskFile in $taskManifest.EngineFiles) {
    $taskPath = Join-Path (Join-Path $taskRoot 'whisper\Release') $taskFile.Name
    if (!(Test-Path -LiteralPath $taskPath) -or (Get-FileHash -LiteralPath $taskPath).Hash.ToLowerInvariant() -ne $taskFile.SHA256) { throw "Pinned engine file differs: $($taskFile.Name)" }
}
FetchChecked 'https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-small-q5_1.bin' (Join-Path $taskRoot 'models\ggml-small-q5_1.bin') 'ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb'
if ($BuildTools -and !(Test-Path -LiteralPath (Join-Path $taskRoot 'sdk\dotnet.exe'))) {
    $taskSdkArchive = Join-Path $taskRoot 'downloads\dotnet-sdk-8.0.425-win-x64.zip'
    FetchChecked 'https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.425/dotnet-sdk-8.0.425-win-x64.zip' $taskSdkArchive 'f0b6f15bf6f1a0507205c0cb102ab99e1dee875c4682c8ed94665be1d580186a06b21455e83b3a01a0ff7f4cd887b67420f2e2fe09ed985534a4cea488ae1af9' 'SHA512'
    Expand-Archive -LiteralPath $taskSdkArchive -DestinationPath (Join-Path $taskRoot 'sdk')
}
if ($BuildTools) {
    $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; $env:DOTNET_NOLOGO='1'; $env:DOTNET_CLI_HOME=Join-Path $taskRoot 'dotnet-home'
    $taskVersion = & (Join-Path $taskRoot 'sdk\dotnet.exe') --version
    if ($taskVersion -ne '8.0.425') { throw 'The build SDK differs from the pinned version.' }
}
Write-Output "Pinned local runtime verified at $taskRoot"
