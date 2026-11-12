#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$ProjectRoot = $PSScriptRoot,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
# Use this installation's checker even when ProjectRoot points to an isolated fixture.
$checkerPath = Join-Path $PSScriptRoot 'Sprawdz-StudioFable.ps1'
if (!(Test-Path -LiteralPath $checkerPath -PathType Leaf)) {
    [Console]::Error.WriteLine('Brak Sprawdz-StudioFable.ps1 obok skryptu kontroli studia.')
    exit 2
}
& $checkerPath -ProjectRoot $ProjectRoot -Studio -Json:$Json
exit $LASTEXITCODE
