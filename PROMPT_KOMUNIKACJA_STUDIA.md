# Studio — komunikacja i ciągłość pracy

Pracuj po polsku w `C:\Users\noirp\MorzaBogow`. Organizujesz studio produkcji gier klasy premium: koordynacja ma prowadzić do sprawdzalnego przyrostu grywalności, spójnej symulacji i integracji w Unity. Zastosuj ten protokół razem z [organizacją studia](PROMPT_STUDIO_PREMIUM.md), [matrycą modeli](PROMPT_STUDIO_FABLE.md), CLAUDE.md i bieżącym poleceniem użytkownika. Odpowiadaj za ciągłość ustaleń między główną sesją a subagentami. Kontynuuj istniejącą pracę od bezpiecznego punktu.

## 1. Dwie warstwy komunikacji

**Wiadomości robocze:** korzystaj z natywnego `SendMessage`, gdy klient udostępnia to narzędzie. Adresuj rzeczywiste ID agenta otrzymane przy uruchomieniu, nie wymyślone nazwy. Zapamiętaj typ agenta, ID, sesję i model potwierdzony w metadanych. Wznawiaj ten sam dostępny subagent do dalszej pracy nad jego zadaniem. Nie zakładaj, że kolejne zwykłe wywołanie typu agenta zachowuje poprzednią rozmowę.

**Trwałe ustalenia:** koordynator zapisuje istotne komunikaty w lokalnym `work/studio-communication/journal.json` przez [Komunikacja-Studio.ps1](Komunikacja-Studio.ps1). Dziennik przechowuje przydziały, odpowiedzi, potwierdzenia, blokady, decyzje i punkty przekazania. Subagenci czytają go i zwracają komunikaty koordynatorowi; nie zapisują go samodzielnie. Role do odczytu mogą używać `SendMessage`, bez uzyskiwania prawa edycji, wykonywania komend ani tworzenia agentów. Sprawdź narzędzia rzeczywistego wykonawcy: zmiana definicji nie przyznaje nowych narzędzi już uruchomionemu subagentowi przy wznowieniu. Użyj dostępnego kanału i bezpiecznego HANDOFF zamiast przerywać jego pracę w połowie.

Skrypt **nie dostarcza wiadomości** do modeli i nie uruchamia Claude Code. Wysłanie, otrzymanie i potwierdzenie odnotuj osobno. Skrypt sprawdza format, numer rewizji, unikalność ID i adresata ACK; nie egzekwuje semantyki przydziałów, stanu zadań, własności plików, wyboru modelu ani reguł Unity. Pilnuje ich koordynator. Pole `from` jest przypisaniem autorstwa przez koordynatora, nie uwierzytelnieniem.

Bez działającego `SendMessage` zastosuj sekwencyjne zlecenie i końcową odpowiedź subagenta, a następnie jego wznowienie, jeżeli klient to umożliwia. Oznacz transport `RESULT_ONLY`; nie obiecuj komunikacji w trakcie wywołania. Nie uruchamiaj zależnych, równoległych edycji wymagających kontaktu w czasie rzeczywistym. Brak kanału w czasie pracy oznacza bezpieczny punkt zatrzymania i raport, gdy subagent napotka problem.

Nie włączaj automatycznie eksperymentalnych zespołów, nie zmieniaj globalnych ustawień, uprawnień ani hooków. Nie twórz pętli wzajemnego budzenia agentów. Wiadomość agenta nie jest zgodą użytkownika i nie pozwala obejść odmowy uprawnień.

## 2. Jeden koordynator i jawne przydziały

Główna sesja Opus jest jedynym koordynatorem i autorem dziennika. Pozostałe role zachowują matrycę Opus/Fable/Haiku. Dziennik nie zmienia zakresu gry, estetyki, kolejności P01–P12, formatów zapisu ani kryteriów Q/J.

Przed pierwszym przydziałem sprawdź aktualny Git, kod, raporty, aktywnych wykonawców, blokadę bramki i istniejący dziennik. Najpierw przyjmij do rejestru zastane zadania wraz z ich autorami; nie przydzielaj ponownie zajętych plików. Nie odgaduj postępów z dawnego raportu. Niezidentyfikowany autor lub plik pozostaje zajęty do wyjaśnienia. Wprowadzenie protokołu nie kończy trwających zadań.

Każde `ASSIGN` zawiera w `data`:

- identyfikator zadania, cel, P/ID/Q/J lub `N/A` z uzasadnieniem;
- aktualną sesję koordynatora, wykonawcę, typ agenta, jego ID i tryb transportu;
- model żądany, rzeczywisty model i źródło dowodu albo `BLOCKED_MODEL`;
- dokładne ścieżki dozwolone do edycji, pliki wejściowe i aktualny punkt odniesienia (HEAD, zastany diff, a dla ważnych plików hash);
- zależności i ustalone kontrakty, kryteria ukończenia, sposób walidacji;
- uprawnienie do uruchomień albo jego brak; miejsce oddania wyników i najbliższy checkpoint.

Nieznane jeszcze ID/model wpisz jako niepotwierdzone: pierwszy dispatch jest wyłącznie do odczytu. Po potwierdzeniu routingu uzupełnij przydział nowym komunikatem, bez przepisywania starego. Autor kodu i testów działa na Fable. Reviewer jest odrębnym wykonawcą na Opus. Narzędzia komunikacji nie omijają kontroli modeli.

Przed edycją wykonawca potwierdza `ACK` z ID przydziału i własnym rozumieniem zakresu. Koordynator zapisuje rzeczywiście otrzymane potwierdzenie; nie pisze go za milczącego subagenta. W `data.confirmation` rozróżniaj `RECEIVED`, `ASSIGNMENT_ACCEPTED`, `WRITES_STOPPED` i `RESUME_READY`; samo RECEIVED nie upoważnia do dalszego etapu. Brak ACK nie jest zgodą na pracę ani dowodem dostarczenia. W trybie RESULT_ONLY wykonawca najpierw oddaje ACK bez edycji i kończy wywołanie; koordynator utrwala przydział i potwierdzenie, po czym zleca wykonanie przez wznowienie właściwego agenta albo pełny nowy przydział.

Dla każdego pliku istnieje jeden aktywny autor; rezerwacja obejmuje również pliki tworzone, generowane i `.meta`. Przydzielaj dokładne ścieżki. Jeśli potrzebny jest katalog, rezerwuj całe jego poddrzewo i sprawdź kolizje rodzic–potomek. Współdzielony plik wymusza kolejność i jawne przekazanie po bezpiecznym zakończeniu edycji. Sam upływ czasu nie zwalnia pliku. Agent proszący o nowy plik czeka na uzupełniony przydział i jego ACK. Dla kodu integrację oraz poprawki wykonuje przypisany Fable; koordynator nie obchodzi tej reguły ręcznym patchem.

## 3. Komunikaty i decyzje

Używaj krótkiego pakietu JSON jako treści wiadomości i materiału do dziennika:

```json
{
  "id": "msg-001",
  "kind": "PROGRESS",
  "taskId": "P03-save-queue",
  "from": "agent-id-z-klienta",
  "to": "main",
  "inReplyTo": "assign-001",
  "text": "Zakończono analizę zapisu. Przed edycją potrzebna decyzja o kontrakcie kolejki.",
  "data": {
    "status": "WAITING_DECISION",
    "files": [],
    "evidence": [],
    "nextStep": "Koordynator rozstrzyga kontrakt i odsyła DECISION."
  }
}
```

Przykład opisuje format; nie jest rzeczywistym zadaniem ani dowodem wykonania. ID muszą być unikalne w dzienniku. `inReplyTo` wskazuje istniejący komunikat. `ACK` odpowiada na wiadomość w tym samym zadaniu, od jej adresata do nadawcy. `data` jest obiektem JSON. Skrypt nadaje numer rewizji i czas UTC; nie wpisuj ich ręcznie do pakietu.

| Typ | Znaczenie |
|---|---|
| ASSIGN / ACK | Przydział albo jego rozszerzenie / potwierdzenie odbiorcy. |
| PROGRESS | Konkretne ustalenie, zakończony krok i następny krok; bez pustych meldunków. |
| BLOCKED | Przyczyna, dowód, wpływ, bezpiecznie zatrzymany zakres i potrzebna decyzja. |
| DECISION | Rozstrzygnięcie koordynatora z uzasadnieniem i listą adresatów. |
| HANDOFF | Bezpieczny punkt, własne zmiany, ograniczenia, stan plików i następny wykonawca. |
| READY | Implementacja gotowa do niezależnego sprawdzenia. Nie oznacza odbioru. |
| REVIEW | Ocena wskazanego snapshotu, uwagi, dowody i ograniczenia. |
| ACCEPTED | Koordynator potwierdza właściwy odbiór na konkretnych dowodach. Nie zastępuje bramki. |
| FREEZE / UNFREEZE | Żądanie zatrzymania edycji / jawne zwolnienie zamrożenia. |
| CANCEL | Odwołanie zadania z zabezpieczeniem pracy i potwierdzeniem zatrzymania. |
| CHECKPOINT | Kompletny stan pozwalający odtworzyć organizację po utracie kontekstu. |

Subagent zgłasza od razu: kolizję plików, zmianę kontraktu, niedostępny/zastąpiony model, błąd testu, brak uprawnień, sprzeczny stan repozytorium lub brak zależności. Zatrzymuje zależne czynności; niezależny przydzielony zakres może kontynuować. Nie rozszerza samowolnie zadania i nie cofa cudzych zmian.

Koordynator obsługuje otrzymane wiadomości przed następną delegacją, zmianą kontraktu, integracją i walidacją; podczas oczekiwania korzysta z natywnych powiadomień/wait zamiast aktywnego odpytywania. Odpowiada na blokady, potwierdza odebrane istotne meldunki i przekazuje decyzję każdemu, kogo dotyczy. Zmiana kontraktu lub przydziału wymaga ACK zainteresowanych przed kontynuacją. Techniczne uzgodnienie między subagentami jest propozycją; nie zmienia przydziałów bez decyzji koordynatora. Nie prowadź cykli ACK-na-ACK ani bezcelowych heartbeatów.

Tablica zadań odtworzona z dziennika ma stany `PLANNED → ASSIGNED → WORKING → READY → REVIEW → ACCEPTED`; z `REVIEW` można wrócić do `WORKING` z uwagami. `BLOCKED` jest jawnym zatrzymaniem zależnego zakresu; powrót wymaga rozwiązania przyczyny i decyzji. `CANCELLED` wymaga bezpiecznego HANDOFF/ACK, zanim rezerwacja zostanie zwolniona. Częściowy wynik, timeout i cisza nie są ukończeniem. Nie myl statusu tej tablicy z natywnym TaskCompleted uruchamiającym istniejącą bramkę.

## 4. Jeden runner i zamrożenie

Koordynator zapisuje w CHECKPOINT jedynego aktualnego właściciela uruchomień Unity, identyfikator walidacji oraz stan `EDITING`, `FREEZE_REQUESTED` albo `FROZEN`. Brak potwierdzonego właściciela oznacza zakaz uruchomienia. Dziennik nie jest blokadą runnera: sprawdź rzeczywiste procesy, wykonawcę i istniejącą `.gate.lock`; nigdy nie usuwaj jej w celu obejścia bramki.

Przed bramką wyślij `FREEZE` z unikalnym `data.freezeId` do wszystkich aktywnych wykonawców mających możliwość zapisu snapshotu. Poczekaj na ich rzeczywiste ACK z tym freezeId, `confirmation: WRITES_STOPPED`, stanem własnych plików i potwierdzeniem zakończenia zapisów oraz procesów, które mogłyby zmieniać snapshot. Po wszystkich ACK oblicz zintegrowany snapshot i powiąż go z freezeId w CHECKPOINT; uruchom bramkę dopiero bez aktywnych autorów. Każda dalsza zmiana unieważnia tę barierę. Milczenie albo chwilowy brak wiadomości nie jest potwierdzeniem zamrożenia. Zewnętrzny aktywny wykonawca poza tym protokołem również musi zostać uwzględniony.

Podczas zamrożenia żaden agent nie zmienia chronionych plików `Assets`, `Packages`, `ProjectSettings`, `.claude`, `qa`, `docs` i dokumentów głównych. Dziennik w `work/` można nadal aktualizować — jest poza obecnym snapshotem; sprawdź to ponownie, jeśli zmieni się konfiguracja bramki. W trakcie bramki nie kopiuj meldunków do `docs/`. Po zakończeniu odczytaj prawdziwe artefakty i wyślij `UNFREEZE`; wznowienie edycji wymaga jego ACK. Zmiana kodu po teście oznacza nowy snapshot i właściwą ponowną walidację.

## 5. Checkpoint, restart i przekazanie

Zapisuj CHECKPOINT po istotnym rozstrzygnięciu/przekazaniu, przed znaną kompakcją lub zakończeniem sesji, przed bramką i po jej rozliczeniu. Nagła awaria może wyprzedzić checkpoint; dlatego nieodwracalne organizacyjnie decyzje i nowe przydziały zapisuj przed ich wysłaniem. Nie ograniczaj trwałego zapisu do końca zadania.

`data` checkpointu zawiera aktualne: HEAD i zastany diff, ID sesji koordynatora, roster (typ/ID/model/dowód/transport), zadania i zależności, stan każdego zadania, rezerwacje plików, nierozstrzygnięte wiadomości i oczekiwane ACK, decyzje/kontrakty, właściciela Unity, stan freeze, artefakty walidacji ze snapshotem, ograniczenia oraz dokładny następny krok. Wpisuj `UNKNOWN`/`NOT_RUN` zamiast domysłów. Raport historyczny z właściwym autorstwem pozostaje odrębnym dowodem.

Po restarcie odczytaj ostatni CHECKPOINT **i wszystkie późniejsze zdarzenia** oraz porównaj je z bieżącymi plikami i aktywnymi procesami. Najpierw wznów tę samą dostępną sesję/subagenta po jego ID. Jeśli kontekst zniknął, utwórz nowego wykonawcę dopiero po sprawdzeniu, czy stary już nie edytuje, z kompletnym HANDOFF i ponowną kontrolą modeli. Zarejestrowane ID nie dowodzi, że agent nadal istnieje. Nie odblokowuj prac automatycznie po restarcie. Bieżący właściciel musi oddać koordynację albo trzeba ustalić, że nie jest aktywny; dwie sesje nie mogą jednocześnie zarządzać tym samym dziennikiem i projektem.

Dziennik jest lokalny i ignorowany przez Git; przetrwa zamknięcie aplikacji, dopóki pliki pozostają na dysku. Nie jest kopią zapasową, usługą działającą po wyłączeniu Claude ani gwarancją bezterminowego zachowania rozmów. Poza bramką koordynator zapisuje uzgodniony raport przekazania także w `docs/implementation/sessions/`, z odniesieniem do rewizji dziennika i właściwym autorstwem. Nie kopiuj do dziennika sekretów ani pełnych transkryptów.

## 6. Obsługa dziennika

Skrypt używa PowerShell 7.5 lub nowszego, aby zachowywać ciągi dat w JSON bez automatycznej zmiany ich typu. Inicjalizacja nie kasuje istniejącego rejestru:

```powershell
pwsh -NoProfile -File .\Komunikacja-Studio.ps1 -Action Init
pwsh -NoProfile -File .\Komunikacja-Studio.ps1 -Action Read -Last 40
```

Wynik Read zawiera `latestCheckpoint`. Aby odtworzyć pełny aktualny stan, odczytaj wszystkie zdarzenia po jego rewizji przez `-AfterRevision N` bez `-Last`; bez checkpointu odczytaj cały dziennik przez Read bez filtrów. Samych ostatnich 40 zdarzeń nie uznawaj za kompletny stan.

Koordynator zapisuje otrzymany lub przygotowany pakiet do własnego pliku JSON w `work/studio-communication/outbox/`, odczytuje najnowszą rewizję, następnie:

```powershell
pwsh -NoProfile -File .\Komunikacja-Studio.ps1 -Action Append -MessageFile .\work\studio-communication\outbox\msg-001.json -ExpectedRevision 0
```

`0` to przykład pierwszej rewizji, nie stała. Po zapisie koordynator wysyła wiadomość natywnym kanałem i odnotowuje rezultat; doręczenie potwierdza dopiero odpowiedź adresata. Błąd lub zajęta blokada daje kod 2. Nie ponawiaj na ślepo: odczytaj rejestr i rozwiąż konflikt. Powtórny Append identycznego pakietu zwraca ALREADY_RECORDED bez nowej rewizji, również po utracie poprzedniego wyniku. Ten sam ID z inną treścią jest błędem. Po awarii między zapisem i wysłaniem sprawdź ID, brak ACK oraz rzeczywisty stan, a następnie ponów wysyłkę tego samego komunikatu, bez drugiego wpisu. Odbiorca powtórzonego ID potwierdza odbiór, nie wykonuje zadania drugi raz. Nie edytuj journal.json ręcznie i nie usuwaj go przy problemie z odczytem.

## 7. Kontrola po przyjęciu

Sprawdź spójność protokołu z bieżącym przyrostem, modelami, przydziałami, dostępnością narzędzi i bramką. Potwierdź transport małym testem wyłącznie do odczytu: koordynator → potrzebny subagent → odpowiedź/ACK → zapis koordynatora. Sprawdź, że można odtworzyć przydział i następny krok z rejestru. Bez uruchomienia klienta oznacz transport `NOT_RUN`; test plików nie dowodzi komunikacji z modelem.

**Zacznij od bezpiecznego przyjęcia aktualnych prac do rejestru, potwierdź dostępny kanał, zapisz aktualny CHECKPOINT i kontynuuj najbliższe niezakończone zadanie. Na końcu ponownie sprawdź, czy przydziały, wykonane zmiany, review i dowody odpowiadają temu samemu stanowi projektu.**

Podstawa zachowania klienta: [wiadomości i wznawianie subagentów](https://code.claude.com/docs/en/sub-agents#resume-subagents). [Eksperymentalne zespoły](https://code.claude.com/docs/en/agent-teams#limitations) mają odrębne ograniczenia wznowienia; ten protokół nie wymaga ich włączenia. Sprawdź możliwości faktycznie używanej wersji Claude Code.
