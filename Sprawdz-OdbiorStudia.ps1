#requires -Version 7.5
[CmdletBinding()]
param(
    [ValidateSet('Regression','Acceptance','Snapshot')][string]$Action = 'Regression',
    [string]$Python,
    [string]$Report,
    [string]$Baseline = 'qa/studio/regression-baseline.json',
    [string]$Contract,
    [string]$Evidence,
    [string]$SessionId,
    [string]$ProjectRoot = $PSScriptRoot,
    [switch]$Historical
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-GateSnapshot {
    # Exactly the existing quality-gate.ps1 algorithm, including sorting and exclusions.
    # Tools/root prompts omitted by that old algorithm must also be pinned in sourceFiles.
    $rows = [Collections.Generic.List[string]]::new()
    foreach ($name in @('Assets','Packages','ProjectSettings','.claude','qa','docs','PROMPT_WDROZENIOWY.md','ASSET_STATUS.json','GAME_VISION.md','GAME_DESIGN.md','MYTHIC_SCENARIOS.md','CLAUDE.md','VISUAL_TECH_SPEC.md','ECONOMY_DESIGN.md','ECONOMY_BALANCE.md','START_PROMPT_PL.md','README_PL.md')) {
        $target = Join-Path $ProjectRoot $name
        if (!(Test-Path -LiteralPath $target)) { continue }
        $files = if (Test-Path -LiteralPath $target -PathType Leaf) { @(Get-Item -LiteralPath $target) } else { @(Get-ChildItem -LiteralPath $target -Recurse -File -Force) }
        foreach ($file in $files) {
            $rel = [IO.Path]::GetRelativePath($ProjectRoot, $file.FullName).Replace('\','/')
            if ($rel.StartsWith('qa/reports/')) { continue }
            if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse point niedozwolony: $rel" }
            $rows.Add($rel + ':' + (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)
        }
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes((@($rows | Sort-Object -CaseSensitive) -join "`n"))
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
}

try {
    $ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot)
    if ($Action -eq 'Snapshot') { Get-GateSnapshot; exit 0 }
    if (!$Python) { throw 'Podaj -Python ze sciezka do rzeczywistego interpretera Python 3.10+.' }
    $arguments = @((Join-Path $PSScriptRoot 'Tools/studio_quality/verify.py'), '--root', $ProjectRoot)
    if ($Action -eq 'Regression') {
        if (!$Report) { throw 'Wymagany -Report (sciezka wzgledem projektu).' }
        $arguments += @('regression','--baseline',$Baseline,'--report',$Report)
        if (!$Historical) { $arguments += @('--expected-snapshot',(Get-GateSnapshot)) }
    } else {
        if (!$Contract -or !$Evidence -or $Historical) { throw 'Acceptance wymaga -Contract, -Evidence i aktualnego snapshotu.' }
        if (!$SessionId) { $SessionId = [Environment]::GetEnvironmentVariable('QUALITY_GATE_SESSION_ID') }
        if (!$SessionId) { throw 'Acceptance wymaga SessionId biezacej sesji, z metadanych klienta lub QUALITY_GATE_SESSION_ID.' }
        $arguments += @('acceptance','--contract',$Contract,'--evidence',$Evidence,'--expected-snapshot',(Get-GateSnapshot))
        $arguments += @('--expected-session',$SessionId)
    }
    & $Python @arguments
    $result = $LASTEXITCODE
    if ($result -eq 0) { exit 0 }
    exit 2
} catch {
    @{status='BLOCKED';reason=$_.Exception.Message} | ConvertTo-Json -Compress
    exit 2
}
