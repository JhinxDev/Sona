param([Parameter(Mandatory)][string]$OutputDirectory,[switch]$DisplaysOnly)
$ErrorActionPreference='Stop'; $env:WINAPP_CLI_TELEMETRY_OPTOUT='1'
$taskRepo=Split-Path $PSScriptRoot -Parent
$taskExe=Join-Path $taskRepo 'src\Sona.App\bin\Release\net8.0-windows\Sona.exe'
$taskAgent=Join-Path $taskRepo 'src\Sona.Agent\bin\Release\net8.0-windows\sona-agent.exe'
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a fresh evidence directory.'}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$taskPrior=$env:SONA_DATA; $env:SONA_DATA=Join-Path $OutputDirectory 'data'
$taskChecks=[Collections.Generic.List[string]]::new()
function Proof([bool]$Value,[string]$Name){if(!$Value){throw $Name};$taskChecks.Add($Name)}
function Native([string[]]$Arguments){$taskRaw=winapp @Arguments --json; if($LASTEXITCODE-ne 0){throw ($taskRaw -join "`n")}; return (($taskRaw -join "`n")|ConvertFrom-Json)}
function Agent([string[]]$Arguments){return ((& $taskAgent @Arguments)-join "`n" | ConvertFrom-Json)}
function Nodes($Node){$Node; foreach($taskChild in $Node.children){Nodes $taskChild}}
$taskProcess=Start-Process -FilePath $taskExe -ArgumentList '--test-ui' -WindowStyle Hidden -PassThru
try{
    for($taskTry=0;$taskTry-lt40;$taskTry++){if((Agent @('status')).ok){break}; Start-Sleep -Milliseconds 100}
    $taskThemes = if($DisplaysOnly){@('charcoal')}else{@('charcoal','amethyst','fjord','mocha','oled','paper','linen','mist')}
    foreach($taskTheme in $taskThemes){
        $taskPatch=Agent @('settings','patch',('{"theme":"'+$taskTheme+'","recordingDisplay":"Panel","modelIdleMinutes":30}'))
        Proof ($taskPatch.ok -and $taskPatch.data.theme-eq$taskTheme) "Theme $taskTheme applies through the local agent interface"
        Native @('ui','invoke','DictationTab','-a',"$($taskProcess.Id)") | Out-Null
        Native @('ui','screenshot','-a',"$($taskProcess.Id)",'--output',(Join-Path $OutputDirectory ($taskTheme+'.png'))) | Out-Null
    }
    Native @('ui','invoke','SettingsTab','-a',"$($taskProcess.Id)") | Out-Null
    $taskAbout=Native @('ui','get-property','AboutVersion','-a',"$($taskProcess.Id)")
    $taskAppStatus=Agent @('status')
    Proof ($taskAbout.properties.Name-eq('Sona '+$taskAppStatus.data.version)) 'About agrees with the running assembly version; sidebar uses the same source and is checked visually'
    foreach($taskDisplay in @('Panel','Capsule')){
        $taskPatch=Agent @('settings','patch',('{"recordingDisplay":"'+$taskDisplay+'"}'))
        Proof ($taskPatch.ok -and $taskPatch.data.recordingDisplay-eq$taskDisplay) "$taskDisplay recording display persists through agent settings"
        Native @('ui','invoke','PreviewVoiceBar','-a',"$($taskProcess.Id)") | Out-Null
        $taskTree=Native @('ui','inspect','-a',"$($taskProcess.Id)",'-d','8')
        $taskBar=@($taskTree.windows | Where-Object {$_.title-eq'Sona recording display'})[0]
        Proof ($null-ne$taskBar) "$taskDisplay opens its non-microphone preview"
        Native @('ui','screenshot','-w',"$($taskBar.hwnd)",'--output',(Join-Path $OutputDirectory ($taskDisplay+'.png'))) | Out-Null
        $taskButtons=@($taskBar.elements | ForEach-Object{Nodes $_} | Where-Object{$_.name-eq'Cancel dictation' -and $_.type-eq'Button' -and $_.isInvokable -and !$_.isOffscreen})
        Proof ($taskButtons.Count-eq1) "$taskDisplay has exactly one accessible cancel action"
        Native @('ui','invoke',$taskButtons[0].selector,'-w',"$($taskBar.hwnd)") | Out-Null
    }
    Native @('ui','invoke','ModelsTab','-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','screenshot','-a',"$($taskProcess.Id)",'--output',(Join-Path $OutputDirectory 'models.png')) | Out-Null
    $taskModels=Agent @('models','list'); Proof (@($taskModels.data | Where-Object selected).Count-eq1) 'Exactly one local model is active for next use'
    Native @('ui','invoke','Close','-a',"$($taskProcess.Id)") | Out-Null
    Proof ($taskProcess.WaitForExit(10000)) 'Appearance test exits cleanly'
    [pscustomobject]@{Passed=$taskChecks.Count;Checks=$taskChecks;MicrophoneOpened=$false;GlobalInputSent=$false}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $OutputDirectory 'appearance-smoke.json')
}finally{
    if(!$taskProcess.HasExited){Native @('ui','invoke','Close','-a',"$($taskProcess.Id)")|Out-Null; $taskProcess.WaitForExit(10000)|Out-Null}
    $env:SONA_DATA=$taskPrior; $taskProcess.Dispose()
}
