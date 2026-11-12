# Prompt 1 — pobranie, instalacja i logowanie

Cel: przygotować nowy komputer z Windows do pracy nad grą w Claude Code. Lista składników jest w [SRODOWISKO_NIEZBEDNE.md](SRODOWISKO_NIEZBEDNE.md). Wykonaj krok 0, potem wklej prompt z kroku 1. Następny etap to [PROMPT_WDROZENIE_SRODOWISKA.md](PROMPT_WDROZENIE_SRODOWISKA.md).

## Krok 0 — ręcznie, raz, w zwykłym oknie PowerShell

Claude Code musi najpierw istnieć i mieć dostęp do repozytorium:

```powershell
winget install --id Anthropic.Claude -e; winget install --id Git.Git -e; winget install --id GitHub.cli -e; winget install --id Microsoft.PowerShell -e
```

Zamknij okno i otwórz nowe, żeby odświeżyć PATH, potem:

```powershell
gh auth login --hostname github.com --git-protocol https --web
gh auth setup-git
gh repo clone MikeDudikov/MorzaBogow "$HOME\MorzaBogow"
```

Uruchom aplikację **Claude**, zaloguj się i otwórz kartę **Code** w folderze `%USERPROFILE%\MorzaBogow`.

## Krok 1 — wklej w Claude Code

```
Przygotowujesz ten komputer do pracy nad grą Morza Bogów (repozytorium w bieżącym folderze).
Masz moją zgodę na instalację WYŁĄCZNIE poniższych składników przez winget, wsl i Unity Hub — nic spoza listy.
Nie wpisuj żadnych haseł, kodów, kluczy ani danych logowania; logowania robię sam, gdy mnie poprosisz.
Nie czytaj plików repozytorium, chyba że polecenie tego wymaga. Polecenia uruchamiaj w PowerShell 7.

1. Sprawdź i doinstaluj brakujące: Unity.UnityHub, OpenJS.NodeJS.LTS (pomiń zainstalowane; `winget list --id <ID> -e`).
   Akceptuję umowy licencyjne tych dwóch pakietów, więc dodaj --accept-source-agreements --accept-package-agreements.
   Okno zgody administratora zatwierdzam sam.
2. Unity Editor 6000.3.28f1 (revision 8f082d2fe476, tylko Windows/Mono, bez dodatkowych modułów):
   a) znajdź "Unity Hub.exe" (domyślnie C:\Program Files\Unity Hub\) i uruchom:
      & "<Unity Hub.exe>" -- --headless install --version 6000.3.28f1 --changeset 8f082d2fe476
   b) jeśli Hub CLI zawiedzie: Start-Process "unityhub://6000.3.28f1/8f082d2fe476" i poproś mnie o kliknięcie Instaluj.
   Instalacja trwa długo: uruchom ją w tle i czekaj na powiadomienie zamiast odpytywać.
3. Poproś mnie, żebym w Unity Hub zalogował się na Unity ID i aktywował licencję (Preferences → Licenses → Add → Personal). Czekaj na moje "gotowe".
4. WSL: jeśli `wsl -l -v` nie pokazuje Ubuntu, uruchom `wsl --install -d Ubuntu` (wymaga administratora). Potem powiedz mi, że trzeba
   zrestartować komputer i przy pierwszym uruchomieniu Ubuntu utworzyć użytkownika Linux — to robię sam.
   Jeśli Ubuntu już działa: `wsl -e python3 --version` musi dać >= 3.10; inaczej poproś mnie o `sudo apt install -y python3` w Ubuntu.
5. Klucz Meshy (tylko do generowania assetów): NIE pytaj o wartość. Pokaż mi polecenie do wpisania samodzielnie:
   [Environment]::SetEnvironmentVariable('MESHY_API_KEY','<mój klucz>','User')  — i zapytaj, czy potok assetów jest mi potrzebny.
6. Na koniec pokaż tylko tabelę: składnik | stan (OK / brak / czeka na mnie) | co zrobić. Nie wykonuj kroków wdrożenia.
```

Po restarcie i utworzeniu użytkownika Ubuntu przejdź do [PROMPT_WDROZENIE_SRODOWISKA.md](PROMPT_WDROZENIE_SRODOWISKA.md).
