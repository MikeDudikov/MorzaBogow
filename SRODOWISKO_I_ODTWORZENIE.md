# Środowisko pracy nad grą — składniki i odtworzenie na innym komputerze

Dokument opisuje wyłącznie środowisko pracy: narzędzia, Unity, WSL oraz konfigurację Claude Code i studia. Nie opisuje stanu gry.

Stan na 12.11.2026, komputer źródłowy: Windows 11 Home 10.0.26200. Dokument jest dla Claude Code. Wykonuj go po kolei i nie czytaj innych dokumentów, dopóki krok 5 (weryfikacja) nie przejdzie. `<REPO>` oznacza katalog klonu.

## 0. Prompt startowy (wklej w Claude Code otwartym w `<REPO>`)

```
Przeczytaj tylko SRODOWISKO_I_ODTWORZENIE.md i wykonaj kroki 1–5 po kolei.
Instaluj wyłącznie wymienione składniki; o każdą instalację i wartość sekretu pytaj mnie.
Nie czytaj innych dokumentów, dopóki krok 5 nie przejdzie. Na koniec pokaż tylko tabelę z sekcji 5.
Potem przeczytaj CLAUDE.md i działaj według niego.
```

## 1. Składniki

| Składnik | Wersja źródłowa | Instalacja (Windows) | Wymagany do |
|---|---|---|---|
| Git | 2.56.0 | `winget install --id Git.Git -e` | wszystko |
| GitHub CLI | 2.103.3 | `winget install --id GitHub.cli -e`, potem `gh auth login` | klon, push |
| PowerShell 7 | 7.6.9 (wymagane ≥ 7.5) | `winget install --id Microsoft.PowerShell -e` | hooki, skrypty studia |
| Claude (aplikacja z Claude Code) | 5.1231.2.0 | `winget install --id Anthropic.Claude -e` | praca agentów |
| Unity Hub | 3.22+ | `winget install --id Unity.UnityHub -e` | instalacja edytora |
| Unity Editor | **6000.3.28f1** (HDRP 17.3.0) | Unity Hub → Installs → 6000.3.28f1, domyślnie `C:\Program Files\Unity\Hub\Editor\6000.3.28f1` | kompilacja, testy, bramka |
| WSL 2 + Ubuntu | Ubuntu 26.04.1 LTS | `wsl --install -d Ubuntu` (wymaga restartu) | Python dla narzędzi |
| Python w WSL | 3.14.4 (wymagane ≥ 3.10) | w Ubuntu: `sudo apt install -y python3` | `check_unity_errors.py`, narzędzia studia, MCP assetów |
| Node.js | 26.13.0 | `winget install --id OpenJS.NodeJS.LTS -e` | MCP Meshy (npx) |
| .NET SDK | 10.0.403 | `winget install --id Microsoft.DotNet.SDK.10 -e`; ID sprawdź `winget search` | opcjonalny harness .NET programistów |
| Codex (OpenAI) | 26.11 | Microsoft Store | opcjonalnie; skrzynka Claude–Codex |
| Blender, ComfyUI | brak na źródłowym | — | opcjonalnie; potok assetów (`BLENDER_PATH`, `COMFYUI_URL`) |

Pythona dla Windows nie instalujemy, bo na komputerze źródłowym jest tylko alias Sklepu Windows. Wszystkie narzędzia Python działają w WSL.

## 2. Klon i ustawienia git

```powershell
gh repo clone MikeDudikov/MorzaBogow <REPO>   # gałąź domyślna: balans-skala
git -C <REPO> config core.autocrlf true      # jak na źródłowym; .gitattributes wymusza LF dla qa/*.json
```

## 3. Dopasowanie do komputera

Zmień tylko to, co różni się od źródłowego:

1. **Ścieżka repozytorium.** Na źródłowym jest to `C:\Users\<użytkownik źródłowy>\MorzaBogow`. Inna ścieżka wymaga zmian:
   - **`.mcp.json`:** serwer `morza-bogow-assets` ma w `args` ścieżkę WSL do `Tools/asset_pipeline/ap.py` z komputera źródłowego. Zamień ją na wynik `wsl wslpath -a '<REPO>\Tools\asset_pipeline\ap.py'`.
   - **`CLAUDE.md`:** zawiera ścieżki źródłowe. Zgłoś różnicę użytkownikowi i zmieniaj dopiero po jego zgodzie, bo to plik instrukcji projektu.
2. **Ścieżka Unity.** `.claude/qa/policy.json` oraz `Tools/asset_pipeline/config/pipeline.json` (`unity.defaultPath`) wskazują `C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe`. Inna ścieżka: ustaw zmienną `UNITY_PATH` i zgłoś różnicę. Polityki bramki nie zmieniaj bez zgody użytkownika.
3. **Zmienne środowiskowe** (zakres User; wartości podaje użytkownik i nigdy nie trafiają do repozytorium):
   - `MESHY_API_KEY` — MCP Meshy i potok assetów (płatne);
   - opcjonalnie `COMFYUI_URL`, `BLENDER_PATH`, `UNITY_PATH`.

   `.mcp.json` przekazuje je do WSL przez `WSLENV=MESHY_API_KEY/u:COMFYUI_URL/u:BLENDER_PATH/p:UNITY_PATH/p`.
4. **Nakładka Python dla `Sprawdz-OdbiorStudia.ps1`**, który wymaga `-Python <plik wykonywalny>`. Utwórz poza repozytorium plik `wsl-python3.ps1`:

```powershell
# Przekazuje argumenty do python3 w WSL; tłumaczy ścieżki X:\... na /mnt/x/...
$conv = foreach ($a in $args) { $s = [string]$a
  if ($s -match '^([A-Za-z]):[\\/](.*)$') { '/mnt/' + $Matches[1].ToLower() + '/' + ($Matches[2] -replace '\\','/') } else { $s } }
& wsl.exe -e python3 -B @conv
exit $LASTEXITCODE
```

## 4. Pierwsze uruchomienie

1. **Import i kompilacja** (pierwszy raz tworzy `Library/`, długo):
   `& '<Unity.exe>' -batchmode -nographics -projectPath <REPO> -quit -logFile <REPO>\artifacts-log\compile.log`
2. **Kontrola błędów:** `wsl python3 /mnt/<dysk>/<ścieżka>/check_unity_errors.py <log w ścieżce WSL>`. Kod 0 = brak błędów, 1 = błędy, 2 = brak logu (to nie sukces).
3. **Dziennik studia:** `pwsh -NoProfile -File <REPO>\Komunikacja-Studio.ps1 -ProjectRoot <REPO> -Action Init`. Historia dziennika z komputera źródłowego (`work/`) nie jest w git; zob. sekcję 6.
4. **Kontrola konfiguracji studia:** `pwsh -NoProfile -File <REPO>\Sprawdz-StudioPremium.ps1 -Json`. Oczekiwany wynik: `CONFIGURED_RUNTIME_UNVERIFIED` — routing modeli potwierdza się dopiero w metadanych sesji.

## 5. Weryfikacja (jedno polecenie, raport w tabeli)

```powershell
$r='<REPO>'; $u='C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe'
[ordered]@{
 git=(git --version); gh=(gh auth status 2>&1 | Select-String 'Logged in' | % Line); pwsh="$($PSVersionTable.PSVersion)"
 unity=(Test-Path $u); node=(node --version); wslPython=(wsl -e python3 --version)
 gitattributes=(Test-Path "$r\.gitattributes"); agenci=(Get-ChildItem "$r\.claude\agents" -Filter *.md).Count
 hooki=((Get-Content "$r\.claude\settings.json" -Raw | ConvertFrom-Json).hooks.PSObject.Properties.Name -join ',')
 meshyKey=[bool][Environment]::GetEnvironmentVariable('MESHY_API_KEY','User')
 snapshot=(pwsh -NoProfile -File "$r\Sprawdz-OdbiorStudia.ps1" -Action Snapshot -ProjectRoot $r)
} | Format-Table -AutoSize
```

Oczekiwane wartości:
- `unity`, `gitattributes`: True;
- `agenci`: 5;
- `hooki`: `SessionStart,TaskCompleted,PreToolUse`;
- `wslPython`: ≥ 3.10;
- `meshyKey`: True tylko wtedy, gdy potrzebny jest potok assetów.

## 6. Czego nie ma w git (opcjonalne przeniesienie ręczne)

| Element | Gdzie na źródłowym | Po co |
|---|---|---|
| Dziennik i skrzynka studia | `<REPO>\work\studio-communication\`, `<REPO>\work\studio-bridge\` | historia przydziałów, ACK i decyzji |
| Harnessy programistów | `%USERPROFILE%\MorzaBogow_zabezpieczenia\` | narzędzia .NET poza repozytorium |
| Pamięć Claude dla projektu | `%USERPROFILE%\.claude\projects\<ścieżka projektu>\memory\` | trwała pamięć sesji |

Bez nich środowisko działa: dziennik zaczyna się od nowa po `-Action Init`.

## 7. Znane ograniczenia i ustalenia

- **MCP studia** `morza-bogow-studio` (`Wdroz-MCPStudia.ps1`) jest celowo nieskonfigurowany (decyzja użytkownika z 12.11.2026). Skrzynka działa przez CLI w WSL: `wsl -e python3 -B <wsl:REPO>/Tools/studio_bridge/studio_mcp.py --root <wsl:REPO> --endpoint claude call --tool studio_receive`.
- **Instalator MCP studia** przy `-Python wsl.exe` tworzy wadliwy wpis. Nie stosować `-Apply` z WSL.
- **`SHA256SUMS.txt`** utrzymuje studio. Nie uruchamiać skryptów z `work/`, bo nadpisują manifest.
- **Role modeli:** koordynacja i recenzje na Opus, kod na Fable, wyszukiwanie na Haiku (`.claude/agents/`). Model ról potwierdzaj w metadanych JSONL sesji, nie w samej konfiguracji.

## 8. Ściąga poleceń (bez czytania innych plików)

- **Testy Unity** (bez `-nographics`): `& '<Unity.exe>' -batchmode -projectPath <REPO> -runTests -testPlatform EditMode|PlayMode -testResults <xml> -logFile <log>`
- **Bramka** (plan w `.claude/qa/tasks/<TASK>.json` z bieżącym sessionId): `'{"hook_event_name":"TaskCompleted","task_id":"<TASK>","session_id":"<SESSION>"}' | pwsh -NoProfile -File <REPO>\.claude\hooks\quality-gate.ps1`
- **Regresja wzorca:** `pwsh -NoProfile -File <REPO>\Sprawdz-OdbiorStudia.ps1 -Action Regression -Python <ścieżka wsl-python3.ps1> -Report qa/reports/<TASK>-<runId>/report.json`
