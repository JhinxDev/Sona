[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$FilePath,
    [string]$Arguments = '',
    [string]$WorkingDirectory = '',
    [ValidateSet('NoActivate','Normal','Hidden')][string]$WindowStyle = 'NoActivate'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not [IO.Path]::IsPathRooted($FilePath) -or -not (Test-Path -LiteralPath $FilePath -PathType Leaf)) {
    throw 'Provide an existing absolute executable or shortcut path.'
}
$target = (Get-Item -LiteralPath $FilePath).FullName
if ([IO.Path]::GetExtension($target).ToLowerInvariant() -notin @('.exe','.lnk')) {
    throw 'Only explicit desktop executables and shortcuts are supported.'
}
if ($Arguments.IndexOf([char]0) -ge 0 -or $Arguments -match '[\r\n]') { throw 'Arguments must be a single command-line string without NUL or newlines.' }
if (-not $WorkingDirectory) { $WorkingDirectory = Split-Path -LiteralPath $target }
if (-not [IO.Path]::IsPathRooted($WorkingDirectory) -or -not (Test-Path -LiteralPath $WorkingDirectory -PathType Container)) {
    throw 'WorkingDirectory must be an existing absolute directory.'
}
$show = @{ NoActivate=4; Normal=1; Hidden=0 }[$WindowStyle]
$shell = $null
$windows = $null
$desktop = $null
$desktopDocument = $null
$broker = $null
try {
    # Delegate to the interactive Windows shell. Do not fall back to a child of
    # this agent's terminal, change job limits, or request elevated application UI.
    $shell = New-Object -ComObject Shell.Application
    $windows = $shell.Windows()
    $location = 0
    $root = 0
    $desktopHwnd = 0
    $desktop = $windows.FindWindowSW([ref]$location, [ref]$root, 8, [ref]$desktopHwnd, 1)
    if ($null -eq $desktop -or $desktopHwnd -eq 0) { throw 'An interactive Explorer desktop is required. No terminal-child fallback was started.' }
    $desktopDocument = $desktop.Document
    $broker = $desktopDocument.Application
    $broker.ShellExecute($target, $Arguments, $WorkingDirectory, 'open', $show)
    [PSCustomObject]@{ Status='DelegatedToWindowsShell'; FilePath=$target; WindowStyle=$WindowStyle }
}
finally {
    foreach ($com in @($broker, $desktopDocument, $desktop, $windows, $shell)) {
        if ($null -ne $com -and [Runtime.InteropServices.Marshal]::IsComObject($com)) {
            [void][Runtime.InteropServices.Marshal]::ReleaseComObject($com)
        }
    }
}
