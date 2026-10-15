param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a fresh output directory'}
$taskRepo=Split-Path $PSScriptRoot -Parent
$taskExe=Join-Path $taskRepo 'src/Sona.App/bin/Release/net8.0-windows/Sona.exe'
$taskAgent=Join-Path $taskRepo 'src/Sona.Agent/bin/Release/net8.0-windows/sona-agent.exe'
$taskPrior=$env:SONA_DATA
$env:SONA_DATA=Join-Path $OutputDirectory 'profile'
New-Item -ItemType Directory -Path $env:SONA_DATA -Force|Out-Null
$taskPreferences=Join-Path $env:SONA_DATA 'preferences.json'
Set-Content -LiteralPath $taskPreferences -Value '{invalid synthetic profile'
$taskBefore=(Get-FileHash -LiteralPath $taskPreferences).Hash
$taskChecks=[Collections.Generic.List[string]]::new()
function Verify([bool]$Value,[string]$Name){if(!$Value){throw $Name};$taskChecks.Add($Name)}
$taskProcess=$null
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
try{
    $taskProcess=Start-Process -FilePath $taskExe -ArgumentList '--test-ui','--start-hidden' -WindowStyle Hidden -PassThru
    $taskState=$null
    for($taskTry=0;$taskTry-lt30;$taskTry++){
        $taskState=((& $taskAgent status)-join "`n"|ConvertFrom-Json)
        if($taskState.ok){break};Start-Sleep -Milliseconds 100
    }
    Verify ($taskState.ok-and$taskState.data.preferencesLoad-eq'Unavailable') 'Unavailable profile is explicit in native status'
    $taskProcess.Refresh()
    Verify ($taskProcess.MainWindowHandle-ne0) 'Failed profile remains visible despite start-hidden'
    $taskRoot=[System.Windows.Automation.AutomationElement]::FromHandle($taskProcess.MainWindowHandle)
    $taskWarning=$taskRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'ProfileWarning'))
    Verify ($taskWarning-and$taskWarning.Current.Name.Contains('preserved')) 'Recovery warning explains saved data preservation'
    $taskPatch=((& $taskAgent settings patch '{"theme":"amethyst"}')-join "`n"|ConvertFrom-Json)
    Verify (!$taskPatch.ok-and(Get-FileHash -LiteralPath $taskPreferences).Hash-eq$taskBefore) 'Settings patch cannot replace an unreadable profile'
    $taskProcess.CloseMainWindow()|Out-Null
    Verify ($taskProcess.WaitForExit(10000)) 'Closing recovery window quits rather than stranding a trayless instance'
    Verify ((Get-FileHash -LiteralPath $taskPreferences).Hash-eq$taskBefore) 'Original unreadable bytes remain intact after quit'
    [pscustomobject]@{Passed=$taskChecks.Count;Checks=$taskChecks;MicrophoneOpened=$false;GlobalInputSent=$false;ProductionProfileChanged=$false}|ConvertTo-Json -Depth 3|Set-Content -LiteralPath (Join-Path $OutputDirectory 'profile-recovery.json')
}catch{$_|Out-String|Set-Content -LiteralPath (Join-Path $OutputDirectory 'failure.txt');throw}
finally{
    if($taskProcess-and!$taskProcess.HasExited){$taskProcess.CloseMainWindow()|Out-Null;if(!$taskProcess.WaitForExit(10000)){throw 'Owned recovery test instance remains'}}
    $env:SONA_DATA=$taskPrior
}
