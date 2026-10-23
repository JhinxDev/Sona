$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'session-history-transfer.ps1')
if(!(Get-Command Wait-SonaOwnedStartup -CommandType Function -ErrorAction SilentlyContinue)){throw 'Owned startup has no bounded readiness wait'}
$checks=[Collections.Generic.List[string]]::new()
function Trial([string]$Name,[string[]]$States,[int]$ReadyAt=1,[bool]$Owns=$false,[bool]$Pending=$false,[bool]$Paused=$false,[string]$Load='Loaded',[string]$Version='0.4.10',[int]$LoseOwnershipAt=100,[int]$ExpectedCalls=1,[string]$Failure='',[int]$VoiceReadyAt=1,[int]$SlowRead=0,[int]$Timeout=30000,[string]$ExpectedLoad='Loaded'){
 $trial=@{Calls=0;Identity=0}
 $read={
  $index=[Math]::Min($trial.Calls,$States.Count-1);$trial.Calls++
  if($SlowRead){Start-Sleep -Milliseconds $SlowRead}
  return @{State=@{ok=($trial.Calls-ge$ReadyAt);data=@{state=$States[$index];version=$Version;unsavedSettings=$Pending;historyPaused=$Paused;preferencesLoad=$Load}};Voice=@{ok=($trial.Calls-ge$VoiceReadyAt);data=@{ownsChanges=$Owns}}}
 }.GetNewClosure()
 $identity={$trial.Identity++;return $trial.Identity-lt$LoseOwnershipAt}.GetNewClosure()
 $errorText=$null;$result=$null
 try{$result=Wait-SonaOwnedStartup -ReadReply $read -IsExactProcess $identity -ExpectedVersion '0.4.10' -ExpectedPreferencesLoad $ExpectedLoad -MaxAttempts 3 -TimeoutMilliseconds $Timeout}catch{$errorText=$_.Exception.Message}
 if($trial.Calls-ne$ExpectedCalls -or ($Failure -and $errorText-notlike('*'+$Failure+'*')) -or (!$Failure -and ($errorText -or $result.state-ne'idle'))){throw ('Owned startup regression: '+$Name+'; calls='+$trial.Calls+'; '+$errorText)}
 $checks.Add($Name)
}
Trial 'Owned ready candidate returns without a wait' @('idle')
Trial 'Owned model warming may settle to idle' @('busy','busy','idle') -ExpectedCalls 3
Trial 'Owned startup tolerates unavailable app status until ready' @('idle') -ReadyAt 3 -ExpectedCalls 3
Trial 'Owned startup tolerates unavailable voice status while warming' @('busy','busy','idle') -VoiceReadyAt 3 -ExpectedCalls 3
Trial 'Owned startup has a finite polling bound' @('busy') -ExpectedCalls 3 -Failure 'did not become idle'
Trial 'A recording refuses immediately' @('recording') -Failure 'recording or owns voice'
Trial 'Owned voice changes refuse immediately' @('busy') -Owns $true -Failure 'recording or owns voice'
Trial 'Pending owner edits refuse immediately' @('busy') -Pending $true -Failure 'preferences changed'
Trial 'Owner pause refuses immediately' @('idle') -Paused $true -Failure 'preferences changed'
Trial 'Unavailable preferences refuse immediately' @('idle') -Load 'Unavailable' -Failure 'preferences changed'
Trial 'Another version cannot satisfy startup' @('idle') -Version '0.4.9' -Failure 'version changed'
Trial 'An unowned process cannot be polled' @('idle') -LoseOwnershipAt 1 -ExpectedCalls 0 -Failure 'process identity'
Trial 'Ownership revoked inside status read cannot authorize completion' @('idle') -LoseOwnershipAt 2 -Failure 'process identity'
Trial 'Ownership revoked between polls does not rebase the candidate' @('busy') -LoseOwnershipAt 3 -Failure 'process identity'
Trial 'Unknown session state refuses immediately' @('unexpected') -Failure 'recording or owns voice'
Trial 'A new recording interrupts startup waiting' @('busy','recording') -ExpectedCalls 2 -Failure 'recording or owns voice'
Trial 'Known voice ownership refuses even when app status is unavailable' @('busy') -ReadyAt 100 -Owns $true -Failure 'recording or owns voice'
Trial 'Ready reply past the soft readiness deadline is not accepted' @('idle') -SlowRead 80 -Timeout 50 -Failure 'did not become idle'
Trial 'A fresh profile may remain Missing when it was already Missing before Quit' @('idle') -Load 'Missing' -ExpectedLoad 'Missing'
Trial 'A saved profile disappearing during startup is rejected' @('idle') -Load 'Missing' -Failure 'preferences changed'
Trial 'Previously Missing preferences may load after a first Save in the old session' @('idle') -ExpectedLoad 'Missing'
Trial 'Previously Missing preferences may not become Unavailable' @('idle') -Load 'Unavailable' -ExpectedLoad 'Missing' -Failure 'preferences changed'
@{Passed=$checks.Count;Checks=$checks;SyntheticOnly=$true;OwnerInstanceTouched=$false;MicrophoneOpened=$false;GlobalInputSent=$false}|ConvertTo-Json -Depth 4