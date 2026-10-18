param([Parameter(Mandatory)][string]$OutputRoot)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Security
$taskRepo=Split-Path $PSScriptRoot -Parent
$taskRoot=[IO.Path]::GetFullPath($OutputRoot)
if((Split-Path $taskRoot -Leaf)-notmatch'^sona-upgrade-rehearsal-[a-f0-9]{32}$' -or (Test-Path -LiteralPath $taskRoot)){throw 'Use a fresh guarded rehearsal root.'}
$taskChecks=[Collections.Generic.List[string]]::new();$taskFailures=[Collections.Generic.List[string]]::new()
function Check([bool]$Value,[string]$Label){if($Value){$taskChecks.Add($Label)}else{$taskFailures.Add($Label)}}
$taskOwners=@(Get-Process Sona -ErrorAction SilentlyContinue|ForEach-Object{@{Id=$_.Id;Started=$_.StartTime.Ticks;Path=$_.Path}})
New-Item -ItemType Directory -Path $taskRoot|Out-Null
[IO.File]::WriteAllText((Join-Path $taskRoot '.sona-rehearsal'),'Synthetic read-only profile preflight.')
$taskInstall=Join-Path $taskRoot 'Programs\Sona\0.0.0-aaaaaaaa';$taskProfile=Join-Path $taskRoot 'profile';$taskRecovery=Join-Path $taskRoot 'recovery'
New-Item -ItemType Directory -Path $taskInstall,$taskProfile,$taskRecovery|Out-Null
$taskExe=Join-Path $taskInstall 'Sona.exe'
[IO.File]::WriteAllText($taskExe,'Synthetic nonexecutable target; profile checks must never launch it.')
@{Path=$taskExe;SHA256=(Get-FileHash -LiteralPath $taskExe).Hash.ToLowerInvariant()}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $taskInstall 'installation.json')
[IO.File]::WriteAllText((Join-Path $taskProfile 'preferences.json'),'Synthetic profile marker.')
$taskLauncher=Join-Path $taskRepo 'tools\start-sona.ps1'
try{$taskReady=& $taskLauncher -FilePath $taskExe -RehearsalRoot $taskRoot -CheckProfileOnly;Check ($taskReady.Ok -and $taskReady.CheckProfileOnly -and !$taskReady.PID) 'Desktop profile preflight succeeds without launching Sona'}catch{Check $false 'Desktop profile preflight succeeds without launching Sona'}
if($taskFailures.Count -eq 0){
 $taskRequest=Join-Path $taskRecovery ('desktop-launch-'+[Guid]::NewGuid().ToString('N')+'.dpapi')
 $taskData=@{FilePath=$taskExe;RehearsalRoot=$taskRoot;Environment=@{};ExpectedDiscordSetup=$true;CheckProfileOnly=$true;ExpiresUtc=[DateTimeOffset]::UtcNow.AddSeconds(20).ToString('o')}
 $taskBytes=[Text.Encoding]::UTF8.GetBytes(($taskData|ConvertTo-Json -Depth 4 -Compress))
 try{[IO.File]::WriteAllBytes($taskRequest,[Security.Cryptography.ProtectedData]::Protect($taskBytes,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser))}finally{[Array]::Clear($taskBytes,0,$taskBytes.Length)}
 & $taskLauncher -BootstrapRequest $taskRequest
 $taskRejected=Get-Content -LiteralPath ($taskRequest+'.reply.json') -Raw|ConvertFrom-Json
 Check (!$taskRejected.Ok -and !$taskRejected.LaunchMayHaveStarted -and $taskRejected.FailureCode-eq'DiscordSetupMissing') 'Expected setup missing from the desktop profile is rejected before any launch'
 Check (![IO.File]::Exists((Join-Path $taskProfile 'discord-credentials.dpapi'))) 'Read-only preflight never manufactures a credential file'
 $taskBlob=[Security.Cryptography.ProtectedData]::Protect([byte[]]@(1,2,3),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
 [IO.File]::WriteAllBytes((Join-Path $taskProfile 'discord-credentials.dpapi'),$taskBlob)
 $taskHash=(Get-FileHash -LiteralPath (Join-Path $taskProfile 'discord-credentials.dpapi')).Hash
 $taskReady=& $taskLauncher -FilePath $taskExe -RehearsalRoot $taskRoot -CheckProfileOnly
 Check ($taskReady.Ok -and $taskReady.CheckProfileOnly -and $taskReady.DiscordSetupPresent -and !$taskReady.PID) 'Retained encrypted setup is visible from the real desktop context'
 Check ((Get-FileHash -LiteralPath (Join-Path $taskProfile 'discord-credentials.dpapi')).Hash-eq$taskHash) 'Preflight preserves the encrypted setup bytes'
 Check ([IO.File]::ReadAllText((Join-Path $taskProfile 'preferences.json'))-eq'Synthetic profile marker.') 'Preflight preserves preferences'
 Check (@(Get-ChildItem -LiteralPath $taskRecovery -Filter '*.claimed').Count-eq0) 'Consumed profile requests are removed'
 foreach($taskOwner in $taskOwners){$taskCurrent=Get-Process -Id $taskOwner.Id;Check ($taskCurrent.Path-eq$taskOwner.Path -and $taskCurrent.StartTime.Ticks-eq$taskOwner.Started) 'Running owner app is preserved without restart'}
}
@{Checks=@($taskChecks);Failures=@($taskFailures);OwnerCredentialsRead=$false;MicrophoneOpened=$false;VoiceSettingsWritten=$false;OwnerRestarted=$false}|ConvertTo-Json -Depth 4|Tee-Object -FilePath (Join-Path $taskRoot 'profile-preflight.json')
if($taskFailures.Count){exit 1}
