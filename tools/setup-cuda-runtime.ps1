param([string]$RuntimeRoot = 'E:\DevelopmentTools\local-dictation')
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath($RuntimeRoot)
if ($taskRoot -eq [IO.Path]::GetPathRoot($taskRoot)) { throw 'Choose a dedicated runtime directory.' }
$taskManifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'cuda-manifest.json') | ConvertFrom-Json
New-Item -ItemType Directory -Path (Join-Path $taskRoot 'downloads') -Force | Out-Null
function FetchPinned($Source, [string]$Destination) {
    if (Test-Path -LiteralPath $Destination) {
        if ((Get-Item -LiteralPath $Destination).Length -ne $Source.Bytes -or (Get-FileHash -LiteralPath $Destination).Hash.ToLowerInvariant() -ne $Source.SHA256) { throw "Preserving different existing dependency: $Destination" }
        return
    }
    New-Item -ItemType Directory -Path (Split-Path $Destination -Parent) -Force | Out-Null
    $taskPart = $Destination + '.part'
    if (Test-Path -LiteralPath $taskPart) { throw "Preserving incomplete acquisition: $taskPart" }
    $taskClient = [Net.Http.HttpClient]::new(); $taskClient.Timeout = [TimeSpan]::FromSeconds(240)
    try {
        $taskResponse = $taskClient.GetAsync($Source.URL, [Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        $taskResponse.EnsureSuccessStatusCode() | Out-Null
        if ($taskResponse.Content.Headers.ContentLength -ne $Source.Bytes) { throw 'Pinned dependency length differs.' }
        $taskStream = [IO.File]::Create($taskPart)
        try { $taskResponse.Content.CopyToAsync($taskStream).GetAwaiter().GetResult() | Out-Null } finally { $taskStream.Dispose() }
    } finally { $taskClient.Dispose() }
    if ((Get-FileHash -LiteralPath $taskPart).Hash.ToLowerInvariant() -ne $Source.SHA256) { throw 'Pinned dependency digest differs.' }
    Move-Item -LiteralPath $taskPart -Destination $Destination
}
$taskEngine = Join-Path $taskRoot 'whisper-cuda\Release'
New-Item -ItemType Directory -Path $taskEngine -Force | Out-Null
foreach ($taskArchive in $taskManifest.Archives) {
    $taskArchivePath = Join-Path (Join-Path $taskRoot 'downloads') $taskArchive.Name
    FetchPinned $taskArchive $taskArchivePath
    $taskZip = [IO.Compression.ZipFile]::OpenRead($taskArchivePath)
    try {
        foreach ($taskFile in $taskManifest.EngineFiles | Where-Object { $_.Archive -eq $taskArchive.Id }) {
            if ($taskFile.Name -notmatch '^[A-Za-z0-9_.-]+$') { throw 'Unexpected runtime filename.' }
            $taskDestination = [IO.Path]::GetFullPath((Join-Path $taskEngine $taskFile.Name))
            if (!$taskDestination.StartsWith($taskRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Runtime member escapes its dedicated root.' }
            if (Test-Path -LiteralPath $taskDestination) {
                if ((Get-FileHash -LiteralPath $taskDestination).Hash.ToLowerInvariant() -ne $taskFile.SHA256) { throw "Preserving different runtime member: $($taskFile.Name)" }
                continue
            }
            $taskMember = $taskZip.GetEntry($taskFile.Member)
            if (!$taskMember -or $taskMember.Length -ne $taskFile.Bytes) { throw 'Pinned archive member differs.' }
            $taskPart = $taskDestination + '.part'
            if (Test-Path -LiteralPath $taskPart) { throw 'Preserving incomplete extracted member.' }
            [IO.Compression.ZipFileExtensions]::ExtractToFile($taskMember, $taskPart, $false)
            if ((Get-FileHash -LiteralPath $taskPart).Hash.ToLowerInvariant() -ne $taskFile.SHA256) { throw 'Extracted member digest differs.' }
            Move-Item -LiteralPath $taskPart -Destination $taskDestination
        }
    } finally { $taskZip.Dispose() }
}
FetchPinned $taskManifest.Model (Join-Path (Join-Path $taskRoot 'models') $taskManifest.Model.Name)
$taskReceipt = @{Release=$taskManifest.Release; EngineFiles=$taskManifest.EngineFiles; Model=$taskManifest.Model; DriverChanged=$false; GlobalPathChanged=$false; ModelStarted=$false; VerifiedAt=[DateTimeOffset]::Now.ToString('o')}
$taskReceipt | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskRoot 'cuda-installation.json') -Encoding utf8
Write-Output "Pinned local GPU runtime verified at $taskRoot"
