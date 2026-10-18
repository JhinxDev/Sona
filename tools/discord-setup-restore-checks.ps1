param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Security
$taskRepo=Split-Path $PSScriptRoot -Parent
$taskOutput=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $taskOutput){throw 'Use a fresh isolated output directory.'}
New-Item -ItemType Directory -Path $taskOutput|Out-Null
$taskSource=Join-Path $taskOutput 'source';$taskDestination=Join-Path $taskOutput 'destination'
New-Item -ItemType Directory -Path $taskSource,$taskDestination|Out-Null
[IO.File]::WriteAllText((Join-Path $taskDestination 'preferences.json'),'Synthetic profile marker.')
$taskPlain=[Text.Encoding]::UTF8.GetBytes('Synthetic Discord setup fixture, never owner credentials.')
try{$taskEncrypted=[Security.Cryptography.ProtectedData]::Protect($taskPlain,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)}finally{[Array]::Clear($taskPlain,0,$taskPlain.Length)}
[IO.File]::WriteAllBytes((Join-Path $taskSource 'discord-credentials.dpapi'),$taskEncrypted)
$taskChecks=[Collections.Generic.List[string]]::new();$taskFailures=[Collections.Generic.List[string]]::new()
function Check([bool]$Value,[string]$Label){if($Value){$taskChecks.Add($Label)}else{$taskFailures.Add($Label)}}
$taskTool=Join-Path $taskRepo 'tools\restore-discord-setup.ps1'
$taskTarget=Join-Path $taskDestination 'discord-credentials.dpapi'
if(Test-Path -LiteralPath $taskTool){& $taskTool -SourceProfileDirectory $taskSource -DestinationProfileDirectory $taskDestination|Out-Null}
Check ([IO.File]::Exists($taskTarget)) 'A missing active-profile setup is restored from the retained encrypted file'
if([IO.File]::Exists($taskTarget)){
 Check ((Get-FileHash -LiteralPath $taskTarget).Hash -eq (Get-FileHash -LiteralPath (Join-Path $taskSource 'discord-credentials.dpapi')).Hash) 'Recovery preserves the encrypted bytes exactly'
 $taskTimestamp=(Get-Item -LiteralPath $taskTarget).LastWriteTimeUtc
 $taskSame=& $taskTool -SourceProfileDirectory $taskSource -DestinationProfileDirectory $taskDestination
 Check ($taskSame.Status -eq 'AlreadyPresent' -and (Get-Item -LiteralPath $taskTarget).LastWriteTimeUtc -eq $taskTimestamp) 'An identical existing setup is left untouched'
 [IO.File]::WriteAllBytes($taskTarget,[byte[]]@(1,2,3))
 $taskConflict=$false;try{& $taskTool -SourceProfileDirectory $taskSource -DestinationProfileDirectory $taskDestination|Out-Null}catch{$taskConflict=$true}
 Check ($taskConflict -and [IO.File]::ReadAllBytes($taskTarget).Length -eq 3) 'A different destination setup is never overwritten'
 [IO.File]::Delete($taskTarget)
 [IO.File]::Delete((Join-Path $taskDestination 'preferences.json'))
 $taskUnknown=$false;try{& $taskTool -SourceProfileDirectory $taskSource -DestinationProfileDirectory $taskDestination|Out-Null}catch{$taskUnknown=$true}
 Check ($taskUnknown -and ![IO.File]::Exists($taskTarget)) 'An unrecognized destination profile is rejected'
 [IO.File]::WriteAllText((Join-Path $taskDestination 'preferences.json'),'Synthetic profile marker.')
 [IO.File]::WriteAllBytes((Join-Path $taskSource 'discord-credentials.dpapi'),[byte[]]::new(131073))
 $taskOversize=$false;try{& $taskTool -SourceProfileDirectory $taskSource -DestinationProfileDirectory $taskDestination|Out-Null}catch{$taskOversize=$true}
 Check ($taskOversize -and ![IO.File]::Exists($taskTarget)) 'An oversized source is rejected before any destination write'
 Check ((Get-ChildItem -LiteralPath $taskDestination -Filter '*.tmp').Count -eq 0) 'No temporary credential copy remains'
 Check ([IO.File]::ReadAllText((Join-Path $taskDestination 'preferences.json')) -eq 'Synthetic profile marker.') 'Recovery leaves preferences unchanged'
}
$taskReport=@{Checks=@($taskChecks);Failures=@($taskFailures);OwnerCredentialsRead=$false;LiveProfileChanged=$false;MicrophoneOpened=$false;VoiceSettingsWritten=$false}
$taskReport|ConvertTo-Json -Depth 4|Tee-Object -FilePath (Join-Path $taskOutput 'report.json')
if($taskFailures.Count){exit 1}
