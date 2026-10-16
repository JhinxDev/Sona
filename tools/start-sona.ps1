[CmdletBinding(DefaultParameterSetName='Launch')]
param(
    [Parameter(Mandatory,ParameterSetName='Launch')][string]$FilePath,
    [Parameter(ParameterSetName='Launch')][string]$RecoveryPath,
    [Parameter(ParameterSetName='Launch')][string]$RehearsalRoot,
    [Parameter(ParameterSetName='Launch')][switch]$Hidden,
    [Parameter(Mandatory,ParameterSetName='Bootstrap')][string]$BootstrapRequest
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Security
function ReadProtected([string]$Path) {
    if((Get-Item -LiteralPath $Path).Length -gt 65536){throw 'Protected launch data exceeds its bound.'}
    $bytes=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($Path),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
    try{return [Text.Encoding]::UTF8.GetString($bytes)}finally{[Array]::Clear($bytes,0,$bytes.Length)}
}
function ValidateTarget([string]$Path,[string]$TestRoot) {
    $root=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\Sona'))
    if($TestRoot){
        $TestRoot=[IO.Path]::GetFullPath($TestRoot)
        if((Split-Path $TestRoot -Leaf)-notmatch'^sona-upgrade-rehearsal-[a-f0-9]{32}$' -or !(Test-Path -LiteralPath (Join-Path $TestRoot '.sona-rehearsal'))){throw 'Unrecognized isolated launch root.'}
        $root=Join-Path $TestRoot 'Programs\Sona'
    }
    $Path=[IO.Path]::GetFullPath($Path)
    if(!(Test-Path -LiteralPath $Path -PathType Leaf) -or !$Path.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $Path -Leaf)-ne'Sona.exe'){throw 'Unexpected Sona launch target.'}
    $receipt=Get-Content -LiteralPath (Join-Path (Split-Path $Path -Parent) 'installation.json') -Raw|ConvertFrom-Json
    if($receipt.Path-ne$Path -or (Get-FileHash -LiteralPath $Path).Hash.ToLowerInvariant()-ne$receipt.SHA256){throw 'Sona launch ownership differs.'}
    return $Path
}
if($PSCmdlet.ParameterSetName-eq'Bootstrap') {
    $request=[IO.Path]::GetFullPath($BootstrapRequest)
    if((Split-Path $request -Leaf)-notmatch'^desktop-launch-[a-f0-9]{32}\.dpapi$'){throw 'Unrecognized desktop launch request.'}
    $claimed=$request+'.claimed';$reply=$request+'.reply.json';$replyTemporary=$reply+'.tmp'
    $ownsRequest=$false;$payload=$null;$info=$null;$child=$null
    try {
        # Atomic claim prevents replay by a second bootstrap. The command line holds
        # only this encrypted request path, never draft text or a private environment.
        [IO.File]::Move($request,$claimed);$ownsRequest=$true
        $data=ReadProtected $claimed|ConvertFrom-Json
        if([DateTimeOffset]::UtcNow -gt ([DateTimeOffset]$data.ExpiresUtc)){throw 'Desktop launch request expired.'}
        $target=ValidateTarget $data.FilePath $data.RehearsalRoot
        $info=[Diagnostics.ProcessStartInfo]::new($target);$info.UseShellExecute=$false;$info.WorkingDirectory=Split-Path $target -Parent
        foreach($name in @('SONA_DATA','MURMUR_DATA','SONA_RUNTIME','MURMUR_RUNTIME','SONA_DRAFT_TRANSFER')){[void]$info.EnvironmentVariables.Remove($name)}
        foreach($name in @('SONA_DATA','MURMUR_DATA','SONA_RUNTIME','MURMUR_RUNTIME')){
            $value=$data.Environment.$name;if($value){$info.EnvironmentVariables[$name]=[string]$value}
        }
        if($data.RehearsalRoot){
            $info.Arguments='--test-ui --test-transfer'
            $info.EnvironmentVariables['SONA_DATA']=Join-Path $data.RehearsalRoot 'profile';$info.EnvironmentVariables['MURMUR_DATA']=$info.EnvironmentVariables['SONA_DATA']
        }
        if($data.Hidden){$info.Arguments=($info.Arguments+' --start-hidden').Trim()}
        if($data.RecoveryPath){
            $payload=ReadProtected $data.RecoveryPath
            if($payload.Length-gt30000){throw 'Draft transfer exceeds its bound.'}
            $info.EnvironmentVariables['SONA_DRAFT_TRANSFER']=$payload
        }
        if([DateTimeOffset]::UtcNow -gt ([DateTimeOffset]$data.ExpiresUtc)){throw 'Desktop launch request expired.'}
        $child=[Diagnostics.Process]::Start($info)
        $result=@{Ok=$true;PID=$child.Id;Path=$target;Started=$child.StartTime.ToUniversalTime().ToString('o');Route='Explorer desktop bootstrap'}
        [IO.File]::WriteAllText($replyTemporary,($result|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false));[IO.File]::Move($replyTemporary,$reply)
    } catch {
        # A claimed request gets a bounded, nonsecret failure receipt. Never echo
        # protected payloads, exception details or inherited environment values.
        if($ownsRequest -and !(Test-Path -LiteralPath $reply)){
            $result=@{Ok=$false;LaunchMayHaveStarted=($null-ne$child)}
            [IO.File]::WriteAllText($replyTemporary,($result|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false));[IO.File]::Move($replyTemporary,$reply)
        }
    } finally {
        if($info){[void]$info.EnvironmentVariables.Remove('SONA_DRAFT_TRANSFER')};$payload=$null
        if($ownsRequest -and (Test-Path -LiteralPath $claimed)){Remove-Item -LiteralPath $claimed}
    }
    return
}
$target=ValidateTarget $FilePath $RehearsalRoot
$directory=Join-Path $env:LOCALAPPDATA 'Sona\recovery'
if($RehearsalRoot){$RehearsalRoot=[IO.Path]::GetFullPath($RehearsalRoot);$directory=Join-Path $RehearsalRoot 'recovery'}
if($RecoveryPath){
    $RecoveryPath=[IO.Path]::GetFullPath($RecoveryPath)
    if(!$RecoveryPath.StartsWith([IO.Path]::GetFullPath($directory)+'\',[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $RecoveryPath -Leaf)-notmatch'^upgrade-[a-f0-9]{32}\.dpapi$'){throw 'Unrecognized draft recovery path.'}
    # Fail before delegation if the checkpoint cannot be decrypted.
    $probe=ReadProtected $RecoveryPath;$probe=$null
}
New-Item -ItemType Directory -Path $directory -Force|Out-Null
$request=Join-Path $directory ('desktop-launch-'+[guid]::NewGuid().ToString('N')+'.dpapi');$reply=$request+'.reply.json'
$environment=@{};foreach($name in @('SONA_DATA','MURMUR_DATA','SONA_RUNTIME','MURMUR_RUNTIME')){$environment[$name]=[Environment]::GetEnvironmentVariable($name)}
$data=@{FilePath=$target;RecoveryPath=$RecoveryPath;RehearsalRoot=$RehearsalRoot;Hidden=[bool]$Hidden;Environment=$environment;ExpiresUtc=[DateTimeOffset]::UtcNow.AddSeconds(20).ToString('o')}
$bytes=[Text.Encoding]::UTF8.GetBytes(($data|ConvertTo-Json -Depth 4 -Compress))
try{[IO.File]::WriteAllBytes($request,[Security.Cryptography.ProtectedData]::Protect($bytes,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser))}finally{[Array]::Clear($bytes,0,$bytes.Length)}
$delegated=$false;$confirmed=$false
try {
    # Windows paths cannot contain quotes. -File consumes explicit path arguments;
    # no expression, encoded script or plaintext draft is placed on the command line.
    $bootstrap=[IO.Path]::GetFullPath($PSCommandPath)
    if($bootstrap -match '["\r\n]' -or $request -match '["\r\n]'){throw 'Unsupported bootstrap path.'}
    $arguments='-NoProfile -NonInteractive -WindowStyle Hidden -File "'+$bootstrap+'" -BootstrapRequest "'+$request+'"'
    $powershell=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $delegated=$true # A COM error can have an ambiguous launch outcome; never double-launch.
    & (Join-Path $PSScriptRoot 'start-desktop-app.ps1') -FilePath $powershell -Arguments $arguments -WindowStyle Hidden|Out-Null
    $until=[DateTime]::UtcNow.AddSeconds(25)
    while(!(Test-Path -LiteralPath $reply) -and [DateTime]::UtcNow-lt$until){Start-Sleep -Milliseconds 50}
    if(!(Test-Path -LiteralPath $reply)){throw 'Desktop launch acknowledgement timed out; encrypted recovery retained.'}
    $result=Get-Content -LiteralPath $reply -Raw|ConvertFrom-Json
    if(!$result.Ok){$delegated=[bool]$result.LaunchMayHaveStarted;throw 'Desktop bootstrap did not confirm a launch; recovery retained.'}
    $process=Get-Process -Id $result.PID -ErrorAction Stop
    if($process.Path-ne$target -or $process.StartTime.ToUniversalTime().Ticks-ne([DateTime]$result.Started).ToUniversalTime().Ticks){throw 'Desktop-launched process identity differs; recovery retained.'}
    $confirmed=$true
    $result
} catch {
    if($delegated){$_.Exception.Data['DesktopLaunchUncertain']=$true}
    throw
} finally {
    if($confirmed -and (Test-Path -LiteralPath $reply)){Remove-Item -LiteralPath $reply}
    if(!$delegated -and (Test-Path -LiteralPath $request)){Remove-Item -LiteralPath $request}
}
