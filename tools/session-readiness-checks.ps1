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
@{Passed=$checks.Count;Checks=$checks;SyntheticOnly=$true;OwnerInstanceTouched=$false;MicrophoneOpened=$false;GlobalInputSent=$false}|ConvertTo-Json -Depth 4
