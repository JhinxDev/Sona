$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'session-history-transfer.ps1')
$checks=[Collections.Generic.List[string]]::new()
function Trial([string]$Name,[int]$AvailableAt,[string]$State,[bool]$Owns,[bool]$Wait,[int]$Calls,[string]$ExpectedFailure,[int]$VoiceAvailableAt=-1){
    if($VoiceAvailableAt -lt 0){$VoiceAvailableAt=$AvailableAt}
    $trial=@{Calls=0}
    $read={
        $trial.Calls++
        return @{State=@{ok=($trial.Calls -ge $AvailableAt);data=@{state=$State;version='fixture'}};Voice=@{ok=($trial.Calls -ge $VoiceAvailableAt);data=@{ownsChanges=$Owns}}}
    }.GetNewClosure()
    $failure=$null;$result=$null
    try{$result=Read-SonaReadyStatus -ReadReply $read -WaitForAvailability:$Wait}catch{$failure=$_.Exception.Message}
    if($trial.Calls -ne $Calls -or ($ExpectedFailure -and $failure -notlike ('*'+$ExpectedFailure+'*')) -or (!$ExpectedFailure -and ($failure -or $result.version -ne 'fixture'))){throw ('Readiness regression: '+$Name)}
    $checks.Add($Name)
}
Trial 'Ready status returns on its first read' 1 idle $false $true 1 ''
Trial 'New-session verification tolerates transient unavailable status' 3 idle $false $true 3 ''
Trial 'Availability retry has a finite attempt bound' 100 idle $false $true 10 unavailable
Trial 'Known recording/busy state refuses immediately' 1 recording $false $true 1 busy
Trial 'Known owned voice changes refuse immediately' 1 idle $true $true 1 busy
Trial 'Ordinary transfer checks do not opt into startup retries' 100 idle $false $false 1 unavailable
Trial 'Known busy state rejects even while voice endpoint is unavailable' 1 recording $false $true 1 busy 100
Trial 'Known owned voice rejects even while app endpoint is unavailable' 100 idle $true $true 1 busy 1
function NativeSaveTrial([string]$Name,[string[]]$States,[bool[]]$Pending,[bool]$Owns,[bool]$StateAvailable,[bool]$VoiceAvailable,[bool]$ExpectedToggle,[int]$Calls,[string]$ExpectedFailure){
    $trial=@{Calls=0}
    $read={
        $index=[Math]::Min($trial.Calls,$States.Count-1);$trial.Calls++
        return @{State=@{ok=$StateAvailable;data=@{state=$States[$index];unsavedSettings=$Pending[[Math]::Min($index,$Pending.Count-1)]}};Voice=@{ok=$VoiceAvailable;data=@{ownsChanges=$Owns}}}
    }.GetNewClosure()
    $toggle={return $ExpectedToggle}.GetNewClosure();$failure=$null
    try{Wait-SonaTransferNativeSave -ReadReply $read -IsExpectedToggle $toggle -MaxAttempts 3}catch{$failure=$_.Exception.Message}
    if($trial.Calls-ne$Calls -or ($ExpectedFailure -and $failure-notlike('*'+$ExpectedFailure+'*')) -or (!$ExpectedFailure -and $failure)){throw ('Native-save regression: '+$Name+'; '+$failure)}
    $checks.Add($Name)
}
NativeSaveTrial 'Own native Save can settle temporary model warming before idle' @('busy','busy','idle') @($false) $false $true $true $true 3 ''
NativeSaveTrial 'Own native Save waits for pending edits to commit' @('idle','idle') @($true,$false) $false $true $true $true 2 ''
NativeSaveTrial 'Native-save wait keeps a finite bound' @('busy') @($false) $false $true $true $true 3 'did not finish'
NativeSaveTrial 'Native-save wait requires the expected checkbox' @('idle') @($false) $false $true $true $false 3 'did not finish'
NativeSaveTrial 'New recording interrupts native-save waiting' @('recording') @($false) $false $true $true $true 1 'busy'
NativeSaveTrial 'Owned voice changes interrupt native-save waiting' @('busy') @($false) $true $true $true $true 1 'busy'
NativeSaveTrial 'Unavailable save status preserves the session' @('idle') @($false) $false $false $true $true 1 'unavailable'
@{Passed=$checks.Count;Checks=$checks;SyntheticOnly=$true;OwnerInstanceTouched=$false;MicrophoneOpened=$false;GlobalInputSent=$false}|ConvertTo-Json -Depth 4
