# Morza Bogów — zasady pracy

## Dokumentacja obowiązująca

Przeczytaj GAME_VISION.md, GAME_DESIGN.md, ECONOMY_DESIGN.md, ECONOMY_BALANCE.md, VISUAL_TECH_SPEC.md, MYTHIC_SCENARIOS.md i README_PL.md. Stosuj reguły E00–E16 dla gospodarki, V00–V13 dla obrazu i S00–S07 oraz M01–M14 dla kampanii. Parametry qa/economy-baseline.json i qa/scenario-design.json odwzoruj w danych projektu. Utrzymuj zgodność dokumentów, konfiguracji i implementacji.

Gra jest strategią bitew morskich. Gospodarka ma tworzyć eskorty, rajdy i walkę o akweny. Obowiązują dwa zasoby, lokalne magazyny, rzeczywiste cargo, automatyczne konwoje, ograniczone premie i skończone kontrakty. Zachowaj role oraz ceny klas okrętów. Nie dodawaj podatku od floty ani systemu potrzeb mieszkańców.

Kampania ma 14 misji. Każda wymaga własnego stanu startowego, wykonalnych celów, jawnych porażek, zapowiedzi zagrożeń i zapisu zdarzeń. Przedmioty oraz postacie mają unikalne identyfikatory. Przejście między misjami nie przenosi floty ani magazynów. Parametry czasu i obrażeń stroi się przez playtesty.

Grafikę rozwijaj na funkcjach HDRP. Weryfikuj obsługę API w zainstalowanym środowisku. Docelowym i jedynym wymaganym profilem jest Ultra, a gra nie ma wymagań sprzętowych. Zachowaj czytelność floty i logiczną mgłę wojny; renderowanie nie może ujawniać ukrytych jednostek. Pomiary GPU i ocena ruchomego obrazu wymagają rzeczywistego urządzenia.

Decyzje wdrożeniowe oceniaj według GAME_VISION.md. Pierwszeństwo mają grywalna operacja morska, czytelne decyzje i działająca automatyka. Pełna rozbudowa portu nie jest warunkiem pierwszego starcia. Kontroluj tempo standardowej potyczki, różnorodność strategii i czas nieplanowanych interwencji gospodarczych. Odbiór tych celów wymaga playtestów.

## Autonomia

Wykonuj rutynową pracę w projekcie, edycję plików, uruchamianie narzędzi, testów i buildów oraz instalację potrzebnych bezpłatnych zależności w ramach dostępnych uprawnień. Najpierw sprawdź obecne instalacje. Nie pytaj ponownie o te czynności. Publikacja, zakupy i usuwanie cudzej pracy wymagają odrębnego upoważnienia. Przy konieczności logowania lub działania użytkownika opisz konkretną blokadę i kontynuuj niezależne prace.

## Kontrola jakości

Koordynator dzieli zadania i integruje wynik. Implementer przygotowuje zmianę, niezależny QA dobiera przypadki graniczne, a reviewer ocenia kod i testy. Korzystaj z dostępnych subagentów. Przy braku niezależnego wykonawcy ujawnij ograniczenie odbioru. Integruj zmiany do jednego zamrożonego snapshotu przed testami.

Powiąż wymagania z implementacją, konkretnym testem i artefaktem. Macierze qa/ECONOMY_ACCEPTANCE.csv, qa/VISUAL_ACCEPTANCE.csv i qa/SCENARIO_ACCEPTANCE.csv wypełniaj wyłącznie na dowodach. Kontrola node qa/scenario-design-check.cjs potwierdza strukturę danych; działanie gry wymaga rzeczywistych testów i playtestów.

## Egzekwowana bramka

Przed zamknięciem zadania przygotuj .claude/qa/tasks/<taskId>.json z identyfikatorem sesji z SessionStart i pełnymi nazwami istniejących expectedTests w EditMode oraz PlayMode. Ustaw rzeczywisty Unity.exe w obu profilach .claude/qa/policy.json. Przykładowe nazwy nie są implementacją testów.

.claude/hooks/quality-gate.ps1 sam uruchamia wymagane zestawy Unity, zapisuje nowe XML i logi oraz sprawdza kody procesu, liczniki, wyniki każdego przypadku, nazwy i asercje. Zero testów, skipped, inconclusive, timeout, brak artefaktu, błąd procesu albo zmiana snapshotu blokują zamknięcie. Nie wpisuj ręcznie report.json i nie osłabiaj polityki w celu uzyskania PASS. Fake runner służy wyłącznie testom bramki w izolowanym katalogu.

PreToolUse sprawdza TaskUpdate/completed, a TaskCompleted także automatyczne zamknięcie członka zespołu. Obie ścieżki uruchamiają testy. Nie ustawiaj async. Zewnętrzny timeout musi przekraczać łączny czas profili. Używaj jednego edytora lub batch runnera dla projektu. Bramka obejmuje dokumenty wykonawcze i qa/ snapshotem.

## Odbiór zadania i wydania

NOT_RUN, BLOCKED, FAIL, INCONCLUSIVE i SKIPPED są odrębnymi stanami. Brak wykonanego testu nie daje PASS. Raportuj polecenie, środowisko, snapshot i ścieżki dowodów. Testy bramki, kompilacja, build, ocena grafiki i playtest mają odrębny zakres. Kampania wymaga przejścia 14/14 misji, a katalog potyczek 40/40 map.

Zmiany hooka, polityki i testów wymagają niezależnego review oraz odpowiednich testów. Wydanie musi przejść zewnętrzne CI na czystym checkoutcie, z chronioną konfiguracją i obowiązkowym statusem. Implementer nie zatwierdza własnego odbioru. Sprawdź rzeczywiste zachowanie obu hooków w Claude Code; lokalna konfiguracja nie zapewnia ochrony przed celową edycją samej polityki.
