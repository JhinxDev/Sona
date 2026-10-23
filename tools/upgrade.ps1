param([Parameter(Mandatory)][string]$PackageDirectory,[int]$ProcessId,[switch]$RecoverySelfTest,[string]$RehearsalRoot,[switch]$PreserveSessionHistory,[scriptblock]$BeforeSessionQuitForTests,[scriptblock]$QuitInvokeForTests)
$ErrorActionPreference='Stop'
if(($BeforeSessionQuitForTests -or $QuitInvokeForTests) -and !$RehearsalRoot){throw 'A transfer test hook requires an isolated rehearsal.'}
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName System.Security.Cryptography.ProtectedData
function SaveRecovery([string]$Payload,[string]$Directory){
    New-Item -ItemType Directory -Path $Directory -Force|Out-Null
    $taskRecovery=Join-Path $Directory ('upgrade-'+[guid]::NewGuid().ToString('N')+'.dpapi')
    $taskBytes=[Text.Encoding]::UTF8.GetBytes($Payload)
    try{[IO.File]::WriteAllBytes($taskRecovery,[Security.Cryptography.ProtectedData]::Protect($taskBytes,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser))}finally{[Array]::Clear($taskBytes,0,$taskBytes.Length)}
    return $taskRecovery
}
function ReadRecovery([string]$Path){$taskBytes=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($Path),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser);try{return [Text.Encoding]::UTF8.GetString($taskBytes)}finally{[Array]::Clear($taskBytes,0,$taskBytes.Length)}}
if($RecoverySelfTest){
    $taskTest=Join-Path $env:TEMP ('sona-upgrade-test-'+[guid]::NewGuid().ToString('N'))
    $taskFile=SaveRecovery '{"Draft":"fixture only","Original":"fixture original"}' $taskTest
    if((ReadRecovery $taskFile)-cne'{"Draft":"fixture only","Original":"fixture original"}'){throw 'Recovery round trip failed'}
    if([Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($taskFile)).Contains('fixture only')){throw 'Recovery was plaintext'}
    Remove-Item -LiteralPath $taskFile;Remove-Item -LiteralPath $taskTest
    Write-Output 'PASS current-user encrypted recovery round trip; no microphone or app update';return
}
$taskPackage=[IO.Path]::GetFullPath($PackageDirectory)
$taskPackageReceipt=Get-Content -LiteralPath (Join-Path $taskPackage 'package.json') -Raw|ConvertFrom-Json
if($taskPackageReceipt.Version-notmatch'^0\.[0-9]+\.[0-9]+$'-or$taskPackageReceipt.SourceHead-notmatch'^[a-f0-9]{40}$'){throw 'Unrecognized candidate receipt; current app preserved'}
foreach($taskEntry in $taskPackageReceipt.Files){$taskFile=[IO.Path]::GetFullPath((Join-Path $taskPackage $taskEntry.Path));if(!$taskFile.StartsWith($taskPackage+'\',[StringComparison]::OrdinalIgnoreCase)-or(Get-FileHash -LiteralPath $taskFile).Hash.ToLowerInvariant()-ne$taskEntry.SHA256){throw 'Candidate package ownership failed'}}
$taskInstances=@(Get-Process -Name Sona -ErrorAction SilentlyContinue)
if($ProcessId){$taskInstances=@($taskInstances|Where-Object Id -eq $ProcessId)}
if($taskInstances.Count-ne1){throw 'Select exactly one running Sona instance'}
$taskOld=$taskInstances[0];$taskOldPath=$taskOld.Path;$taskProgramRoot=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\Sona'))
if($RehearsalRoot){
    $RehearsalRoot=[IO.Path]::GetFullPath($RehearsalRoot)
    if((Split-Path $RehearsalRoot -Leaf)-notmatch'^sona-upgrade-rehearsal-[a-f0-9]{32}$'-or!(Test-Path -LiteralPath (Join-Path $RehearsalRoot '.sona-rehearsal'))-or[IO.Path]::GetFullPath($env:SONA_DATA)-ne(Join-Path $RehearsalRoot 'profile')){throw 'Unrecognized isolated rehearsal profile'}
    $taskProgramRoot=Join-Path $RehearsalRoot 'Programs\Sona'
}
if(!$taskOldPath.StartsWith($taskProgramRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected Sona install root'}
$taskOwned=Get-Content -LiteralPath (Join-Path (Split-Path $taskOldPath) 'installation.json') -Raw|ConvertFrom-Json
if($taskOwned.Path-ne$taskOldPath-or(Get-FileHash -LiteralPath $taskOldPath).Hash.ToLowerInvariant()-ne$taskOwned.SHA256){throw 'Current Sona ownership failed'}
if($taskOwned.SourceHead-eq$taskPackageReceipt.SourceHead){Write-Output 'This verified package is already installed';return}
$taskAgent=Join-Path (Split-Path $taskOldPath) 'sona-agent.exe'
function Idle{ $taskState=((& $taskAgent status)-join "`n"|ConvertFrom-Json);$taskVoice=((& $taskAgent protection status)-join "`n"|ConvertFrom-Json);return $taskState.ok-and$taskState.data.state-eq'idle'-and$taskVoice.ok-and!$taskVoice.data.ownsChanges }
if(!(Idle)){throw 'Current app is busy or owns voice changes; preserved'}
# Verify the target desktop view before normal Quit. A packaged caller can see a
# retained Discord setup that Explorer cannot see at the same logical path.
$taskProfileCheck=@{FilePath=$taskOldPath;CheckProfileOnly=$true}
if($RehearsalRoot){$taskProfileCheck.RehearsalRoot=$RehearsalRoot}
& (Join-Path $PSScriptRoot 'start-sona.ps1') @taskProfileCheck|Out-Null
# Refuse to discard session-only History. The explicit finite transfer uses the
# native save path, restores retention Off and removes only its staged file.
. (Join-Path $PSScriptRoot 'session-history-transfer.ps1')
$taskTransferState=Get-SonaTransferStatus $taskOld.Id
if($taskTransferState.unsavedSettings -or $taskTransferState.preferencesLoad -eq 'Unavailable'){throw 'Save pending settings before upgrade; current app preserved.'}
$taskTransferWindow=Get-SonaTransferWindow $taskOld.Id
$taskTransferCount=Get-SonaTransferCount $taskTransferWindow
(Get-SonaTransferField $taskTransferWindow 'SettingsTab').GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
(Get-SonaTransferField $taskTransferWindow 'SettingsPrivacyNav').GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
$taskKeepsHistory=(Get-SonaTransferField $taskTransferWindow 'KeepHistory').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -eq 'On'
$taskSessionStage=$null;$taskSessionResult=$null;$taskNew=$null;$taskQuitAttempted=$false
try{
if(!$taskKeepsHistory -and $taskTransferCount.Recent -gt 0){
    if(!$PreserveSessionHistory){throw 'Session-only History would be lost. Use the qualified finite session transfer before Quit.'}
    $taskProfile=Join-Path $env:LOCALAPPDATA 'Sona'
    if($RehearsalRoot){$taskProfile=Join-Path $RehearsalRoot 'profile'}
    elseif($env:SONA_DATA){$taskProfile=$env:SONA_DATA}
    elseif($env:MURMUR_DATA){$taskProfile=$env:MURMUR_DATA}
    elseif(!(Test-Path -LiteralPath (Join-Path $taskProfile 'preferences.json')) -and (Test-Path -LiteralPath (Join-Path $env:LOCALAPPDATA 'Murmur'))){$taskProfile=Join-Path $env:LOCALAPPDATA 'Murmur'}
    $taskSessionRecovery=Join-Path $env:LOCALAPPDATA 'Sona\recovery'
    if($RehearsalRoot){$taskSessionRecovery=Join-Path $RehearsalRoot 'recovery'}
    $taskSessionStage=Save-SonaSessionHistory $taskOld.Id $taskProfile (Join-Path $taskSessionRecovery ('session-history-'+[Guid]::NewGuid().ToString('N')+'.dpapi'))
}
if($BeforeSessionQuitForTests){& $BeforeSessionQuitForTests $taskOld.Id $taskProfile}
$taskRoot=[System.Windows.Automation.AutomationElement]::FromHandle($taskOld.MainWindowHandle)
if(!$taskRoot-or$taskRoot.Current.ProcessId-ne$taskOld.Id){throw 'Open Sona from its tray; hidden app was preserved'}
function ById([string]$Id){return $taskRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$Id))}
function SelectTab([string]$Id){(ById $Id).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()}
function Value([string]$Id){$taskField=ById $Id;if(!$taskField){throw 'Required draft field unavailable; app preserved'};return $taskField.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value}
SelectTab 'DictationTab';$taskDraft=Value 'Draft'
if(!(ById 'Original')){$taskExpanders=$taskRoot.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Original transcript'));foreach($taskExpander in $taskExpanders){$taskPattern=$null;if($taskExpander.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern,[ref]$taskPattern)){$taskPattern.Expand();break}}}
$taskOriginal=Value 'Original'
if($taskDraft.Length-gt6000-or$taskOriginal.Length-gt6000){throw 'Draft exceeds bounded transfer; app preserved'}
$taskPayload=[pscustomobject]@{Draft=$taskDraft;Original=$taskOriginal}|ConvertTo-Json -Compress
if($taskPayload.Length-gt30000){throw 'Transfer exceeds environment bound; app preserved'}
$taskRecoveryDirectory=Join-Path $env:LOCALAPPDATA 'Sona\recovery'
if($RehearsalRoot){$taskRecoveryDirectory=Join-Path $RehearsalRoot 'recovery'}
$taskRecovery=SaveRecovery $taskPayload $taskRecoveryDirectory
if((ReadRecovery $taskRecovery)-cne$taskPayload){throw 'Encrypted recovery readback differs; app preserved'}
if(!(Idle)-or(Value 'Draft')-cne$taskDraft){throw 'App or draft changed; app preserved; encrypted checkpoint retained'}
if($taskSessionStage){
    Confirm-SonaSessionHistory $taskSessionStage
    SelectTab 'DictationTab'
    if(!(Idle)-or(Value 'Draft')-cne$taskDraft){throw 'The app changed before Quit; preserve the session.'}
}
SelectTab 'SettingsTab'
$taskAbout=ById 'SettingsAboutNav'
if($taskAbout){$taskAbout.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()}
$taskQuit=$taskRoot.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.AndCondition]::new([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Quit Sona'),[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button)))
if($taskQuit.Count-ne1){throw 'Quit control not unique; app preserved'}
function LaunchPrivate([string]$Path,[string]$Checkpoint,[bool]$Rollback=$false){
    $launch=@{FilePath=$Path;RecoveryPath=$Checkpoint};if($RehearsalRoot){$launch.RehearsalRoot=$RehearsalRoot};if($Rollback -and !$RehearsalRoot){$launch.Hidden=$true}else{$launch.Minimized=$true}
    $launched=& (Join-Path $PSScriptRoot 'start-sona.ps1') @launch
    return Get-Process -Id $launched.PID -ErrorAction Stop
}
    $taskQuitAttempted=$true # Once Invoke may begin, keep both session recovery copies.
    if($QuitInvokeForTests){& $QuitInvokeForTests}else{$taskQuit[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()}
    if(!$taskOld.WaitForExit(15000)){throw 'Exit not confirmed; checkpoint retained'}
    $taskInstallArgs=@{PackageDirectory=$taskPackage};if($RehearsalRoot){$taskInstallArgs.RehearsalRoot=$RehearsalRoot}
    & (Join-Path $PSScriptRoot 'install.ps1') @taskInstallArgs|Out-Null
    $taskNewPath=Join-Path $taskProgramRoot ($taskPackageReceipt.Version+'-'+$taskPackageReceipt.SourceHead.Substring(0,8)+'\Sona.exe')
    $taskNew=LaunchPrivate $taskNewPath $taskRecovery
    $taskStartupId=$taskNew.Id;$taskStartupTicks=$taskNew.StartTime.ToUniversalTime().Ticks
    $taskStartupAgent=Join-Path (Split-Path $taskNewPath) 'sona-agent.exe'
    $taskStartupRead={
        return @{State=(((& $taskStartupAgent status)-join "`n")|ConvertFrom-Json);Voice=(((& $taskStartupAgent protection status)-join "`n")|ConvertFrom-Json)}
    }.GetNewClosure()
    $taskStartupOwned={
        $candidate=Get-Process -Id $taskStartupId -ErrorAction SilentlyContinue
        return $candidate -and $candidate.Path-eq$taskNewPath -and $candidate.StartTime.ToUniversalTime().Ticks-eq$taskStartupTicks
    }.GetNewClosure()
    $null=Wait-SonaOwnedStartup -ReadReply $taskStartupRead -IsExactProcess $taskStartupOwned -ExpectedVersion $taskPackageReceipt.Version -ExpectedPreferencesLoad $taskTransferState.preferencesLoad
    for($taskTry=0;$taskTry-lt100;$taskTry++){$taskNew.Refresh();if($taskNew.MainWindowHandle-ne0){break};if($taskNew.HasExited){throw 'Candidate exited'};Start-Sleep -Milliseconds 100}
    $taskNewRoot=[System.Windows.Automation.AutomationElement]::FromHandle($taskNew.MainWindowHandle)
    $taskNewField=$taskNewRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'Draft'))
    if(!$taskNewField-or$taskNewField.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value-cne$taskDraft){throw 'Draft readback differs; encrypted checkpoint retained'}
    if($taskSessionStage){$taskSessionResult=Complete-SonaSessionHistory $taskSessionStage $taskNew.Id}
    Remove-Item -LiteralPath $taskRecovery
    [pscustomobject]@{Version=$taskPackageReceipt.Version;PID=$taskNew.Id;SessionHistoryTransferred=$taskSessionResult.RecentPreserved;DraftPreserved=$true;MicrophoneOpened=$false;GlobalInputSent=$false}|ConvertTo-Json
}catch{
    $taskOld.Refresh()
    if(!$taskQuitAttempted -and !$taskOld.HasExited){
        if($taskSessionStage){Undo-SonaSessionHistory $taskSessionStage|Out-Null}
        throw [InvalidOperationException]::new('Upgrade stopped before Quit. The original session and retention choice are preserved.', $_.Exception)
    }
    $taskLaunchUncertain=[bool]$_.Exception.Data['DesktopLaunchUncertain']
    $taskRemaining=@(Get-Process -Name Sona -ErrorAction SilentlyContinue)
    if($RehearsalRoot){$taskRemaining=@($taskRemaining|Where-Object{$_.Path-and$_.Path.StartsWith($taskProgramRoot+'\',[StringComparison]::OrdinalIgnoreCase)})}
    if(!$taskRemaining.Count -and !$taskLaunchUncertain){LaunchPrivate $taskOldPath $taskRecovery $true|Out-Null}
    throw [InvalidOperationException]::new('Update did not qualify. Encrypted draft recovery is retained; any running app was preserved.', $_.Exception)
}finally{$taskDraft=$null;$taskOriginal=$null;$taskPayload=$null}
