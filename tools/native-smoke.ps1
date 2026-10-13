param([string]$OutputDirectory = (Join-Path $env:TEMP ('murmur-native-' + [guid]::NewGuid().ToString('N'))), [string]$Winapp = 'C:\Users\qazws\AppData\Local\Microsoft\WindowsApps\winapp.exe', [string]$ReplayFile)
$ErrorActionPreference = 'Stop'; $env:WINAPP_CLI_TELEMETRY_OPTOUT = '1'
$taskRepo = Split-Path $PSScriptRoot -Parent
$taskExe = Join-Path $taskRepo 'src\Murmur.App\bin\Release\net8.0-windows\Sona.exe'
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Choose a fresh evidence directory; preserve previous results.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$env:MURMUR_DATA = Join-Path $OutputDirectory 'preferences'
$taskChecks = [System.Collections.Generic.List[string]]::new()
function Native([string[]]$ActionArgs) {
    $taskRaw = & $Winapp @ActionArgs --json
    if ($LASTEXITCODE -ne 0) { throw ($taskRaw -join [Environment]::NewLine) }
    $taskResult = ($taskRaw -join [Environment]::NewLine) | ConvertFrom-Json
    if ($taskResult.error) { throw $taskResult.error.message }; return $taskResult
}
function Nodes($Node) { $Node; foreach ($taskChild in $Node.children) { Nodes $taskChild } }
function Exact([string]$Name, [string]$Type) {
    $taskTree = Native @('ui','inspect','-a',"$($taskProcess.Id)",'--depth','9')
    $taskMatches = @($taskTree.windows | ForEach-Object { $_.elements } | ForEach-Object { Nodes $_ } | Where-Object { $_.name -ceq $Name -and $_.type -eq $Type -and ($Type -ne 'ListItem' -or $_.isInvokable) })
    if ($taskMatches.Count -ne 1) { throw "Expected one $Type named $Name; found $($taskMatches.Count)." }; return $taskMatches[0].selector
}
function Verify([bool]$Condition,[string]$Message) { if (!$Condition) { throw $Message }; $taskChecks.Add($Message); Write-Output "PASS $Message" }
function CollapseCombo([string]$AutomationId) {
    Add-Type -AssemblyName UIAutomationClient
    $taskRoot=[System.Windows.Automation.AutomationElement]::FromHandle($taskProcess.MainWindowHandle)
    if ($taskRoot.Current.ProcessId -ne $taskProcess.Id) { throw 'Test window identity changed.' }
    $taskElement=$taskRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$AutomationId))
    if (!$taskElement) { throw 'Owned combo box was not found.' }
    $taskElement.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
}
function Launch([string[]]$LaunchArgs) {
    $script:taskProcess = Start-Process -FilePath $taskExe -ArgumentList $LaunchArgs -WindowStyle Hidden -PassThru
    for ($taskAttempt=0; $taskAttempt -lt 100; $taskAttempt++) { $taskProcess.Refresh(); if ($taskProcess.MainWindowHandle -ne 0) { return }; if ($taskProcess.HasExited) { throw 'Sona exited before exposing its test window.' }; Start-Sleep -Milliseconds 50 }
    throw 'Test window did not start within five seconds.'
}
function CloseTest { Native @('ui','invoke','Close','-w',"$($taskProcess.MainWindowHandle)") | Out-Null; Verify ($taskProcess.WaitForExit(10000)) 'Test window exits cleanly with no hidden instance' }
function Settings { Get-Content -LiteralPath (Join-Path $env:MURMUR_DATA 'preferences.json') -Raw | ConvertFrom-Json }
function WaitSettings([scriptblock]$Predicate) { for ($i=0;$i -lt 30;$i++) { if ((Test-Path -LiteralPath (Join-Path $env:MURMUR_DATA 'preferences.json')) -and (& $Predicate (Settings))) { return }; Start-Sleep -Milliseconds 50 }; throw 'Preference save did not reach its expected receipt' }
try {
    Launch @('--test-ui')
    Verify ((Native @('ui','get-property','RecordButton','-a',"$($taskProcess.Id)")).properties.IsEnabled -eq 'False') 'Isolated UI mode disables microphone recording'
    Native @('ui','screenshot','-a',"$($taskProcess.Id)",'--output',(Join-Path $OutputDirectory 'dictation.png')) | Out-Null
    Native @('ui','invoke','SettingsTab','-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','invoke','PreviewVoiceBar','-a',"$($taskProcess.Id)") | Out-Null
    $taskBarTree = Native @('ui','inspect','-a',"$($taskProcess.Id)",'--depth','5')
    $taskBar = @($taskBarTree.windows | Where-Object { $_.title -eq 'Sona recording display' })
    Verify ($taskBar.Count -eq 1) 'Nonactivating voice bar has its own accessible window without a microphone'
    Native @('ui','screenshot','-w',"$($taskBar[0].hwnd)",'--output',(Join-Path $OutputDirectory 'voice-bar.png')) | Out-Null
    Native @('ui','invoke',(Exact 'Cancel dictation' 'Button'),'-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','screenshot','-a',"$($taskProcess.Id)",'--output',(Join-Path $OutputDirectory 'settings.png')) | Out-Null
    Native @('ui','invoke','DictationTab','-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','set-value','Draft','Keep this unsent draft.','-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','invoke','SettingsTab','-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','invoke','PrepareInsertionTest','-a',"$($taskProcess.Id)") | Out-Null
    Verify ((Native @('ui','get-value','Draft','-a',"$($taskProcess.Id)")).text -eq 'Sona insertion test: bonjour Codex, foo_bar = 42. This is an unsent draft.') 'Prepared test uses a harmless fixed draft and returns to Dictation'
    Native @('ui','invoke','HistoryTab','-a',"$($taskProcess.Id)") | Out-Null
    Verify ((Exact 'Keep this unsent draft.' 'ListItem').Length -gt 0) 'Preparing the shortcut test retains the previous unsent draft in session history'
    Native @('ui','invoke','VocabularyTab','-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','set-value','WordInput','Kubernetes','-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','invoke',(Exact 'Add word' 'Button'),'-a',"$($taskProcess.Id)") | Out-Null
    WaitSettings { param($s) $s.Terms -contains 'Kubernetes' }
    Verify ((Settings).Terms -contains 'Kubernetes') 'Word form saves an exact recognition term'
    Native @('ui','set-value','WordInput','Kubernetes','-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','invoke',(Exact 'Add word' 'Button'),'-a',"$($taskProcess.Id)") | Out-Null
    Verify ((Native @('ui','get-value','VocabularyResult','-a',"$($taskProcess.Id)")).text -match 'unique' -and @((Settings).Terms | Where-Object { $_ -eq 'Kubernetes' }).Count -eq 1) 'Duplicate word is rejected visibly without changing saved entries'
    Native @('ui','set-value','WordInput','ffmpeg','-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','set-value','HeardInput','f f mpeg','-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','invoke',(Exact 'Add word' 'Button'),'-a',"$($taskProcess.Id)") | Out-Null
    WaitSettings { param($s) @($s.Vocabulary | Where-Object { $_.Spoken -eq 'f f mpeg' -and $_.Written -eq 'ffmpeg' }).Count -eq 1 }
    Verify (@((Settings).Vocabulary | Where-Object { $_.Written -eq 'ffmpeg' }).Count -eq 1) 'Heard-as form saves a separate explicit spelling correction'
    Native @('ui','screenshot','-a',"$($taskProcess.Id)",'--output',(Join-Path $OutputDirectory 'vocabulary.png')) | Out-Null
    Native @('ui','invoke','SnippetsTab','-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','set-value','SnippetTrigger','qa draft','-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','set-value','SnippetExpansion','Keep foo_bar unchanged.','-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','invoke','AddSnippetButton','-a',"$($taskProcess.Id)") | Out-Null
    WaitSettings { param($s) $s.Snippets.Trigger -contains 'qa draft' }
    Native @('ui','invoke',(Exact 'Edit qa draft' 'Button'),'-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','set-value','SnippetExpansion','Keep foo_bar and https://example.com unchanged.','-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','invoke','AddSnippetButton','-a',"$($taskProcess.Id)") | Out-Null
    WaitSettings { param($s) @($s.Snippets | Where-Object { $_.Trigger -eq 'qa draft' -and $_.Expansion -eq 'Keep foo_bar and https://example.com unchanged.' }).Count -eq 1 }
    Verify (@((Settings).Snippets | Where-Object { $_.Trigger -eq 'qa draft' }).Count -eq 1) 'Snippet add and edit update one saved record'
    Native @('ui','screenshot','-a',"$($taskProcess.Id)",'--output',(Join-Path $OutputDirectory 'snippets.png')) | Out-Null
    Native @('ui','invoke',(Exact 'Remove qa draft' 'Button'),'-a',"$($taskProcess.Id)") | Out-Null
    WaitSettings { param($s) $s.Snippets.Trigger -notcontains 'qa draft' }
    Verify ((Settings).Snippets.Count -eq 1) 'Removing a snippet preserves the unrelated built-in entry'
    CloseTest
    Launch @('--test-ui')
    Native @('ui','invoke','VocabularyTab','-a',"$($taskProcess.Id)") | Out-Null
    Verify ((Exact 'Remove Kubernetes' 'Button').Length -gt 0 -and (Settings).Vocabulary.Written -contains 'ffmpeg') 'Structured vocabulary survives an actual app relaunch'
    CloseTest
    Launch @('--test-ui')
    Native @('ui','invoke','SettingsTab','-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','invoke','KeepHistory','-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','invoke','ShortcutMode','-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','invoke',(Exact 'Toggle' 'ListItem'),'-a',"$($taskProcess.Id)") | Out-Null
    CollapseCombo 'ShortcutMode'
    Native @('ui','invoke',(Exact 'Save preferences' 'Button'),'-a',"$($taskProcess.Id)") | Out-Null
    WaitSettings { param($s) $s.KeepHistory -and $s.ShortcutMode -eq 'Toggle' }
    Verify ((Settings).KeepHistory -and (Settings).ShortcutMode -eq 'Toggle') 'Native settings save explicit toggle mode and opt-in text history'
    CloseTest
    $taskHistoryPath=Join-Path $env:MURMUR_DATA 'history.json'
    @(@{Id=[guid]::NewGuid().ToString();CreatedAt=[DateTimeOffset]::Now.ToString('o');Original='Bonjour le monde';Draft='Hello Codex';Inserted=$true},@{Id=[guid]::NewGuid().ToString();CreatedAt=[DateTimeOffset]::Now.AddMinutes(-1).ToString('o');Original='Use foo_bar';Draft='Use foo_bar';Inserted=$false}) | ConvertTo-Json | Set-Content -LiteralPath $taskHistoryPath -Encoding utf8
    Launch @('--test-ui')
    Native @('ui','invoke','HistoryTab','-a',"$($taskProcess.Id)") | Out-Null
    Verify ((Exact 'Hello Codex' 'ListItem').Length -gt 0 -and (Exact 'Use foo_bar' 'ListItem').Length -gt 0) 'Opt-in history loads bounded synthetic records in a dedicated page'
    Native @('ui','screenshot','-a',"$($taskProcess.Id)",'--output',(Join-Path $OutputDirectory 'history.png')) | Out-Null
    Native @('ui','set-value','HistorySearch','BONJOUR','-a',"$($taskProcess.Id)") | Out-Null
    Verify ((Exact 'Hello Codex' 'ListItem').Length -gt 0) 'History search matches the original transcript as well as the cleaned draft'
    Native @('ui','invoke',(Exact 'Hello Codex' 'ListItem'),'-a',"$($taskProcess.Id)") | Out-Null
    Verify ((Native @('ui','get-value','Draft','-a',"$($taskProcess.Id)")).text -eq 'Hello Codex') 'Opening a history result restores a draft without recording or automatic insertion'
    Native @('ui','invoke','HistoryTab','-a',"$($taskProcess.Id)") | Out-Null
    Native @('ui','invoke',(Exact 'Clear history' 'Button'),'-a',"$($taskProcess.Id)") | Out-Null
    for ($i=0;$i -lt 30 -and (Test-Path -LiteralPath $taskHistoryPath);$i++) { Start-Sleep -Milliseconds 50 }
    Verify (!(Test-Path -LiteralPath $taskHistoryPath) -and (Native @('ui','get-value','HistoryEmpty','-a',"$($taskProcess.Id)")).text -match 'No history yet') 'Clear history removes its exact saved file and empties the page'
    Native @('ui','invoke','DictationTab','-a',"$($taskProcess.Id)") | Out-Null
    Verify ((Native @('ui','get-value','Draft','-a',"$($taskProcess.Id)")).text -eq 'Hello Codex') 'Clearing history preserves the current draft'
    CloseTest
    if ($ReplayFile) {
        if ($ReplayFile.Contains('"') -or !(Test-Path -LiteralPath $ReplayFile -PathType Leaf)) { throw 'Select an existing public fixture WAV.' }
        Launch @('--test-ui','--replay',('"' + $ReplayFile + '"'))
        $taskReplay = Join-Path $env:MURMUR_DATA 'replay-result.json'
        for ($i=0;$i -lt 100 -and !(Test-Path -LiteralPath $taskReplay);$i++) { Start-Sleep -Milliseconds 50 }
        if (!(Test-Path -LiteralPath $taskReplay)) { throw 'Local file replay did not complete within five seconds.' }
        $taskResult = Get-Content -LiteralPath $taskReplay -Raw -Encoding utf8 | ConvertFrom-Json
        Verify ($taskResult.State -eq 'Ready' -and $taskResult.Original -match 'accident') 'Production GPU transcriber completes the licensed French file into a ready draft'
        Verify (@(Get-CimInstance Win32_Process -Filter "ParentProcessId = $($taskProcess.Id)" | Where-Object { $_.Name -eq 'whisper-server.exe' }).Count -eq 1) 'Completed production transcription retains exactly one resident model worker'
        Verify (@(Get-ChildItem -LiteralPath (Join-Path $env:MURMUR_DATA 'scratch') -Filter audio.wav -Recurse -ErrorAction SilentlyContinue).Count -eq 0) 'Completed production transcription deletes temporary audio'
        CloseTest
        $taskSettings=Settings; $taskSettings.Engine='FastCpu'; $taskSettings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $env:MURMUR_DATA 'preferences.json')
        Launch @('--test-ui','--replay',('"' + $ReplayFile + '"'))
        $taskChildren=@()
        for ($i=0;$i -lt 50 -and !$taskChildren.Count;$i++) { $taskChildren=@(Get-CimInstance Win32_Process -Filter "ParentProcessId = $($taskProcess.Id)" | Where-Object { $_.Name -eq 'whisper-server.exe' }); if (!$taskChildren.Count) { Start-Sleep -Milliseconds 50 } }
        Verify ($taskChildren.Count -eq 1) 'CPU option starts one contained transcription worker'
        CloseTest
        Verify (!(Get-Process -Id $taskChildren[0].ProcessId -ErrorAction SilentlyContinue)) 'Quit during CPU transcription leaves no worker'
        Verify (@(Get-ChildItem -LiteralPath (Join-Path $env:MURMUR_DATA 'scratch') -Filter audio.wav -Recurse -ErrorAction SilentlyContinue).Count -eq 0) 'Quit during CPU transcription removes its temporary audio'
    }
    @{Passed=$taskChecks.Count;Checks=$taskChecks;MicrophoneOpened=$false;GlobalInputSent=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'native-smoke.json')
} catch { $_ | Out-String | Set-Content -LiteralPath (Join-Path $OutputDirectory 'failure.txt'); throw } finally {
    if ($taskProcess -and !$taskProcess.HasExited) {
        try { Native @('ui','invoke','Close','-a',"$($taskProcess.Id)") | Out-Null } catch { }
        if (!$taskProcess.WaitForExit(10000)) { if ($taskProcess.Path -eq $taskExe) { $taskProcess.Kill(); $taskProcess.WaitForExit() }; throw 'Only the exact owned test instance was terminated after graceful closure failed.' }
    }
}
