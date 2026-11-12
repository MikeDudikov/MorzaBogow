#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$ProjectRoot = $PSScriptRoot,
    [switch]$Json,
    [switch]$Studio
)

$ErrorActionPreference = 'Stop'
$checks = [Collections.Generic.List[object]]::new()

function Add-Check([string]$Id, [string]$Status, [string]$Detail) {
    $checks.Add([pscustomobject]@{ Id = $Id; Status = $Status; Detail = $Detail })
}

function Read-AgentHeader([string]$Path) {
    $body = [IO.File]::ReadAllText($Path)
    $match = [regex]::Match($body, '\A---\r?\n(?<header>[\s\S]*?)\r?\n---(?:\r?\n|\z)')
    if (!$match.Success) { throw "Brak poprawnego naglowka definicji: $Path" }
    return $match.Groups['header'].Value.Replace("`r`n", "`n")
}

function Read-Scalar([string]$Header, [string]$Field) {
    $matches = [regex]::Matches($Header, '(?m)^' + [regex]::Escape($Field) + ':[ \t]*(?<value>[^\r\n]*)$')
    if ($matches.Count -ne 1) { throw "Pole $Field musi wystapic dokladnie raz" }
    return $matches[0].Groups['value'].Value.Trim().Trim([char[]]@('"', "'"))
}

function Test-AdditionalAgent([string]$Directory, [string]$Name, [string]$Model, [bool]$ReadOnly) {
    $path = Join-Path $Directory ($Name + '.md')
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Brak definicji $Name" }
    $header = Read-AgentHeader $path
    if ((Read-Scalar $header 'name') -cne $Name) { throw "Nieprawidlowa nazwa: $Name" }
    if ([string]::IsNullOrWhiteSpace((Read-Scalar $header 'description'))) { throw "Pusty opis: $Name" }
    if ((Read-Scalar $header 'model') -cne $Model) { throw "Nieprawidlowy model dla $Name" }
    if ((Read-Scalar $header 'disallowedTools') -cne 'Agent') { throw "Brak blokady dalszego delegowania: $Name" }
    if ($ReadOnly) {
        $tools = @((Read-Scalar $header 'tools').Split(',') | ForEach-Object { $_.Trim() })
        $readTools = @('Read','Glob','Grep','WebSearch','WebFetch','SendMessage')
        if (!$tools.Count -or @($tools | Where-Object { $_ -cnotin $readTools }).Count) { throw "Rola $Name ma narzedzie spoza listy odczytu" }
        if ('Read' -cnotin $tools) { throw "Rola $Name nie ma narzedzia Read" }
    }
    $sameNames = 0
    foreach ($candidate in Get-ChildItem -LiteralPath $Directory -Recurse -File -Filter '*.md') {
        try { $candidateHeader = Read-AgentHeader $candidate.FullName }
        catch { continue }
        $nameMatches = [regex]::Matches($candidateHeader, '(?m)^name:[ \t]*(?<value>[^\r\n]*)$')
        if ($nameMatches.Count -ne 1) { continue }
        if ($nameMatches[0].Groups['value'].Value.Trim().Trim([char[]]@('"', "'")) -ceq $Name) { $sameNames++ }
    }
    if ($sameNames -ne 1) { throw "Kolizja nazwy: $Name" }
    Add-Check ('agent:' + $Name) 'PASS' "Jawny model $Model; poprawna rola i unikalna nazwa."
}

try {
    $resolvedRoot = [IO.Path]::GetFullPath($ProjectRoot)
    $agentDirectory = Join-Path $resolvedRoot '.claude/agents'
    $agentPath = Join-Path $agentDirectory 'studio-programmer-fable.md'
    $promptPath = Join-Path $resolvedRoot 'PROMPT_STUDIO_FABLE.md'
    if (!(Test-Path -LiteralPath $agentPath -PathType Leaf)) { throw 'Brak definicji studio-programmer-fable' }
    if (!(Test-Path -LiteralPath $promptPath -PathType Leaf)) { throw 'Brak PROMPT_STUDIO_FABLE.md' }
    $header = Read-AgentHeader $agentPath
    if ((Read-Scalar $header 'name') -cne 'studio-programmer-fable') { throw 'Nieprawidlowa nazwa subagenta' }
    if ([string]::IsNullOrWhiteSpace((Read-Scalar $header 'description'))) { throw 'Pusty opis subagenta' }
    if ((Read-Scalar $header 'model') -cne 'fable') { throw 'Programista nie ma model: fable' }
    if ((Read-Scalar $header 'disallowedTools') -cne 'Agent') { throw 'Programista powinien miec zablokowane dalsze delegowanie przez Agent' }
    Add-Check 'definition' 'PASS' 'Definicja programisty ma jawny model fable i blokade dalszego delegowania.'

    $sameNames = 0
    foreach ($candidate in Get-ChildItem -LiteralPath $agentDirectory -Recurse -File -Filter '*.md') {
        try { $candidateHeader = Read-AgentHeader $candidate.FullName }
        catch { continue } # Documentation and unloaded definitions do not shadow a valid agent.
        $nameMatches = [regex]::Matches($candidateHeader, '(?m)^name:[ \t]*(?<value>[^\r\n]*)$')
        if ($nameMatches.Count -ne 1) { continue }
        $candidateName = $nameMatches[0].Groups['value'].Value.Trim().Trim([char[]]@('"', "'"))
        if ($candidateName -ceq 'studio-programmer-fable') { $sameNames++ }
    }
    if ($sameNames -ne 1) { throw 'Kolizja nazw studio-programmer-fable w projekcie' }
    Add-Check 'unique-name' 'PASS' 'Jedna projektowa definicja o tej nazwie.'

    if ($Studio) {
        Test-AdditionalAgent $agentDirectory 'studio-architect-opus' 'opus' $true
        Test-AdditionalAgent $agentDirectory 'studio-test-engineer-fable' 'fable' $false
        Test-AdditionalAgent $agentDirectory 'studio-reviewer-opus' 'opus' $true
        Test-AdditionalAgent $agentDirectory 'studio-search-haiku' 'haiku' $true
    }

    # Check the effective values of known layers; local settings override project settings.
    # User/managed settings and the actual client still require runtime verification.
    $aliasNames = @('ANTHROPIC_DEFAULT_FABLE_MODEL')
    if ($Studio) { $aliasNames += @('ANTHROPIC_DEFAULT_OPUS_MODEL', 'ANTHROPIC_DEFAULT_HAIKU_MODEL') }
    $modelEnvNames = @('CLAUDE_CODE_SUBAGENT_MODEL_FORCE', 'CLAUDE_CODE_SUBAGENT_MODEL') + $aliasNames
    $knownModelEnv = @{}
    $knownMainModel = $null
    $projectMainModel = $null
    foreach ($envName in $modelEnvNames) {
        $processValue = [Environment]::GetEnvironmentVariable($envName, 'Process')
        if ($null -ne $processValue) { $knownModelEnv[$envName] = [string]$processValue }
    }
    foreach ($relativePath in @('.claude/settings.json', '.claude/settings.local.json')) {
        $settingsPath = Join-Path $resolvedRoot $relativePath
        if (!(Test-Path -LiteralPath $settingsPath -PathType Leaf)) { continue }
        $settings = [IO.File]::ReadAllText($settingsPath) | ConvertFrom-Json -AsHashtable
        if ($settings -isnot [Collections.IDictionary]) { throw "Niepoprawny obiekt ustawien: $relativePath" }
        if ($Studio -and $settings.Contains('model')) { $knownMainModel = [string]$settings['model'] }
        if ($Studio -and $relativePath -ceq '.claude/settings.json' -and $settings.Contains('model')) { $projectMainModel = [string]$settings['model'] }
        if ($Studio -and $settings.Contains('agent')) {
            Add-Check ($relativePath + ':main-agent') 'NEEDS_RUNTIME' 'Zdefiniowano niestandardowego agenta glownego; potwierdz jego model i uprawnienia w kliencie.'
        }
        $settingsEnv = $settings['env']
        if ($null -ne $settingsEnv) {
            if ($settingsEnv -isnot [Collections.IDictionary]) { throw "Niepoprawne env: $relativePath" }
            foreach ($envName in $modelEnvNames) {
                if ($settingsEnv.Contains($envName)) { $knownModelEnv[$envName] = [string]$settingsEnv[$envName] }
            }
        }
        if ($settings.Contains('availableModels')) {
            Add-Check ($relativePath + ':allowlist') 'NEEDS_RUNTIME' 'Lista dostepnych modeli wymaga sprawdzenia w kliencie. Skrypt nie ustala dostepu dostawcy.'
        }
    }

    if ($Studio) {
        if ($projectMainModel -cne 'opus' -and $projectMainModel -cnotmatch '^claude-opus-') {
            Add-Check 'main-model' 'BLOCKED' 'Projektowy domyslny model w settings.json powinien nalezec do rodziny Opus.'
        } else {
            Add-Check 'main-model' 'PASS' 'Projektowy domyslny model nalezy do rodziny Opus; rzeczywista sesja wymaga potwierdzenia.'
        }
        if ($knownMainModel -cne 'opus' -and $knownMainModel -cnotmatch '^claude-opus-') {
            Add-Check 'local-main-model' 'NEEDS_RUNTIME' 'Znane ustawienie lokalne wskazuje inny model lub kasuje wybor. Aktywna sesja moze miec Opus wybrany przez /model lub CLI; potwierdz w kliencie.'
        }
    }

    $effectiveForce = [string]$knownModelEnv['CLAUDE_CODE_SUBAGENT_MODEL_FORCE']
    $effectiveModel = [string]$knownModelEnv['CLAUDE_CODE_SUBAGENT_MODEL']
    if ($effectiveForce.ToLowerInvariant() -in @('1', 'true', 'yes', 'on')) {
        if ($Studio) {
            Add-Check 'known-force' 'BLOCKED' 'Globalne wymuszenie jednego modelu koliduje z matryca Opus/Fable/Haiku. Skrypt nie zmienia ustawien.'
        } elseif ([string]::IsNullOrWhiteSpace($effectiveModel)) {
            Add-Check 'known-force' 'NEEDS_RUNTIME' 'Znane warstwy wymuszaja model rozmowy glownej. Potwierdz, czy jest to Fable; skrypt nie zna stanu klienta.'
        } elseif ($effectiveModel -ceq 'fable' -or $effectiveModel -cmatch '^claude-fable-') {
            Add-Check 'known-force' 'NEEDS_RUNTIME' 'Zastane wymuszenie Fable obejmuje caly zespol. Nie zmieniono go; potwierdz rzeczywisty model i role w kliencie.'
        } else {
            Add-Check 'known-force' 'BLOCKED' 'Po polaczeniu znanych warstw wymuszany jest inny model. Rozstrzygnij konfiguracje w kliencie przed programowaniem; skrypt niczego nie zmienia.'
        }
    }
    foreach ($envName in (@('CLAUDE_CODE_SUBAGENT_MODEL') + $aliasNames)) {
        if ($knownModelEnv.ContainsKey($envName) -and $knownModelEnv[$envName]) {
            Add-Check ('known:' + $envName) 'NEEDS_RUNTIME' 'Istnieje ustawienie domyslnego modelu lub aliasu; potwierdz routing w sesji Claude Code.'
        }
    }
    if ($Studio) {
        foreach ($envName in @('ANTHROPIC_MODEL', 'ANTHROPIC_DEFAULT_MODEL')) {
            if ([Environment]::GetEnvironmentVariable($envName, 'Process')) {
                Add-Check ('process:' + $envName) 'NEEDS_RUNTIME' 'Wykryto zmienna wyboru modelu glownego. Domyslny wpis projektu nie dowodzi aktualnego wyboru sesji.'
            }
        }
    }
    $claudeCommand = Get-Command -Name claude -ErrorAction SilentlyContinue
    Add-Check 'cli-on-path' 'INFO' $(if ($claudeCommand) { 'Polecenie claude jest widoczne. Nie uruchamiano go ani zadania modelu.' } else { 'Polecenie claude nie jest widoczne na PATH tego procesu. Uzyj kontroli w swoim kliencie Claude Code.' })
    Add-Check 'runtime' 'NEEDS_RUNTIME' $(if ($Studio) { 'Potwierdz Opus glownej sesji oraz faktyczny Opus/Fable/Haiku u uzywanych rol. Ustawienia uzytkownika, organizacji i dostawcy nie sa w pelni sprawdzane.' } else { 'Potwierdz zaladowanie agenta i model Fable w metadanych lub /tasks przed edycja. Ustawienia uzytkownika, organizacji i dostawcy nie sa w pelni sprawdzane.' })
} catch {
    Add-Check 'configuration' 'BLOCKED' $_.Exception.Message
}

$blocked = @($checks | Where-Object Status -eq 'BLOCKED').Count -gt 0
$result = [pscustomobject]@{
    Status = $(if ($blocked) { 'BLOCKED_CONFIGURATION' } else { 'CONFIGURED_RUNTIME_UNVERIFIED' })
    ReadOnly = $true
    RuntimeVerified = $false
    Scope = $(if ($Studio) { 'STUDIO_MATRIX' } else { 'FABLE_PROGRAMMER' })
    Checks = @($checks.ToArray())
}
if ($Json) { $result | ConvertTo-Json -Depth 6 }
else {
    $result.Status
    $checks | Format-Table Id, Status, Detail -Wrap
    'Kontrola jest tylko do odczytu. Kod 0 nie oznacza potwierdzonego dostepu do modeli; kod 2 oznacza konflikt konfiguracji.'
}
if ($blocked) { exit 2 }
exit 0
