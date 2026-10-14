param([Parameter(Mandatory)][string]$CandidateDirectory,[Parameter(Mandatory)][string]$OutputDirectory,[Parameter(Mandatory)][string]$DotnetPath)
$ErrorActionPreference='Stop';$taskRoot=Split-Path $PSScriptRoot -Parent
if(Test-Path -LiteralPath $OutputDirectory){throw 'Preserve previous proof and choose a fresh output directory.'}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$taskCandidate=[IO.Path]::GetFullPath($CandidateDirectory);$taskOutput=[IO.Path]::GetFullPath($OutputDirectory)
$taskPriorData=$env:SONA_DATA;$taskPriorDraft=$env:SONA_DRAFT_TRANSFER
$env:SONA_DATA=Join-Path $taskOutput 'data';New-Item -ItemType Directory -Path $env:SONA_DATA | Out-Null
@{Theme='amethyst-glass';Model='small';Language='en';AutoInsert=$false;CopyToClipboard=$false;KeepHistory=$false;Startup=$false;CloseToTray=$false;CheckForUpdates=$false;CleanupEnabled=$false;ProtectionEnabled=$false;Terms=@('Sona');Vocabulary=@();Snippets=@()}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $env:SONA_DATA 'preferences.json') -Encoding utf8
$env:SONA_DRAFT_TRANSFER=@{Draft="Sona release checklist`n`nReview the glass theme and keyboard focus.`nKeep this draft unsent.";Original='Review the Sona glass theme and keyboard focus. Keep this draft unsent.'}|ConvertTo-Json -Compress
# This guard deliberately watches the legacy owner's profile during isolated previews.
$taskOwnerPreferences=Join-Path $env:LOCALAPPDATA 'Murmur/preferences.json';$taskOwnerHash=if(Test-Path -LiteralPath $taskOwnerPreferences){(Get-FileHash -LiteralPath $taskOwnerPreferences).Hash}else{$null}
$taskRunnerOutput=Join-Path $taskOutput 'runner'
$taskProcess=$null;$taskCaptured=[Collections.Generic.HashSet[string]]::new()
if(-not('SonaThemeForeground' -as [type])){Add-Type 'using System; using System.Runtime.InteropServices; public static class SonaThemeForeground { [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow(); }'}
$taskForegroundBefore=[SonaThemeForeground]::GetForegroundWindow().ToInt64()
try {
 & $DotnetPath build (Join-Path $PSScriptRoot 'theme-material-preview/Theme.MaterialPreview.csproj') -c Release --disable-build-servers -p:UseSharedCompilation=false -p:SonaCandidateDirectory=$taskCandidate -o $taskRunnerOutput
 if($LASTEXITCODE-ne0){throw 'Theme fixture build failed.'}
 $taskForegroundBefore=[SonaThemeForeground]::GetForegroundWindow().ToInt64()
 $taskProcess=Start-Process -FilePath (Join-Path $taskRunnerOutput 'Sona.MaterialPreview.exe') -ArgumentList @('--test-ui','--test-transfer',('--material-proof='+$taskOutput)) -WindowStyle Hidden -PassThru
 $taskProcess.Id|Set-Content -LiteralPath (Join-Path $taskOutput 'owned-preview.pid')
 $taskTimer=[Diagnostics.Stopwatch]::StartNew()
 while(!$taskProcess.HasExited-and$taskTimer.Elapsed.TotalSeconds-lt150){
  if(Test-Path -LiteralPath (Join-Path $taskOutput 'failure.txt')){throw (Get-Content -LiteralPath (Join-Path $taskOutput 'failure.txt') -Raw)}
  $taskReady=$null
  if(Test-Path -LiteralPath (Join-Path $taskOutput 'capture-ready.json')){try{$taskReady=Get-Content -LiteralPath (Join-Path $taskOutput 'capture-ready.json') -Raw|ConvertFrom-Json}catch{}}
  if($taskReady-and!$taskCaptured.Contains($taskReady.name)){
   if($taskReady.processId-ne$taskProcess.Id){throw 'Preview process identity changed.'}
   winapp ui screenshot -w $taskReady.hwnd --output (Join-Path $taskOutput ($taskReady.name+'.png')) --json | Set-Content -LiteralPath (Join-Path $taskOutput ($taskReady.name+'-screenshot.json'))
   if($LASTEXITCODE-ne0){throw 'Exact-window native screenshot failed.'}
   $taskCaptured.Add($taskReady.name)|Out-Null;New-Item -ItemType File -Path (Join-Path $taskOutput ($taskReady.name+'.captured'))|Out-Null
  }
  Start-Sleep -Milliseconds 70
 }
 if(!$taskProcess.HasExited){throw 'Bounded theme fixture did not exit.'}
 if(!(Test-Path -LiteralPath (Join-Path $taskOutput 'theme-material-receipt.json'))){throw 'Theme fixture did not produce its completion receipt.'}
 $taskOwnerAfter=if(Test-Path -LiteralPath $taskOwnerPreferences){(Get-FileHash -LiteralPath $taskOwnerPreferences).Hash}else{$null}
 if($taskOwnerAfter-ne$taskOwnerHash){throw 'Owner preferences changed during theme proof.'}
 $taskForegroundAfter=[SonaThemeForeground]::GetForegroundWindow().ToInt64()
 if($taskForegroundAfter-ne$taskForegroundBefore){throw 'Foreground changed during the bounded fixture; no restoration attempted.'}
 [ordered]@{OwnerPreferencesUnchanged=$true;OwnerPreferencesBeforeSha256=$taskOwnerHash;OwnerPreferencesAfterSha256=$taskOwnerAfter;ExactPreviewExited=$taskProcess.HasExited;ForegroundBefore=$taskForegroundBefore;ForegroundAfter=$taskForegroundAfter;Captured=$taskCaptured.Count;CandidateDllSha256=(Get-FileHash -LiteralPath (Join-Path $taskCandidate 'Sona.dll')).Hash}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $taskOutput 'preservation-receipt.json')
 Get-Content -LiteralPath (Join-Path $taskOutput 'theme-material-receipt.json') -Raw
}finally{
 if($taskProcess){if(!$taskProcess.HasExited){$null=$taskProcess.CloseMainWindow();$taskProcess.WaitForExit(10000)|Out-Null};$taskProcess.Dispose()}
 $env:SONA_DATA=$taskPriorData;$env:SONA_DRAFT_TRANSFER=$taskPriorDraft
}
