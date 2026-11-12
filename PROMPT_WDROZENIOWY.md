# Prompt wdrożeniowy dla Claude Code

**Stały rygor:** jedynym nadrzędnym planem produktu jest [docs/NOWY_PLAN_GRY.md](docs/NOWY_PLAN_GRY.md), wraz z §0. Obowiązuje mitologiczna Grecja IV w. p.n.e. (400–301 p.n.e., z 400 r. włącznie). Przed każdą mutacją i po niej stosuj [kartę zgodności](docs/KARTA_ZGODNOSCI_Z_PLANEM.md). Nie wykonuj działania sprzecznego z planem; zależny zakres oznacz BLOCKED_PLAN_CONFLICT. Odczyt dokumentu pomocniczego nie daje zgody na realizację jego niezgodnych założeń.

Pracuj w istniejącym projekcie „Morza Bogów” w katalogu `C:\Users\noirp\MorzaBogow`. Odpowiadaj po polsku. Twoim zadaniem jest **wdrażanie zatwierdzonego planu i kontynuowanie działającego projektu**, z możliwie małą ingerencją w gotowe systemy. Przejdź od rozpoznania stanu do konkretnej implementacji, testów i odbioru. Sam opis zamiaru nie kończy zadania.

## 1. Cel i dokumenty

Tworzymy jednoosobowy naval RTS w mitologicznej Grecji IV w. p.n.e., głównie w Attyce i na pobliskich akwenach. Kampania zawiera elementy RPG, własne scenariusze, bohaterów i dosłowne moce. Potyczki rozgrywa jeden człowiek przeciw AI. Funkcje sieciowe nie należą do zakresu.

Przeczytaj przed zmianami:

1. [CLAUDE.md](CLAUDE.md) i [AGENTS.md](AGENTS.md) — zasady pracy, architektura i odbiór.
2. [docs/NOWY_PLAN_GRY.md](docs/NOWY_PLAN_GRY.md) — nadrzędne reguły produktu, kontrakty Q01–Q12.
3. [docs/KATALOG_MECHANIK.md](docs/KATALOG_MECHANIK.md) — kompletny zakres i identyfikatory funkcji.
4. [docs/PARAMETRY_ROBOCZE.md](docs/PARAMETRY_ROBOCZE.md) — liczby, koszty, wymagania i jawne hipotezy do prób.
5. [docs/MAPY_I_SCENARIUSZE.md](docs/MAPY_I_SCENARIUSZE.md) — sektory, kampania K01–K14 i potyczki.
6. [docs/PLAN_WDROZENIA.md](docs/PLAN_WDROZENIA.md) — etapy P01–P12, zależności i kryteria odbioru.
7. [docs/ANALIZA_STANU.md](docs/ANALIZA_STANU.md) — rozpoznany punkt wznowienia i dowody wymagające kontroli względem bieżącego kodu.
8. [docs/HISTORIA_I_ZRODLA.md](docs/HISTORIA_I_ZRODLA.md) — obowiązkowy protokół badań.
9. [docs/GRYWALNOSC_I_ODBIOR.md](docs/GRYWALNOSC_I_ODBIOR.md) — obowiązkowe kontrakty J01–J10: ruch, komendy, trudność, cele, automatyka i ciągłość postępu.

Plan gry określa zachowanie docelowe. Analiza stanu opisuje dowody implementacji. Kod i aktualne uruchomienia rozstrzygają, co rzeczywiście działa. Przy rozbieżności nazwij problem, ustal najmniejszą zgodną poprawkę i zapisz decyzję. Nie zastępuj pełnego katalogu własnym, węższym zakresem.

**Zachowaj estetykę:** modele, materiały, kolory, światło, wodę, animacje, styl interfejsu i profile jakości. Stan nowej mechaniki przedstawiaj przy użyciu istniejącego języka wizualnego. Zmiana logiki morza nie jest zgodą na przeprojektowanie jego obrazu.

## 2. Rozpoznanie i wybór pierwszej pracy

Sprawdź gałąź, HEAD, `git status`, bieżący diff, najnowsze raporty prac, rzeczywiste wyniki bramki, wersję zapisu i dostępne narzędzia. Odróżnij zastane zmiany użytkownika od własnych. Nie resetuj repozytorium i nie nadpisuj cudzych plików.

Dokumentacja wskazuje bazę `81ec638`, `SaveGame.Version=18`, gotowe konwoje i raport 364/147. **Potwierdź stan aktualny**; te liczby nie są wynikiem Twoich testów ani poleceniem cofnięcia projektu do tej bazy.

Wybierz najwcześniejsze nieukończone zadanie z wykonanymi zależnościami. Jeśli nie ma dowodu zakończenia P01, zacznij od P01. Zweryfikuj kolejno:

- względny ruch i geometrię podejścia do abordażu, w tym przeciwne kursy i przecięcie pod kątem 90°;
- uwzględnienie oszczepników w ocenie strat AI;
- dystans podejścia wynikający ze sprawnego i obsadzonego uzbrojenia;
- granicę zapisu oraz zachowanie oczekujących komend;
- zapis Pending w pauzie bez wykonania ticku oraz podstawowy wynik i powód odmowy komendy, bez ujawniania ukrytej wiedzy;
- istniejącą odmowę złomowania jednostki z cargo;
- ciągłość konwojów, rezerwacji i projektów po zapisie.

To lista hipotez do sprawdzenia, nie nakaz ponownej naprawy kodu już poprawionego. Dla istniejącego rozwiązania znajdź aktualny dowód, zachowaj je i przejdź do rzeczywistej luki.

P01 odbiera aktualny bufor i zapis w pauzie. P03 dodaje sekwencyjne zadania jednostki, trwałą automatykę i przejścia grup. Nie przedstawiaj istniejącego przyjmowania komend w pauzie jako gotowej kolejki tras. Nie przepisuj całej nawigacji: najpierw wspólna legalność kadłuba, potem zwężenie grupy, kolejność i zebranie. P02 przygotowuje definicje zgodnie z zależnościami planu.

## 3. Badania w każdej sesji

Za każdym razem po odczytaniu planu samodzielnie otwórz wiarygodne źródła dla bazowych założeń świata i dla twierdzeń zależnych od wybranego zadania. Korzystaj z publikacji naukowych, dokumentacji archeologicznej, komentowanych inskrypcji i oznaczonych wydań tekstów antycznych. Pamięć modelu, dokument repozytorium i raport innego agenta nie zastępują odczytu.

Rozdziel realia materialne, tradycję mitologiczną i reguły gry. Moce działają naprawdę w fikcji; nie przedstawiaj ich jako potwierdzonych naukowo zjawisk. Koszty, obrażenia, czasy, odporności i proporcje map są parametrami projektu. Sprawdzaj rok i region poświadczenia technologii oraz fazę zabudowy miejsca.

Zapisz raport w `docs/research/sessions/YYYY-MM-DD-HHMM-<zadanie>.md`: autor lub instytucja, URL, wydanie, lokalizator, faktycznie odczytany zakres, ograniczenie i decyzja. Dla nowych lub spornych tez szukaj niezależnego potwierdzenia. Nie wymyślaj publikacji, stron ani dostępu. Przy braku materiału oznacz zależny zakres `BLOCKED_RESEARCH` lub `NIEUSTALONE` i kontynuuj niezależną pracę techniczną.

## 4. Sposób implementacji

Pracuj małymi, kompletnymi przyrostami. Przed edycją określ etap P, właściwe ID katalogu, kontrakty Q i J, pliki, oczekiwane zachowanie i sposób sprawdzenia. Dla błędu najpierw zbierz dowód i odtwórz go. Dla funkcji sprawdź zależności i legalną drogę użycia od początku partii.

Rozwijaj istniejący World, komendy, katalog klas, broń, gospodarkę, lokalne magazyny, transport, konwoje, morze i zapis. Nowe warianty oraz scenariusze opisuj danymi i małymi rozszerzeniami. Nie twórz drugiej symulacji ani odrębnej gospodarki dla mitologii. Reguły należą do czystej symulacji; prezentacja wysyła komendy i odczytuje stan.

Szczególnie pilnuj następujących kontraktów:

- Ilości D/S mają właściwą precyzję, bilans i rezerwacje; transfer nie tworzy zasobu. Koszt kadłuba, obsady i modułów nie może mieć ujemnego składnika.
- Zachowuj zapisane numery enumów. Nowe wartości dopisuj. Niezgodna zmiana zapisu podnosi jego wersję i jawnie określa obsługę niezgodnego pliku.
- Wymagania budynków włączaj wraz z osiągalnym podgrafem rozwoju. Nie blokuj otwarcia wymogiem obiektu, którego jeszcze nie można uzyskać.
- Relacje trzech stałych slotów właścicieli wdrażaj zgodnie z P02, z pełną aktualizacją tablic i walidacji; tylko dwie główne strony rozstrzygają zwykły wynik partii.
- Ogień, osłony, niewidzialność, bohaterowie i artefakty korzystają ze wspólnego kontraktu efektów. Rozdziel odebranie konfiguracji w P06 od jej skutków w P07.
- Zmiana terenu unieważnia właściwe cache przez TerrainRevision także przy niezmienionej liczbie wysp. Zapis obejmuje aktywne efekty i zmieniony teren.
- Cele misji i przenoszenie kampanii mają wykonalne warunki, unikalne ID oraz jednokrotne rozliczenia.
- Zadanie ekonomiczne zachowuje politykę i wznawia pracę po zwolnieniu miejsca, nowej partii lub ustąpieniu blokady. Nie wymaga ponawiania tego samego rozkazu.
- Profile AI zmieniają decyzje, nie ukryte ceny, statystyki czy wiedzę. Posiłki mają skończony manifest, jawny warunek i zapowiedź; nie zależą potajemnie od oceny wyniku gracza.
- Sukces rozstrzygają wymagane ObjectiveID i pełne gałęzie alternatywne. Niepowiązany ocalały wróg oraz cele opcjonalne nie blokują końca operacji. Wymagane dane celu nie mogą domyślnie zerować jego warunku.
- Rozdziel zatwierdzony postęp, rezerwę, manifest wyprawy i wynik. Powtórka odtwarza snapshot wejścia, bez cofania wcześniejszych ukończeń ani powielania nagród. Rezerwa nie omija slotów artefaktów i fizycznej ewakuacji.
- Zapis ręczny, quicksave i autosave mają oddzielne sloty i kompletne generacje wraz z danymi powiązanymi haszem. Błąd nie niszczy poprzedniej dobrej generacji.

Zachowuj pełny zakres P01–P12. Pierwszy grywalny wycinek K01–K03 jest etapem integracji; nie zastępuje mocy, postaci, stworzeń ani pozostałych operacji. AI rozwijaj przyrostowo wraz z nowymi regułami.

## 5. Testy i niezależny odbiór

Dobierz testy do rzeczywistego ryzyka i zachowania. Nowa reguła symulacji wymaga istotnego testu EditMode i odpowiedniej kontroli integracji. Sprawdź właściwe przypadki brzegowe, księgę, zmianę właściciela, zapis/wczytanie i deterministyczne rozstrzygnięcie zdarzeń. Nie zastępuj testu gry samym sprawdzeniem struktury JSON.

Dla właściwych etapów sprawdź: mieszaną grupę w cieśninie; prawdziwy powód blokady; pauza → komendy → zapis → odczyt → wznowienie; pełny magazyn → wolne miejsce bez dodatkowego rozkazu; identyczne taryfy obu stron; te same decyzje AI przy identycznych obserwacjach i RNG; koniec misji mimo odległego niepowiązanego wroga; ponowienie bez utraty zatwierdzonego postępu; awarię zapisu bez utraty dobrego zestawu. Czytelność i obciążenie uwagi odbieraj również z udziałem człowieka.

Po zmianie C# wykonaj kontrolę błędów według CLAUDE.md. Korzystaj z istniejącej bramki, rzeczywistych EditMode/PlayMode oraz aktualnego `QUALITY_GATE_SESSION_ID`. Nie zmieniaj polityki, hooków ani progów, aby uzyskać PASS. Brak testów, skipped, inconclusive, brak logu i błąd procesu nie są sukcesem.

Wykorzystuj dostępnych subagentów do niezależnego review i QA, z konkretnym zakresem oraz własnością plików. Zachowaj jednego właściciela uruchomień Unity; nie uruchamiaj równocześnie dwóch runnerów tego projektu. Przed bramką zintegruj poprawki i zamroź snapshot. Nie edytuj go podczas uruchomienia. Zmiana po bramce wymaga właściwej ponownej weryfikacji.

Raportuj osobno kompilację, testy, build, playtest i badania źródeł. Przy braku narzędzia podaj rzeczywiste ograniczenie i oznacz wynik `NOT_RUN` lub właściwym stanem; nie deklaruj ukończenia na podstawie planu. Dokumenty i macierze aktualizuj po zakończonej walidacji, zachowując zgodność raportu z jego snapshotem.

## 6. Ciągłość pracy

Po odebraniu przyrostu przejdź do następnego zadania z wykonanymi zależnościami. Nie zatrzymuj się na samym audycie, propozycji ani pytaniu o zgodę na rutynowy krok objęty tym poleceniem. Rozwiązuj zwykłe decyzje implementacyjne na podstawie planu, kodu i dowodów. Rzeczywistą blokadę opisz precyzyjnie i kontynuuj zadania, które od niej nie zależą.

Przy zmianie sesji lub przekazaniu pracy zapisz raport w `docs/implementation/sessions/YYYY-MM-DD-HHMM-<zadanie>.md`. Podaj etap P, ID katalogu, Q i J, aktualny commit i stan roboczy, zmienione pliki, dowody testów, źródła, ograniczenia oraz dokładny następny krok. Odróżniaj zaplanowane, wdrożone i odebrane. Nie wyliczaj gotowości gry z liczby plików lub testów.

W komunikacji używaj samodzielnego nazewnictwa „Mórz Bogów” i opisuj wynikające z dokumentacji reguły. Zachowaj pełen zakres mechanik i spójność dokumentów.

**Zacznij teraz od sprawdzenia aktualnego stanu projektu, kwerendy i najwcześniejszego nieukończonego zadania. Następnie wykonaj konkretną implementację oraz właściwy odbiór.**
