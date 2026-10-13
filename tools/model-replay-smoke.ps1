param([Parameter(Mandatory)][string]$WaveFile,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path $PSScriptRoot -Parent
if(Test-Path -LiteralPath $OutputDirectory){throw 'Choose a fresh evidence directory.'}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$taskData=Join-Path $OutputDirectory 'data';New-Item -ItemType Directory -Path $taskData | Out-Null
'{"Language":"fr","Model":"tiny","Engine":"FastCpu","AutoInsert":false}' | Set-Content -LiteralPath (Join-Path $taskData 'preferences.json')
$taskPriorData=$env:MURMUR_DATA;$env:MURMUR_DATA=$taskData
$taskProcess=Start-Process -FilePath (Join-Path $taskRepo 'src\Murmur.App\bin\Release\net8.0-windows\Sona.exe') -ArgumentList @('--test-ui','--replay',('"'+[IO.Path]::GetFullPath($WaveFile)+'"')) -WindowStyle Hidden -PassThru
try
{
    $taskResultFile=Join-Path $taskData 'replay-result.json'
    for($taskTry=0;$taskTry -lt 120;$taskTry++){if(Test-Path -LiteralPath $taskResultFile){break};if($taskProcess.HasExited){throw 'Replay exited before producing a result'};Start-Sleep -Milliseconds 250}
    if(-not(Test-Path -LiteralPath $taskResultFile)){throw 'Replay result exceeded its deadline'}
    $taskResult=Get-Content -LiteralPath $taskResultFile -Raw | ConvertFrom-Json
    if($taskResult.State -ne 'Ready' -or [string]::IsNullOrWhiteSpace($taskResult.Draft)){throw 'Selected-model replay did not create a ready local draft'}
    [pscustomobject]@{Model='tiny';State=$taskResult.State;OriginalCharacters=$taskResult.Original.Length;DraftCharacters=$taskResult.Draft.Length;MicrophoneOpened=$false;AutomaticInsertion=$false;Corpus='Caller-provided licensed/synthetic WAV'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'model-replay.json')
}
finally
{
    if(-not $taskProcess.HasExited){$taskProcess.Refresh();$null=$taskProcess.CloseMainWindow();if(-not $taskProcess.WaitForExit(10000)){throw 'Replay app did not quit; its test deadline remains active.'}}
    $env:MURMUR_DATA=$taskPriorData
}
