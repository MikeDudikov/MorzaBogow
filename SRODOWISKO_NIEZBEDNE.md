# Niezbędne składniki środowiska pracy nad grą w Claude Code

Lista obejmuje wszystko, bez czego na innym komputerze nie da się pracować tak jak na komputerze źródłowym, stan na 12.11.2026. Kolejność odtworzenia:
1. [PROMPT_INSTALACJA_SRODOWISKA.md](PROMPT_INSTALACJA_SRODOWISKA.md) — pobranie, instalacja i logowania;
2. [PROMPT_WDROZENIE_SRODOWISKA.md](PROMPT_WDROZENIE_SRODOWISKA.md) — konfiguracja i weryfikacja.

Szczegóły i ściąga poleceń są w [SRODOWISKO_I_ODTWORZENIE.md](SRODOWISKO_I_ODTWORZENIE.md).

## A. Konta i logowania

Logowania wykonuje wyłącznie człowiek. Claude nie wpisuje haseł, kodów ani kluczy.

| Konto | Do czego | Niezbędne |
|---|---|---|
| Anthropic (aplikacja Claude) z dostępem do modeli **Opus** i **Fable** | Claude Code i role studia: koordynator i recenzent na Opus, programista na Fable | tak |
| GitHub z dostępem do `MikeDudikov/MorzaBogow` | klon, commit, push (`gh auth login`) | tak |
| Unity ID z licencją Unity Personal (lub wyższą) | edytor, testy i bramka w trybie batch | tak |
| Konto WSL Ubuntu (lokalny użytkownik Linux) | Python dla narzędzi | tak |
| Meshy (klucz API, płatny) | generowanie modeli 3D w potoku assetów | tylko do assetów |
| OpenAI Codex (Microsoft Store) | skrzynka wiadomości Claude–Codex | nie |

## B. Narzędzia niezbędne

| Narzędzie | Wersja źródłowa | Instalacja | Do czego |
|---|---|---|---|
| Windows 10/11 x64 | Windows 11 Home 10.0.26200 | — | hooki i skrypty PowerShell, Unity |
| Aplikacja Claude (Claude Code) | 5.1231.2.0 | `winget install --id Anthropic.Claude -e` | praca w Claude Code |
| Git | 2.56.0 | `winget install --id Git.Git -e` | repozytorium; `core.autocrlf=true` |
| GitHub CLI | 2.103.3 | `winget install --id GitHub.cli -e` | logowanie i klon |
| PowerShell 7 (`pwsh`) | 7.6.9 (≥ 7.5) | `winget install --id Microsoft.PowerShell -e` | hooki `.claude/settings.json`, skrypty studia, bramka |
| Unity Hub | 3.22+ | `winget install --id Unity.UnityHub -e` | instalacja edytora i licencja |
| Unity Editor | **6000.3.28f1**, revision `8f082d2fe476`; backend Mono (IL2CPP niepotrzebny); HDRP 17.3.0 | Hub CLI albo link `unityhub://6000.3.28f1/8f082d2fe476` | kompilacja, EditMode/PlayMode, bramka |
| WSL 2 + Ubuntu | Ubuntu 26.04.1 LTS | `wsl --install -d Ubuntu` (administrator, restart) | Python |
| Python 3 w WSL | 3.14.4 (≥ 3.10, sama biblioteka standardowa) | w Ubuntu domyślnie; inaczej `sudo apt install -y python3` | `check_unity_errors.py`, `Tools/studio_bridge`, `Tools/studio_quality`, `Tools/asset_pipeline` |
| Node.js LTS | 26.13.0 | `winget install --id OpenJS.NodeJS.LTS -e` | serwer MCP Meshy (`npx`) |

## C. Narzędzia opcjonalne

| Narzędzie | Instalacja | Do czego |
|---|---|---|
| .NET SDK 10 | `winget install --id Microsoft.DotNet.SDK.10 -e`; ID sprawdź przez `winget search` | runnery .NET programistów poza Unity |
| OpenAI Codex | Microsoft Store | drugi wykonawca i skrzynka |
| Blender, ComfyUI | według potrzeb potoku assetów | `BLENDER_PATH`, `COMFYUI_URL` |
| Visual Studio / VS Code | `winget install --id Microsoft.VisualStudio.Community -e` | wygodna edycja C#, nie jest wymagana do kompilacji |

## D. Dokumenty i konfiguracja

Wszystkie są w repozytorium na gałęzi `balans-skala`, sprawdzone 12.11.2026: 42/42 w git. Brak któregokolwiek zmienia sposób pracy.

| Grupa | Pliki | Rola |
|---|---|---|
| Instrukcje główne | `CLAUDE.md`, `AGENTS.md` | zasady pracy Claude Code, wczytywane na starcie |
| Plan (nadrzędny i wykonawczy) | `docs/NOWY_PLAN_GRY.md`, `docs/PLAN_WDROZENIA.md`, `docs/ANALIZA_STANU.md`, `docs/HISTORIA_I_ZRODLA.md`, `docs/KATALOG_MECHANIK.md`, `docs/PARAMETRY_ROBOCZE.md`, `docs/MAPY_I_SCENARIUSZE.md`, `docs/GRYWALNOSC_I_ODBIOR.md` | czytane przy każdym rozpoczęciu pracy (`CLAUDE.md`) |
| Rygor i kontrakty | `docs/RYGOR_PRODUKCJI_I_ODBIORU.md`, `docs/KARTA_ZGODNOSCI_Z_PLANEM.md`, `docs/KONTRAKTY_MORZA_I_WIATRU.md`, `docs/KOMUNIKACJA_CLAUDE_CODEX_I_REFERENCJE.md` | odbiór, zgodność z planem, morze, skrzynka |
| Protokół studia | `PROMPT_KOMUNIKACJA_STUDIA.md`, `PROMPT_STUDIO_PREMIUM.md`, `PROMPT_STUDIO_FABLE.md` | dziennik, ACK i FREEZE, macierz modeli |
| Role studia | `.claude/agents/` (5 plików: architect, reviewer, search, programmer, test-engineer) | subagenci z modelami Opus, Fable i Haiku |
| Skille | `.claude/skills/komunikacja-studia/`, `.claude/skills/asset-pipeline/` | komenda `/komunikacja-studia`, potok assetów |
| Ustawienia i hooki | `.claude/settings.json`, `.claude/hooks/session-context.ps1`, `.claude/hooks/quality-gate.ps1` | model domyślny, hooki SessionStart, TaskCompleted i PreToolUse (bramka) |
| Bramka i QA | `.claude/qa/policy.json`, `.claude/qa/tasks/`, `qa/studio/regression-baseline.json` | profile EditMode/PlayMode, plany bramek, wzorzec regresji |
| Skrypty studia | `Komunikacja-Studio.ps1`, `Komunikacja-ClaudeCodex.ps1`, `Sprawdz-StudioPremium.ps1`, `Sprawdz-StudioFable.ps1`, `Sprawdz-OdbiorStudia.ps1`, `Wdroz-KomunikacjeStudia.ps1`, `Wdroz-MCPStudia.ps1` | dziennik, kontrole konfiguracji i odbioru |
| Narzędzia | `check_unity_errors.py`, `Tools/studio_bridge/`, `Tools/studio_quality/`, `Tools/asset_pipeline/`, `Tools/visual/` (`water_guard.py`) | kontrola logu Unity, skrzynka, walidatory, potok assetów, ochrona wody |
| MCP i git | `.mcp.json` (serwery `meshy`, `morza-bogow-assets`), `.gitattributes` | narzędzia MCP w sesjach, LF dla `qa/*.json` |
| Dane QA | `qa/runtime-catalog.json`, `qa/legacy-map.json` | eksport katalogu i mapa danych LEGACY |

## E. Zmienne środowiskowe (zakres User)

Wartości podaje i wpisuje człowiek.
- `MESHY_API_KEY` — wymagana tylko do potoku assetów i MCP Meshy;
- `UNITY_PATH` — tylko gdy edytor nie stoi w `C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe`;
- `BLENDER_PATH`, `COMFYUI_URL` — opcjonalne.

`.mcp.json` przekazuje je do WSL przez `WSLENV`.

## F. Elementy lokalne spoza git

Nie są potrzebne do pracy; przenosi się je ręcznie, jeśli trzeba zachować historię:
- dziennik i skrzynka studia (`work/`);
- harnessy .NET (`%USERPROFILE%\MorzaBogow_zabezpieczenia\`);
- pamięć Claude dla projektu.

Szczegóły: [SRODOWISKO_I_ODTWORZENIE.md](SRODOWISKO_I_ODTWORZENIE.md), sekcja 6.

## G. Ścieżki zależne od komputera

Hooki używają `${CLAUDE_PROJECT_DIR}`, a polityka bramki `{project}`, więc nie zależą od położenia klonu.

Bezwzględną ścieżkę komputera źródłowego zawierają (sprawdzone 12.11.2026):
- `.mcp.json` — ścieżka WSL do `Tools/asset_pipeline/ap.py`;
- `CLAUDE.md` — polecenie kompilacji batch;
- `PROMPT_KOMUNIKACJA_STUDIA.md`, `PROMPT_STUDIO_PREMIUM.md`, `PROMPT_STUDIO_FABLE.md` — katalog pracy;
- `.claude/skills/komunikacja-studia/SKILL.md` — trzy polecenia dziennika.

Klon w tej samej ścieżce nie wymaga zmian. Inną ścieżkę dopasowuje [PROMPT_WDROZENIE_SRODOWISKA.md](PROMPT_WDROZENIE_SRODOWISKA.md) w kroku 2; to lokalna adaptacja, której nie commitujemy.

Bramka (`.claude/qa/policy.json`) wywołuje edytor w domyślnej lokalizacji Hub: `C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe`.
