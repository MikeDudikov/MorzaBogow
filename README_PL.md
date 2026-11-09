# Morza Bogów — dokumentacja dla Claude Code

Paczka określa wymagania gry oraz sposób przygotowania projektu, implementacji i odbioru zadań.

Głównym doświadczeniem jest dowodzenie flotą podczas bitew, eskort, rajdów i wypraw mitologicznych. Gospodarka dwóch zasobów, lokalne porty i automatyczne konwoje tworzą cele do walki. Czytelność, dostępna kontrgra i obciążenie uwagi gracza są warunkami odbioru.

## Dokumenty

- GAME_VISION.md — doświadczenie gracza, filary rozgrywki, cele tempa i balansu.
- GAME_DESIGN.md — mechaniki, floty, walka, magia, SI, środowisko i zakres produkcji.
- ECONOMY_DESIGN.md — reguły E00–E16 dotyczące dwóch zasobów, portów i logistyki morskiej.
- ECONOMY_BALANCE.md — parametry, zależności liczbowe i cele balansu.
- VISUAL_TECH_SPEC.md — wymagania V00–V13 dotyczące obrazu, wody, światła i wydajności.
- MYTHIC_SCENARIOS.md — 14 misji kampanii z celami, flotami, zasobami i przebiegiem.
- CLAUDE.md — zasady pracy, kontroli jakości i zamykania zadań.
- START_PROMPT_PL.md — prompt uruchamiający pracę.
- qa/ — parametry oraz macierze kryteriów odbioru.
- .claude/ — konfiguracja hooków i egzekwowana bramka testów.

## Przygotowanie

1. Umieść zawartość paczki w katalogu głównym projektu, razem z .claude. Scal istniejące ustawienia oraz tablice hooks.
2. Zapewnij PowerShell 7, Unity Editor z HDRP, Unity Test Framework i Windows Build Support. Jeśli pwsh.exe nie jest dostępny na PATH, ustaw jego pełną ścieżkę w .claude/settings.json.
3. W obu profilach .claude/qa/policy.json zastąp CONFIGURE_ABSOLUTE_PATH_TO_UNITY_EXE pełną ścieżką Unity.exe. Nieuzupełniona ścieżka blokuje zamykanie zadań.
4. Rozpocznij sesję Claude Code, sprawdź /hooks i wklej START_PROMPT_PL.md. SessionStart podaje QUALITY_GATE_SESSION_ID, który należy wpisać w planach testów.
5. Dla zadania przygotuj .claude/qa/tasks/<taskId>.json według szablonu. Podaj taskId, sessionId, wymagania i pełne nazwy rzeczywistych testów EditMode oraz PlayMode.
6. Przed uruchomieniem bramki zamknij edytor Unity korzystający z tego samego projektu. Bramka wymaga wyłącznego dostępu.

## Kontrola danych i testów

Uruchom node qa/scenario-design-check.cjs do kontroli danych kampanii. STRUCTURE_VALID potwierdza strukturę, ID, parametry i graf faz. Działanie scenariuszy, geometrię tras i balans odbiera się przez testy Unity oraz playtesty.

Uruchom pwsh -NoProfile -File qa/tests/test-gate.ps1 do sprawdzenia mechanizmu bramki w izolowanym katalogu work/. Syntetyczny runner służy wyłącznie temu sprawdzeniu. Wyniki testów gry muszą powstawać podczas rzeczywistego uruchomienia Unity.

TaskCompleted i PreToolUse dla TaskUpdate/completed wywołują synchroniczną bramkę. Każde zamknięcie uruchamia wymagane testy, zapisuje nowe XML i logi oraz weryfikuje kody procesu, liczniki, nazwy testów i asercje. Zero testów, skipped, inconclusive, błędny XML, brak artefaktów, timeout lub modyfikacja plików podczas testów blokują wynik. Sam napis PASS ani istnienie raportu nie wystarczają.

Snapshot obejmuje Assets, Packages, ProjectSettings, .claude, qa z wyjątkiem qa/reports, docs oraz wszystkie dokumenty wykonawcze paczki. Po dodaniu plików runtime poza tym zakresem rozszerz konfigurację snapshotu.

## Odbiór

Macierze ECONOMY_ACCEPTANCE.csv, VISUAL_ACCEPTANCE.csv i SCENARIO_ACCEPTANCE.csv określają wymagane dowody. Status NOT_RUN zmienia się wyłącznie po rzeczywistym wykonaniu danego sprawdzenia. Wymagane są testy EditMode i PlayMode, build Windows, ocena obrazu, pomiary wydajności i playtesty.

Lokalny hook jest osobnym procesem, ale pliki konfiguracyjne pozostają edytowalne. Twardy odbiór wydania wymaga zewnętrznego CI z chronioną polityką, czystym checkoutem i obowiązkowym review. onFailure:block nie zapewnia blokady awarii uruchomienia TaskCompleted; ścieżkę ręcznego TaskUpdate chroni dodatkowy PreToolUse. Obie ścieżki należy sprawdzić w działającym Claude Code.
