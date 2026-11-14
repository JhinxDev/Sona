param(
    [Parameter(Mandatory)][string]$OutputRoot,
    [string]$Installer
)
$ErrorActionPreference = 'Stop'
if (!$Installer) { $Installer = Join-Path $PSScriptRoot 'install.ps1' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if ((Split-Path $OutputRoot -Leaf) -notmatch '^sona-upgrade-rehearsal-[a-f0-9]{32}$' -or (Test-Path -LiteralPath $OutputRoot)) {
    throw 'Use a fresh sona-upgrade-rehearsal-<32 hex digits> root.'
}
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
Set-Content -LiteralPath (Join-Path $OutputRoot '.sona-rehearsal') -Value 'synthetic installer packages only'
$programRoot = Join-Path $OutputRoot 'Programs\Sona'
$current = Join-Path $programRoot 'current'
$menu = Join-Path $OutputRoot 'Sona.lnk'
$shell = New-Object -ComObject WScript.Shell
$checks = [Collections.Generic.List[string]]::new()

function Assert-Check([bool]$Passed, [string]$Description) {
    if (!$Passed) { throw $Description }
    $checks.Add($Description)
}

function New-FixturePackage([string]$Label) {
    $package = Join-Path $OutputRoot "package-$Label"
    New-Item -ItemType Directory -Path (Join-Path $package 'data') | Out-Null
    $files = foreach ($name in @('Sona.exe', 'sona-agent.exe', 'data\payload.txt')) {
        $path = Join-Path $package $name
        Set-Content -LiteralPath $path -Value "synthetic $Label $name"
        @{ Path = $name; SHA256 = (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() }
    }
    @{ Version = '0.9.1'; SourceHead = ($Label * 40); Files = @($files) } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'package.json')
    return $package
}

function Install-Fixture([string]$Package) {
    & $Installer -PackageDirectory $Package -RehearsalRoot $OutputRoot -NextLaunch | Out-Null
}

function Install-WithInterruptedCopy([string]$Package) {
    function Copy-Item([string]$LiteralPath, [string]$Destination, [switch]$Force) {
        if ((Split-Path $LiteralPath -Leaf) -eq 'payload.txt') {
            [IO.File]::WriteAllText($Destination, 'interrupted')
            throw 'Injected interruption after a partial file write.'
        }
        Microsoft.PowerShell.Management\Copy-Item @PSBoundParameters
    }
    try { Install-Fixture $Package } catch { return $_.Exception.Message }
}

function Install-WithPromotionConflict([string]$Package, [string]$ConflictPath) {
    function Copy-Item([string]$LiteralPath, [string]$Destination, [switch]$Force) {
        Microsoft.PowerShell.Management\Copy-Item @PSBoundParameters
        if ((Split-Path $LiteralPath -Leaf) -eq 'payload.txt') {
            New-Item -ItemType Directory -Path $ConflictPath | Out-Null
            Set-Content -LiteralPath (Join-Path $ConflictPath 'keep.txt') -Value 'competing directory'
        }
    }
    try { Install-Fixture $Package } catch { return $_.Exception.Message }
}

function Install-WithJunctionFailure([string]$Package, [string]$NewTarget) {
    function New-Item([string]$ItemType, [string]$Path, [string]$Value, [switch]$Force) {
        if ($ItemType -eq 'Junction' -and $Value -eq $NewTarget) {
            throw 'Injected current junction creation failure.'
        }
        Microsoft.PowerShell.Management\New-Item @PSBoundParameters
    }
    try { Install-Fixture $Package } catch { return $_.Exception.Message }
}

function Read-CurrentTarget {
    [string]@((Get-Item -LiteralPath $current).Target)[0]
}

$oldPackage = New-FixturePackage a
$newPackage = New-FixturePackage b
$oldVersion = Join-Path $programRoot '0.9.1-aaaaaaaa'
$newVersion = Join-Path $programRoot '0.9.1-bbbbbbbb'
Install-Fixture $oldPackage
$oldReceipt = Join-Path $oldVersion 'installation.json'
$oldReceiptHash = (Get-FileHash -LiteralPath $oldReceipt).Hash
$oldShortcutHash = (Get-FileHash -LiteralPath $menu).Hash
$unrelated = Join-Path $programRoot '.install-staging-unrelated'
New-Item -ItemType Directory -Path $unrelated | Out-Null
Set-Content -LiteralPath (Join-Path $unrelated 'keep.txt') -Value 'unrelated'

$failure = Install-WithInterruptedCopy $newPackage
Assert-Check ($failure -eq 'Injected interruption after a partial file write.') 'The installer reports the interrupted copy.'
Assert-Check ((Read-CurrentTarget) -eq $oldVersion -and
    (Get-FileHash -LiteralPath $menu).Hash -eq $oldShortcutHash) 'The interruption preserves both aliases.'
$visibleAfterInterruption = Test-Path -LiteralPath $newVersion
try { Install-Fixture $newPackage }
catch { throw "Same-package retry failed after the interrupted copy: $($_.Exception.Message)" }
Assert-Check (!$visibleAfterInterruption) 'A partial version never becomes visible at the final path.'

$newReceipt = Join-Path $newVersion 'installation.json'
$receipt = Get-Content -LiteralPath $newReceipt -Raw | ConvertFrom-Json
Assert-Check ($receipt.Path -eq (Join-Path $newVersion 'Sona.exe') -and
    $receipt.SourceHead -eq ('b' * 40) -and
    $receipt.SHA256 -eq (Get-FileHash -LiteralPath $receipt.Path).Hash) 'Retry publishes a receipt for the final executable.'
foreach ($file in $receipt.Files) {
    Assert-Check ((Get-FileHash -LiteralPath (Join-Path $newVersion $file.Path)).Hash -eq $file.SHA256) "Retry publishes verified $($file.Path)."
}
Assert-Check ((Read-CurrentTarget) -eq $newVersion -and
    $shell.CreateShortcut($menu).TargetPath -eq $receipt.Path) 'Retry switches both aliases to the complete version.'
Assert-Check ((Get-FileHash -LiteralPath $oldReceipt).Hash -eq $oldReceiptHash -and
    (Get-Content -LiteralPath (Join-Path $unrelated 'keep.txt')) -eq 'unrelated') 'Prior recovery and unrelated files are preserved.'

$newReceiptHash = (Get-FileHash -LiteralPath $newReceipt).Hash
Install-Fixture $newPackage
Assert-Check ((Get-FileHash -LiteralPath $newReceipt).Hash -eq $newReceiptHash) 'Reinstall preserves the healthy version receipt.'

$payload = Join-Path $newVersion 'data\payload.txt'
Set-Content -LiteralPath $payload -Value 'existing different bytes'
$failure = $null
try { Install-Fixture $newPackage } catch { $failure = $_.Exception.Message }
Assert-Check ($failure -like 'Preserving a different existing version file:*' -and
    (Get-Content -LiteralPath $payload) -eq 'existing different bytes') 'Existing different version bytes are refused and preserved.'

$conflictPackage = New-FixturePackage c
$conflictVersion = Join-Path $programRoot '0.9.1-cccccccc'
$conflictMarker = Join-Path $conflictVersion 'keep.txt'
$failure = Install-WithPromotionConflict $conflictPackage $conflictVersion
Assert-Check ($failure -and (Get-Content -LiteralPath $conflictMarker) -eq 'competing directory' -and
    @(Get-ChildItem -LiteralPath $conflictVersion -Force).Count -eq 1) 'Failed promotion preserves a competing final directory without writing into it.'
Assert-Check ((Read-CurrentTarget) -eq $newVersion -and
    $shell.CreateShortcut($menu).TargetPath -eq $receipt.Path) 'Failed promotion preserves both aliases.'
Assert-Check (@(Get-ChildItem -LiteralPath $programRoot -Directory -Force |
    Where-Object Name -like '.install-staging-*').Count -eq 1) 'Failed attempts clean only their private staging directories.'
Remove-Item -LiteralPath $conflictMarker
Remove-Item -LiteralPath $conflictVersion
Install-Fixture $conflictPackage
Assert-Check ((Read-CurrentTarget) -eq $conflictVersion) 'Same-package retry succeeds after the promotion conflict clears.'

$aliasPackage = New-FixturePackage d
$aliasVersion = Join-Path $programRoot '0.9.1-dddddddd'
$shortcutBeforeFailure = (Get-FileHash -LiteralPath $menu).Hash
$failure = Install-WithJunctionFailure $aliasPackage $aliasVersion
Assert-Check ($failure -eq 'Injected current junction creation failure.' -and
    (Read-CurrentTarget) -eq $conflictVersion -and
    (Get-FileHash -LiteralPath $menu).Hash -eq $shortcutBeforeFailure) 'A failed junction update restores both previous aliases.'
Install-Fixture $aliasPackage
Assert-Check ((Read-CurrentTarget) -eq $aliasVersion -and
    $shell.CreateShortcut($menu).TargetPath -eq (Join-Path $aliasVersion 'Sona.exe')) 'Same-package retry completes both aliases after a junction failure.'

[pscustomobject]@{ Passed = $checks.Count; Checks = $checks; OutputRoot = $OutputRoot }
