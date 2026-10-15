param([Parameter(Mandatory)][string]$OutputDirectory, [switch]$DownloadTiny)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path $PSScriptRoot -Parent
$taskExe=Join-Path $taskRepo 'src\Sona.App\bin\Release\net8.0-windows\Sona.exe'
$taskAgent=Join-Path $taskRepo 'src\Sona.Agent\bin\Release\net8.0-windows\sona-agent.exe'
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a fresh evidence directory.'}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$taskPriorData=$env:SONA_DATA
$env:SONA_DATA=Join-Path $OutputDirectory 'data'
$taskChecks=[Collections.Generic.List[string]]::new()
function Verify([bool]$Condition,[string]$Name){if(-not $Condition){throw $Name};$taskChecks.Add($Name)}
function Agent([string[]]$Arguments){$taskOutput=& $taskAgent @Arguments; $taskResult=($taskOutput -join "`n") | ConvertFrom-Json; return $taskResult}
$taskProcess=Start-Process -FilePath $taskExe -ArgumentList '--test-ui' -WindowStyle Hidden -PassThru
try
{
    $taskStatus=$null
    for($taskTry=0;$taskTry -lt 40;$taskTry++){$taskStatus=Agent @('status');if($taskStatus.ok){break};Start-Sleep -Milliseconds 100}
    Verify ($taskStatus.ok -and $taskStatus.data.state -eq 'idle' -and -not $taskStatus.data.microphoneControl) 'Local agent discovers isolated idle app without microphone controls'
    $taskInitialSettings=Agent @('settings','get')
    Verify ($taskInitialSettings.data.pasteCompletedDraft -and -not $taskInitialSettings.data.enterToSend) 'Whole-draft paste defaults on while Enter submission requires opt-in'
    $taskPersonalization=Agent @('settings','patch','{"pasteCompletedDraft":false,"enterToSend":true,"liveTranscription":true,"soundEffects":false}')
    Verify ($taskPersonalization.ok -and -not $taskPersonalization.data.pasteCompletedDraft -and $taskPersonalization.data.enterToSend -and $taskPersonalization.data.liveTranscription -and -not $taskPersonalization.data.soundEffects) 'Dictation and sound preferences round trip without starting microphone or sending input'
    $taskModels=Agent @('models','list')
    Verify ($taskModels.ok -and $taskModels.data.Count -eq 7 -and @($taskModels.data | Where-Object id -eq 'small')[0].installed) 'Agent lists seven local choices and actual installed state'
    $taskParakeetInfo=Agent @('models','info','parakeet-v3')
    Verify ($taskParakeetInfo.ok -and $taskParakeetInfo.data.license-eq'CC-BY-4.0' -and $taskParakeetInfo.data.revision-eq'541d1f99c6b0c3cd0b11a95167540bb8edefd82b') 'Agent publishes provider-specific Parakeet license and immutable artifact revision'
    $taskParakeetPatch=Agent @('settings','patch','{"model":"parakeet-v3","theme":"amethyst","copyToClipboard":true}')
    Verify ($taskParakeetPatch.ok -and $taskParakeetPatch.data.model-eq'parakeet-v3' -and $taskParakeetPatch.data.theme-eq'amethyst' -and $taskParakeetPatch.data.copyToClipboard) 'Parakeet selection uses the validated agent settings path with saved theme and clipboard backup'
    $taskPatch=Agent @('settings','patch','{"language":"fr","engine":"FastCpu","model":"small","autoInsert":false}')
    Verify ($taskPatch.ok -and $taskPatch.data.language -eq 'fr' -and -not $taskPatch.data.autoInsert) 'Validated patch changes supported local settings'
    $taskSaved=Get-Content -LiteralPath (Join-Path $env:SONA_DATA 'preferences.json') -Raw | ConvertFrom-Json
    Verify ($taskSaved.Language -eq 'fr' -and $taskSaved.Model -eq 'small') 'Agent settings are persisted by desktop app'
    $taskRejected=Agent @('settings','patch','{"language":"arbitrary-secret-like-value","token":"do-not-echo"}')
    Verify (-not $taskRejected.ok -and (($taskRejected | ConvertTo-Json) -notmatch 'do-not-echo|arbitrary-secret')) 'Unknown settings fail without echoing their contents'
    $taskDevice=Agent @('settings','patch','{"device":255}')
    Verify (-not $taskDevice.ok) 'Unavailable microphone ID fails before persistence'
    $taskPreferenceFile=Join-Path $env:SONA_DATA 'preferences.json'
    $taskLock=[IO.File]::Open($taskPreferenceFile,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    try{$taskLockedPatch=Agent @('settings','patch','{"language":"en"}');Verify (-not $taskLockedPatch.ok) 'Unwritable preferences reject settings commit'}finally{$taskLock.Dispose()}
    $taskRead=Agent @('settings','get')
    Verify ($taskRead.data.language -eq 'fr') 'Rejected write preserves active settings'
    Verify (-not $taskRead.data.cleanupEnabled -and $taskRead.data.cleanupEngine -eq 'Auto' -and $taskRead.data.cleanupTone -eq 'formal' -and $taskRead.data.cleanupIdleMinutes -eq 10) 'New installations keep optional cleanup off with GPU preference and a finite idle timeout'
    $taskCleanupPatch=Agent @('settings','patch','{"cleanupEnabled":true,"cleanupTone":"casual","cleanupEngine":"Cpu","cleanupIdleMinutes":0}')
    Verify ($taskCleanupPatch.ok -and $taskCleanupPatch.data.cleanupEnabled -and $taskCleanupPatch.data.cleanupTone -eq 'casual' -and $taskCleanupPatch.data.cleanupEngine -eq 'Cpu' -and $taskCleanupPatch.data.cleanupIdleMinutes -eq 0) 'Four cleanup settings round trip through validated agent patches'
    $taskCleanupRejected=Agent @('settings','patch','{"cleanupIdleMinutes":241}')
    Verify (-not $taskCleanupRejected.ok -and (Agent @('settings','get')).data.cleanupIdleMinutes -eq 0) 'Out-of-range cleanup timeout preserves active settings'
    $taskCleanupStatus=Agent @('cleanup','status')
    Verify ($taskCleanupStatus.ok -and $taskCleanupStatus.data.enabled -and -not $taskCleanupStatus.data.processId -and -not $taskCleanupStatus.data.modelVramMiB) 'Microphone-free UI testing never warms a cleanup worker or advertises GPU memory'
    $taskCleanupCancel=Agent @('cleanup','cancel')
    Verify ($taskCleanupCancel.ok -and -not $taskCleanupCancel.data.setupCleanupPending) 'Cleanup cancel also retries retained setup cleanup while idle'
    $taskCleanupUnload=Agent @('cleanup','unload')
    Verify ($taskCleanupUnload.ok -and -not $taskCleanupUnload.data.backend) 'Cleanup unload is safe without a resident worker'
    $taskInfo=[Diagnostics.ProcessStartInfo]::new($taskAgent)
    $taskInfo.ArgumentList.Add('--mcp');$taskInfo.UseShellExecute=$false;$taskInfo.CreateNoWindow=$true
    $taskInfo.RedirectStandardInput=$true;$taskInfo.RedirectStandardOutput=$true;$taskInfo.RedirectStandardError=$true
    $taskMcp=[Diagnostics.Process]::Start($taskInfo)
    $taskMcp.StandardInput.WriteLine('{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"sona-smoke","version":"1"}}}')
    $taskMcp.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
    $taskMcp.StandardInput.WriteLine('{"jsonrpc":"2.0","id":2,"method":"tools/list"}')
    $taskMcp.StandardInput.WriteLine('{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"sona_settings_get","arguments":{}}}')
    $taskMcp.StandardInput.Close();$taskMcpOutput=$taskMcp.StandardOutput.ReadToEndAsync()
    if(-not $taskMcp.WaitForExit(20000)){$taskMcp.Kill();throw 'MCP smoke exceeded deadline'}
    $taskReplies=@($taskMcpOutput.GetAwaiter().GetResult().Split("`n",[StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object{$_|ConvertFrom-Json})
    Verify ($taskReplies.Count -eq 3 -and $taskReplies[0].result.protocolVersion -eq '2025-11-25' -and $taskReplies[1].result.tools.Count -eq 18) 'MCP stdio negotiates protocol and exposes eighteen named tools'
    Verify ($taskReplies[2].result.structuredContent.ok -and $taskReplies[2].result.structuredContent.data.language -eq 'fr' -and -not $taskReplies[2].result.isError) 'MCP tool calls use the same live validated settings layer'
    $taskMcpSettings=@($taskReplies[1].result.tools | Where-Object name -eq 'sona_settings_patch')[0].inputSchema.properties
    Verify ($taskMcpSettings.cleanupEnabled.type -eq 'boolean' -and $taskMcpSettings.cleanupTone.enum.Count -eq 3 -and $taskMcpSettings.cleanupEngine.enum.Count -eq 2 -and $taskMcpSettings.cleanupIdleMinutes.maximum -eq 240) 'MCP publishes the same bounded cleanup settings contract'
    Verify ($taskMcpSettings.pasteCompletedDraft.type -eq 'boolean' -and $taskMcpSettings.enterToSend.type -eq 'boolean' -and $taskMcpSettings.liveTranscription.type -eq 'boolean' -and $taskMcpSettings.soundEffects.type -eq 'boolean') 'MCP publishes the same dictation preference contract'
    if($DownloadTiny)
    {
        $taskDownload=Agent @('models','install','tiny');Verify ($taskDownload.ok -and $taskDownload.data.accepted) 'Explicit Tiny model installation starts through the desktop service'
        for($taskTry=0;$taskTry -lt 120;$taskTry++){Start-Sleep -Seconds 1;$taskProgress=Agent @('status');if(-not $taskProgress.data.downloadingModel){break}}
        Verify ($taskProgress.data.downloadOutcome -eq 'verified') 'Pinned Tiny model download finishes with verified integrity'
        $taskVerify=Agent @('models','verify','tiny');Verify ($taskVerify.ok -and $taskVerify.data.verified) 'Downloaded model independently matches pinned SHA256'
        $taskTiny=Agent @('settings','patch','{"model":"tiny"}');Verify ($taskTiny.ok -and $taskTiny.data.model -eq 'tiny') 'Downloaded model can be selected without changing production preferences'
    }
    [pscustomobject]@{Passed=$taskChecks.Count;Checks=$taskChecks;MicrophoneOpened=$false;GlobalInputSent=$false;DiscordContacted=$false}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $OutputDirectory 'agent-smoke.json')
}
catch{$_|Out-String|Set-Content -LiteralPath (Join-Path $OutputDirectory 'failure.txt');throw}
finally
{
    if(-not $taskProcess.HasExited){$taskProcess.Refresh();$null=$taskProcess.CloseMainWindow();if(-not $taskProcess.WaitForExit(10000)){throw 'Isolated app did not quit; 180 second test deadline remains active.'}}
    $env:SONA_DATA=$taskPriorData
}
