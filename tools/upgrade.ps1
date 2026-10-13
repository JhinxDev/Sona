param([Parameter(Mandatory)][string]$PackageDirectory,[int]$ProcessId,[switch]$RecoverySelfTest,[string]$RehearsalRoot)
$ErrorActionPreference='Stop'
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
    if((Split-Path $RehearsalRoot -Leaf)-notmatch'^sona-upgrade-rehearsal-[a-f0-9]{32}$'-or!(Test-Path -LiteralPath (Join-Path $RehearsalRoot '.sona-rehearsal'))-or[IO.Path]::GetFullPath($env:MURMUR_DATA)-ne(Join-Path $RehearsalRoot 'profile')){throw 'Unrecognized isolated rehearsal profile'}
    $taskProgramRoot=Join-Path $RehearsalRoot 'Programs\Sona'
}
if(!$taskOldPath.StartsWith($taskProgramRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected Sona install root'}
$taskOwned=Get-Content -LiteralPath (Join-Path (Split-Path $taskOldPath) 'installation.json') -Raw|ConvertFrom-Json
if($taskOwned.Path-ne$taskOldPath-or(Get-FileHash -LiteralPath $taskOldPath).Hash.ToLowerInvariant()-ne$taskOwned.SHA256){throw 'Current Sona ownership failed'}
if($taskOwned.SourceHead-eq$taskPackageReceipt.SourceHead){Write-Output 'This verified package is already installed';return}
$taskAgent=Join-Path (Split-Path $taskOldPath) 'sona-agent.exe'
function Idle{ $taskState=((& $taskAgent status)-join "`n"|ConvertFrom-Json);$taskVoice=((& $taskAgent protection status)-join "`n"|ConvertFrom-Json);return $taskState.ok-and$taskState.data.state-eq'idle'-and$taskVoice.ok-and!$taskVoice.data.ownsChanges }
if(!(Idle)){throw 'Current app is busy or owns voice changes; preserved'}
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
SelectTab 'SettingsTab'
$taskQuit=$taskRoot.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.AndCondition]::new([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Quit Sona'),[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button)))
if($taskQuit.Count-ne1){throw 'Quit control not unique; app preserved'}
function LaunchPrivate([string]$Path,[string]$Payload){$taskLaunch=[Diagnostics.ProcessStartInfo]::new($Path);$taskLaunch.UseShellExecute=$false;$taskLaunch.Environment['SONA_DRAFT_TRANSFER']=$Payload;if($RehearsalRoot){$taskLaunch.ArgumentList.Add('--test-ui');$taskLaunch.ArgumentList.Add('--test-transfer');$taskLaunch.Environment['MURMUR_DATA']=Join-Path $RehearsalRoot 'profile'};try{return [Diagnostics.Process]::Start($taskLaunch)}finally{$taskLaunch.Environment.Remove('SONA_DRAFT_TRANSFER')|Out-Null}}
$taskNew=$null
try{
    $taskQuit[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    if(!$taskOld.WaitForExit(15000)){throw 'Exit not confirmed; checkpoint retained'}
    $taskInstallArgs=@{PackageDirectory=$taskPackage};if($RehearsalRoot){$taskInstallArgs.RehearsalRoot=$RehearsalRoot}
    & (Join-Path $PSScriptRoot 'install.ps1') @taskInstallArgs|Out-Null
    $taskNewPath=Join-Path $taskProgramRoot ($taskPackageReceipt.Version+'-'+$taskPackageReceipt.SourceHead.Substring(0,8)+'\Sona.exe')
    $taskNew=LaunchPrivate $taskNewPath (ReadRecovery $taskRecovery)
    for($taskTry=0;$taskTry-lt100;$taskTry++){$taskNew.Refresh();if($taskNew.MainWindowHandle-ne0){break};if($taskNew.HasExited){throw 'Candidate exited'};Start-Sleep -Milliseconds 100}
    $taskNewRoot=[System.Windows.Automation.AutomationElement]::FromHandle($taskNew.MainWindowHandle)
    $taskNewField=$taskNewRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'Draft'))
    if(!$taskNewField-or$taskNewField.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value-cne$taskDraft){throw 'Draft readback differs; encrypted checkpoint retained'}
    Remove-Item -LiteralPath $taskRecovery
    [pscustomobject]@{Version=$taskPackageReceipt.Version;PID=$taskNew.Id;DraftPreserved=$true;MicrophoneOpened=$false;GlobalInputSent=$false}|ConvertTo-Json
}catch{
    $taskRemaining=@(Get-Process -Name Sona -ErrorAction SilentlyContinue)
    if($RehearsalRoot){$taskRemaining=@($taskRemaining|Where-Object{$_.Path-and$_.Path.StartsWith($taskProgramRoot+'\',[StringComparison]::OrdinalIgnoreCase)})}
    if(!$taskRemaining.Count){LaunchPrivate $taskOldPath (ReadRecovery $taskRecovery)|Out-Null}
    throw 'Update did not qualify. Encrypted draft recovery is retained; any running app was preserved.'
}finally{$taskDraft=$null;$taskOriginal=$null;$taskPayload=$null}
