param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'; $env:WINAPP_CLI_TELEMETRY_OPTOUT='1'
$taskRepo=Split-Path $PSScriptRoot -Parent
$taskExe=Join-Path $taskRepo 'src\Sona.App\bin\Release\net8.0-windows\Sona.exe'
$taskAgent=Join-Path $taskRepo 'src\Sona.Agent\bin\Release\net8.0-windows\sona-agent.exe'
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a fresh evidence directory.'}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$taskPrior=$env:SONA_DATA; $env:SONA_DATA=Join-Path $OutputDirectory 'data'
$taskChecks=[Collections.Generic.List[string]]::new(); $taskProcess=$null
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
function Verify([bool]$Value,[string]$Name){if(!$Value){throw $Name};$taskChecks.Add($Name)}
function Agent([string[]]$Arguments){return ((& $taskAgent @Arguments)-join "`n"|ConvertFrom-Json)}
function Native([string[]]$Arguments){$taskRaw=winapp @Arguments --json;if($LASTEXITCODE-ne 0){throw ($taskRaw-join "`n")};return (($taskRaw-join "`n")|ConvertFrom-Json)}
function Element([string]$Id){
    $taskRoot=[System.Windows.Automation.AutomationElement]::FromHandle($taskProcess.MainWindowHandle)
    if($taskRoot.Current.ProcessId-ne $taskProcess.Id){throw 'Owned window identity changed.'}
    $taskFound=$taskRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$Id))
    if(!$taskFound){throw "Missing owned control $Id"};return $taskFound
}
function Choose([string]$Id,[string]$Name){
    $taskCombo=Element $Id; $taskExpand=$taskCombo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $taskExpand.Expand()
    try{
        $taskItem=$taskCombo.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$Name))
        if(!$taskItem){throw "Missing choice $Name"}
        $taskItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    }finally{$taskExpand.Collapse()}
}
function Selected([string]$Id){return (Element $Id).GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()[0].Current.Name}
function Launch{
    $script:taskProcess=Start-Process -FilePath $taskExe -ArgumentList '--test-ui' -WindowStyle Hidden -PassThru
    for($taskTry=0;$taskTry-lt50;$taskTry++){if((Agent @('status')).ok){break};Start-Sleep -Milliseconds 100}
    Native @('ui','invoke','SettingsTab','-a',"$($taskProcess.Id)")|Out-Null
}
function CloseOwned{
    Native @('ui','invoke','Close','-a',"$($taskProcess.Id)")|Out-Null
    if(!$taskProcess.WaitForExit(10000)){throw 'Owned theme instance did not quit.'}
    $taskProcess.Dispose();$script:taskProcess=$null
}
try{
    Launch
    $taskPatch=Agent @('settings','patch','{"language":"fr","theme":"charcoal"}')
    Verify ($taskPatch.ok) 'Theme regression starts from a known persisted profile'
    Choose 'SpeechLanguage' 'English'
    Choose 'ThemeChoice' 'Amethyst'
    $taskSaved=Get-Content -LiteralPath (Join-Path $env:SONA_DATA 'preferences.json') -Raw|ConvertFrom-Json
    $taskRead=Agent @('settings','get')
    Verify ($taskSaved.Theme-eq 'amethyst'-and $taskRead.data.theme-eq 'amethyst') 'Choosing Amethyst saves it immediately without Save preferences'
    Verify ($taskSaved.Language-eq 'fr'-and (Selected 'SpeechLanguage')-eq 'English') 'Theme save preserves other unsaved form choices and their prior persisted values'
    CloseOwned; Launch
    $taskRead=Agent @('settings','get')
    Verify ($taskRead.data.theme-eq 'amethyst'-and (Selected 'ThemeChoice')-eq 'Amethyst') 'Saved Amethyst restores after an actual app relaunch'
    $taskPreferenceFile=Join-Path $env:SONA_DATA 'preferences.json'
    Choose 'SpeechLanguage' 'English'
    $taskPriorResult=(Native @('ui','get-value','SettingsResult','-a',"$($taskProcess.Id)")).text
    $taskLock=[IO.File]::Open($taskPreferenceFile,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    try{
        Native @('ui','invoke','Save preferences','-a',"$($taskProcess.Id)")|Out-Null
        # InvokePattern queues WPF Click. Keep the lock until that handler
        # reports its result, rather than releasing at the invoke return.
        for($taskTry=0;$taskTry-lt50;$taskTry++){
            $taskSaveResult=(Native @('ui','get-value','SettingsResult','-a',"$($taskProcess.Id)")).text
            if($taskSaveResult-and $taskSaveResult-ne $taskPriorResult){break}
            Start-Sleep -Milliseconds 100
        }
        [pscustomobject]@{PriorResult=$taskPriorResult;SaveResult=$taskSaveResult;PersistedLanguage=(Get-Content -LiteralPath $taskPreferenceFile -Raw|ConvertFrom-Json).Language;ActiveLanguage=(Agent @('settings','get')).data.language}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $OutputDirectory 'locked-save-readback.json')
        Verify ($taskSaveResult-ne $taskPriorResult-and $taskSaveResult-match 'denied|cannot access|being used|used by another') 'Locked-file general Save reports its completed failure before the lock releases'
    }finally{$taskLock.Dispose()}
    $taskRead=Agent @('settings','get'); $taskSaved=Get-Content -LiteralPath $taskPreferenceFile -Raw|ConvertFrom-Json
    Verify ($taskRead.data.language-eq 'fr'-and $taskSaved.Language-eq 'fr'-and (Selected 'SpeechLanguage')-eq 'English') 'Failed general Save keeps active and persisted preferences unchanged while preserving form edits'
    Choose 'ThemeChoice' 'Fjord'
    $taskRead=Agent @('settings','get'); $taskSaved=Get-Content -LiteralPath $taskPreferenceFile -Raw|ConvertFrom-Json
    Verify ($taskRead.data.language-eq 'fr'-and $taskSaved.Language-eq 'fr'-and $taskSaved.Theme-eq 'fjord') 'A later theme save cannot commit choices from a failed general Save'
    Choose 'ThemeChoice' 'Amethyst'
    $taskLock=[IO.File]::Open($taskPreferenceFile,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    try{Choose 'ThemeChoice' 'Linen'}finally{$taskLock.Dispose()}
    $taskRead=Agent @('settings','get'); $taskSaved=Get-Content -LiteralPath $taskPreferenceFile -Raw|ConvertFrom-Json
    Verify ($taskSaved.Theme-eq 'amethyst'-and $taskRead.data.theme-eq 'amethyst'-and (Selected 'ThemeChoice')-eq 'Amethyst') 'Failed theme persistence restores the saved selection and active theme'
    $taskError=Native @('ui','get-value','SettingsResult','-a',"$($taskProcess.Id)")
    Verify ($taskError.text-match 'could not be saved') 'Failed theme persistence is reported visibly'
    Choose 'ThemeChoice' 'Paper'; Choose 'ThemeChoice' 'Amethyst'
    Verify ((Agent @('settings','get')).data.theme-eq 'amethyst') 'Theme can be saved again after the temporary lock releases'
    Native @('ui','invoke','Save preferences','-a',"$($taskProcess.Id)")|Out-Null
    $taskRead=Agent @('settings','get'); $taskSaved=Get-Content -LiteralPath $taskPreferenceFile -Raw|ConvertFrom-Json
    Verify ($taskRead.data.language-eq 'en'-and $taskSaved.Language-eq 'en'-and $taskSaved.Theme-eq 'amethyst') 'Explicit general Save commits form choices after the temporary lock releases'
    Native @('ui','screenshot','-a',"$($taskProcess.Id)",'--output',(Join-Path $OutputDirectory 'amethyst-restored.png'))|Out-Null
    CloseOwned
    Verify ($null-eq $taskProcess) 'Theme regression exits all owned instances'
    [pscustomobject]@{Passed=$taskChecks.Count;Checks=$taskChecks;MicrophoneOpened=$false;GlobalInputSent=$false;DiscordContacted=$false}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $OutputDirectory 'theme-persistence-smoke.json')
}catch{$_|Out-String|Set-Content -LiteralPath (Join-Path $OutputDirectory 'failure.txt');throw}
finally{
    if($taskProcess-and !$taskProcess.HasExited){$null=$taskProcess.CloseMainWindow();if(!$taskProcess.WaitForExit(10000)){throw 'Owned test instance remains.'}}
    $env:SONA_DATA=$taskPrior
}
