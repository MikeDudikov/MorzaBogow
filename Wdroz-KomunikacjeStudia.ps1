#requires -Version 7.5
<##
Wdraza tylko PROMPT_KOMUNIKACJA_STUDIA.md i Komunikacja-Studio.ps1:
rejestruje /komunikacja-studia i inicjalizuje lokalny dziennik bez kasowania wpisow.
Nie uruchamia Claude, Unity ani agentow i nie zmienia modeli, hookow lub uprawnien.
##>
[CmdletBinding()]
param([string]$ProjectRoot = $PSScriptRoot, [switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
$deploymentLock = $null
$gateWriteGuard = $null
$temporaryPath = $null
try {
    $root = [IO.Path]::GetFullPath($ProjectRoot)
    if (!(Test-Path -LiteralPath $root -PathType Container)) { throw 'PROJECT_ROOT_NOT_FOUND' }
    $promptPath = Join-Path $root 'PROMPT_KOMUNIKACJA_STUDIA.md'
    $journalScript = Join-Path $root 'Komunikacja-Studio.ps1'
    $stateDirectory = Join-Path $root 'work/studio-communication'
    $journalPath = Join-Path $stateDirectory 'journal.json'
    $skillDirectory = Join-Path $root '.claude/skills/komunikacja-studia'
    $skillPath = Join-Path $skillDirectory 'SKILL.md'
    $deployLockPath = Join-Path $stateDirectory '.deploy.lock'
    $gateLockPath = Join-Path $root 'qa/reports/.gate.lock'
    foreach ($candidate in @($root, $promptPath, $journalScript, (Join-Path $root 'work'), $stateDirectory,
            $journalPath, (Join-Path $stateDirectory '.journal.lock'), $deployLockPath,
            (Join-Path $root '.claude'), (Join-Path $root '.claude/skills'), $skillDirectory, $skillPath,
            (Join-Path $root 'qa'), (Join-Path $root 'qa/reports'), $gateLockPath)) {
        if ((Test-Path -LiteralPath $candidate) -and
            (((Get-Item -LiteralPath $candidate -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) { throw 'REPARSE_POINT_NOT_ALLOWED' }
    }
    foreach ($required in @($promptPath, $journalScript)) {
        if (!(Test-Path -LiteralPath $required -PathType Leaf)) { throw "MISSING_COMMUNICATION_FILE: $required" }
        if ((Get-Item -LiteralPath $required).Length -eq 0) { throw "EMPTY_COMMUNICATION_FILE: $required" }
    }
    $tokens = $null; $parseErrors = $null
    [Management.Automation.Language.Parser]::ParseFile($journalScript,[ref]$tokens,[ref]$parseErrors) | Out-Null
    if ($parseErrors.Count) { throw 'JOURNAL_SCRIPT_SYNTAX_ERROR' }
    $skill = @'
---
name: komunikacja-studia
description: Aktywuje tylko protokol komunikacji i trwaly dziennik istniejacego zespolu.
disable-model-invocation: true
---

Zastosuj wyłącznie protokół z `{{PROMPT_PATH}}` oraz narzędzie `{{JOURNAL_SCRIPT}}` w projekcie `{{PROJECT_ROOT}}`. Odpowiadaj po polsku.

To polecenie aktywuje komunikację i ciągłość zapisu. Zachowaj istniejące role, modele, przydziały oraz postępy. Nie wdrażaj ponownie całej organizacji studia, nie rekonfiguruj modeli, hooków ani uprawnień i nie rozpoczynaj nowego zadania programistycznego w wyniku tej aktywacji. Bieżące prace doprowadź do bezpiecznego punktu bez przymusowego przerywania operacji.

1. Przeczytaj cały wskazany prompt komunikacji. Sprawdź, kto obecnie koordynuje, jakie zadania i pliki są zajęte oraz czy trwa bramka. Nie przejmuj koordynacji od aktywnej sesji. Jeśli wdrożenie wymagałoby edycji zamrożonego snapshotu, odłóż tę edycję.
2. W PowerShell wykonaj:

   ```powershell
   pwsh -NoProfile -File '{{JOURNAL_PS_PATH}}' -ProjectRoot '{{ROOT_PS_PATH}}' -Action Init
   pwsh -NoProfile -File '{{JOURNAL_PS_PATH}}' -ProjectRoot '{{ROOT_PS_PATH}}' -Action Read -Last 40
   ```

   Po każdym poleceniu sprawdź kod zakończenia. Przy błędzie zatrzymaj zależną aktywację i zgłoś przyczynę; nie kasuj dziennika ani blokad. Odtwórz ostatni CHECKPOINT i WSZYSTKIE późniejsze zdarzenia przez Read -AfterRevision N bez -Last; bez checkpointu odczytaj cały dziennik. Porównaj zapis z rzeczywistym stanem projektu.
3. Użyj istniejącego kanału do głównego agenta i subagentów. Sprawdź SendMessage w faktycznej sesji; nie zakładaj, że zmiana definicji nadała je już działającym instancjom. Sprawdź komunikat i rzeczywisty ACK na jednym dostępnym wykonawcy, wyłącznie do odczytu. Zapisz wynik przez koordynatora. Bez dostępnego kanału zastosuj RESULT_ONLY i jawny punkt przekazania; transport bez testu oznacz NOT_RUN. Nie uruchamiaj całego zespołu do tego sprawdzenia i nie zmieniaj przypisanych modeli.
4. Przyjmij zastane zadania do rejestru. Utrwal prawdziwe przydziały, potwierdzenia, rezerwacje plików, blokady, decyzje i oczekiwane odpowiedzi. Tylko koordynator zapisuje dziennik przez Append z aktualnym ExpectedRevision; role do odczytu zwracają komunikaty bez edycji plików. Nie wymyślaj ACK ani autorstwa.
5. Zachowaj jednego autora na plik i jednego właściciela uruchomień Unity. Stosuj FREEZE/freezeId i potwierdzenia WRITES_STOPPED przed przyszłą bramką; sama aktywacja nie wymaga uruchomienia Unity. Dziennik pozostaje w work/ poza obecnym snapshotem.
6. Zapisz CHECKPOINT z aktualnymi zadaniami, sesją, agentami/ID, dowodami modeli lub UNKNOWN, rezerwacjami, decyzjami, nierozstrzygniętymi ACK, stanem freeze, runnerem i następnym krokiem. Ponownie sprawdź spójność rejestru z rzeczywistą pracą i zakresem tego polecenia. Zachowaj wpisy między sesjami, odtwarzaj je po wznowieniu i nie traktuj dziennika jako działającego w tle transportu.

Zakończ krótkim raportem aktywacji: ścieżka i rewizja dziennika, rzeczywiście sprawdzony kanał, wynik ACK, przyjęte zadania, ograniczenia i punkt kontynuacji. Sama obecność plików nie dowodzi aktywnej komunikacji. Nie rozpoczynaj kolejnego przyrostu gry w ramach tego polecenia.
'@
    $skill = $skill.Replace('{{PROMPT_PATH}}',$promptPath).Replace('{{JOURNAL_SCRIPT}}',$journalScript).Replace('{{PROJECT_ROOT}}',$root)
    $skill = $skill.Replace('{{JOURNAL_PS_PATH}}',$journalScript.Replace("'","''")).Replace('{{ROOT_PS_PATH}}',$root.Replace("'","''"))
    $skill = $skill.Replace("`r`n","`n").Trim() + "`n"
    function Test-ExistingSkill {
        if (Test-Path -LiteralPath $skillPath) {
            if (!(Test-Path -LiteralPath $skillPath -PathType Leaf)) { throw 'COMMAND_PATH_IS_NOT_FILE' }
            $existing = [IO.File]::ReadAllText($skillPath).Replace("`r`n","`n").Trim()
            if ($existing -cne $skill.Trim()) { throw 'EXISTING_COMMAND_DIFFERS: zachowano istniejacy SKILL.md' }
            return $true
        }
        return $false
    }
    function Test-GateIdle {
        if (Test-Path -LiteralPath $gateLockPath) {
            try { $probe = [IO.File]::Open($gateLockPath,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite) }
            catch { throw 'BLOCKED_QUALITY_GATE: poczekaj na zakonczenie aktywnej bramki' }
            $probe.Dispose()
        }
    }
    $alreadyInstalled = Test-ExistingSkill
    Test-GateIdle
    $revision = $null
    if (Test-Path -LiteralPath $journalPath) {
        $readOutput = & $journalScript -ProjectRoot $root -Action Read -Last 1
        if ($LASTEXITCODE -ne 0) { throw 'EXISTING_JOURNAL_INVALID_OR_BUSY' }
        $revision = ($readOutput | ConvertFrom-Json -DateKind String).revision
    }
    if ($CheckOnly) {
        @{status='CHECK_OK'; command='/komunikacja-studia'; journalRevision=$revision; writes=$false; runtimeActivated=$false} | ConvertTo-Json
        exit 0
    }
    Test-GateIdle
    [IO.Directory]::CreateDirectory($stateDirectory) | Out-Null
    $deploymentLock = [IO.File]::Open($deployLockPath,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    $initOutput = & $journalScript -ProjectRoot $root -Action Init
    if ($LASTEXITCODE -ne 0) { throw 'JOURNAL_INIT_FAILED' }
    $revision = ($initOutput | ConvertFrom-Json -DateKind String).revision
    Test-GateIdle
    $alreadyInstalled = Test-ExistingSkill
    if (!$alreadyInstalled) {
        # Ten sam uchwyt co w bramce wyklucza start testow podczas zapisu SKILL.md.
        # Plik blokady pozostaje; instalator nie uruchamia runnera i go nie usuwa.
        [IO.Directory]::CreateDirectory((Split-Path -Parent $gateLockPath)) | Out-Null
        try { $gateWriteGuard = [IO.File]::Open($gateLockPath,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None) }
        catch { throw 'BLOCKED_QUALITY_GATE: bramka przejela blokade przed rejestracja polecenia' }
        [IO.Directory]::CreateDirectory($skillDirectory) | Out-Null
        $temporaryPath = Join-Path $skillDirectory ([Guid]::NewGuid().ToString('N') + '.tmp')
        [IO.File]::WriteAllText($temporaryPath,$skill,[Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temporaryPath,$skillPath)
        $temporaryPath = $null
    }
    @{status=$(if ($alreadyInstalled) {'ALREADY_INSTALLED'} else {'DEPLOYED'}); command='/komunikacja-studia';
      skillPath=$skillPath; journalPath=$journalPath; journalRevision=$revision;
      runtimeActivated=$false; nextStep='W Claude Code wpisz /komunikacja-studia. Jesli polecenia nie widac, uzyj /reload-skills.'} | ConvertTo-Json
    exit 0
} catch {
    [Console]::Error.WriteLine((@{status='ERROR';error=$_.Exception.Message} | ConvertTo-Json -Compress))
    exit 2
} finally {
    if ($temporaryPath -and [IO.File]::Exists($temporaryPath)) { [IO.File]::Delete($temporaryPath) }
    if ($gateWriteGuard) { $gateWriteGuard.Dispose() }
    if ($deploymentLock) { $deploymentLock.Dispose() }
}
