param([Parameter(Mandatory)][string]$PackageDirectory,[Parameter(Mandatory)][string]$OutputRoot,[switch]$WithoutTransfer,[ValidateSet('None','AfterStage','HistoryChanged','QuitInFlight')][string]$FailurePoint='None')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName System.Security
. (Join-Path $PSScriptRoot 'session-history-transfer.ps1')
if(Test-Path -LiteralPath $OutputRoot){throw 'Use a fresh isolated rehearsal root.'}
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
if((Split-Path $OutputRoot -Leaf) -notmatch '^sona-upgrade-rehearsal-[a-f0-9]{32}$'){throw 'Use a guarded rehearsal root.'}
New-Item -ItemType Directory -Path $OutputRoot|Out-Null
Set-Content -LiteralPath (Join-Path $OutputRoot '.sona-rehearsal') -Value 'synthetic only'
$taskPriorData=$env:SONA_DATA;$env:SONA_DATA=Join-Path $OutputRoot 'profile'
$taskProfile=$env:SONA_DATA;New-Item -ItemType Directory -Path $taskProfile|Out-Null
$taskProduction=@(Get-Process -Name Sona -ErrorAction SilentlyContinue|Where-Object{$_.Path -and !$_.Path.StartsWith($OutputRoot+'\',[StringComparison]::OrdinalIgnoreCase)}|ForEach-Object{@{Id=$_.Id;Started=$_.StartTime.ToUniversalTime().Ticks}})
$taskChecks=[Collections.Generic.List[string]]::new()
function Check([bool]$Result,[string]$Name){if(!$Result){throw $Name};$taskChecks.Add($Name)}
function Scoped{ return @(Get-Process -Name Sona -ErrorAction SilentlyContinue|Where-Object{$_.Path -and $_.Path.StartsWith($OutputRoot+'\',[StringComparison]::OrdinalIgnoreCase)}) }
function ReadDraft($Window,[string]$Id){
    (Get-SonaTransferField $Window 'DictationTab').GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
    if($Id -eq 'Original' -and !(Get-SonaTransferField $Window $Id)){
        $items=$Window.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Original transcript'))
        foreach($item in $items){$pattern=$null;if($item.TryGetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern,[ref]$pattern)){$pattern.Expand();break}}
    }
    return (Get-SonaTransferField $Window $Id).GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value
}
try{
    [void][Reflection.Assembly]::LoadFrom((Join-Path $PackageDirectory 'Sona.Core.dll'))
    $taskPrefs=[Sona.Core.Preferences]::new();$taskPrefs.Save((Join-Path $taskProfile 'preferences.json'))
    $taskPreferencesHash=(Get-FileHash -LiteralPath (Join-Path $taskProfile 'preferences.json')).Hash
    $taskRows=@(1..4|ForEach-Object{@{Id=[Guid]::NewGuid();CreatedAt=[DateTimeOffset]::UtcNow.AddMinutes(-$_);Original=('Synthetic original '+$_);Draft=('Synthetic draft '+$_);Inserted=$false;Saved=$false;Generated=$null}})
    $taskHistory=Join-Path $taskProfile 'history.json'
    [IO.File]::WriteAllText($taskHistory,($taskRows|ConvertTo-Json -Depth 4 -Compress))
    $taskHistoryHash=(Get-FileHash -LiteralPath $taskHistory).Hash
    $taskReceipt=Get-Content -LiteralPath (Join-Path $PackageDirectory 'package.json') -Raw|ConvertFrom-Json
    foreach($taskLabel in @('a','b')){
        $taskPackage=Join-Path $OutputRoot ('package-'+$taskLabel);New-Item -ItemType Directory -Path $taskPackage|Out-Null
        Copy-Item -Path (Join-Path $PackageDirectory '*') -Destination $taskPackage -Recurse
        @{Version=$taskReceipt.Version;SourceHead=($taskLabel*40);Files=$taskReceipt.Files;SyntheticReceipt=$true}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $taskPackage 'package.json')
    }
    & (Join-Path $PSScriptRoot 'install.ps1') -PackageDirectory (Join-Path $OutputRoot 'package-a') -RehearsalRoot $OutputRoot|Out-Null
    $taskOldPath=Join-Path $OutputRoot ('Programs\Sona\'+$taskReceipt.Version+'-aaaaaaaa\Sona.exe')
    $taskInfo=[Diagnostics.ProcessStartInfo]::new($taskOldPath);$taskInfo.UseShellExecute=$false;$taskInfo.ArgumentList.Add('--test-ui');$taskInfo.ArgumentList.Add('--test-transfer');$taskInfo.Environment['SONA_DATA']=$taskProfile;$taskInfo.Environment['MURMUR_DATA']=$taskProfile
    $taskInfo.Environment['SONA_DRAFT_TRANSFER']=(@{Draft='Synthetic working draft';Original='Synthetic working original'}|ConvertTo-Json -Compress)
    $taskOld=[Diagnostics.Process]::Start($taskInfo)
    for($taskAttempt=0;$taskAttempt -lt 100;$taskAttempt++){$taskOld.Refresh();if($taskOld.MainWindowHandle){break};Start-Sleep -Milliseconds 100}
    $taskWindow=Get-SonaTransferWindow $taskOld.Id
    Check ($taskWindow.Current.BoundingRectangle.Right -lt 0) 'Isolated test window stays offscreen without activation'
    Check ((Get-SonaTransferCount $taskWindow).Recent -eq 4) 'Synthetic session starts with four complete History records'
    Set-SonaTransferRetention $taskOld.Id $true
    (Get-SonaTransferField $taskWindow 'SettingsTab').GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
    (Get-SonaTransferField $taskWindow 'SettingsPrivacyNav').GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
    (Get-SonaTransferField $taskWindow 'KeepHistory').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle()
    for($taskAttempt=0;$taskAttempt -lt 40;$taskAttempt++){if((Get-SonaTransferStatus $taskOld.Id).unsavedSettings){break};Start-Sleep -Milliseconds 50}
    Check ((Get-SonaTransferStatus $taskOld.Id).unsavedSettings -and (Get-Content -LiteralPath (Join-Path $taskProfile 'preferences.json') -Raw|ConvertFrom-Json).KeepHistory) 'Fixture reproduces pending Off while History storage is still saved On'
    Set-SonaTransferRetention $taskOld.Id $false
    Check (!(Get-SonaTransferStatus $taskOld.Id).unsavedSettings -and !(Get-Content -LiteralPath (Join-Path $taskProfile 'preferences.json') -Raw|ConvertFrom-Json).KeepHistory) 'Transfer settles a matching pending checkbox instead of assuming it is saved'
    (Get-SonaTransferField $taskWindow 'KeepHistory').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle()
    for($taskAttempt=0;$taskAttempt -lt 40;$taskAttempt++){if((Get-SonaTransferStatus $taskOld.Id).unsavedSettings){break};Start-Sleep -Milliseconds 50}
    Check ((Get-SonaTransferStatus $taskOld.Id).unsavedSettings -and !(Get-Content -LiteralPath (Join-Path $taskProfile 'preferences.json') -Raw|ConvertFrom-Json).KeepHistory) 'Fixture reproduces pending On while History storage is still saved Off'
    Set-SonaTransferRetention $taskOld.Id $false
    Check (!(Get-SonaTransferStatus $taskOld.Id).unsavedSettings -and !(Get-Content -LiteralPath (Join-Path $taskProfile 'preferences.json') -Raw|ConvertFrom-Json).KeepHistory) 'Transfer accepts an inverse pending edit restored to its saved value without a save'
    Remove-Item -LiteralPath $taskHistory
    Check (!(Test-Path -LiteralPath $taskHistory)) 'All four records now exist only in the isolated app session'
    (Get-SonaTransferField $taskWindow 'DictationTab').GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Check (!(Get-SonaTransferField $taskWindow 'RecordButton').Current.IsEnabled) 'Synthetic app has microphone capture disabled'
    if($WithoutTransfer){
        $taskRejected=$false;try{& (Join-Path $PSScriptRoot 'upgrade.ps1') -PackageDirectory (Join-Path $OutputRoot 'package-b') -ProcessId $taskOld.Id -RehearsalRoot $OutputRoot|Out-Null}catch{$taskRejected=$true}
        $taskOld.Refresh()
        Check ($taskRejected -and !$taskOld.HasExited) 'Unqualified upgrade refuses to discard session-only History before Quit'
    }elseif($FailurePoint -ne 'None'){
        $taskTransferHelper=Join-Path $PSScriptRoot 'session-history-transfer.ps1'
        $taskHook={param($FixtureProcessId,$FixtureProfile)
            . $taskTransferHelper
            [IO.File]::WriteAllText((Join-Path $OutputRoot 'hook-ran.txt'),'Stage existed before injected failure.')
            if($FailurePoint -eq 'AfterStage'){throw 'Synthetic post-stage pre-Quit failure'}
            $fixturePath=Join-Path $FixtureProfile 'history.json'
            $fixtureBytes=[IO.File]::ReadAllBytes($fixturePath)
            $fixtureRows=@([Text.Encoding]::UTF8.GetString($fixtureBytes)|ConvertFrom-Json)
            $fixtureRows+=@{Id=[Guid]::NewGuid();CreatedAt=[DateTimeOffset]::UtcNow;Original='Synthetic later original';Draft='Synthetic later draft';Inserted=$false;Saved=$false;Generated=$null}
            [IO.File]::WriteAllText($fixturePath,($fixtureRows|ConvertTo-Json -Depth 4 -Compress))
            Set-SonaTransferRetention $FixtureProcessId $true
            Set-SonaTransferRetention $FixtureProcessId $false
            [IO.File]::WriteAllBytes($fixturePath,$fixtureBytes)
            [Array]::Clear($fixtureBytes,0,$fixtureBytes.Length)
        }.GetNewClosure()
        $taskRejected=$false
        $taskFaultArgs=@{BeforeSessionQuitForTests=$taskHook}
        if($FailurePoint -eq 'QuitInFlight'){
            $taskFaultArgs=@{QuitInvokeForTests={ [IO.File]::WriteAllText((Join-Path $OutputRoot 'hook-ran.txt'),'Quit delivery is uncertain.');throw 'Synthetic ambiguous Quit invocation' }.GetNewClosure()}
        }
        try{& (Join-Path $PSScriptRoot 'upgrade.ps1') -PackageDirectory (Join-Path $OutputRoot 'package-b') -ProcessId $taskOld.Id -RehearsalRoot $OutputRoot -PreserveSessionHistory @taskFaultArgs|Out-Null}catch{$taskRejected=$true;($_|Out-String)+$_.ScriptStackTrace|Set-Content -LiteralPath (Join-Path $OutputRoot 'injected-failure.txt')}
        $taskOld.Refresh()
        Check (Test-Path -LiteralPath (Join-Path $OutputRoot 'hook-ran.txt')) 'The intended fault occurs only after successful session staging'
        Check ($taskRejected -and !$taskOld.HasExited) 'Injected failure preserves the original synthetic session'
        if($FailurePoint -eq 'QuitInFlight'){
            $guards=@(Get-ChildItem -LiteralPath (Join-Path $OutputRoot 'recovery') -Filter 'session-history-*.dpapi')
            Check ((Test-Path -LiteralPath $taskHistory) -and $guards.Count -eq 1) 'Uncertain Quit retains both session recovery copies even while old process remains alive'
            $taskStage=[pscustomobject]@{ProcessId=$taskOld.Id;ProcessPath=$taskOld.Path;ProcessStarted=$taskOld.StartTime.ToUniversalTime().Ticks;Profile=$taskProfile;HistoryPath=$taskHistory;HistorySHA256=(Get-FileHash -LiteralPath $taskHistory).Hash;RecoveryPath=$guards[0].FullName;RecoverySHA256=$guards[0].Hash}
            $taskStage.RecoverySHA256=(Get-FileHash -LiteralPath $guards[0].FullName).Hash
            # This fixture knows its substituted Invoke never requested Quit.
            Undo-SonaSessionHistory $taskStage|Out-Null
        }
        Check (!(Test-Path -LiteralPath $taskHistory)) 'Aborted transfer removes its owned plaintext staging file'
        Check ((Get-FileHash -LiteralPath (Join-Path $taskProfile 'preferences.json')).Hash -eq $taskPreferencesHash) 'Aborted transfer restores byte-identical preferences with History storage Off'
        $taskExpectedCount=if($FailurePoint -eq 'HistoryChanged'){5}else{4}
        Check ((Get-SonaTransferCount (Get-SonaTransferWindow $taskOld.Id)).Recent -eq $taskExpectedCount) 'Every original or newly added session entry survives an aborted transfer'
        Check (@(Get-ChildItem -LiteralPath (Join-Path $OutputRoot 'recovery') -Filter 'session-history-*.dpapi').Count -eq 0) 'Aborted transfer removes only its finite completed session guard'
    }else{
        & (Join-Path $PSScriptRoot 'upgrade.ps1') -PackageDirectory (Join-Path $OutputRoot 'package-b') -ProcessId $taskOld.Id -RehearsalRoot $OutputRoot -PreserveSessionHistory|Out-Null
        $taskNew=@(Scoped);Check ($taskNew.Count -eq 1 -and $taskNew[0].Path -ne $taskOldPath) 'Canonical guarded upgrade launches the isolated new version'
        $taskWindow=Get-SonaTransferWindow $taskNew[0].Id
        Check ($taskWindow.GetCurrentPattern([Windows.Automation.WindowPattern]::Pattern).Current.WindowVisualState -eq 'Minimized') 'Protected desktop launcher honors minimized nonactivating startup'
        Check ((ReadDraft $taskWindow 'Draft') -ceq 'Synthetic working draft' -and (ReadDraft $taskWindow 'Original') -ceq 'Synthetic working original') 'Working draft and original survive with session History'
        $taskCount=Get-SonaTransferCount $taskWindow
        Check ($taskCount.Recent -eq 4 -and $taskCount.Saved -eq 0) 'All four recent entries remain recent after restart and cleanup'
        Check (!(Test-Path -LiteralPath $taskHistory)) 'Temporary History storage is removed after live session verification'
        Check ((Get-FileHash -LiteralPath (Join-Path $taskProfile 'preferences.json')).Hash -eq $taskPreferencesHash) 'History storage stays Off with byte-identical original preferences'
        Check (@(Get-ChildItem -LiteralPath (Join-Path $OutputRoot 'recovery') -Filter '*.dpapi').Count -eq 0) 'Successful transfer removes only its completed encrypted recovery files'
        $taskState=Get-SonaTransferStatus $taskNew[0].Id
        Check (!$taskState.unsavedSettings -and !$taskState.microphoneControl) 'Completed synthetic update has no pending preference or microphone operation'
        $taskGuard=Join-Path (Join-Path $OutputRoot 'recovery') ('session-history-'+[Guid]::NewGuid().ToString('N')+'.dpapi')
        [IO.File]::WriteAllText($taskHistory,'[]')
        $taskRejected=$false;try{Save-SonaSessionHistory $taskNew[0].Id $taskProfile $taskGuard|Out-Null}catch{$taskRejected=$true}
        Check ($taskRejected -and [IO.File]::ReadAllText($taskHistory) -ceq '[]') 'Transfer refuses to replace or delete preexisting retained History'
        Remove-Item -LiteralPath $taskHistory
    }
    foreach($taskProcess in $taskProduction){$taskStill=Get-Process -Id $taskProcess.Id -ErrorAction Stop;Check ($taskStill.StartTime.ToUniversalTime().Ticks -eq $taskProcess.Started) 'Production Sona process lifetime remains unchanged'}
    [pscustomobject]@{Passed=$taskChecks.Count;Checks=$taskChecks;WithoutTransfer=[bool]$WithoutTransfer;FailurePoint=$FailurePoint;SyntheticOnly=$true;MicrophoneOpened=$false;GlobalInputSent=$false;DiscordTouched=$false;OwnerInstanceStopped=$false}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $OutputRoot 'session-upgrade-proof.json')
}catch{($_|Out-String)+$_.ScriptStackTrace|Set-Content -LiteralPath (Join-Path $OutputRoot 'failure.txt');throw}finally{
    foreach($taskProcess in @(Scoped)){
        $taskWindow=Get-SonaTransferWindow $taskProcess.Id
        (Get-SonaTransferField $taskWindow 'SettingsTab').GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
        (Get-SonaTransferField $taskWindow 'SettingsAboutNav').GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
        $taskQuit=$taskWindow.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.AndCondition]::new([Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Quit Sona'),[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Button)))
        if($taskQuit.Count -eq 1){$taskQuit[0].GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();[void]$taskProcess.WaitForExit(10000)}
    }
    $env:SONA_DATA=$taskPriorData
}
