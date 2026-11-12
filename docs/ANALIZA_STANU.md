# Morza Bogów — analiza wykonanej pracy i wnioski

Data audytu: 11 listopada 2026. Audyt dokumentacji, konfiguracji, kodu oraz istniejących lokalnych wyników. Nie uruchamiano ponownie Unity, partii ani profilera. Część ustaleń sprawdzono niezależnie w dwóch przeglądach tylko do odczytu. **Estetyka nie podlega ocenie ani zmianie.**

**Rola dokumentu:** zapis dowodów dotyczących implementacji. Zalecenia tego audytu nie ustanawiają reguł docelowych. Wiążący zakres jednej gry mitologicznej, pełny katalog funkcji i kolejność integracji określają [NOWY_PLAN_GRY.md](NOWY_PLAN_GRY.md), [KATALOG_MECHANIK.md](KATALOG_MECHANIK.md) oraz [PLAN_WDROZENIA.md](PLAN_WDROZENIA.md).

## 1. Dokładny punkt wznowienia prac

| Pole | Stan potwierdzony w repozytorium |
|---|---|
| Gałąź | `balans-skala` |
| HEAD | `81ec6383af6f27b4c0b00d9bd2765c6b527dea1e` |
| Commit | 11.11.2026, **16:35:55 Europe/Warsaw** |
| Ostatnia zakończona praca | Naprawa od pełnej ceny, łup osad, machiny we wrakach, zbieranie wraków przez SI, automatyczne konwoje |
| Unity | `6000.3.28f1`, HDRP i VFX Graph `17.3.0`, Input System `1.20.1`, Test Framework `1.6.0` |
| Symulacja | Czysty C#, stały krok 30 Hz, ziarno RNG, ilości D/S w `long` i setnych jednostki |
| Zapis | `SaveGame.Version = 18` |
| Faktyczny tryb gry | Potyczka 1v1 z SI; jeden układ mapy z profilami długości |

Wznowienie **nie** zaczyna się od ponownego budowania konwojów, odtwarzania środowiska ani dodawania podstawowej walki. Systemy już istnieją. Najbliższy krok to naprawa ich konkretnych luk i przygotowanie spójnych rozszerzeń według planu integracji.

Stan roboczy zastany podczas audytu:

- zmienione `ProjectSettings/GraphicsSettings.asset` i `ProjectSettings/QualitySettings.asset`;
- nieśledzone `ArtWorkspace/cc0/`;
- nieśledzona scena `Assets/InitTestScenef97c17f4-3f41-42ad-b519-379b23f0ab0b.unity` z meta.

Tych plików nie zmieniamy, nie resetujemy i nie przypisujemy do niniejszej pracy. `HEAD` nie opisuje całego stanu roboczego. Raport bramki ma własny snapshot, którego nie wolno utożsamiać z każdym późniejszym plikiem w folderze.

## 2. Rzeczywiste dowody walidacji

Najnowszy odczytany raport:

`qa/reports/M1-32050a1300614634b45d24d04fd011ce/report.json`.

- status **PASS**;
- EditMode **364/364**;
- PlayMode **147/147**;
- zakończenie **11.11.2026, 16:34:42 Europe/Warsaw**;
- snapshot: `BA6F4F6738360567CED54D7384AD010A8F7560D7ABF6B52AF892D8686FD1C9C9`.

Suma: **511 wykonanych przypadków**. Nie oznacza 511 niezależnych mechanik, pełnego pokrycia kodu, zgodności historycznej ani ukończonej gry. Sam procent przejścia `511/511=100%` opisuje wyłącznie wybrany zestaw w jego snapshotcie.

`artifacts-log/build-eco.log:10543` zawiera `Build Finished, Result: Success`; lokalny build zakończył się około 16:36:56. To dowód zakończenia odczytanego procesu, a nie wykonany w tej pracy build. `showcase-eco.log` jest dowodem demonstracji; ostrzeżenia `Look rotation viewing vector is zero` wymagają osobnej oceny, bez przypisywania im niezmierzonego skutku.

Macierze po ostatniej pracy:

| Zakres | PASS | NOT_RUN | Pokrycie pozycji wynikiem PASS |
|---|---:|---:|---:|
| `qa/ECONOMY_ACCEPTANCE.csv` | 10 | 30 | 25% |
| `qa/SCENARIO_ACCEPTANCE.csv` | 0 | 25 | 0% |
| `qa/VISUAL_ACCEPTANCE.csv` | 0 | 32 | 0% |

To stan wpisów macierzy, nie estymata procentu gotowości produktu. W tej pracy nie potwierdzono playtestów z człowiekiem, zewnętrznego CI ani pełnego przejścia kampanii. Kampania nie ma jeszcze wykonawcy runtime. Test struktury danych scenariuszy nie zmienia tego faktu.

## 3. Co rzeczywiście istnieje

| System | Dowód w kodzie | Ocena użyteczności dla nowego planu |
|---|---|---|
| Oddzielenie logiki od prezentacji | `Runtime/Simulation`, `Runtime/Presentation`, oddzielne asmdef | Zachować; umożliwia zmianę reguł bez przebudowy oprawy. |
| 7 klas i 9 niepustych typów broni | `Definitions.cs`, `WeaponCatalog`, `ShipCatalog` | Zachować infrastrukturę; katalog grywalny wymaga filtra daty i zgodności. |
| Taran, ostrzał, abordaż | `World.cs`, `World.Weapons.cs` | Są podstawy trzech metod walki; geometria i kontrgra wymagają odbioru. |
| Nawigacja i formacje | `World.Navigation.cs`, `Formation.cs` | Kontakty kadłubów są kapsułowe; linia i klin wyznaczają punkty docelowe. Nie jest to jeszcze trwała doktryna eskadry utrzymującej szyk podczas walki. |
| Załogi, ranni, morale, plany obsady | `Entities.cs`, `Definitions.cs`, `World.Economy.cs` | Zachować funkcje, przebudować proporcje i rozdzielić role. |
| Lokalne magazyny i fizyczny transport | `World.Economy.cs`, `World.Expansion.cs` | Główna wartość istniejącej ekonomii. |
| Automatyczne konwoje | `World.Convoy.cs` oraz testy konwojów | Grupy do 3 transportów, minima źródła, port priorytetowy i uwzględnianie ładunku w drodze już działają w kodzie. |
| Warsztat, projekty portu, arsenał | `World.Arsenal.cs`, `World.Weapons.cs` | Można wykorzystać do osprzętu, szkolenia i remontu; nie trzeba dodawać kolejnej ekonomii. |
| Naprawa i odzysk | `World.Economy.cs` | Nowe poprawki są wdrożone; zwykłe złomowanie z cargo jest blokowane. Rozszerzenia nadal wymagają kontroli księgi i podstawy odzysku. |
| Przejęcie okrętu | `World.cs:570–773` i testy | Są kapitulacja i załoga pryzowa; rozbudować przy migracji ról, nie pisać ponownie. |
| Mgła wojny i SI | `Opponent.cs`, `World.Fog.cs` | Zachować obserwację i wspólną ścieżkę komend; ograniczyć odczyt ukrytego świata. |
| Morze i wiatr | `Simulation/Marine`, `World.Marine` | Stan jest w symulacji i zapisie; wpływ na ruch jest domyślnie wyłączony. |
| Zapis atomowy i odtwarzanie stanu | `SaveGame.cs` | Fundament trwałości; nowa kampania potrzebuje dodatkowego modelu stanu. |
| Input System i HUD | `Presentation/Hud/GameInput.cs`, UI Toolkit | Migracja wejścia jest wykonana. Nowy plan nie każe ponownie przechodzić ze starego wejścia. |

Nie potwierdzono zaimplementowanych systemów: wykonawcy misji, nieliniowego grafu kampanii, trwałych oficerów i doświadczenia kampanii, drugiej grywalnej cywilizacji, wieloosobowości, nowego modelu zmęczenia ani historycznie zweryfikowanego katalogu floty. Opis w dokumentacji nie jest implementacją.

## 4. Ustalenia wymagające działania

Poziomy: **P1** — istotna poprawność lub blokada rdzenia; **P2** — ważna spójność i jakość; **P3** — zakres późniejszy. „Potwierdzone statycznie” oznacza widoczną ścieżkę kodu, której nie odtworzono w nowym uruchomieniu gry.

### A01 — kontrola istniejącego zabezpieczenia złomowania

`World.cs:219–223` zawiera jawny warunek `if (ship.Cargo > 0 || ship.HoldWood + ship.HoldSilver > 0) return;`. Zwykły rozkaz złomowania z cargo jest zatem **odrzucany**. `ClearOrder` odłącza wcześniejsze zadania. Nie znaleziono normalnej ścieżki dopisywania cargo w trakcie złomowania.

`StepScrapping` sam nie rozładowuje zasobów, ale nie dowodzi to utraty: trzeba uwzględnić warunek wejścia. Nie ma obecnie wystarczającej podstawy do zgłoszenia błędu księgi w tej ścieżce. Wniosek koryguje hipotezę pierwszego przeglądu, która przeoczyła zabezpieczenie.

Zachować guard i sprawdzić pokrycie przypadków: kuter, handlowiec z D/S, pełny magazyn oraz zapis. Nie implementować ponownie istniejącej odmowy. Jeżeli później powstanie automatyczne rozładowanie przed złomowaniem, otrzyma własny kontrakt księgowy i test. Status: potwierdzone zabezpieczenie, brak nowej reprodukcji runtime.

### A02 — P1: warunek abordażu ignoruje kursy

`World.cs:584` używa `abs(speedA−speedB)`. Przy 5,2 m/s na przeciwnych kursach wynik wynosi 0 zamiast 10,4 m/s. Okręty mogą więc spełniać warunek małej prędkości względnej, choć szybko się mijają.

Pewność: potwierdzone statycznie. Zalecenie: norma różnicy wektorów prędkości oraz weryfikacja położenia burt. Nie wystarczy zmiana jednej stałej. Odbiór: ten sam i przeciwny kurs, skrzyżowanie pod 90°, jednostka stojąca, mijanie, zatrzymanie i zapis. Koszt: średni.

### A03 — P1: prosty wzrost liczby załogi może stworzyć ujemne ceny

`Definitions.cs:97–119` wiąże minimalnych marynarzy z około 25% załogi. `TemplatePricing`, około w. 231–242, odejmuje koszt obsady od stałej ceny szablonu. Przy hipotetycznych 200 ludziach i cenie Ram 100 S:

`50 marynarzy + 150 wojowników`;

`CrewCost = 0,5×50 + 1×150 = 175 S`;

`ConstructionSilver = 0,4×(100−175) = −30 S`;

`EquipmentSilver = 0,6×(100−175) = −45 S`.

To kontrprzykład dla proponowanej naiwnej zmiany danych, **nie stwierdzenie, że obecny katalog już ma ujemne ceny**. Przed zmianą obsady trzeba rozdzielić wioślarzy, służbę pokładową i piechotę oraz wycenę składników. Test całego katalogu musi wymagać nieujemności każdej części. Koszt: średni/duży.

### A04 — P2: SI pomija oszczepników przy ocenie strat

`Opponent.cs:331–332` porównuje liczbę wojowników z `Plan.Swordsmen + Plan.Archers`, bez `Plan.Javelins`. Dla domyślnego abordażowca jest 40+20+7=67 wojowników, lecz próg korzysta z 47 zamiast 67. Połowa wynosi odpowiednio 23,5 i 33,5 przed ewentualnym zaokrągleniem.

Pewność: potwierdzone statycznie. Zalecenie: jeden wspólny licznik planowanej obsady bojowej, test każdego preset. Wpływ na wynik meczu wymaga pomiaru; nie przypisujemy mu z góry wszystkich błędów SI. Koszt: mały.

### A05 — P1/P2: katalog jest niespójny z historyczną ramą produktu

`Definitions.cs` udostępnia szeroki zestaw machin, a ciężkie konfiguracje uczestniczą w cyklu produkcji SI. Data istnienia określonej technologii gdzie indziej nie uzasadnia jej standardowego użycia na ateńskich okrętach przyjętego okresu.

Pewność: potwierdzona zawartość katalogu; ocena poszczególnych konfiguracji wymaga źródeł. Zalecenie: filtr rulesetu we wszystkich drogach pozyskania jednostki i wyposażenia. Nie kasować użytecznej implementacji pocisków ani nie przeprowadzać zmiany wyglądu. Koszt: średni. Odbiór: katalog, AI, nagrody, szablony, warsztat i wczytanie.

### A06 — P2: odnowienie złóż jest tworzeniem zasobu, a nie wydobyciem

`World.Economy.cs:32–43` zwiększa stan punktów i `GrownWood/GrownSilver`. Księga jawnie zna tę kreację, więc sama obecność odnowienia nie dowodzi błędu rachunkowego. Problem dotyczy znaczenia historycznego i planu zasobów.

Dla pustego, pozostawionego bez pracy punktu Standard, przy stałych parametrach:

`T_D = 60 + 3300/(12×0,53×0,15) = 3519,12 s = 58,65 min`;

`T_S = 60 + 1650/(4×0,53×0,15) = 5248,68 s = 87,48 min`.

Założenia rachunku: brak ponownej pracy, niezmienny limit tempa, początkowe zero i pełne odtworzenie pojemności. Czynna eksploatacja i opóźnienie po każdym pobraniu zmieniają wynik. Zalecenie: skończone partie i jawny dopływ z zaplecza; nie wystarczy nazwa „kopalnia odnawialna”. Koszt: średni.

### A07 — P2: rezerwacja miejsca w porcie i priorytety przeładunku są wymaganiami

`World.Expansion.cs:133–140` rezerwuje zasób źródła, a `World.Economy.cs:392–408` przydziela miejsca przez FIFO. Nie potwierdzono docelowej rezerwacji pojemności magazynu odbiorcy ani priorytetu dostawy wojskowej rosnącego po 20 s.

To rozbieżność dokumentacja–kod, nie dowód, że każdy rozładunek gubi zasoby. Nowy plan zachowuje potrzebę rezerwacji celu, lecz nie narzuca bez testu arbitralnego 20-sekundowego progu. Odbiór: równoczesne dostawy, pełny cel, zmiana celu, zatopienie, upadek portu i zwolnienie wszystkich rezerwacji. Koszt: średni.

### A08 — P2: nie ma jeszcze dowodu zbalansowanego trójkąta walki

Odczytane protokoły pojedynków obejmowały limity i konkretne ustawienia: para jednostek 180 m od siebie, dziobami do siebie, pełna obsada i domyślna broń, atak według roli, limit 300 s. Para taranowców mogła nie zadać obrażeń przez cały limit, a abordażowiec dominował w próbach, w których przeciwnik do niego podpływał. Są to sygnały diagnostyczne, nie ogólny ranking klas; wyników nie odtworzono w tym audycie.

Jeśli obie jednostki od początku dostają rozkaz wejścia w zasięg abordażu, test nie mierzy pełnej kontrgry manewrowej. Potrzebne są przypadki z możliwością wycofania, przestrzenią, ochroną celu i osłoną. Nie wolno stroić całej gry według samych pojedynków 1v1. Koszt: średni/duży.

### A09 — P2: planer SI ma zbyt prostą obronę i koncentrację sił

`Opponent.cs` operuje celem ataku i stałymi odległościami; znana pozycja startowego portu wymaga jawnego rozstrzygnięcia reguł wiedzy mapowej. Sama znajomość publicznego portu nie jest dowodem oszustwa. Ukryte zasoby, jednostki i kolejki nie mogą jednak trafiać do planera przez bezpośredni odczyt świata.

Zalecenie: oddzielić publiczne dane scenariusza od obserwacji i pamięci kontaktów, potem rozdzielić eskortę, obronę, rozpoznanie i odwód. SI zbiera już wraki, ale nie korzysta jeszcze ze wszystkich dostępnych graczowi funkcji konwojowych. Koszt: duży, po stabilizacji walki i ekonomii.

### A10 — P2: modele i dane QA nie są aktualnym rulesetem

`docs/economy-model/economy-parameters.json`, wyniki modelu i `qa/economy-baseline.json` nie odzwierciedlają wszystkich zmian bieżącego katalogu, odnowienia, budynków i obsad. `qa/scenario-design.json` opisuje projekty scenariuszy, nie działającą kampanię.

Zalecenie: oznaczyć ich zakres; parametry eksportować z faktycznie używanego katalogu. Nie kasować ani poprawiać ręcznie raportów testów, aby pasowały do planu. Zmiany testów i danych wykonawczych realizować w zadaniu implementacji z własnym odbiorem. Koszt: średni.

### A11 — P2: determinizm wymaga zakresu i sprawdzenia

Stały krok, `long` dla zasobów i RNG z ziarnem to wartościowe fundamenty. Pozycje i model morza korzystają również z liczb zmiennoprzecinkowych. Nie ma z tego automatycznego dowodu identycznych bitów między procesorami, systemami i buildami.

Zalecenie: nagrywanie komend, hash istotnego stanu, porównanie przebiegu ciągłego z przebiegiem po zapisie. Multiplayer otrzymuje osobną decyzję architektoniczną; nie traktować go jako gotowej konsekwencji tego projektu. Koszt: średni, dla sieci osobny duży zakres.

### A12 — P2: pomiary tempa i wydajności mają ograniczony zakres

Odczytane pomiary AI po ostatnich zmianach: ziarna 9 i 23, odpowiednio Krótka **13,4/30,0 min**, Standardowa **35,2/34,1 min**, Długa **30,9/30,9 min**. Są to łącznie **6 przebiegów**, nie próba reprezentatywna dla graczy. Dwa zbliżone wyniki nie określają rozrzutu populacji.

Dotychczasowe raporty techniczne zawierają krótkie pomiary GPU i kosztu załóg; nie odtwarzano ich w tym audycie. Sekwencja bez docelowej walki nie waliduje dużej floty. Parametry sprzętu w opisach wymagają każdorazowego przypisania do konkretnego pomiaru. Nie wyprowadzamy z tego rekomendacji zmiany estetyki. Koszt: osobny pomiar po gotowości scenariusza bojowego.

### A13 — P2: stały dystans ataku ignoruje zainstalowaną broń

`World.cs:278–285` używa podejścia do `Rules.ArcherRange×0,8`, czyli 80 m, bez wyprowadzenia dystansu z aktywnych stanowisk. To potwierdzone statycznie ograniczenie: broń krótszego zasięgu może nie dosięgać celu, a dłuższego nie wykorzystuje przewagi dystansu. Zalecenie: dystans według obsadzonej broni, jej minimalnego/maksymalnego zasięgu i doktryny, z zachowaniem widoczności oraz drogi odejścia. Testy: sam łuk, same oszczepy, brak obsady, mieszane stanowiska. Koszt: średni, P04.

### A14 — P1/P2: oczekujące rozkazy nie przechodzą przez zapis

`World.cs:32–34` oznacza `Pending` jako niezapisywane. Zapis pomiędzy przyjęciem rozkazu a następnym tickiem może zgubić tę komendę. To ograniczenie kontraktu potwierdzone statycznie, bez nowej reprodukcji runtime. Zalecenie: zapisywać na granicy ticku po wykonaniu komend albo serializować ich porządek. Test musi zaczynać się z niepustą kolejką, porównując dalszy przebieg z wykonaniem bez wczytania. Koszt: mały/średni po ustaleniu granicy, P01.

### A15 — P2: dowolny pozostały kadłub może podtrzymywać mecz

`World.cs:806–814` traktuje istnienie dowolnego własnego statku jako przeszkodę dla porażki. Warunek nie rozstrzyga zdolności do ruchu, walki, odbudowy lub wykonania celu. Sama możliwość z kodu nie dowodzi, że wystąpiła w odczytanych meczach. Zalecenie: rozstrzygać osiągalność celu operacji, dostępne dostawy i zasoby zamiast wyłącznie istnienia obiektu. P07 musi wykrywać trwały brak możliwości kontynuowania lub mieć jawny limit operacji. Koszt: średni.

## 5. Wnioski produkcyjne

1. Największą wartością jest wykonany rdzeń operacyjny: okręt, komendy, port, transport, przejęcie i zapis. Jego odrzucenie nie ma uzasadnienia.
2. Sam rozrost katalogu nie rozwiązuje błędów geometrii, przeznaczenia załogi ani decyzji SI. Najpierw poprawność i kontrgra, później liczba jednostek.
3. Kampania potrzebuje własnego wykonawcy, grafu i rozliczenia stanu; 14 opisów misji nie jest 14 wykonanymi poziomami.
4. Historia ma ograniczać dobór dostępnych reguł, nie służyć dekoracyjnemu uzasadnianiu dowolnej mechaniki.
5. Dwa zasoby, fizyczny transport i kilka lokalnych portów wystarczą do tworzenia złożonych decyzji. Trzeci zasób i potrzeby miasta nie są obecnie konieczne.
6. Taktyczna głębia wynika z pozycji, ryzyka i informacji. Morale, rany, zmęczenie i oficerowie muszą wspierać te decyzje zamiast tworzyć niezależne paski do obsługi.
7. Rozszerzenia należy wykonywać po jednej przyczynie zmiany wyniku: nie mieszać nowej obsady, całej SI, pogody i cen w jednym nieporównywalnym pomiarze.
8. Poprawne testy jednostkowe, build, badanie historyczne i playtest są potrzebne, ale mierzą różne rzeczy. Żaden nie zastępuje pozostałych.

## 6. Granice pewności tego raportu

Potwierdzono kodowe ścieżki i odczytane pliki. Nie potwierdzono ponownym uruchomieniem nowych reprodukcji, współczynników balansu, pełnej wydajności, zachowania na innym sprzęcie ani kompletnej prawdziwości wszystkich danych historycznych. Liczby linii odnoszą się do wskazanego HEAD; po zmianach trzeba je ponownie ustalić.

Nie przedstawiamy liczby linii kodu ani liczby commitów jako procentu ukończenia. Nie ma wspólnego mianownika, który pozwalałby matematycznie stwierdzić „gra jest gotowa w X%”. Gotowość mierzymy spełnionymi kontraktami mechanik i przejściem konkretnych scenariuszy.
