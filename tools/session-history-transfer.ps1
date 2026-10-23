# Dot-source these functions around the canonical upgrade. Temporary retention
# exists only for the transfer; the owner's KeepHistory choice stays Off.
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName System.Security
. (Join-Path $PSScriptRoot 'resolve-physical-file.ps1')
function Get-SonaTransferField($Window,[string]$Id){
    return $Window.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$Id))
}
function Get-SonaTransferWindow([int]$ProcessId){
    for($attempt=0;$attempt -lt 100;$attempt++){
        $process=Get-Process -Id $ProcessId -ErrorAction Stop
        $process.Refresh()
        if($process.MainWindowHandle -ne [IntPtr]::Zero){
            $window=[Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
            if($window -and $window.Current.ProcessId -eq $ProcessId){return $window}
        }
        Start-Sleep -Milliseconds 100
    }
    throw 'Exact Sona transfer window is unavailable.'
}
# Only local status availability is retried. A known busy/owned session refuses
# immediately; no restart, setting change, or input is performed by this wait.
function Read-SonaReadyStatus([scriptblock]$ReadReply,[switch]$WaitForAvailability){
    for($attempt=0;$attempt -lt 10;$attempt++){
        $reply=& $ReadReply
        if(($reply.State.ok -and $reply.State.data.state -ne 'idle') -or ($reply.Voice.ok -and $reply.Voice.data.ownsChanges)){throw 'Sona is busy or owns voice changes; preserve the session.'}
        if(!$reply.State.ok -or !$reply.Voice.ok){
            if($WaitForAvailability -and $attempt -lt 9){Start-Sleep -Milliseconds 100;continue}
            throw 'Sona local status is unavailable; preserve recovery.'
        }
        return $reply.State.data
    }
}
function Get-SonaTransferStatus([int]$ProcessId,[switch]$WaitForAvailability){
    $process=Get-Process -Id $ProcessId -ErrorAction Stop
    $agent=Join-Path (Split-Path $process.Path) 'sona-agent.exe'
    $read={
        $state=((& $agent status)-join "`n")|ConvertFrom-Json
        $voice=((& $agent protection status)-join "`n")|ConvertFrom-Json
        return @{State=$state;Voice=$voice}
    }.GetNewClosure()
    return Read-SonaReadyStatus -ReadReply $read -WaitForAvailability:$WaitForAvailability
}
# Only the candidate just launched by this upgrade may wait through model warming.
# Preflight and ordinary session checks retain their immediate busy refusal.
# This is a soft readiness deadline around synchronous status polls. Each CLI
# call has its own 16s cancellation timeout; an in-flight poll can extend return.
function Wait-SonaOwnedStartup([scriptblock]$ReadReply,[scriptblock]$IsExactProcess,[Parameter(Mandatory)][string]$ExpectedVersion,[ValidateSet('Loaded','Missing')][string]$ExpectedPreferencesLoad='Loaded',[ValidateRange(1,300)][int]$MaxAttempts=300,[ValidateRange(1,30000)][int]$TimeoutMilliseconds=30000){
    $clock=[Diagnostics.Stopwatch]::StartNew()
    for($attempt=0;$attempt-lt$MaxAttempts;$attempt++){
        if(!(& $IsExactProcess)){throw 'Owned startup process identity changed; preserve recovery.'}
        if($clock.ElapsedMilliseconds-ge$TimeoutMilliseconds){break}
        $reply=& $ReadReply
        if(!(& $IsExactProcess)){throw 'Owned startup process identity changed; preserve recovery.'}
        if(($reply.State.ok -and $reply.State.data.state-notin@('idle','busy')) -or ($reply.Voice.ok -and $reply.Voice.data.ownsChanges)){throw 'Sona is recording or owns voice changes; preserve recovery.'}
        if($reply.State.ok){
            if($reply.State.data.version-ne$ExpectedVersion){throw 'Owned startup version changed; preserve recovery.'}
            # The app reports its initial load result for the entire session. A
            # formerly Missing profile can have been saved before this restart.
            $validLoad=$reply.State.data.preferencesLoad-eq$ExpectedPreferencesLoad -or ($ExpectedPreferencesLoad-eq'Missing' -and $reply.State.data.preferencesLoad-eq'Loaded')
            if($reply.State.data.unsavedSettings -or $reply.State.data.historyPaused -or !$validLoad){throw 'Owner preferences changed during startup; preserve recovery.'}
        }
        if($clock.ElapsedMilliseconds-ge$TimeoutMilliseconds){break}
        if($reply.State.ok -and $reply.Voice.ok -and $reply.State.data.state-eq'idle'){return $reply.State.data}
        if($attempt-lt$MaxAttempts-1){Start-Sleep -Milliseconds ([Math]::Min(100,[Math]::Max(0,$TimeoutMilliseconds-$clock.ElapsedMilliseconds)))}
    }
    throw 'Owned startup did not become idle within its bound; preserve recovery.'
}
function Get-SonaTransferCount($Window){
    (Get-SonaTransferField $Window 'HistoryTab').GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $text=(Get-SonaTransferField $Window 'HistoryCountPreview').Current.Name
    if($text -notmatch '^(\d+) recent records? in All recent history\. (\d+) saved'){throw 'Use unfiltered All recent history before a session transfer.'}
    return @{Recent=[int]$Matches[1];Saved=[int]$Matches[2]}
}
# Only the bounded wait following our own native Save may observe temporary busy.
# Normal preflight and ordinary session checks still reject every known busy state.
function Wait-SonaTransferNativeSave([scriptblock]$ReadReply,[scriptblock]$IsExpectedToggle,[ValidateRange(1,300)][int]$MaxAttempts=300){
    for($attempt=0;$attempt -lt $MaxAttempts;$attempt++){
        $reply=& $ReadReply
        if(($reply.State.ok -and $reply.State.data.state -notin @('idle','busy')) -or ($reply.Voice.ok -and $reply.Voice.data.ownsChanges)){throw 'Sona is busy or owns voice changes; preserve the session.'}
        if(!$reply.State.ok -or !$reply.Voice.ok){throw 'Sona local status is unavailable; preserve recovery.'}
        $state=$reply.State.data
        if($state.state -eq 'idle' -and !$state.unsavedSettings -and (& $IsExpectedToggle)){return}
        if($attempt -lt $MaxAttempts-1){Start-Sleep -Milliseconds 100}
    }
    throw 'Native preference Save did not finish; preserve the session.'
}
function Set-SonaTransferRetention([int]$ProcessId,[bool]$Enabled){
    $null=Get-SonaTransferStatus $ProcessId
    $window=Get-SonaTransferWindow $ProcessId
    (Get-SonaTransferField $window 'SettingsTab').GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
    (Get-SonaTransferField $window 'SettingsPrivacyNav').GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $toggle=(Get-SonaTransferField $window 'KeepHistory').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)
    # A matching checkbox may still be a pending edit after an interrupted save.
    # The active preference and native pending state must settle before returning.
    if(($toggle.Current.ToggleState -eq 'On') -eq $Enabled){
        if(!(Get-SonaTransferStatus $ProcessId).unsavedSettings){return}
    }else{$toggle.Toggle()}
    $button=$null
    for($attempt=0;$attempt -lt 30;$attempt++){
        $button=Get-SonaTransferField $window 'SavePreferencesButton'
        $pending=(Get-SonaTransferStatus $ProcessId).unsavedSettings
        if(!$pending -and (($toggle.Current.ToggleState -eq 'On') -eq $Enabled)){return}
        if($button -and $button.Current.IsEnabled -and $pending){break}
        $button=$null;Start-Sleep -Milliseconds 100
    }
    if(!$button){throw 'The native preference save is unavailable; session preserved.'}
    $button.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
    $agent=Join-Path (Split-Path (Get-Process -Id $ProcessId -ErrorAction Stop).Path) 'sona-agent.exe'
    $read={
        return @{State=(((& $agent status)-join "`n")|ConvertFrom-Json);Voice=(((& $agent protection status)-join "`n")|ConvertFrom-Json)}
    }.GetNewClosure()
    $expected={return (($toggle.Current.ToggleState -eq 'On') -eq $Enabled)}.GetNewClosure()
    Wait-SonaTransferNativeSave -ReadReply $read -IsExpectedToggle $expected
}
function Save-SonaSessionHistory([int]$ProcessId,[string]$ProfileDirectory,[string]$RecoveryPath){
    $ErrorActionPreference='Stop'
    $state=Get-SonaTransferStatus $ProcessId
    if($state.unsavedSettings -or $state.preferencesLoad -ne 'Loaded' -or $state.historyPaused){throw 'Save pending edits and preserve an explicitly paused History before transfer.'}
    $profile=[IO.Path]::GetFullPath($ProfileDirectory)
    $preferences=Join-Path $profile 'preferences.json';$history=Join-Path $profile 'history.json'
    if((Split-Path (Resolve-SonaPhysicalFile $preferences) -Parent) -ne $profile){throw 'Use the actual desktop profile, not a redirected alias.'}
    if(Test-Path -LiteralPath $history){throw 'This finite transfer is for session-only history; existing retained history is preserved.'}
    $prior=Get-Content -LiteralPath $preferences -Raw|ConvertFrom-Json
    if($prior.KeepHistory){throw 'History storage must already be Off for this transfer.'}
    $preferenceHash=(Get-FileHash -LiteralPath $preferences).Hash
    $count=Get-SonaTransferCount (Get-SonaTransferWindow $ProcessId)
    if($count.Recent -le 0){throw 'No session history needs transfer.'}
    $recovery=[IO.Path]::GetFullPath($RecoveryPath)
    if((Split-Path $recovery -Leaf) -notmatch '^session-history-[a-f0-9]{32}\.dpapi$' -or (Test-Path -LiteralPath $recovery) -or !(Test-Path -LiteralPath (Split-Path $recovery -Parent) -PathType Container)){throw 'Use a fresh guarded recovery file in an existing private directory.'}
    $process=Get-Process -Id $ProcessId -ErrorAction Stop
    $stage=[pscustomobject]@{Profile=$profile;HistoryPath=$history;HistorySHA256=$null;PreferencesSHA256=$preferenceHash;RecoveryPath=$recovery;RecoverySHA256=$null;Recent=$count.Recent;Saved=$count.Saved;KeepHistory=$false;ProcessId=$ProcessId;ProcessPath=$process.Path;ProcessStarted=$process.StartTime.ToUniversalTime().Ticks}
    $retentionAttempted=$false;$stageHash=$null
    try{
    try{
        $retentionAttempted=$true;Set-SonaTransferRetention $ProcessId $true
        $saved=Get-Content -LiteralPath $preferences -Raw|ConvertFrom-Json
        if(!$saved.KeepHistory -or !(Test-Path -LiteralPath $history)){throw 'Native save did not stage session History.'}
        $stage.HistorySHA256=(Get-FileHash -LiteralPath $history).Hash
        $bytes=[IO.File]::ReadAllBytes($history)
        try{
            $rows=@([Text.Encoding]::UTF8.GetString($bytes)|ConvertFrom-Json)
            if($rows.Count -ne $count.Recent -or @($rows|Where-Object Saved).Count){throw 'Staged History does not match the complete recent session.'}
            $stageHash=(Get-FileHash -LiteralPath $history).Hash
            [IO.File]::WriteAllBytes($recovery,[Security.Cryptography.ProtectedData]::Protect($bytes,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser))
            $stage.RecoverySHA256=(Get-FileHash -LiteralPath $recovery).Hash
            $restored=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($recovery),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
            try{if([Convert]::ToBase64String($restored) -cne [Convert]::ToBase64String($bytes)){throw 'Protected session recovery differs.'}}finally{[Array]::Clear($restored,0,$restored.Length)}
        }finally{[Array]::Clear($bytes,0,$bytes.Length);$rows=$null}
    }finally{
        if($retentionAttempted){Set-SonaTransferRetention $ProcessId $false}
    }
    if((Get-FileHash -LiteralPath $preferences).Hash -ne $preferenceHash -or (Get-FileHash -LiteralPath $history).Hash -ne $stageHash){throw 'Preferences or staged History changed; preserve recovery and session.'}
    return $stage
    }catch{
        # Before Quit the original process still owns every in-memory entry.
        # A failed transfer must not leave its temporary plaintext on disk.
        Undo-SonaSessionHistory $stage|Out-Null
        throw
    }
}
function Undo-SonaSessionHistory($Stage){
    $ErrorActionPreference='Stop'
    $process=Get-Process -Id $Stage.ProcessId -ErrorAction Stop
    if($process.Path -ne $Stage.ProcessPath -or $process.StartTime.ToUniversalTime().Ticks -ne $Stage.ProcessStarted){throw 'Original session identity changed; retain recovery.'}
    $profile=[IO.Path]::GetFullPath($Stage.Profile);$history=Join-Path $profile 'history.json'
    $preferences=Join-Path $profile 'preferences.json'
    if((Get-Content -LiteralPath $preferences -Raw|ConvertFrom-Json).KeepHistory){throw 'Retention could not be restored Off; preserve app and recovery for retry.'}
    if(Test-Path -LiteralPath $history){
        if(!$Stage.HistorySHA256 -or $Stage.HistoryPath -ne $history -or (Resolve-SonaPhysicalFile $history) -ne $history -or (Get-FileHash -LiteralPath $history).Hash -ne $Stage.HistorySHA256){throw 'Staged file changed; preserve it and the original session.'}
        Remove-Item -LiteralPath $history
    }
    if(Test-Path -LiteralPath $Stage.RecoveryPath){
        $recovery=[IO.Path]::GetFullPath($Stage.RecoveryPath)
        if(!$Stage.RecoverySHA256 -or (Split-Path $recovery -Leaf) -notmatch '^session-history-[a-f0-9]{32}\.dpapi$' -or (Get-Item -LiteralPath $recovery).Attributes -band [IO.FileAttributes]::ReparsePoint -or (Get-FileHash -LiteralPath $recovery).Hash -ne $Stage.RecoverySHA256){throw 'Protected recovery ownership changed; preserve it.'}
        Remove-Item -LiteralPath $recovery
    }
    return [pscustomobject]@{OriginalSessionPreserved=$true;TemporaryHistoryRemoved=$true;KeepHistory=$false}
}
function Confirm-SonaSessionHistory($Stage){
    $ErrorActionPreference='Stop'
    $state=Get-SonaTransferStatus $Stage.ProcessId
    if($state.unsavedSettings -or (Get-FileHash -LiteralPath (Join-Path $Stage.Profile 'preferences.json')).Hash -ne $Stage.PreferencesSHA256 -or (Get-FileHash -LiteralPath $Stage.HistoryPath).Hash -ne $Stage.HistorySHA256){throw 'The staged session changed before Quit; preserve the original app.'}
    $count=Get-SonaTransferCount (Get-SonaTransferWindow $Stage.ProcessId)
    if($count.Recent -ne $Stage.Recent -or $count.Saved -ne $Stage.Saved){throw 'History changed after staging; preserve the original app.'}
    # Serialize the current native entries again to detect edits as well as rows.
    # Existing staged bytes are already guarded and remain owned by this transfer.
    try{Set-SonaTransferRetention $Stage.ProcessId $true}
    finally{Set-SonaTransferRetention $Stage.ProcessId $false}
    $currentHash=(Get-FileHash -LiteralPath $Stage.HistoryPath).Hash
    if($currentHash -ne $Stage.HistorySHA256){
        $Stage.HistorySHA256=$currentHash # only for abort cleanup of this native write
        throw 'History content changed after staging; preserve the original app.'
    }
    if((Get-FileHash -LiteralPath (Join-Path $Stage.Profile 'preferences.json')).Hash -ne $Stage.PreferencesSHA256){throw 'Preferences changed during session verification; preserve the app.'}
}
function Complete-SonaSessionHistory($Stage,[int]$ProcessId){
    $ErrorActionPreference='Stop'
    $state=Get-SonaTransferStatus $ProcessId -WaitForAvailability
    if($state.unsavedSettings -or $state.historyPaused){throw 'App changed; retain session recovery.'}
    $profile=[IO.Path]::GetFullPath($Stage.Profile)
    $history=Join-Path $profile 'history.json';$preferences=Join-Path $profile 'preferences.json'
    if($Stage.HistoryPath -ne $history -or (Resolve-SonaPhysicalFile $history) -ne $history -or (Get-FileHash -LiteralPath $history).Hash -ne $Stage.HistorySHA256 -or (Get-FileHash -LiteralPath $preferences).Hash -ne $Stage.PreferencesSHA256){throw 'Transfer file ownership changed; retain all recovery.'}
    $count=Get-SonaTransferCount (Get-SonaTransferWindow $ProcessId)
    if($count.Recent -ne $Stage.Recent -or $count.Saved -ne $Stage.Saved){throw 'The new live session did not load every History entry.'}
    $recovery=[IO.Path]::GetFullPath($Stage.RecoveryPath)
    if((Split-Path $recovery -Leaf) -notmatch '^session-history-[a-f0-9]{32}\.dpapi$' -or (Get-Item -LiteralPath $recovery).Attributes -band [IO.FileAttributes]::ReparsePoint -or (Get-FileHash -LiteralPath $recovery).Hash -ne $Stage.RecoverySHA256){throw 'Protected session recovery ownership changed.'}
    # Both exact files belong to this finite transfer. Remove no prior History.
    Remove-Item -LiteralPath $history
    $after=Get-SonaTransferCount (Get-SonaTransferWindow $ProcessId)
    if($after.Recent -ne $Stage.Recent -or $after.Saved -ne $Stage.Saved){throw 'Live session changed during cleanup; retain protected recovery.'}
    Remove-Item -LiteralPath $recovery
    return [pscustomobject]@{RecentPreserved=$after.Recent;SavedPreserved=$after.Saved;KeepHistory=$false;TemporaryHistoryRemoved=$true;RecoveryRemoved=$true}
}
