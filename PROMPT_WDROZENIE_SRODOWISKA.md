# Prompt 2 — wdrożenie i weryfikacja środowiska

Uruchom po [PROMPT_INSTALACJA_SRODOWISKA.md](PROMPT_INSTALACJA_SRODOWISKA.md), gdy narzędzia są zainstalowane, Unity ma licencję, a Ubuntu w WSL jest skonfigurowane. Wklej poniższy prompt w Claude Code otwartym w folderze klonu.

```
Wdrażasz środowisko pracy nad grą Morza Bogów w bieżącym folderze (klon MikeDudikov/MorzaBogow, gałąź balans-skala).
Nie instaluj nowych programów. Nie wpisuj haseł ani kluczy. Nie czytaj dokumentów gry, dopóki krok 7 nie przejdzie.
Polecenia uruchamiaj w PowerShell 7; $r = pełna ścieżka bieżącego folderu, $u = C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe
(jeśli tam go nie ma, użyj zmiennej UNITY_PATH albo znajdź Unity.exe 6000.3.28f1 i podaj mi ścieżkę).

1. Git: `git -C $r config core.autocrlf true`; sprawdź `git -C $r status -sb` (gałąź balans-skala, czysto).
2. Ścieżki zależne od komputera (hooki używają ${CLAUDE_PROJECT_DIR}, a bramka {project} — tych nie zmieniaj):
   a) Znajdź ścieżki komputera źródłowego:
      Select-String -Path "$r\.mcp.json","$r\CLAUDE.md","$r\PROMPT_KOMUNIKACJA_STUDIA.md","$r\PROMPT_STUDIO_PREMIUM.md","$r\PROMPT_STUDIO_FABLE.md","$r\.claude\skills\komunikacja-studia\SKILL.md" -Pattern 'Users[\\/]+[^\\/]+[\\/]+MorzaBogow'
      Jeśli to dokładnie $r, pomiń resztę kroku 2.
   b) .mcp.json: w serwerze morza-bogow-assets zamień ścieżkę do ap.py na wynik  wsl -e wslpath -a "$r\Tools\asset_pipeline\ap.py"
      (koniecznie z -e — bez niego powłoka WSL usuwa ukośniki \). Resztę pliku zostaw.
   c) Pozostałe znalezione pliki (instrukcje, prompty studia, skill komunikacja-studia): pokaż mi każdą różnicę i zamień na $r
      dopiero po mojej zgodzie. Raportów w docs/ nie zmieniaj — to historia.
   d) Jeśli Unity nie stoi w $u, nie zmieniaj .claude/qa/policy.json bez mojej zgody (to polityka bramki).
   e) Zmian z kroku 2 nie commituj — dotyczą tylko tego komputera. Przed git pull: git stash; po nim: git stash pop.
3. Pierwszy import i kompilacja Unity (długo, nawet godzina; uruchom w tle i czekaj na powiadomienie zamiast odpytywać).
   Samo `& $u` nie czeka na koniec Unity (to aplikacja okienkowa), dlatego:
   $log = "$r\artifacts-log\compile.log"; New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
   $p = Start-Process -FilePath $u -ArgumentList "-batchmode -nographics -projectPath `"$r`" -quit -logFile `"$log`"" -Wait -PassThru -NoNewWindow; $p.ExitCode
   Potem: wsl -e python3 "$(wsl -e wslpath -a "$r\check_unity_errors.py")" "$(wsl -e wslpath -a $log)"
   Kod 0 = OK; 1 = błędy (pokaż je); 2 = brak logu. Błąd licencji w logu: poproś mnie o aktywację licencji w Unity Hub.
   W jednej chwili może działać tylko jedna instancja Unity dla projektu.
4. Dziennik studia: pwsh -NoProfile -File "$r\Komunikacja-Studio.ps1" -ProjectRoot $r -Action Init
5. Konfiguracja studia: pwsh -NoProfile -File "$r\Sprawdz-StudioPremium.ps1" -Json   (oczekiwane: CONFIGURED_RUNTIME_UNVERIFIED)
6. Nakładka Pythona dla Sprawdz-OdbiorStudia.ps1: utwórz POZA repozytorium plik "$HOME\wsl-python3.ps1" z treścią:
   $conv = foreach ($a in $args) { $s = [string]$a; if ($s -match '^([A-Za-z]):[\\/](.*)$') { '/mnt/' + $Matches[1].ToLower() + '/' + ($Matches[2] -replace '\\','/') } else { $s } }
   & wsl.exe -e python3 -B @conv
   exit $LASTEXITCODE
7. Weryfikacja — pokaż tylko tę tabelę:
   git, gh (zalogowany?), pwsh (>= 7.5), unity (Test-Path $u), node, wslPython (>= 3.10), .gitattributes (jest?),
   agenci (liczba plików .claude/agents/*.md = 5), hooki (.claude/settings.json: SessionStart,TaskCompleted,PreToolUse),
   kompilacja (kod check_unity_errors), dziennik (rev po Init), studio (wynik kroku 5), meshyKey (zmienna User istnieje? — bez wartości).
8. Jeśli wszystko OK: przeczytaj CLAUDE.md i działaj według niego od następnego polecenia. Jeśli nie: wypisz tylko, co blokuje.
```

Opcjonalnie, żeby zachować historię, przenieś ręcznie elementy z sekcji F listy [SRODOWISKO_NIEZBEDNE.md](SRODOWISKO_NIEZBEDNE.md): dziennik studia, harnessy i pamięć Claude.
