param([Parameter(Mandatory)][string]$OutputDirectory,[switch]$Compact)
$ErrorActionPreference='Stop'; $env:WINAPP_CLI_TELEMETRY_OPTOUT='1'
$taskRepo=Split-Path $PSScriptRoot -Parent
$taskExe=Join-Path $taskRepo 'src\Murmur.App\bin\Release\net8.0-windows\Sona.exe'
$taskAgent=Join-Path $taskRepo 'src\Sona.Agent\bin\Release\net8.0-windows\sona-agent.exe'
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a fresh evidence directory.'}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$taskPriorData=$env:MURMUR_DATA; $env:MURMUR_DATA=Join-Path $OutputDirectory 'data'
$taskChecks=[Collections.Generic.List[string]]::new()
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
function Verify([bool]$Value,[string]$Name){if(!$Value){throw $Name};$taskChecks.Add($Name)}
function Agent([string[]]$Arguments){return ((& $taskAgent @Arguments)-join "`n" | ConvertFrom-Json)}
function Native([string[]]$Arguments){$taskRaw=winapp @Arguments --json;if($LASTEXITCODE-ne 0){throw ($taskRaw-join "`n")};return (($taskRaw-join "`n")|ConvertFrom-Json)}
function Element([string]$Id){
    $taskRoot=[System.Windows.Automation.AutomationElement]::FromHandle($taskProcess.MainWindowHandle)
    if($taskRoot.Current.ProcessId-ne $taskProcess.Id){throw 'Owned test window identity changed.'}
    $taskFound=$taskRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$Id))
    if(!$taskFound){throw "Missing owned control $Id"};return $taskFound
}
function Choices([string]$Id){
    $taskCombo=Element $Id
    $taskExpand=$taskCombo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $taskExpand.Expand()
    try{return @($taskCombo.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ListItem)) | ForEach-Object{$_.Current.Name})}
    finally{$taskExpand.Collapse()}
}
function Choose([string]$Id,[string]$Name){
    $taskCombo=Element $Id
    $taskExpand=$taskCombo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $taskExpand.Expand()
    try{
        $taskItem=$taskCombo.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$Name))
        if(!$taskItem){throw "Missing choice $Name"}
        $taskItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    }finally{$taskExpand.Collapse()}
}
function VisibleBounds([string]$Id){
    $taskWindow=[System.Windows.Automation.AutomationElement]::FromHandle($taskProcess.MainWindowHandle).Current.BoundingRectangle
    $taskControl=(Element $Id).Current.BoundingRectangle
    return $taskControl.Width-gt 0-and $taskControl.Height-gt 0-and $taskControl.Top-ge ($taskWindow.Top+38)-and $taskControl.Bottom-le ($taskWindow.Bottom-12)-and $taskControl.Left-ge $taskWindow.Left-and $taskControl.Right-le $taskWindow.Right
}
$taskLaunch=if($Compact){@('--test-ui','--compact')}else{@('--test-ui')}
$taskProcess=Start-Process -FilePath $taskExe -ArgumentList $taskLaunch -WindowStyle Hidden -PassThru
try{
    for($taskTry=0;$taskTry-lt50;$taskTry++){if((Agent @('status')).ok){break};Start-Sleep -Milliseconds 100}
    $taskDefault=Agent @('settings','get')
    Verify ($taskDefault.ok-and !$taskDefault.data.cleanupEnabled) 'Optional local cleanup starts disabled in a fresh profile'
    Verify ((Native @('ui','get-property','RecordButton','-a',"$($taskProcess.Id)")).properties.IsEnabled-eq 'False') 'Isolated native cleanup test disables microphone capture'
    Agent @('settings','patch','{"theme":"amethyst"}') | Out-Null
    Native @('ui','invoke','SettingsTab','-a',"$($taskProcess.Id)") | Out-Null
    Verify ((Choices 'CleanupToneChoice').Count-eq 3) 'Settings exposes three formatting choices'
    Verify ((Choices 'CleanupEngineChoice').Count-eq 2) 'Settings exposes GPU preference and explicit CPU processing'
    Verify ((Choices 'CleanupIdleChoice')-contains 'Keep until quit') 'Cleanup residency includes finite timeout and explicit Keep until quit'
    Choose 'CleanupToneChoice' 'Casual'; Choose 'CleanupEngineChoice' 'CPU / RAM'; Choose 'CleanupIdleChoice' 'Keep until quit'
    (Element 'CleanupEnabled').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Native @('ui','invoke','Save preferences','-a',"$($taskProcess.Id)") | Out-Null
    $taskSaved=$null
    for($taskTry=0;$taskTry-lt50;$taskTry++){
        if(Test-Path -LiteralPath (Join-Path $env:MURMUR_DATA 'preferences.json')){$taskSaved=Get-Content -LiteralPath (Join-Path $env:MURMUR_DATA 'preferences.json') -Raw | ConvertFrom-Json;if($taskSaved.CleanupEnabled-and $taskSaved.CleanupEngine-eq 'Cpu'-and $taskSaved.CleanupTone-eq 'casual'-and $taskSaved.CleanupIdleMinutes-eq 0){break}}
        Start-Sleep -Milliseconds 100
    }
    Verify ($taskSaved.CleanupEnabled-and $taskSaved.CleanupEngine-eq 'Cpu'-and $taskSaved.CleanupTone-eq 'casual'-and $taskSaved.CleanupIdleMinutes-eq 0) 'Semantic UI choices persist all four cleanup preferences'
    $taskAgentSettings=Agent @('settings','get')
    Verify ($taskAgentSettings.data.cleanupEnabled-and $taskAgentSettings.data.cleanupEngine-eq 'Cpu'-and $taskAgentSettings.data.cleanupTone-eq 'casual'-and $taskAgentSettings.data.cleanupIdleMinutes-eq 0) 'CLI agrees with UI-saved cleanup settings'
    $taskCleanup=Agent @('cleanup','status')
    Verify ($taskCleanup.ok-and !$taskCleanup.data.processId-and !$taskCleanup.data.installing) 'UI-only verification starts no cleanup worker or download'
    $taskMemory=Native @('ui','get-value','CleanupMemory','-a',"$($taskProcess.Id)")
    Verify ($taskMemory.text-match 'Installed|Set up'-and $taskMemory.text-ne 'Cleanup model memory status') 'Assistive tools read the actual cleanup memory state'
    $taskCursor=Element 'CleanupEnabled'; $taskScroll=$null
    while($taskCursor){$taskPattern=$null;if($taskCursor.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern,[ref]$taskPattern)){$taskScroll=$taskPattern;break};$taskCursor=[System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($taskCursor)}
    if(!$taskScroll){throw 'Settings scroll container unavailable.'}
    for($taskPercent=0;$taskPercent-le 100;$taskPercent++){
        $taskScroll.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll,$taskPercent)
        Start-Sleep -Milliseconds 30
        $taskWindow=[System.Windows.Automation.AutomationElement]::FromHandle($taskProcess.MainWindowHandle).Current.BoundingRectangle
        if((VisibleBounds 'CleanupEnabled')-and (VisibleBounds 'CleanupIdleChoice')-and (Element 'CleanupEnabled').Current.BoundingRectangle.Top-le ($taskWindow.Top+100)){break}
    }
    Verify ((VisibleBounds 'CleanupEnabled')-and (VisibleBounds 'CleanupIdleChoice')) 'Cleanup controls have visible bounds inside the Settings viewport'
    $taskBounds=(Element 'CleanupEngineChoice').Current.BoundingRectangle
    Verify ($taskBounds.Width-ge 180) 'Processing choices keep their full layout width'
    Native @('ui','screenshot','-a',"$($taskProcess.Id)",'--output',(Join-Path $OutputDirectory 'cleanup-settings.png')) | Out-Null
    $taskStatus=Agent @('status'); $taskAbout=Native @('ui','get-property','AboutVersion','-a',"$($taskProcess.Id)")
    Verify ($taskAbout.properties.Name-eq ('Sona '+$taskStatus.data.version)) 'Cleanup Settings About agrees with the running version'
    Native @('ui','invoke','Close','-a',"$($taskProcess.Id)") | Out-Null
    Verify ($taskProcess.WaitForExit(10000)) 'Isolated cleanup UI quits without leaving a hidden instance'
    [pscustomobject]@{Passed=$taskChecks.Count;Checks=$taskChecks;Compact=[bool]$Compact;MicrophoneOpened=$false;GlobalInputSent=$false;DiscordContacted=$false}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $OutputDirectory 'cleanup-ui-smoke.json')
}catch{$_|Out-String|Set-Content -LiteralPath (Join-Path $OutputDirectory 'failure.txt');throw}
finally{
    if(!$taskProcess.HasExited){$taskProcess.Refresh();$null=$taskProcess.CloseMainWindow();if(!$taskProcess.WaitForExit(10000)){throw 'Owned test instance did not quit.'}}
    $env:MURMUR_DATA=$taskPriorData; $taskProcess.Dispose()
}
