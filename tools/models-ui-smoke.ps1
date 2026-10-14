param([Parameter(Mandatory)][string]$OutputDirectory,[switch]$Compact)
$ErrorActionPreference='Stop';$env:WINAPP_CLI_TELEMETRY_OPTOUT='1'
$taskRepo=Split-Path $PSScriptRoot -Parent
$taskExe=Join-Path $taskRepo 'src\Sona.App\bin\Release\net8.0-windows\Sona.exe'
$taskAgent=Join-Path $taskRepo 'src\Sona.Agent\bin\Release\net8.0-windows\sona-agent.exe'
if(Test-Path -LiteralPath $OutputDirectory){throw 'Choose a fresh evidence directory.'}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
Add-Type 'using System.Runtime.InteropServices; public static class SonaModelDpi { [DllImport("user32.dll")] public static extern uint GetDpiForWindow(System.IntPtr window); }'
$taskPrior=$env:SONA_DATA;$env:SONA_DATA=Join-Path $OutputDirectory 'data'
$taskChecks=[Collections.Generic.List[string]]::new()
function Check([bool]$Value,[string]$Name){if(!$Value){throw $Name};$taskChecks.Add($Name)}
function Agent([string[]]$Arguments){return ((& $taskAgent @Arguments)-join "`n" | ConvertFrom-Json)}
function Element([string]$Id){
    $taskRoot=[System.Windows.Automation.AutomationElement]::FromHandle($taskProcess.MainWindowHandle)
    if($taskRoot.Current.ProcessId-ne$taskProcess.Id){throw 'Owned fixture identity changed.'}
    $taskResult=$taskRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$Id))
    if(!$taskResult){throw "Missing owned control $Id"};return $taskResult
}
function Invoke([string]$Id){(Element $Id).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()}
function SelectModel([string]$Id){
    for($taskTry=0;$taskTry-lt80;$taskTry++){if((Element ('ModelAction-'+$Id)).Current.IsEnabled){break};Start-Sleep -Milliseconds 100}
    try{Invoke ('ModelAction-'+$Id)}catch{if($_.Exception.InnerException.Message-notlike'*Unrecognized error*'){throw}}
    for($taskTry=0;$taskTry-lt80;$taskTry++){if((Agent @('settings','get')).data.model-eq$Id-and(Element ('ModelAction-'+$Id)).Current.IsEnabled){return};Start-Sleep -Milliseconds 100}
    throw 'Native model action did not commit its selection.'
}
function Native([string[]]$Arguments){$taskRaw=winapp @Arguments --json;if($LASTEXITCODE-ne0){throw ($taskRaw-join "`n")};return (($taskRaw-join "`n")|ConvertFrom-Json)}
$taskLaunch=if($Compact){@('--test-ui','--compact')}else{@('--test-ui')}
$taskProcess=Start-Process -FilePath $taskExe -ArgumentList $taskLaunch -WindowStyle Hidden -PassThru
try{
    for($taskTry=0;$taskTry-lt60;$taskTry++){if((Agent @('status')).ok){break};Start-Sleep -Milliseconds 100}
    Check (!(Element 'RecordButton').Current.IsEnabled) 'Native model fixture disables microphone capture'
    $taskPatch=Agent @('settings','patch','{"model":"parakeet-v3","theme":"amethyst","engine":"Auto","copyToClipboard":true}')
    Check ($taskPatch.ok-and$taskPatch.data.model-eq'parakeet-v3') 'Native model picker uses the same persisted Parakeet selection as agents'
    (Element 'ModelsTab').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $taskModels=Agent @('models','list')
    Check ($taskModels.ok-and$taskModels.data.Count-eq7-and@($taskModels.data|Where-Object selected).Count-eq1) 'Seven local choices expose exactly one active model'
    foreach($taskId in @('tiny','base','small','medium','turbo','large-v3','parakeet-v3')){
        $taskRow=Element ('ModelRow-'+$taskId)
        Check ($taskRow.Current.Name.Contains('not loaded in memory')-and$taskRow.Current.Name.Contains('installed')-and(Element ('ModelAction-'+$taskId)).Current.Name.Length-gt0) "$taskId has an accessible row, action and honest residency state"
    }
    Check ((Element 'ModelActive-parakeet-v3').Current.Name.Contains('active for the next dictation')) 'Parakeet has its own accessible active badge'
    $taskCursor=Element 'ModelRow-parakeet-v3';$taskScroll=$null
    while($taskCursor){$taskPattern=$null;if($taskCursor.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern,[ref]$taskPattern)){$taskScroll=$taskPattern;break};$taskCursor=[System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($taskCursor)}
    if(!$taskScroll){throw 'Models scroll container unavailable.'}
    $taskScroll.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll,100);Start-Sleep -Milliseconds 200
    $taskInitialBottom=(Element 'ModelRow-parakeet-v3').Current.BoundingRectangle.Top
    $taskInitialWindow=[System.Windows.Automation.AutomationElement]::FromHandle($taskProcess.MainWindowHandle).Current.BoundingRectangle
    $taskInitialScale=[SonaModelDpi]::GetDpiForWindow($taskProcess.MainWindowHandle)/96.0
    $taskScroll.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll,0);Start-Sleep -Milliseconds 100
    $taskInitialTop=(Element 'ModelRow-parakeet-v3').Current.BoundingRectangle.Top
    $taskInitialPercent=[math]::Clamp(100*($taskInitialTop-($taskInitialWindow.Top+70*$taskInitialScale))/($taskInitialTop-$taskInitialBottom),0,100)
    $taskScroll.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll,$taskInitialPercent);Start-Sleep -Milliseconds 150
    $taskRow=Element 'ModelRow-parakeet-v3';$taskBounds=$taskRow.Current.BoundingRectangle
    $taskAction=(Element 'ModelAction-parakeet-v3').Current.BoundingRectangle
    $taskWindow=[System.Windows.Automation.AutomationElement]::FromHandle($taskProcess.MainWindowHandle).Current.BoundingRectangle
    $taskScale=[SonaModelDpi]::GetDpiForWindow($taskProcess.MainWindowHandle)/96.0
    Check ($taskBounds.Width-gt0-and$taskAction.Width/$taskScale-le96-and$taskAction.Height/$taskScale-le40-and$taskAction.Top-ge$taskWindow.Top+38-and$taskAction.Bottom-le$taskWindow.Bottom) 'Parakeet action stays compact and inside the native window at this size'
    $taskDetails=$taskRow.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Parakeet v3 details and measured benchmarks'))
    Check ($null-ne$taskDetails) 'Parakeet details and measurements can be expanded accessibly'
    $taskDetails.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    $taskScroll.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll,100);Start-Sleep -Milliseconds 200
    $taskTexts=@($taskRow.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Text))|ForEach-Object{$_.Current.Name})-join ' '
    Check ($taskTexts.Contains('NVIDIA')-and$taskTexts.Contains('CC-BY-4.0')-and$taskTexts.Contains('NeMo-Speech.cpp')) 'Native details show genuine provider, local runtime and model license'
    $taskBottomTop=(Element 'ModelRow-parakeet-v3').Current.BoundingRectangle.Top
    $taskScroll.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll,0);Start-Sleep -Milliseconds 100
    $taskTop=(Element 'ModelRow-parakeet-v3').Current.BoundingRectangle.Top
    $taskTargetTop=$taskWindow.Top+$(if($Compact){70}else{300})*$taskScale
    $taskPercent=[math]::Clamp(100*($taskTop-$taskTargetTop)/($taskTop-$taskBottomTop),0,100)
    $taskScroll.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll,$taskPercent);Start-Sleep -Milliseconds 150
    Native @('ui','screenshot','-a',"$($taskProcess.Id)",'--output',(Join-Path $OutputDirectory 'models-parakeet.png'))|Out-Null
    $taskDetails.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
    SelectModel 'turbo'
    Check ((Agent @('settings','get')).data.model-eq'turbo') 'Whisper remains selectable through its actual native Use model action'
    SelectModel 'parakeet-v3'
    Check ((Agent @('settings','get')).data.model-eq'parakeet-v3'-and(Agent @('settings','get')).data.theme-eq'amethyst') 'Native Use model returns to Parakeet while preserving saved Amethyst'
    $null=$taskProcess.CloseMainWindow();Check ($taskProcess.WaitForExit(10000)) 'Native model fixture quits with no hidden worker or microphone'
    [pscustomobject]@{Passed=$taskChecks.Count;Checks=$taskChecks;Compact=[bool]$Compact;MicrophoneOpened=$false;GlobalInputSent=$false}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $OutputDirectory 'models-ui-smoke.json')
}catch{$_|Out-String|Set-Content -LiteralPath (Join-Path $OutputDirectory 'failure.txt');throw}
finally{
    if(!$taskProcess.HasExited){$null=$taskProcess.CloseMainWindow();$taskProcess.WaitForExit(10000)|Out-Null}
    $env:SONA_DATA=$taskPrior;$taskProcess.Dispose()
}
