# Restore an existing current-user encrypted setup after a verified profile split.
# Run from the destination app's desktop context. Never overwrite its setup.
param(
 [Parameter(Mandatory)][string]$SourceProfileDirectory,
 [Parameter(Mandatory)][string]$DestinationProfileDirectory
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Security
. (Join-Path $PSScriptRoot 'resolve-physical-file.ps1')
$taskSourceRoot=[IO.Path]::GetFullPath($SourceProfileDirectory).TrimEnd('\')
$taskDestinationRoot=[IO.Path]::GetFullPath($DestinationProfileDirectory).TrimEnd('\')
$taskPreferences=Join-Path $taskDestinationRoot 'preferences.json'
if(![IO.Directory]::Exists($taskDestinationRoot)-or![IO.File]::Exists($taskPreferences)){throw 'Select an existing active Sona profile with preferences.json.'}
$taskPhysicalPreferences=Resolve-SonaPhysicalFile $taskPreferences
if((Split-Path $taskPhysicalPreferences -Parent)-ne$taskDestinationRoot){throw 'Destination profile is redirected. Run recovery from the app desktop context with its physical profile path.'}
$taskSource=Join-Path $taskSourceRoot 'discord-credentials.dpapi'
$taskSourceItem=Get-Item -LiteralPath $taskSource -ErrorAction Stop
if($taskSourceItem.Attributes -band [IO.FileAttributes]::ReparsePoint -or $taskSourceItem.Length -lt 1 -or $taskSourceItem.Length -gt 131072){throw 'The retained encrypted setup is missing, redirected or outside its size bound.'}
$taskSource=Resolve-SonaPhysicalFile $taskSource
$taskDestination=Join-Path $taskDestinationRoot 'discord-credentials.dpapi'
$taskSourceHash=(Get-FileHash -LiteralPath $taskSource -Algorithm SHA256).Hash
if([IO.File]::Exists($taskDestination)){
 $taskExisting=Get-Item -LiteralPath $taskDestination
 if($taskExisting.Attributes -band [IO.FileAttributes]::ReparsePoint -or (Get-FileHash -LiteralPath $taskDestination -Algorithm SHA256).Hash-ne$taskSourceHash){throw 'The active profile already has a different setup. Both copies were preserved.'}
 [pscustomobject]@{Status='AlreadyPresent';SHA256=$taskSourceHash;SourcePreserved=$true};return
}
$taskBytes=[IO.File]::ReadAllBytes($taskSource)
$taskTemporary=Join-Path $taskDestinationRoot ('.discord-restore-'+[Guid]::NewGuid().ToString('N')+'.tmp')
$taskOwnTemporary=$false
try{
 # Validate current-user encryption without decoding or returning any credential.
 $taskPlain=[Security.Cryptography.ProtectedData]::Unprotect($taskBytes,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
 [Array]::Clear($taskPlain,0,$taskPlain.Length);$taskPlain=$null
 $taskFile=[IO.File]::Open($taskTemporary,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None);$taskOwnTemporary=$true
 try{$taskFile.Write($taskBytes,0,$taskBytes.Length);$taskFile.Flush()}finally{$taskFile.Dispose()}
 if((Get-FileHash -LiteralPath $taskTemporary -Algorithm SHA256).Hash-ne$taskSourceHash){throw 'Retained setup changed during recovery. Destination was preserved.'}
 # Two-argument Move is atomic and fails if another actor created the target.
 [IO.File]::Move($taskTemporary,$taskDestination);$taskOwnTemporary=$false
 if((Get-FileHash -LiteralPath $taskDestination -Algorithm SHA256).Hash-ne$taskSourceHash){throw 'Restored setup readback differs. Preserve both copies for inspection.'}
 [pscustomobject]@{Status='Restored';SHA256=$taskSourceHash;SourcePreserved=$true}
}finally{
 [Array]::Clear($taskBytes,0,$taskBytes.Length)
 if($taskOwnTemporary -and [IO.File]::Exists($taskTemporary)){[IO.File]::Delete($taskTemporary)}
}
