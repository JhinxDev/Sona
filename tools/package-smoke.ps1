param([Parameter(Mandatory)][string]$PackageDirectory,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
$taskPackage=[IO.Path]::GetFullPath($PackageDirectory)
$taskOutput=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $taskOutput){throw 'Evidence exists'}
New-Item -ItemType Directory -Path $taskOutput | Out-Null
$taskReceipt=Get-Content -LiteralPath (Join-Path $taskPackage 'package.json') -Raw | ConvertFrom-Json
foreach($taskFile in $taskReceipt.Files){if((Get-FileHash -LiteralPath (Join-Path $taskPackage $taskFile.Path)).Hash.ToLowerInvariant()-ne$taskFile.SHA256){throw 'Package hash mismatch'}}
$taskPriorData=$env:SONA_DATA; $taskPriorLegacy=$env:MURMUR_DATA
$env:SONA_DATA=Join-Path $taskOutput 'profile'
$env:MURMUR_DATA=$env:SONA_DATA
$taskAgent=Join-Path $taskPackage 'sona-agent.exe'
$taskProcess=$null
function Agent([string[]]$Arguments){return ((& $taskAgent @Arguments)-join "`n"|ConvertFrom-Json)}
function Launch {
 $script:taskProcess=Start-Process -FilePath (Join-Path $taskPackage 'Sona.exe') -ArgumentList '--test-ui' -WindowStyle Hidden -PassThru
 for($taskTry=0;$taskTry-lt50;$taskTry++){$taskStatus=Agent @('status');if($taskStatus.ok){return $taskStatus};Start-Sleep -Milliseconds 100}
 throw 'Isolated package failed to start'
}
function CloseOwned {
 $taskProcess.Refresh()
 $taskRoot=[System.Windows.Automation.AutomationElement]::FromHandle($taskProcess.MainWindowHandle)
 if($taskRoot.Current.ProcessId-ne$taskProcess.Id){throw 'Owned process changed'}
 $taskTab=$taskRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'SettingsTab'))
 $taskTab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
 $taskAbout=$taskRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'SettingsAboutNav'))
 if($taskAbout){$taskAbout.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()}
 $taskQuit=$taskRoot.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.AndCondition]::new([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Quit Sona'),[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button)))
 if($taskQuit.Count-ne1){throw 'Quit not unique'}
 $taskQuit[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
 if(!$taskProcess.WaitForExit(10000)){throw 'Quit failed'}
 $script:taskProcess=$null
}
try{
 $taskFirst=Launch
 if($taskFirst.data.version-ne$taskReceipt.Version-or$taskFirst.data.buildId-notmatch'^[a-f0-9]{12}$'){throw 'Wrong candidate identity'}
 $taskPatch=Agent @('settings','patch','{"theme":"amethyst","liveTranscription":true,"pasteCompletedDraft":true,"enterToSend":true,"soundEffects":false}')
 if(!$taskPatch.ok){throw 'Isolated settings patch failed'}
 CloseOwned
 $taskSecond=Launch
 $taskSettings=Agent @('settings','get')
 if($taskSecond.data.buildId-ne$taskFirst.data.buildId-or$taskSettings.data.theme-ne'amethyst'-or!$taskSettings.data.liveTranscription-or!$taskSettings.data.pasteCompletedDraft-or!$taskSettings.data.enterToSend-or$taskSettings.data.soundEffects){throw 'Packaged preferences failed relaunch'}
 CloseOwned
 [pscustomobject]@{Passed=4;Checks=@('Every packaged file matches source receipt','Native packaged version and build identity verified','Theme and dictation settings survive packaged relaunch','Both isolated packaged launches quit cleanly');SourceHead=$taskReceipt.SourceHead;Version=$taskFirst.data.version;BuildId=$taskFirst.data.buildId;ExeSHA256=(Get-FileHash -LiteralPath (Join-Path $taskPackage 'Sona.exe')).Hash.ToLowerInvariant();MicrophoneOpened=$false;GlobalInputSent=$false;ProductionTouched=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskOutput 'package-verification.json')
 Get-Content -LiteralPath (Join-Path $taskOutput 'package-verification.json')
}finally{try{if($taskProcess-and!$taskProcess.HasExited){CloseOwned}}finally{$env:SONA_DATA=$taskPriorData;$env:MURMUR_DATA=$taskPriorLegacy}}
