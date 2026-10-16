param([Parameter(Mandatory)][string]$PackageDirectory,[Parameter(Mandatory)][string]$OutputRoot,[Parameter(Mandatory)][string]$TestExecutable)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName System.Security.Cryptography.ProtectedData
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot);$PackageDirectory=[IO.Path]::GetFullPath($PackageDirectory)
if((Split-Path $OutputRoot -Leaf)-notmatch'^sona-upgrade-rehearsal-[a-f0-9]{32}$' -or (Test-Path -LiteralPath $OutputRoot)){throw 'Fresh guarded rehearsal root required.'}
$productionRoot=Join-Path $env:LOCALAPPDATA 'Programs\Sona'
$owners=@(Get-Process Sona -ErrorAction SilentlyContinue|Where-Object {$_.Path -and $_.Path.StartsWith($productionRoot+'\',[StringComparison]::OrdinalIgnoreCase)}|ForEach-Object {@{Id=$_.Id;Path=$_.Path;Started=$_.StartTime.Ticks}})
$profile=Join-Path $env:LOCALAPPDATA 'Sona\preferences.json';if(!(Test-Path -LiteralPath $profile)){$profile=Join-Path $env:LOCALAPPDATA 'Murmur\preferences.json'}
$profileHash=if(Test-Path -LiteralPath $profile){(Get-FileHash -LiteralPath $profile).Hash}else{$null}
$checks=[Collections.Generic.List[string]]::new()
function Proof([bool]$ok,[string]$label){if(!$ok){throw $label};$checks.Add($label)}
function TestApps{return @(Get-Process Sona -ErrorAction SilentlyContinue|Where-Object {$_.Path -and $_.Path.StartsWith($OutputRoot+'\',[StringComparison]::OrdinalIgnoreCase)})}
function Window($process){$process.Refresh();return [Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)}
function Field($window,[string]$id){return $window.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$id))}
function Quit($process){$window=Window $process;(Field $window 'SettingsTab').GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select();(Field $window 'SettingsAboutNav').GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select();$button=$window.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Quit Sona'));$button.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();if(!$process.WaitForExit(10000)){throw 'Exact fixture did not quit.'}}
try {
    New-Item -ItemType Directory -Path $OutputRoot|Out-Null;Set-Content -LiteralPath (Join-Path $OutputRoot '.sona-rehearsal') -Value 'Synthetic desktop lifetime check'
    $fixture=Join-Path $OutputRoot 'package-a';New-Item -ItemType Directory -Path $fixture|Out-Null
    $manifest=Get-Content -LiteralPath (Join-Path $PackageDirectory 'package.json') -Raw|ConvertFrom-Json
    foreach($file in $manifest.Files){$source=[IO.Path]::GetFullPath((Join-Path $PackageDirectory $file.Path));if(!$source.StartsWith($PackageDirectory+'\',[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $source).Hash.ToLowerInvariant()-ne$file.SHA256){throw 'Fixture input package differs.'};$destination=Join-Path $fixture $file.Path;New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force|Out-Null;Copy-Item -LiteralPath $source -Destination $destination}
    $manifest.Version='0.3.0';$manifest.SourceHead='a'*40;$manifest|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $fixture 'package.json')
    & (Join-Path $PSScriptRoot 'install.ps1') -PackageDirectory $fixture -RehearsalRoot $OutputRoot|Out-Null
    $data=Join-Path $OutputRoot 'profile';New-Item -ItemType Directory -Path $data|Out-Null
    '{"SoundEffects":false,"KeepHistory":false,"AutoInsert":false,"CopyToClipboard":false,"CloseToTray":false,"ProtectionEnabled":false}'|Set-Content -LiteralPath (Join-Path $data 'preferences.json')
    $recovery=Join-Path $OutputRoot 'recovery';New-Item -ItemType Directory -Path $recovery|Out-Null
    $checkpoint=Join-Path $recovery ('upgrade-'+[guid]::NewGuid().ToString('N')+'.dpapi');$draft="Synthetic cafe draft`nfoo_bar = 42";$payload=@{Draft=$draft;Original='Synthetic original'}|ConvertTo-Json -Compress
    $bytes=[Text.Encoding]::UTF8.GetBytes($payload);try{[IO.File]::WriteAllBytes($checkpoint,[Security.Cryptography.ProtectedData]::Protect($bytes,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser))}finally{[Array]::Clear($bytes,0,$bytes.Length)}
    $checkpointHash=(Get-FileHash -LiteralPath $checkpoint).Hash
    $launcher=Join-Path $OutputRoot 'launcher.ps1';$sourceLauncher=(Join-Path $PSScriptRoot 'start-sona.ps1').Replace("'","''")
    @"
param([string]`$FilePath,[string]`$RehearsalRoot,[string]`$RecoveryPath)
`$ErrorActionPreference='Stop'
& '$sourceLauncher' -FilePath `$FilePath -RehearsalRoot `$RehearsalRoot -RecoveryPath `$RecoveryPath | ConvertTo-Json -Compress
"@ | Set-Content -LiteralPath $launcher
    $cases=@()
    foreach($route in @('direct','explorer')){
        & $TestExecutable --desktop-launch-parent $OutputRoot $launcher $route
        if($LASTEXITCODE-ne0){throw ('Parent fixture failed: '+$route)}
        $receipt=Get-Content -LiteralPath (Join-Path $OutputRoot ($route+'-launch.json')) -Raw|ConvertFrom-Json
        Start-Sleep -Milliseconds 500
        $app=Get-Process -Id $receipt.PID -ErrorAction SilentlyContinue
        if($route-eq'direct'){Proof ($receipt.InLauncherJob -and !$app) 'Direct Sona control dies when its launcher job closes'}else{
            Proof (!$receipt.InLauncherJob -and $null-ne$app -and $app.Path-eq$receipt.Path) 'Desktop Sona survives closure of its former launcher job'
            $window=Window $app;$actual=(Field $window 'Draft').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value
            Proof ($actual-ceq$draft) 'Private draft transfer survives desktop delegation and parent exit'
            Proof (!(Field $window 'RecordButton').Current.IsEnabled) 'Isolated app cannot open the microphone'
            $command=(Get-CimInstance Win32_Process -Filter ('ProcessId='+$app.Id)).CommandLine
            Proof (!$command.Contains('Synthetic') -and !$command.Contains('foo_bar')) 'Draft is absent from the launched command line'
            Proof ((Get-FileHash -LiteralPath $checkpoint).Hash-eq$checkpointHash) 'Desktop launch preserves the encrypted upgrade recovery checkpoint'
            Proof (@(Get-ChildItem -LiteralPath $recovery -Filter 'desktop-launch-*').Count-eq0) 'Successful request and acknowledgement files are cleaned up'
            Quit $app;Proof (!(Get-Process -Id $receipt.PID -ErrorAction SilentlyContinue)) 'Only the exact isolated survivor is quit normally'
        }
        $cases+=@{Route=$route;PID=$receipt.PID;InLauncherJob=$receipt.InLauncherJob;Survived=($null-ne$app)}
    }
    $expired=Join-Path $recovery ('desktop-launch-'+[guid]::NewGuid().ToString('N')+'.dpapi')
    $expiredData=@{ExpiresUtc=[DateTimeOffset]::UtcNow.AddMinutes(-1).ToString('o');FilePath=Join-Path $OutputRoot 'Programs/Sona/0.3.0-aaaaaaaa/Sona.exe';RehearsalRoot=$OutputRoot;RecoveryPath=$checkpoint;Hidden=$false;Environment=@{}}
    $bytes=[Text.Encoding]::UTF8.GetBytes(($expiredData|ConvertTo-Json -Compress))
    try{[IO.File]::WriteAllBytes($expired,[Security.Cryptography.ProtectedData]::Protect($bytes,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser))}finally{[Array]::Clear($bytes,0,$bytes.Length)}
    & (Join-Path $PSScriptRoot 'start-sona.ps1') -BootstrapRequest $expired
    $rejected=Get-Content -LiteralPath ($expired+'.reply.json') -Raw|ConvertFrom-Json
    Proof (!$rejected.Ok -and !$rejected.LaunchMayHaveStarted -and @(TestApps).Count-eq0) 'Expired protected request cannot launch Sona'
    & (Join-Path $PSScriptRoot 'start-sona.ps1') -BootstrapRequest $expired
    Proof (!(Test-Path -LiteralPath $expired) -and !(Test-Path -LiteralPath ($expired+'.claimed')) -and @(TestApps).Count-eq0) 'An already consumed request cannot be replayed'
    Proof ((Get-FileHash -LiteralPath $checkpoint).Hash-eq$checkpointHash) 'Rejected and replayed requests preserve draft recovery'
    foreach($owner in $owners){$current=Get-Process -Id $owner.Id;Proof ($current.Path-eq$owner.Path -and $current.StartTime.Ticks-eq$owner.Started) 'Existing production instance and start time are preserved'}
    if($profileHash){Proof ((Get-FileHash -LiteralPath $profile).Hash-eq$profileHash) 'Owner preferences are byte-identical'}
    [pscustomobject]@{Passed=$checks.Count;Checks=$checks;Cases=$cases;MicrophoneOpened=$false;GlobalInputSent=$false;OwnerRestarted=$false}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $OutputRoot 'lifetime.json')
    Get-Content -LiteralPath (Join-Path $OutputRoot 'lifetime.json')
} finally {foreach($app in @(TestApps)){Quit $app}}
