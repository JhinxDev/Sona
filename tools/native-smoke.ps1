param(
    [string]$OutputDirectory = (Join-Path $env:TEMP ('murmur-native-' + [guid]::NewGuid().ToString('N'))),
    [string]$Winapp = 'C:\Users\qazws\AppData\Local\Microsoft\WindowsApps\winapp.exe',
    [string]$ReplayFile
)
$ErrorActionPreference = 'Stop'
$env:WINAPP_CLI_TELEMETRY_OPTOUT = '1'
$taskRepo = Split-Path $PSScriptRoot -Parent
$taskExe = Join-Path $taskRepo 'src\Murmur.App\bin\Release\net8.0-windows\Murmur.exe'
if (Get-Process -Name Murmur -ErrorAction SilentlyContinue) { throw 'Close the existing Murmur instance before running its isolated UI test.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$env:MURMUR_DATA = Join-Path $OutputDirectory 'preferences'
$taskChecks = [System.Collections.Generic.List[string]]::new()
function Native([string[]]$ActionArgs) {
    $taskRaw = & $Winapp @ActionArgs --json
    if ($LASTEXITCODE -ne 0) { throw ($taskRaw -join "`n") }
    $taskResult = ($taskRaw -join "`n") | ConvertFrom-Json
    if ($taskResult.error) { throw $taskResult.error.message }
    return $taskResult
}
function Nodes($Node) { $Node; foreach ($taskChild in $Node.children) { Nodes $taskChild } }
function Exact([string]$Name, [string]$Type) {
    $taskTree = Native @('ui','inspect','-a',"$($taskProcess.Id)",'--depth','8')
    $taskMatches = @($taskTree.windows | ForEach-Object { $_.elements } | ForEach-Object { Nodes $_ } | Where-Object { $_.name -ceq $Name -and $_.type -eq $Type })
    if ($taskMatches.Count -ne 1) { throw "Expected one $Type named $Name; found $($taskMatches.Count)." }
    return $taskMatches[0].selector
}
function Verify([bool]$Condition,[string]$Message) { if (!$Condition) { throw $Message }; $taskChecks.Add($Message); Write-Output "PASS $Message" }
function Launch([string[]]$LaunchArgs) {
    $script:taskProcess = Start-Process -FilePath $taskExe -ArgumentList $LaunchArgs -WindowStyle Hidden -PassThru
    for ($taskAttempt=0; $taskAttempt -lt 100; $taskAttempt++) { $taskProcess.Refresh(); if ($taskProcess.MainWindowHandle -ne 0) { return }; if ($taskProcess.HasExited) { throw 'Murmur exited before exposing its test window.' }; Start-Sleep -Milliseconds 50 }
    throw 'Test window did not start within five seconds.'
}
function CloseTest {
    Native @('ui','invoke','Close','-a',"$($taskProcess.Id)") | Out-Null
    Verify ($taskProcess.WaitForExit(10000)) 'Window close exits cleanly without a hidden test instance'
}
try {
    Launch @('--test-ui')
    $taskRecord = Native @('ui','get-property','RecordButton','-a',"$($taskProcess.Id)")
    Verify ($taskRecord.properties.IsEnabled -eq 'False') 'UI test mode disables microphone recording'
    Native @('ui','invoke','PrepareInsertionTest','-a',"$($taskProcess.Id)") | Out-Null
    $taskDraft = Native @('ui','get-value','Draft','-a',"$($taskProcess.Id)")
    Verify ($taskDraft.text -eq 'Murmur insertion test: bonjour Codex, foo_bar = 42. This is an unsent draft.') 'Selected tab exposes its accessible harmless draft controls'
    $taskVocabularyTab = Exact 'Vocabulary' 'TabItem'
    Native @('ui','invoke',$taskVocabularyTab,'-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','set-value','Vocabulary',"Kubernetes`npower shell = PowerShell",'-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','invoke',(Exact 'Save vocabulary' 'Button'),'-a',"$($taskProcess.Id)") | Out-Null
    for ($taskAttempt=0; $taskAttempt -lt 10 -and !(Test-Path -LiteralPath (Join-Path $env:MURMUR_DATA 'preferences.json')); $taskAttempt++) { Start-Sleep -Milliseconds 50 }
    if (!(Test-Path -LiteralPath (Join-Path $env:MURMUR_DATA 'preferences.json'))) { $taskResult = Native @('ui','get-value','VocabularyResult','-a',"$($taskProcess.Id)"); throw "Native save did not persist preferences: $($taskResult.text)" }
    $taskSettings = Get-Content -LiteralPath (Join-Path $env:MURMUR_DATA 'preferences.json') | ConvertFrom-Json
    Verify ($taskSettings.Terms -contains 'Kubernetes' -and $taskSettings.Vocabulary[0].Written -eq 'PowerShell') 'Native vocabulary save preserves separate hints and spelling corrections'
    CloseTest
    Launch @('--test-ui')
    Native @('ui','invoke',(Exact 'Vocabulary' 'TabItem'),'-a',"$($taskProcess.Id)") | Out-Null
    $taskVocabulary = Native @('ui','get-value','Vocabulary','-a',"$($taskProcess.Id)")
    Verify ($taskVocabulary.text.Replace("`r",'') -eq "Kubernetes`npower shell = PowerShell") 'Vocabulary persists through an actual app relaunch'
    CloseTest
    if ($ReplayFile) {
        if ($ReplayFile.Contains('"') -or !(Test-Path -LiteralPath $ReplayFile -PathType Leaf)) { throw 'Select an existing fixture WAV.' }
        Launch @('--test-ui','--replay',('"' + $ReplayFile + '"'))
        $taskChildren = @()
        for ($taskAttempt=0; $taskAttempt -lt 50 -and !$taskChildren.Count; $taskAttempt++) { $taskChildren = @(Get-CimInstance Win32_Process -Filter "ParentProcessId = $($taskProcess.Id)" | Where-Object { $_.Name -eq 'whisper-cli.exe' }); if (!$taskChildren.Count) { Start-Sleep -Milliseconds 50 } }
        Verify ($taskChildren.Count -eq 1) 'File replay starts one owned transcription worker'
        CloseTest
        Verify (!(Get-Process -Id $taskChildren[0].ProcessId -ErrorAction SilentlyContinue)) 'Quit during transcription leaves no worker'
        Verify (@(Get-ChildItem -LiteralPath (Join-Path $env:MURMUR_DATA 'scratch') -Filter audio.wav -Recurse -ErrorAction SilentlyContinue).Count -eq 0) 'Quit during transcription removes its temporary audio'
    }
    @{Passed=$taskChecks.Count; Checks=$taskChecks; MicrophoneOpened=$false; GlobalInputSent=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'native-smoke.json')
} finally {
    if ($taskProcess -and !$taskProcess.HasExited) {
        try { Native @('ui','invoke','Close','-a',"$($taskProcess.Id)") | Out-Null } catch { }
        if (!$taskProcess.WaitForExit(10000)) { if ($taskProcess.Path -eq $taskExe) { $taskProcess.Kill(); $taskProcess.WaitForExit() }; throw 'Native smoke failed to shut down gracefully; its exact owned test instance was terminated.' }
    }
}
