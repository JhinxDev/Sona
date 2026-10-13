param([Parameter(Mandatory)][string]$OutputRoot)
$ErrorActionPreference='Stop';Add-Type -AssemblyName UIAutomationClient
if(Test-Path -LiteralPath $OutputRoot){throw 'Use a fresh rehearsal root'}
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
if((Split-Path $OutputRoot -Leaf)-notmatch'^sona-upgrade-rehearsal-[a-f0-9]{32}$'){throw 'Rehearsal root must have a unique guarded name'}
New-Item -ItemType Directory -Path $OutputRoot|Out-Null
Set-Content -LiteralPath (Join-Path $OutputRoot '.sona-rehearsal') -Value 'synthetic only; no production profile'
$taskRepo=Split-Path $PSScriptRoot -Parent;$taskRuntime='E:\DevelopmentTools\local-dictation'
$taskPriorData=$env:MURMUR_DATA;$env:MURMUR_DATA=Join-Path $OutputRoot 'profile'
$taskProductionAgent=Join-Path $env:LOCALAPPDATA 'Programs\Sona\current\sona-agent.exe'
$taskProductionBefore=(& $taskProductionAgent status)-join "`n"
$taskProductionPID=@(Get-Process -Name Sona|Where-Object{$_.Path.StartsWith((Join-Path $env:LOCALAPPDATA 'Programs\Sona')+'\',[StringComparison]::OrdinalIgnoreCase)}|Select-Object -ExpandProperty Id)
$taskUserPath=[Environment]::GetEnvironmentVariable('Path','User')
$taskRun=(Get-ItemProperty -LiteralPath HKCU:\Software\Microsoft\Windows\CurrentVersion\Run).Sona
$taskProductionCurrent=(Get-Item -LiteralPath (Join-Path $env:LOCALAPPDATA 'Programs\Sona\current')).Target
$taskMenu=Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Sona.lnk';$taskMenuHash=(Get-FileHash -LiteralPath $taskMenu).Hash
$taskChecks=[Collections.Generic.List[string]]::new()
function Proof([bool]$Value,[string]$Name){if(!$Value){throw $Name};$taskChecks.Add($Name)}
function ScopedProcess{ return @(Get-Process -Name Sona -ErrorAction SilentlyContinue|Where-Object{$_.Path-and$_.Path.StartsWith((Join-Path $OutputRoot 'Programs\Sona')+'\',[StringComparison]::OrdinalIgnoreCase)}) }
function Root($Process){for($taskTry=0;$taskTry-lt100;$taskTry++){$Process.Refresh();if($Process.MainWindowHandle-ne0){return [System.Windows.Automation.AutomationElement]::FromHandle($Process.MainWindowHandle)};if($Process.HasExited){throw 'Isolated process exited early'};Start-Sleep -Milliseconds 50};throw 'Isolated window unavailable'}
function Field($Window,[string]$Id){return $Window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$Id))}
function ReadField($Window,[string]$Id){
    $taskField=$null;for($taskFieldTry=0;$taskFieldTry-lt40;$taskFieldTry++){$taskField=Field $Window $Id;if($taskField){break};Start-Sleep -Milliseconds 50}
    if(!$taskField-and$Id-eq'Original'){$taskExpanders=$Window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Original transcript'));foreach($taskExpander in $taskExpanders){$taskPattern=$null;if($taskExpander.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern,[ref]$taskPattern)){$taskPattern.Expand();break}};$taskField=Field $Window $Id}
    if(!$taskField){throw "Synthetic field unavailable: $Id"};return $taskField.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
}
function SelectPage($Window,[string]$Id){(Field $Window $Id).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()}
function Recovery($Path){$taskBytes=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($Path),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser);try{return ([Text.Encoding]::UTF8.GetString($taskBytes)|ConvertFrom-Json)}finally{[Array]::Clear($taskBytes,0,$taskBytes.Length)}}
function PrivateLaunch([string]$Path,[string]$Payload){$taskInfo=[Diagnostics.ProcessStartInfo]::new($Path);$taskInfo.UseShellExecute=$false;$taskInfo.ArgumentList.Add('--test-ui');$taskInfo.ArgumentList.Add('--test-transfer');$taskInfo.Environment['MURMUR_DATA']=$env:MURMUR_DATA;$taskInfo.Environment['SONA_DRAFT_TRANSFER']=$Payload;return [Diagnostics.Process]::Start($taskInfo)}
try{
    $env:DOTNET_CLI_HOME=Join-Path $taskRuntime 'dotnet-home';$env:NUGET_PACKAGES=Join-Path $taskRuntime 'nuget';$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
    $taskBuilt=Join-Path $OutputRoot 'built'
    & (Join-Path $taskRuntime 'sdk\dotnet.exe') publish (Join-Path $taskRepo 'src\Murmur.App\Murmur.App.csproj') -c Release -o $taskBuilt --no-restore --disable-build-servers -p:UseSharedCompilation=false|Out-Null
    if($LASTEXITCODE-ne0){throw 'Isolated app publish failed'}
    & (Join-Path $taskRuntime 'sdk\dotnet.exe') publish (Join-Path $taskRepo 'src\Sona.Agent\Sona.Agent.csproj') -c Release -o $taskBuilt --no-restore --disable-build-servers -p:UseSharedCompilation=false|Out-Null
    if($LASTEXITCODE-ne0){throw 'Isolated agent publish failed'}
    $taskFiles=@(Get-ChildItem -LiteralPath $taskBuilt -File -Recurse|ForEach-Object{@{Path=[IO.Path]::GetRelativePath($taskBuilt,$_.FullName);SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant();Bytes=$_.Length}})
    foreach($taskFixture in @('a','b')){$taskPackage=Join-Path $OutputRoot ('package-'+$taskFixture);New-Item -ItemType Directory -Path $taskPackage|Out-Null;Copy-Item -Path (Join-Path $taskBuilt '*') -Destination $taskPackage -Recurse;@{Version='0.3.0';SourceHead=($taskFixture*40);Files=$taskFiles;SyntheticReceipt=$true}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $taskPackage 'package.json')}
    & (Join-Path $PSScriptRoot 'install.ps1') -PackageDirectory (Join-Path $OutputRoot 'package-a') -RehearsalRoot $OutputRoot|Out-Null
    $taskOldPath=Join-Path $OutputRoot 'Programs\Sona\0.3.0-aaaaaaaa\Sona.exe'
    $taskDraft="Synthetic café draft`nfoo_bar = 42";$taskOriginal="fixture original`nline two"
    $taskPayload=@{Draft=$taskDraft;Original=$taskOriginal}|ConvertTo-Json -Compress
    $taskOld=PrivateLaunch $taskOldPath $taskPayload;$taskWindow=Root $taskOld
    Proof ((ReadField $taskWindow 'Draft')-ceq$taskDraft-and(ReadField $taskWindow 'Original')-ceq$taskOriginal) 'Synthetic draft and original seed through the actual restore path'
    Proof (!(Field $taskWindow 'RecordButton').Current.IsEnabled) 'Isolated UI disables microphone capture'
    $taskAgent=Join-Path (Split-Path $taskOldPath) 'sona-agent.exe';$taskStatus=((& $taskAgent status)-join "`n"|ConvertFrom-Json);$taskVoice=((& $taskAgent protection status)-join "`n"|ConvertFrom-Json)
    Proof ($taskStatus.ok-and$taskStatus.data.residentProcessId-eq$null-and!$taskVoice.data.enabled-and!$taskVoice.data.connected) 'Isolated pipe has no resident model or Discord protection'
    $taskCurrent=Join-Path $OutputRoot 'Programs\Sona\current';if((Get-Item -LiteralPath $taskCurrent).LinkType-ne'Junction'){throw 'Unexpected isolated current type'}
    Remove-Item -LiteralPath $taskCurrent -Force;New-Item -ItemType Directory -Path $taskCurrent|Out-Null;Set-Content -LiteralPath (Join-Path $taskCurrent 'failure.marker') -Value 'synthetic deliberate unrelated-current failure'
    $taskFailed=$false;try{& (Join-Path $PSScriptRoot 'upgrade.ps1') -PackageDirectory (Join-Path $OutputRoot 'package-b') -ProcessId $taskOld.Id -RehearsalRoot $OutputRoot|Out-Null}catch{$taskFailed=$true}
    Proof ($taskFailed-and$taskOld.HasExited) 'Deliberate installer failure occurs after orderly Quit'
    $taskCheckpoints=@(Get-ChildItem -LiteralPath (Join-Path $OutputRoot 'recovery') -Filter '*.dpapi');Proof ($taskCheckpoints.Count-eq1) 'Failed update retains its pre-Quit encrypted checkpoint'
    $taskSaved=Recovery $taskCheckpoints[0].FullName;Proof ($taskSaved.Draft-ceq$taskDraft-and$taskSaved.Original-ceq$taskOriginal) 'Retained checkpoint exactly restores both synthetic fields'
    Proof (![Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($taskCheckpoints[0].FullName)).Contains('Synthetic café')) 'Retained checkpoint never stores plaintext fixture text'
    $taskFallback=@();for($taskTry=0;$taskTry-lt100;$taskTry++){$taskFallback=ScopedProcess;if($taskFallback.Count-eq1){break};Start-Sleep -Milliseconds 50}
    Proof ($taskFallback.Count-eq1-and$taskFallback[0].Path-eq$taskOldPath) 'Failure relaunches the isolated prior version despite the live owner instance'
    $taskWindow=Root $taskFallback[0];Proof ((ReadField $taskWindow 'Draft')-ceq$taskDraft-and(ReadField $taskWindow 'Original')-ceq$taskOriginal) 'Fallback live fields exactly match the synthetic source'
    Remove-Item -LiteralPath (Join-Path $taskCurrent 'failure.marker');Remove-Item -LiteralPath $taskCurrent;New-Item -ItemType Junction -Path $taskCurrent -Value (Split-Path $taskOldPath)|Out-Null
    & (Join-Path $PSScriptRoot 'upgrade.ps1') -PackageDirectory (Join-Path $OutputRoot 'package-b') -ProcessId $taskFallback[0].Id -RehearsalRoot $OutputRoot|Out-Null
    $taskNew=ScopedProcess;Proof ($taskNew.Count-eq1-and$taskNew[0].Path-ne$taskOldPath) 'Successful retry launches the isolated candidate'
    $taskWindow=Root $taskNew[0];Proof ((ReadField $taskWindow 'Draft')-ceq$taskDraft-and(ReadField $taskWindow 'Original')-ceq$taskOriginal) 'Successful candidate exactly preserves draft and original'
    Proof (@(Get-ChildItem -LiteralPath (Join-Path $OutputRoot 'recovery') -Filter '*.dpapi').Count-eq1) 'Success removes only its own checkpoint and preserves prior failed recovery'
    Proof ([Environment]::GetEnvironmentVariable('Path','User')-ceq$taskUserPath-and(Get-ItemProperty -LiteralPath HKCU:\Software\Microsoft\Windows\CurrentVersion\Run).Sona-ceq$taskRun-and(Get-FileHash -LiteralPath $taskMenu).Hash-eq$taskMenuHash-and(Get-Item -LiteralPath (Join-Path $env:LOCALAPPDATA 'Programs\Sona\current')).Target-ceq$taskProductionCurrent) 'Production PATH startup shortcut and current junction are unchanged'
    Proof (@($taskProductionPID|Where-Object{!(Get-Process -Id $_ -ErrorAction SilentlyContinue)}).Count-eq0) 'Owner live instance remains running'
    [pscustomobject]@{Passed=$taskChecks.Count;Checks=$taskChecks;SyntheticOnly=$true;SyntheticPackageIdentities=$true;MicrophoneOpened=$false;GlobalInputSent=$false;DiscordTouched=$false;OwnerInstanceStopped=$false}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $OutputRoot 'rehearsal.json')
}catch{($_|Out-String)+$_.ScriptStackTrace|Set-Content -LiteralPath (Join-Path $OutputRoot 'failure.txt');throw}finally{
    foreach($taskProcess in @(ScopedProcess)){$taskWindow=Root $taskProcess;SelectPage $taskWindow 'SettingsTab';$taskQuit=$taskWindow.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.AndCondition]::new([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Quit Sona'),[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button)));if($taskQuit.Count-eq1){$taskQuit[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();$taskProcess.WaitForExit(10000)|Out-Null}}
    $env:MURMUR_DATA=$taskPriorData
}
