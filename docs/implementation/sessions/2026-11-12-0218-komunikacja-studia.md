# Komunikacja i ciągłość pracy studia

Data: 2026-11-12. Autor konfiguracji i narzędzia: Codex. Zakres: organizacja produkcji gry; P/ID/Q/J = N/A, ponieważ nie zmieniano mechanik ani ich odbioru. Status: `LOCAL_PROTOCOL_READY`; komunikacja i modele w działającym Claude Code: `NOT_RUN`.

## Wynik

Gotowy [prompt komunikacji](../../../PROMPT_KOMUNIKACJA_STUDIA.md) łączy natywne wiadomości/wznowienie subagentów z lokalnym dziennikiem koordynatora. Wymaga potwierdzonych przydziałów, jednego autora na plik, obsługi blokad, bezpiecznych przekazań i odtworzenia checkpointu wraz z późniejszymi zdarzeniami. Standard pracy odnosi się do studia produkującego gry oraz grywalnych przyrostów w Unity.

[Komunikacja-Studio.ps1](../../../Komunikacja-Studio.ps1) obsługuje Init/Read/Append. Używa blokady uchwytu pliku, rewizji oczekiwanej, atomowego zastąpienia w tym samym katalogu oraz idempotentnego ponowienia identycznej wiadomości. Odrzuca zmienioną treść tego samego ID i ACK niewłaściwego adresata. Istniejący uszkodzony rejestr nie jest zerowany przez Init. Rejestr nie wysyła wiadomości, nie uruchamia modeli i nie egzekwuje semantyki własności plików ani stanów zadań.

Przed bramką protokół wymaga FREEZE z unikalnym freezeId oraz rzeczywistych ACK WRITES_STOPPED wszystkich autorów, także wykonawców spoza tego protokołu. Snapshot jest ustalany po tej barierze. Jeden właściciel Unity, rzeczywiste procesy i istniejąca blokada bramki nadal wymagają sprawdzenia.

Pięć definicji agentów odwołuje się do protokołu. Trzy role do odczytu otrzymały SendMessage; zachowują brak narzędzi edycji, komend i tworzenia agentów. Kontrola matrycy dopuszcza komunikację w tych rolach. Modele Opus/Fable/Haiku są zachowane. Trwające instancje mogą nadal mieć wcześniejszy zestaw narzędzi; protokół przewiduje RESULT_ONLY i bezpieczne przekazanie.

Odnośniki dodano do CLAUDE.md, AGENTS.md, obu promptów studia, startu i README. Nie zmieniano ustawień, hooków ani polityki bramki. Nie modyfikowano kodu gry, scen ani estetyki w ramach tego zadania.

## Walidacja

- Dziennik: **33/33 przypadki PASS**. Artefakty lokalne: `work/communication-tests/test_journal.py`, `work/communication-tests/results.json`. Sprawdzono inicjalizację, brak zapisów przy odczycie, sekwencję rewizji, ACK, konflikt rewizji, dwie równoczesne próby zapisu, ponowienia i odmienną treść, klucze rozróżniające wielkość liter, ciągi dat ISO, głębokość JSON, uszkodzony rejestr i odmowę zastąpienia pliku bez utraty poprzedniego stanu.
- Pełna matryca modeli: **28/28 przypadków PASS**; pięć definicji i projektowy domyślny Opus. Dowód lokalny: `work/studio-matrix/results.json`. Status pozostaje `CONFIGURED_RUNTIME_UNVERIFIED`.
- Parser PowerShell: PASS; uruchomienia na PowerShell 7.6.9. Narzędzie wymaga 7.5, aby zachować ciągi dat JSON przez DateKind String.
- Odczyt przed i po potwierdził niezmienione SHA256 ustawień, dwóch hooków, polityki i przykładowego planu bramki. Niezależny subagent Codex przejrzał protokół i skrypt; konieczne uwagi o canonicalizacji kluczy i datach uwzględniono. Przegląd statyczny nie stanowi uruchomienia Claude ani testu gry.
- Unity, build, playtest, nowe badania historyczne: `NOT_RUN` — zadanie dotyczy narzędzia organizacyjnego, bez implementacji historycznie zależnej mechaniki. Test dziennika nie certyfikuje gry.

## Źródła techniczne

Otworzono oficjalną [dokumentację wiadomości i wznowienia subagentów](https://code.claude.com/docs/en/sub-agents#resume-subagents) i [ograniczeń zespołów](https://code.claude.com/docs/en/agent-teams#limitations). Protokół nie wymaga włączania eksperymentalnego trybu. Otworzono także [dokumentację ConvertFrom-Json](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/convertfrom-json?view=powershell-7.5) dla zachowania ciągów ISO. Dostęp: publiczny tekst HTML; nie zweryfikowano działania tych funkcji w lokalnym kliencie Claude.

## Stan przekazania

Lokalny dziennik `work/studio-communication/journal.json` zainicjalizowano na rewizji 0, bez przypisywania zastanych zadań i autorstwa. `work/` pozostaje ignorowany przez Git. To trwałość na lokalnym dysku, nie kopia zapasowa ani proces działający po zamknięciu aplikacji.

Następny krok w Claude Code: odczytać prompt komunikacji, przyjąć rzeczywiste aktywne zadania do rejestru bez przerywania ich w połowie, sprawdzić dostępne narzędzia oraz rzeczywiste modele, wykonać test wiadomości/ACK wyłącznie do odczytu, zapisać aktualny CHECKPOINT i kontynuować najbliższy niezakończony przyrost. CLI Claude nie jest dostępne na PATH procesu Codex; nie deklaruje się aktywacji transportu ani uruchomienia Fable.
