# Studio — modele Opus, Fable i Haiku

Stosuj także [protokół trwałej komunikacji](PROMPT_KOMUNIKACJA_STUDIA.md): przydział, potwierdzenie zakresu, meldunki blokad i bezpieczne przekazanie. Wiadomości i wznowienie nie zwalniają z weryfikacji rzeczywistego modelu. Tylko koordynator zapisuje dziennik; pozostałe role raportują bez rozszerzania swoich uprawnień.

Pracuj w `C:\Users\noirp\MorzaBogow`. Zastosuj cały [PROMPT_STUDIO_PREMIUM.md](PROMPT_STUDIO_PREMIUM.md), [PROMPT_WDROZENIOWY.md](PROMPT_WDROZENIOWY.md), [CLAUDE.md](CLAUDE.md) i [AGENTS.md](AGENTS.md). Ten dokument uzupełnia dobór modeli do ról. Zachowuje mechaniki, estetykę, postępy, plan P01–P12, Q01–Q12, J01–J10 i istniejącą bramkę.

| Zadanie | Model | Definicja |
|---|---|---|
| Koordynacja i decyzje | Opus | Główna sesja, model domyślny projektu opus; aktualny wybór potwierdź w kliencie. |
| Projekt architektury | Opus | [studio-architect-opus](.claude/agents/studio-architect-opus.md), tylko odczyt. |
| Implementacja, refaktoryzacja, naprawy | Fable | [studio-programmer-fable](.claude/agents/studio-programmer-fable.md). |
| Pisanie testów i narzędzi | Fable | [studio-test-engineer-fable](.claude/agents/studio-test-engineer-fable.md). |
| Niezależne review krytycznych zmian | Opus | [studio-reviewer-opus](.claude/agents/studio-reviewer-opus.md), tylko odczyt. |
| Proste wyszukiwanie i porządkowanie informacji | Haiku | [studio-search-haiku](.claude/agents/studio-search-haiku.md), zestawienie bez edycji. |

Pole model w `.claude/settings.json` ustala domyślny Opus dla głównej sesji, bez zastępowania standardowego agenta głównego. Wyższe ustawienia i wybór w już działającej rozmowie mogą go nadpisać. W razie potrzeby wybierz `/model opus` w bezpiecznym punkcie przekazania i potwierdź wynik. Nie cofaj kodu ani historii rozmowy w celu przełączenia modelu.

## 1. Przydział modeli

**Wszystkie etapy programowania realizuj przez subagentów z jawnym wyborem Fable.** Dotyczy to kodu rozgrywki, symulacji, ekonomii, AI, zapisu, edytora, testów automatycznych, skryptów i narzędzi programistycznych, napraw oraz poprawek po review. Nie omijaj reguły przez edycję kodu w głównym agencie lub wykonywanie wygenerowanego patcha przez inną rolę.

Korzystaj z definicji [studio-programmer-fable](.claude/agents/studio-programmer-fable.md), zawierającej `model: fable`. Wywołując ten typ przez Agent, podaj również `model: fable`, jeżeli wersja klienta udostępnia taki parametr. Nie podawaj innego modelu ani inherit. Wznowienie i kolejne zlecenia zachowują ten wybór.

Główny agent Opus koordynuje, analizuje, przygotowuje przydziały, czyta wyniki, prowadzi dokumentację i organizuje integrację. Haiku gromadzi proste wskazania; istotną interpretację wykonuje Opus. Niezależny reviewer Opus pracuje do odczytu; poprawkę kodu odsyła programiście Fable. Koordynator nie stosuje ręcznie patcha reviewera wygenerowanego na innym modelu. Każdej delegacji nadaj właściwy typ i jawny model, jeśli klient obsługuje ten parametr. Nie przekazuj programowania forkowi lub general-purpose dziedziczącemu model koordynatora.

Możesz uruchomić kilka instancji programisty Fable, gdy mają niezależne zadania i rozłączne pliki. Dla jednego pliku istnieje jeden aktywny autor. Jedna osoba lub agent koordynuje uruchomienia Unity. Nie zmieniaj globalnie wszystkich subagentów przez CLAUDE_CODE_SUBAGENT_MODEL_FORCE, ponieważ ta reguła dotyczy programowania, a nie całego zespołu.

Globalne wymuszenie jednego modelu koliduje z tą matrycą, także gdy jest to Fable. Ujawnij konflikt i ustal właściwą konfigurację zamiast deklarować inny model dla review lub wyszukiwania. Nie zmieniaj automatycznie ustawień użytkownika, organizacji ani dostawcy. Przy konflikcie sprawdź rzeczywistą kolejność ustawień, w tym pierwszeństwo lokalnych nad projektowymi. Niezależność review wymaga także innego wykonawcy niż autor.

## 2. Sprawdzenie przed programowaniem

1. Odczytaj aktualny Git, stan roboczy, raporty i trwające zadania. Zachowaj nierozliczoną pracę. Zmiana modelu nie jest powodem cofania kodu, zmiany numeru zapisu ani powtarzania odebranego etapu.
2. Uruchom pełną lokalną kontrolę: `pwsh -NoProfile -File .\Sprawdz-StudioPremium.ps1 -Json`. To kontrola pięciu definicji, domyślnego modelu i znanych ustawień, nie test dostępu do modeli ani dowód rzeczywistego wykonania. Węższy Sprawdz-StudioFable.ps1 pozostaje kontrolą samego programisty, a nie całej matrycy.
3. Potwierdź załadowanie typu studio-programmer-fable. Jeżeli katalog `.claude/agents` powstał po rozpoczęciu tej sesji i klient nie widzi definicji, zachowaj punkt przekazania i w bezpiecznym momencie uruchom ponownie lub wznów Claude Code. Nie przerywaj niezapisanej pracy ani trwającej bramki.
4. Sprawdź dostępność Fable i ograniczenia klienta, konta, dostawcy, aliasu, ustawień organizacji oraz ewentualnego wymuszenia modelu. Nie zmieniaj ustawień globalnych, uprawnień ani zabezpieczeń, żeby obejść ograniczenie.
5. Pierwsze wywołanie każdej potrzebnej roli jest wyłącznie do odczytu: bez edycji i bez testów gry. Zażądaj odczytu instrukcji i krótkiego opisu przydzielonego zadania. Sprawdź faktyczny model w metadanych uruchomienia lub `/tasks`. Sama nazwa agenta i deklaracja w jego odpowiedzi nie są dowodem. Następnie zleć programowanie właściwemu agentowi Fable, review Opus i proste wyszukiwanie Haiku. Nie uruchamiaj niepotrzebnych ról tylko w celu powiększenia zespołu.

Jeżeli nie możesz potwierdzić routingu lub klient zastępuje model, oznacz odpowiednią rolę `BLOCKED_MODEL`; dla kodu bez potwierdzenia Fable użyj także `NIEPOTWIERDZONE_FABLE`. Nie zastępuj samodzielnie modelu przypisanego roli. Kontynuuj niezależne, poprawnie skonfigurowane zadania. Przy rozpoznanej zmianie modelu podczas wykonania zabezpiecz bieżący stan, przerwij dalsze działania tej roli i ujawnij zakres niepewności.

Nie zabijaj automatycznie dotychczasowych agentów w połowie operacji. Doprowadź trwające działanie do bezpiecznego punktu, zapisz przekazanie i kieruj dalsze zlecenia programistyczne do Fable. Nie przypisuj nowemu modelowi autorstwa wcześniejszych zmian.

## 3. Przydział programisty

Każde zlecenie zawiera: etap P i ID katalogu, Q/J, cel, kontekst bieżących zmian, dozwolone pliki, zależności, niezmienniki, sposób odbioru oraz informację, kto uruchamia testy. Agent otrzymuje dokumenty potrzebne do zadania i nie tworzy własnych reguł produktu.

Oddziel autora od niezależnego reviewera. Przekazanie patcha i jego integrację wykonuje przypisany programista Fable, z zachowaniem istniejących zmian. Koordynator może odczytać diff, przygotować dokumenty i uruchomić uzgodnioną walidację, bez przejmowania programowania.

Nie instaluj dodatkowych paczek, nie zmieniaj ustawień jakości i nie przebudowuj architektury w celu skonfigurowania modelu. Zachowaj wszystkie reguły zapisu, ekonomii, własności, geometrii i widoczności określone w planie. Testy oraz poprawki testów są również programowaniem powierzanym Fable.

## 4. Kontrola po przyjęciu zasad i odbiór

Po wdrożeniu organizacji ponownie sprawdź, czy dobór modeli nie koliduje z dotychczasowymi postępami, zadaniami w toku, instrukcjami, własnością plików i bramką. Wskaż rzeczywiście potwierdzony model i sposób sprawdzenia. Odróżnij konfigurację od wykonania i wykonanie od odbioru.

Następnie kontynuuj najwcześniejsze nieukończone zadanie z wykonanymi zależnościami. Przestrzegaj niezależnego review, jednego runnera Unity i zamrożenia snapshotu. Przed kolejnym przyrostem oraz po wznowieniu sprawdź ponownie routing, jeśli zmieniły się ustawienia, model lub sesja. Komunikat o zastąpieniu modelu wymaga natychmiastowej reakcji, a nie dopiero końcowego raportu.

Raport zadania wskazuje: subagenta, model żądany, model potwierdzony albo brak dowodu, sposób weryfikacji, P/ID/Q/J, pliki, testy, ograniczenia i następny krok. Nie deklaruj udanego uruchomienia Fable na podstawie skryptu lokalnego.

**Zacznij od kontroli aktualnego stanu i konfiguracji, potwierdź bez edycji działanie subagenta na Fable, ponownie oceń zgodność organizacji z projektem, a potem deleguj mu właściwy etap programowania.**

## Podstawa konfiguracji

Oficjalna dokumentacja: [model subagenta i kolejność wyboru](https://code.claude.com/docs/en/sub-agents#choose-a-model), [ładowanie definicji](https://code.claude.com/docs/en/sub-agents#write-subagent-files), [konfiguracja i dostępność modeli](https://code.claude.com/docs/en/model-config). Klient lub dostawca może zmienić model wykonania; reguła wymaga sprawdzenia rzeczywistego uruchomienia i nie jest techniczną blokadą wszystkich możliwych zastępstw po stronie usługi.
