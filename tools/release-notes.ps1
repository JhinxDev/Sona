# Validates release tag metadata against Directory.Build.props and CHANGELOG.md,
# and renders Markdown release notes for GitHub Releases.
param(
    [Parameter(Mandatory)] [string]$Tag,
    [string]$RepoRoot,
    [string]$BuildInfoPath,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$trimmedTag = $Tag.Trim()
if ($trimmedTag -notmatch '^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
    throw "Release tag must match vMAJOR.MINOR.PATCH (got '$Tag')."
}
$version = $trimmedTag.Substring(1)

if (-not $RepoRoot) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
} else {
    $RepoRoot = (Resolve-Path $RepoRoot).Path
}

$propsPath = Join-Path $RepoRoot 'Directory.Build.props'
$propsMatch = Select-String -LiteralPath $propsPath -Pattern '<Version>([0-9]+\.[0-9]+\.[0-9]+)</Version>'
if (-not $propsMatch) {
    throw "Could not read <Version> from $propsPath."
}
$propsVersion = $propsMatch.Matches[0].Groups[1].Value
if ($propsVersion -ne $version) {
    throw "Tag $trimmedTag does not match Directory.Build.props Version ($propsVersion)."
}

$changelogPath = Join-Path $RepoRoot 'CHANGELOG.md'
$changelogText = Get-Content -LiteralPath $changelogPath -Raw
$headingMatches = [regex]::Matches($changelogText, '(?m)^##\s+([^\r\n]+)$')
$sectionStart = -1
$sectionEnd = $changelogText.Length
for ($i = 0; $i -lt $headingMatches.Count; $i++) {
    $heading = $headingMatches[$i].Groups[1].Value.Trim()
    if ($heading -match "^$([regex]::Escape($version))(?:\s|$)") {
        $sectionStart = $headingMatches[$i].Index + $headingMatches[$i].Length
        if ($i + 1 -lt $headingMatches.Count) {
            $sectionEnd = $headingMatches[$i + 1].Index
        }
        break
    }
}
if ($sectionStart -lt 0) {
    throw "CHANGELOG.md does not contain a '## $version' section."
}
$changelogBody = $changelogText.Substring($sectionStart, $sectionEnd - $sectionStart).Trim()
if ([string]::IsNullOrWhiteSpace($changelogBody)) {
    throw "CHANGELOG.md section for '$version' is empty."
}

$lines = [System.Collections.Generic.List[string]]::new()
if ($BuildInfoPath) {
    $info = Get-Content -LiteralPath $BuildInfoPath -Raw | ConvertFrom-Json
    $installer = [string]$info.Installer
    $bytes = ([int64]$info.InstallerBytes).ToString('N0', [Globalization.CultureInfo]::InvariantCulture)
    $sha = [string]$info.InstallerSHA256
    $head = [string]$info.SourceHead

    $lines.Add("Per-user Windows x64 installer for **Sona $trimmedTag** (source commit ``$head``).")
    $lines.Add("")
    $lines.Add("> **Unsigned build notice:** This installer is currently unsigned, so Windows SmartScreen may prompt on first run. Verify the installer hash below against ``SHA256SUMS.txt`` before running.")
    $lines.Add("")
}

$lines.Add("## Highlights")
$lines.Add("")
$lines.Add($changelogBody)

if ($BuildInfoPath) {
    $lines.Add("")
    $lines.Add("## Checksums")
    $lines.Add("")
    $lines.Add("- **File:** ``$installer`` ($bytes bytes)")
    $lines.Add("- **SHA-256:** ``$sha``")
    $lines.Add("- **Verify in PowerShell:** ``Get-FileHash .\$installer -Algorithm SHA256``")
}

$lines.Add("")
$lines.Add("[Install Guide](https://github.com/JhinxDev/Sona/blob/$trimmedTag/docs/INSTALL.md) · [Changelog](https://github.com/JhinxDev/Sona/blob/$trimmedTag/CHANGELOG.md)")
$rendered = ($lines -join "`n").TrimEnd() + "`n"

if ($OutputPath) {
    $outDir = Split-Path -Parent $OutputPath
    if ($outDir) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($OutputPath, $rendered, $utf8NoBom)
    Write-Output "Wrote release notes for $trimmedTag to $OutputPath"
} else {
    Write-Output $rendered
}
