param([Parameter(Mandatory)][string]$OutputDirectory,[switch]$Baseline)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path $PSScriptRoot -Parent
$taskOutput=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $taskOutput){throw 'Use a fresh path-check output directory'}
New-Item -ItemType Directory -Path $taskOutput|Out-Null
if(-not $Baseline){. (Join-Path $taskRepo 'tools\resolve-physical-file.ps1')}
$taskRoot=Join-Path $env:LOCALAPPDATA 'Sona\recovery'
New-Item -ItemType Directory -Path $taskRoot -Force|Out-Null
$taskLogical=Join-Path $taskRoot ('path-check-'+[Guid]::NewGuid().ToString('N')+'.txt')
$taskChecks=[Collections.Generic.List[string]]::new();$taskFailures=[Collections.Generic.List[string]]::new()
function Check([bool]$Value,[string]$Label){if($Value){$taskChecks.Add($Label)}else{$taskFailures.Add($Label)}}
$taskPhysical=$null
try {
 [IO.File]::WriteAllText($taskLogical,'Synthetic Sona path visibility fixture.',[Text.UTF8Encoding]::new($false))
 $taskCandidate=if($Baseline){[IO.Path]::GetFullPath($taskLogical)}else{Resolve-SonaPhysicalFile $taskLogical}
 $taskPhysical=$taskCandidate
 Check ([IO.File]::Exists($taskCandidate)) 'The returned file exists in the calling process'
 Check ((Split-Path $taskCandidate -Leaf) -eq (Split-Path $taskLogical -Leaf)) 'Physical normalization preserves the unique fixture filename'
 if(-not $Baseline){
  Check ((Resolve-SonaPhysicalFile $taskCandidate) -eq $taskCandidate) 'Resolving an already physical path is idempotent'
  $taskMissingRejected=$false;try{Resolve-SonaPhysicalFile ($taskLogical+'.missing')|Out-Null}catch{$taskMissingRejected=$true}
  Check $taskMissingRejected 'A missing file cannot acquire an invented physical identity'
 }
 $taskProbe=Join-Path $taskOutput 'probe.ps1';$taskReply=Join-Path $taskOutput 'reply.json'
 @'
param([string]$InputPath,[string]$ReplyPath)
$ErrorActionPreference='Stop'
$r=@{Exists=[IO.File]::Exists($InputPath);PID=$PID;Parent=(Get-CimInstance Win32_Process -Filter ('ProcessId='+$PID)).ParentProcessId}
if($r.Exists){$r.Hash=(Get-FileHash -LiteralPath $InputPath).Hash}
[IO.File]::WriteAllText($ReplyPath,($r|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
'@ | Set-Content -LiteralPath $taskProbe
 $taskArguments='-NoProfile -NonInteractive -WindowStyle Hidden -File "'+$taskProbe+'" -InputPath "'+$taskCandidate+'" -ReplyPath "'+$taskReply+'"'
 & (Join-Path $taskRepo 'tools\start-desktop-app.ps1') -FilePath (Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe') -Arguments $taskArguments -WindowStyle Hidden|Out-Null
 $taskUntil=[DateTime]::UtcNow.AddSeconds(8)
 while(-not(Test-Path -LiteralPath $taskReply) -and [DateTime]::UtcNow -lt $taskUntil){Start-Sleep -Milliseconds 100}
 Check (Test-Path -LiteralPath $taskReply) 'The hidden desktop probe acknowledges within its bound'
 if(Test-Path -LiteralPath $taskReply){
  $taskReadback=Get-Content -LiteralPath $taskReply -Raw|ConvertFrom-Json
  Check ([bool]$taskReadback.Exists) 'Explorer can see the handed-off file path'
  Check ($taskReadback.Exists -and $taskReadback.Hash -eq (Get-FileHash -LiteralPath $taskLogical).Hash) 'The desktop process reads the exact original synthetic bytes'
  Check ((Get-Process -Id $taskReadback.Parent -ErrorAction Stop).ProcessName -eq 'explorer') 'The independent probe belongs to the Explorer desktop'
 }
} finally {
 if(Test-Path -LiteralPath $taskLogical){Remove-Item -LiteralPath $taskLogical}
}
$taskReport=@{Checks=@($taskChecks);Failures=@($taskFailures);Baseline=[bool]$Baseline;LogicalPath=$taskLogical;HandedOffPath=$taskPhysical;MicrophoneOpened=$false;GlobalInputSent=$false;AppLaunched=$false;ProfileChanged=$false}
$taskReport|ConvertTo-Json -Depth 4|Tee-Object -FilePath (Join-Path $taskOutput 'report.json')
if($taskFailures.Count){exit 1}
