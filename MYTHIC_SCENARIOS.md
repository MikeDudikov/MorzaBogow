# Morza Bogów — scenariusze mitycznych wypraw

## S00. Struktura kampanii

Misje realizują GAME_VISION.md przez cele wymagające dowodzenia flotą: eskortę, nawigację, kontrolę przejścia, odwrót, abordaż i ratunek. Warianty z ograniczonym zapleczem wykorzystują przygotowaną flotę, a cele gospodarcze służą wykonaniu operacji. Każda misja ma własne tempo i stan początkowy.

Kampania obejmuje 14 misji w trzech cyklach: M01–M07 to wyprawa Eneasza, M08–M12 to podróż Odyseusza, a M13–M14 to niezależne opowieści. Odblokowanie misji nie oznacza ciągłości floty ani magazynów. Odrębny katalog obejmuje 40 potyczek B01–B08/R01–R05. Zwycięstwo zależy od jawnych celów scenariusza; niszczenie jednostek neutralnych nie jest automatycznie nagradzane.

## S01. Kontrakt rozgrywki i skala

Gra pozostaje strategią morską. Ponad 70% aktywnego czasu misji projektuj jako sterowanie statkami, walkę, eskortę, nawigację i logistykę na wodzie. To cel playtestu, nie wynik już wykonanego pomiaru. Zdarzenia na lądzie obsługują interakcje przy brzegu i krótkie sceny; nie tworzymy osobnej armii lądowej, systemu lotnictwa ani rozbudowanej gospodarki nowych cywilizacji.

Okręt Eneasza to jeden wyjątkowy okręt fabularny korzystający z bazowego kadłuba królewskiego; nie można produkować jego kopii. W cyklu Odyseusza używaj osobnej nazwy „Okręt Odyseusza”. Liczbę eskort określa konfiguracja każdej misji.

Wszystkie startowe okręty są wyposażone i obsadzone według jawnego MissionLoadout; stanowią początkowy majątek scenariusza, nie zakupy odliczane po starcie. Ceny i parametry bazowych klas pozostają takie jak w głównej specyfikacji. Nieosiągalne w porcie klasy są ukryte w menu budowy. Podane D/S trafiają do jednej własnej osady; cargo z M03 jest dodatkowym rzeczywistym ładunkiem o osobnych ID. Dwa typy zasobów pozostają jedynymi wydawanymi walutami.

Szablony bazy: **REPAIR_AND_LIGHT_BUILD** — port, stocznia ze stanowiskiem remontu, budowa wyłącznie kutra/taranowca/niszczyciela załóg oraz zwykłe koszty E; **REPAIR_ONLY** — ten sam zespół istniejących budynków, ale budowa/rozbudowa wyłączona regułą misji, remont opłacany lokalnie; **SEA_ONLY** — brak osady, D/S startowe 0, bez budowy, opłat ani magicznego globalnego portfela. Rytuały fabularne są jednokrotnymi interakcjami celu, nie darmowym odblokowaniem dwunastu zaklęć poza sanktuariami.

Węzły podane jako wood/silver to łączne początkowe zapasy na mapie, nie liczba węzłów. Dziel je na 1–2 rzeczywiście osiągalne punkty pracy; oba zasoby są skończone (E02). Zero oznacza brak punktu, nie nieskończone źródło. Budżety i ładunek misji określa E12. Handel neutralny i skarby domyślnie wyłączone w tych misjach, chyba że specyfikacja misji jawnie je doda. Nie trzeba kupować specjalizacji miasta, aby wykonać cel fabularny.

Podane czasy są sekundami symulacji; pauza zatrzymuje pogodę, naloty, pociski i zegary. planningSeconds w danych jest parametrem opisowym, a nie automatycznym warunkiem przejścia; 0 oznacza brak podanej wartości czasu. Rzeczywiste zakończenie etapu wymaga predicate z opisu misji. W M12 zegar pierwszego okna 8:00 licz od początku misji, nie początku P2. Cykl pogody z M11/M12 jest lokalnym wyjątkiem do standardu mapy i musi być zapowiedziany w briefie.

## S02. Bohaterowie, towarzysze i mityczni przeciwnicy

Zachowaj jeden aktywny slot bohatera ze zdolnościami na okręt. Eneasz i Sybilla mogą być równocześnie obecni w opowieści o Eneaszu, lecz pozostali są nieaktywnymi towarzyszami fabularnymi: bez dodatkowych aur, zdolności, załogi bojowej i wypłat XP. Manifest fabularny ma unikalne actorId; nie jest sposobem dublowania aktywnych bohaterów. Poza zdarzeniami konkretnej misji transfer wymaga normalnych reguł portowych. Nie obsadzaj automatycznie wszystkich dziewięciu bohaterów w każdym epizodzie.

Anchizes, Dydona, Palinur, Kirke, Dionizos, Andromeda i pozostali aktorzy fabularni są NPC/aktorami scenariusza; nie rozszerzają katalogu dziewięciu grywalnych bohaterów. Frakcje mityczne używają istniejących profili statków z własną oprawą, nie otrzymują przypadkowo tożsamości innej cywilizacji tylko dlatego, że korzystają z jej komponentu technicznego.

Scylla, Charybda, Harpie, Syreny i objawienie Dionizosa nie są zwykłymi okrętami z HP. Morski potwór z M14 jest Ketosem. Każde zagrożenie ma własne zachowania określone przez scenariusz. Nie przyznawaj graczowi możliwości zabicia boga przez przewagę DPS.

## S03. Sprawiedliwe napięcie i gospodarka

Wrogie okręty pochodzą z jawnego, skończonego EnemyManifest. Okręty startowe i odwody w każdej misji sumują się do enemyHullBudget z danych; żadnych nieskończonych respawnów ani mnożenia grup po wejściu/wyjściu z triggera. Odwody mogą być predefiniowanymi wpływami scenariusza, zgodnie z księgą E10, a nie fikcyjną produkcją SI bez kosztu. Ich miejsce wejścia i powód muszą być zapowiedziane.

Normalny poziom trudności zachowuje bazowe HP, obrażenia i ekonomię. Łatwy daje dłuższe o 30% zapowiedzi i o 25% dłuższe okna krytyczne; trudny zmniejsza te czasy o 15% przy zachowaniu minimum 4 s dla niszczących ataków. Wszystkie zegary misji działają w sekundach symulacji. Brak skalowania trudności po cichu z wielkością floty.

Znaczące zagrożenie musi mieć kontrgrę: strefę ucieczki, wycofanie, okno postoju albo wybór strat. Nie wymagaj manewru niemożliwego dla danego zanurzenia, promienia skrętu i najwolniejszego obowiązkowego statku. Parametry cyklu skał i wiru są hipotezą do testów geometrii; jeśli brak fizycznie osiągalnego przejścia, misja jest BLOCKED, nie „trudna”.

Nie karz poprawnej gry obowiązkowym ukrytym buntem lub zniszczeniem całej floty dla zgodności z tragicznym finałem. Krytyczne wydarzenie może wystąpić przed rozpoczęciem gry albo prowadzić do jawnego alternatywnego wyniku. Ograniczenia dotyczące głodu, opłat za flotę i dwóch walut E01 zachowują pierwszeństwo. Każdy cel ma zapas wykonalności: dodatkowych ocalałych, drugą drogę lub możliwość odbudowania wymaganego kutra. Gdy cel staje się nieosiągalny, pokaż porażkę/wybór wczytania, bez bezterminowego utknięcia.

## S04. Cele, zdarzenia i zapis

Każda misja ma MissionDefinition z id, arc, mode, map, MissionLoadout, EnemyManifest, zasobami, fazami, predicate celów, ograniczeniami budowy i warunkami końca. qa/scenario-design.json jest projektem danych, nie gotowym plikiem Unity. Claude Code musi opracować adapter do ScriptableObject lub innego istniejącego systemu scenariuszy. Dostarczony walidator sprawdza strukturę danych, nie działanie zdarzeń silnika. Nie traktuj słownych flag w JSON jako już działających celów.

Success wymaga wszystkich allWinFlags, failure dowolnego anyFailFlags. W tym samym ticku najpierw rozlicz śmierć, utratę celu i nieosiągalność, potem sukces. Jednorazowe skutki mają eventId i zapisany commit. Finisz musi sprawdzać pełną geometrię/statki znajdujące się w obszarze oraz stan żywych wymaganych aktorów. Przekroczenie granicy nie daje nieśmiertelności przed zatwierdzeniem ewakuacji.

Przedmiot celu ma itemId, kontener i rejestr transferów. Utrata statku nie usuwa przedmiotu celu; powstaje oznaczony wrak bez czasu wygasania, z realnie dostępnym sposobem podjęcia przedmiotu. Jeżeli nie ma żadnego sprawnego zbieracza ani możliwości jego budowy/przejęcia, sprawdź goal_unreachable zamiast udawać, że zadanie nadal działa. Ocalałych identyfikuj pojedynczo; licznik nie może zwiększać się od wielokrotnego wpłynięcia w ten sam trigger.

Zapis obejmuje fazę misji, pozostały czas, stan zagrożeń, pending telegraphs, trasy pocisków, manifest wejść przeciwników, cele i zdarzenia committed, pasażerów/przedmioty, flagi opowieści i wynik. Po load nie naliczaj ponownie grabieży, kosztu wyprawy, transformacji, nagrody ani uszkodzenia zakończonego wcześniej ataku. Zapis po zakończeniu przechowuje wynik i nie uruchamia jeszcze raz ekranu nagród.

## S05. Kampania, powtarzanie i nagrody

M01–M07 mają kolejność 1→2→3→4→5→6→7; M08–M12 8→9→10→11→12. M13 i M14 są niezależnymi opowieściami odblokowywanymi po wprowadzeniu podstaw sterowania; mogą być rozgrywane oddzielnie. Menu prezentuje rekomendowaną kolejność cykli. Nie przenoś floty, magazynów i nagród między cyklami ani między powtórzeniami misji.

Każda misja startuje z jawnym, stałym zestawem jednostek i zasobów. Między misjami zachowywane są ukończenia, medale i kronika. Bohaterowie otrzymują poziomy oraz obsadę określone dla danej misji. Straty, wyposażenie, rany i zapasy nie przenoszą się do następnej misji. Powtórzenie misji odtwarza jej pełny stan początkowy.

Cele poboczne dają medale, wpisy i warianty dialogu, bez stałego +10% dochodu lub przewagi w potyczkach. Kronika opisuje wynik osiągnięty przez gracza. Funkcja powtórzenia przywraca stan wejściowy, nie kopiuje istniejącego wyposażenia i nie produkuje darmowego złota.

## S06. Zestawienie scenariuszy

| ID | Tytuł | Główna decyzja | Czas docelowy |
|---|---|---|---|
| M01 | Popioły Ilionu | Najpierw zabrać ludzi z bliższego, płonącego nabrzeża albo wcześniej przebić się po penaty. | 15–20 min |
| M02 | Szpony nad Strofadami | Szybki załadunek na odsłoniętym składzie albo dłuższy rejs do osłoniętej zatoki. | 12–18 min |
| M03 | Noc odpłynięcia z Kartaginy | Płytka cieśnina wymusza kolumnę; otwarte morze daje miejsce do tarana, ale skraca pościg. | 22–30 min |
| M04 | Ogień na plaży Drepanum | Gasić najbliższe okręty załogą albo odholować zdrowe kadłuby i oddać płonące. | 18–24 min |
| M05 | Palinur u steru | Dłuższa droga z dala od raf albo krótszy przesmyk wymagający dokładnego szyku po utracie sternika. | 16–22 min |
| M06 | Głos z Kume | Zdobyć patrol i otworzyć spokojne podejście albo przebić blokadę i uruchomić skończony odwód. | 20–28 min |
| M07 | Ujście Tybru | Bronić ujścia wszystkimi siłami albo odciągnąć rajdy od kotwicowiska kosztem pozycji. | 24–32 min |
| M08 | Imię zapisane na falach | Odbiór blisko klifu oszczędza rejs, ale zwiększa ryzyko skał; druga zatoka wymaga rozpoznania. | 16–22 min |
| M09 | Wiatry wyrwane z worka | Najpierw zebrać najwolniejsze transporty albo osłonić drogę i prowadzić jednostki pojedynczo. | 18–25 min |
| M10 | Port, który stał się pułapką | Bezpieczny odwrót bohatera albo dodatkowe ryzyko dla ludzi w porcie. | 15–22 min |
| M11 | Sześć głów, jeden przesmyk | Bliżej Scylli — ryzyko załogi; dalej od klifu — większa siła wiru i ryzyko manewrowe. | 16–23 min |
| M12 | Przysięga pod słońcem | Wcześniejszy zbiór na narażonym składzie albo ostrożna praca i późniejsze okno wyjścia. | 18–26 min |
| M13 | Bóg, którego nie utrzymają liny | Szybszy abordaż osłabionego celu albo wcześniejsze odciągnięcie eskorty, by nie zatonąć razem z ratowanym statkiem. | 20–28 min |
| M14 | Andromeda: cień pod falą | Połączyć ewakuację z walką w dwóch grupach albo najpierw zapewnić bezpieczny korytarz i dopiero przejść do finału. | 28–36 min |

## M01. Popioły Ilionu

**Fabuła:** Ilion płonie. Eneasz wywozi z miasta ojca Anchizesa, święte wizerunki penatów i tylu ocalałych, ilu zdołają zabrać okręty.

**Otwarcie:** Troada, mapa około 1.5 × 1.5 km; cel 15–20 min. Aktywny bohater: Eneasz. Baza REPAIR_AND_LIGHT_BUILD. Flota: royal ×1, cutter ×2, crewDestroyer ×1. Lokalnie 150 D/80 S; zasoby mapy 360 D/660 S początkowo. Łączny limit okrętów przeciwnika: 4; osobne zagrożenia: Płonące nabrzeże zamykane odcinkami.

**Przebieg:** Kutry podejmują 24 ocalałych z trzech fizycznych przystani; każda osoba ma actorId i wsiada raz. Penaty powstają jako jedyny przedmiot przy brzegu i wymagają transferu na okręt Eneasza. Cztery greckie patrolowce pilnują wyjścia; można je ominąć lub związać walką. Zwycięstwo wymaga wyjścia okrętu Eneasza z penatami, nie zniszczenia patroli.

**Fazy:** P1 Podejmij ocalałych (koniec warunkiem) → P2 Zabierz penaty (parametr czasu 60 s) → P3 Wyjdź z zatoki (koniec warunkiem). Wskazane czasy etapów to parametry celu; dokładne użycie zegara opisuje przebieg i presja, nie automatyczne przejście po odczekaniu.

**Napięcie i kontrgra:** Odcinek nabrzeża zamyka się co 90 s po zapowiedzi 20 s; osoby z zamkniętej przystani można podjąć z sąsiedniej. Patrol reaguje na obserwację, nie na wiedzę spod mgły.

**Zwycięstwo — wszystkie warunki:** survivors_boarded, penates_on_flagship, flagship_extracted. **Porażka — dowolny warunek:** flagship_lost, aeneas_lost, penates_unrecoverable. Nazwy flag należy podłączyć do konkretnych predicate w implementacji, nie uznawać za gotowy kod.

**Decyzja taktyczna:** Najpierw zabrać ludzi z bliższego, płonącego nabrzeża albo wcześniej przebić się po penaty.

**Cel poboczny:** Podjąć wszystkich 24 ocalałych i nie stracić kutra.

**Zapis i test regresyjny:** Lista actorId na pokładach, stan przystani, jedno globalne ID penatów. Utrata transportu z penatami pozostawia odzyskiwalny wrak bez limitu czasu.

**Nagroda i przejście:** M02 i wpis o wyjściu z Ilionu; penaty są przedmiotem fabuły bez mnożnika ekonomicznego.

## M02. Szpony nad Strofadami

**Fabuła:** Głodna flota zatrzymuje się na Strofadach. Harpie niszczą zapasy, a Celajno zapowiada wygnańcom dalsze trudy.

**Otwarcie:** Archipelag Egejski, mapa około 1.5 × 1.5 km; cel 12–18 min. Aktywny bohater: Eneasz. Baza REPAIR_ONLY. Flota: royal ×1, cutter ×1, ram ×2. Lokalnie 100 D/50 S; zasoby mapy 0 D/0 S początkowo; ładunek misji: 320 jednostek zapasów w dwóch składach, wymagane 200. Łączny limit okrętów przeciwnika: 0; osobne zagrożenia: Harpie: trzy zapowiadane naloty.

**Przebieg:** Gracz zabiera 200 jednostek zapasów na statki i dowozi je do własnej przystani. Zapasy są ładunkiem misji (E12), nie srebrem. Harpie atakują wyłącznie odsłonięty ładunek w strefie zapowiedzi; ostrzał lekki je odpędza, a zwycięstwo nie wymaga ich zabicia. Po trzecim nalocie otwiera się bezpieczne wyjście.

**Fazy:** P1 Zabezpiecz skład (parametr czasu 45 s) → P2 Dostarcz zapasy (koniec warunkiem) → P3 Wyprowadź flotę (parametr czasu 120 s). Wskazane czasy etapów to parametry celu; dokładne użycie zegara opisuje przebieg i presja, nie automatyczne przejście po odczekaniu.

**Napięcie i kontrgra:** Nalot co 60 s, sygnał 8 s przed nurkowaniem, strata najwyżej 25 jednostek z odsłoniętego ładunku, nigdy z dostarczonej części. Statek w osłoniętej zatoce nie jest celem.

**Zwycięstwo — wszystkie warunki:** supplies_delivered, raids_outlasted, flagship_extracted. **Porażka — dowolny warunek:** flagship_lost, aeneas_lost. Nazwy flag należy podłączyć do konkretnych predicate w implementacji, nie uznawać za gotowy kod.

**Decyzja taktyczna:** Szybki załadunek na odsłoniętym składzie albo dłuższy rejs do osłoniętej zatoki.

**Cel poboczny:** Dostarczyć 200 jednostek, tracąc najwyżej 50.

**Zapis i test regresyjny:** Skradzione ilości, licznik nalotów i pozostały czas. Po wczytaniu nie zwracaj skradzionych zapasów i nie powtarzaj zakończonego nalotu.

**Nagroda i przejście:** M03 i wpis o przepowiedni Celajno; bez premii D/S przenoszonej dalej.

## M03. Noc odpłynięcia z Kartaginy

**Fabuła:** Eneasz po rozkazie bogów opuszcza Kartaginę nocą. W tej misji Iarbas, odtrącony król sąsiedni, rusza za odpływającym konwojem.

**Otwarcie:** Wybrzeża Perskie, mapa około 3 × 3 km; cel 22–30 min. Aktywny bohater: Eneasz. Baza REPAIR_ONLY. Flota: royal ×1, merchant ×2, ram ×2, crewDestroyer ×1. Lokalnie 140 D/60 S; zasoby mapy 0 D/0 S początkowo. Łączny limit okrętów przeciwnika: 12; osobne zagrożenia: Dwie trasy o innym zanurzeniu; Noc ograniczająca widzenie.

**Przebieg:** Transporty mają po 90 D rzeczywistego ładunku, poza początkowym zapasem osady. Płytsza cieśnina dopuszcza handlowce, ale wymusza kolumnę; otwarte morze daje przestrzeń do tarana, lecz skraca podejście przechwytujących. Tylna straż może przeciąć linię abordażem albo osłonić odwrót. Zwycięstwo po wyjściu okrętu Eneasza i jednego transportu oraz utrzymaniu rozdzielenia 90 s.

**Fazy:** P1 Rozpoznaj przejścia (parametr czasu 60 s) → P2 Przeprowadź konwój (koniec warunkiem) → P3 Oderwij pościg (parametr czasu 90 s). Wskazane czasy etapów to parametry celu; dokładne użycie zegara opisuje przebieg i presja, nie automatyczne przejście po odczekaniu.

**Napięcie i kontrgra:** Grupa 4 okrętów na starcie, kolejne 4 po pierwszej obserwacji konwoju, ostatnie 4 po wejściu okrętu Eneasza w zewnętrzny sektor. Każda przypływa z zapowiedzianej krawędzi, bez respawnu.

**Zwycięstwo — wszystkie warunki:** flagship_extracted, at_least_one_supply_extracted, pursuit_separation_90s. **Porażka — dowolny warunek:** flagship_lost, aeneas_lost, all_supply_lost. Nazwy flag należy podłączyć do konkretnych predicate w implementacji, nie uznawać za gotowy kod.

**Decyzja taktyczna:** Płytka cieśnina wymusza kolumnę; otwarte morze daje miejsce do tarana, ale skraca pościg.

**Cel poboczny:** Wyprowadzić oba transporty z ich realnym ładunkiem.

**Zapis i test regresyjny:** Wejścia grup, stan cargo, przekroczenie granicy i czas rozdzielenia. Ponowne wejście w sektor nie tworzy kolejnej grupy.

**Nagroda i przejście:** M04; wynik eskorty zapisuje medal, nie większy kapitał następnej misji.

## M04. Ogień na plaży Drepanum

**Fabuła:** Podczas igrzysk na Sycylii znużone tułaczką kobiety podpalają okręty na plaży Drepanum. Deszcz zesłany przez Jowisza ratuje resztę floty.

**Otwarcie:** Wyspy Wulkaniczne, mapa około 1.5 × 1.5 km; cel 18–24 min. Aktywny bohater: Eneasz. Baza REPAIR_AND_LIGHT_BUILD. Flota: royal ×1, cutter ×2, merchant ×1, crewDestroyer ×1 oraz cztery fabularne kadłuby do ratowania na kotwicy. Lokalnie 180 D/100 S; zasoby mapy 240 D/660 S początkowo. Łączny limit okrętów przeciwnika: 0; osobne zagrożenia: Pożar przenoszący się między bliskimi kadłubami.

**Przebieg:** Ogień korzysta z reguł C05: intensywność, narastanie, gaszenie zajmujące załogę. Gracz gasi albo odholowuje kadłuby, rozdzielając odległości, by przerwać rozprzestrzenianie. Deszcz nadchodzi raz, po zapowiedzi, i mnoży narastanie przez 0,5. Przeciwnikiem jest czas i układ kotwicowiska, nie flota.

**Fazy:** P1 Oceń pożar (parametr czasu 30 s) → P2 Opanuj ogień (koniec warunkiem) → P3 Przygotuj flotę do drogi (parametr czasu 120 s). Wskazane czasy etapów to parametry celu; dokładne użycie zegara opisuje przebieg i presja, nie automatyczne przejście po odczekaniu.

**Napięcie i kontrgra:** Zapowiedź deszczu 30 s przed nadejściem, najpóźniej w 8:00 misji. Ogień nie zabija załogi szybciej niż 1/s i pozostaje czytelny na każdym profilu grafiki.

**Zwycięstwo — wszystkie warunki:** fire_contained, hulls_saved_min3, fleet_ready. **Porażka — dowolny warunek:** flagship_lost, aeneas_lost, hulls_below_3. Nazwy flag należy podłączyć do konkretnych predicate w implementacji, nie uznawać za gotowy kod.

**Decyzja taktyczna:** Gasić najbliższe okręty załogą albo odholować zdrowe kadłuby i oddać płonące.

**Cel poboczny:** Ocalić wszystkie cztery kadłuby.

**Zapis i test regresyjny:** Intensywność ognia per kadłub, stan deszczu, zdarzenia gaszenia. Po wczytaniu nie uruchamiaj deszczu drugi raz.

**Nagroda i przejście:** M05; zapis ocalonych kadłubów w kronice, bez przenoszenia floty.

## M05. Palinur u steru

**Fabuła:** Noc jest spokojna, gdy bóg snu strąca sternika Palinura do morza. Eneasz sam przejmuje ster, gdy okręt dryfuje ku skałom Syren.

**Otwarcie:** Wyspy Syren, mapa około 1.5 × 1.5 km; cel 16–22 min. Aktywny bohater: Eneasz. Baza REPAIR_ONLY. Flota: royal ×1, merchant ×1, ram ×1, crewDestroyer ×1. Lokalnie 90 D/60 S; zasoby mapy 0 D/0 S początkowo. Łączny limit okrętów przeciwnika: 0; osobne zagrożenia: Nocna żegluga przy rafach; Utrata sternika.

**Przebieg:** Noc zmniejsza widzenie, a rafy mają logiczny obrys ujawniany przez rozpoznanie. Na wyznaczonej bramce następuje jednorazowe zdarzenie: okręt Eneasza traci 30% zwrotności do czasu 12 s interakcji bohatera przy sterze, wykonywanej w ruchu. Gracz utrzymuje szyk i prędkość transportu.

**Fazy:** P1 Rozpoznaj kanał (parametr czasu 45 s) → P2 Przejmij ster (parametr czasu 12 s) → P3 Wyjdź za ostatni przylądek (koniec warunkiem). Wskazane czasy etapów to parametry celu; dokładne użycie zegara opisuje przebieg i presja, nie automatyczne przejście po odczekaniu.

**Napięcie i kontrgra:** Zapowiedź zdarzenia 10 s przed bramką. Dryf ku rafie jest widoczny na trasie, a ręczny rozkaz odwrotu działa. Rafa nie zabija natychmiast sprawnego statku.

**Zwycięstwo — wszystkie warunki:** channel_crossed, helm_recovered, flagship_extracted. **Porażka — dowolny warunek:** flagship_lost, aeneas_lost, all_supply_lost. Nazwy flag należy podłączyć do konkretnych predicate w implementacji, nie uznawać za gotowy kod.

**Decyzja taktyczna:** Dłuższa droga z dala od raf albo krótszy przesmyk wymagający dokładnego szyku po utracie sternika.

**Cel poboczny:** Nie wejść na rafę i zachować transport.

**Zapis i test regresyjny:** Stan bramki, helm_event_done, postęp interakcji i szyk. Po wczytaniu zdarzenie nie powtarza się.

**Nagroda i przejście:** M06; wpis o Palinurze bez stałej premii do nawigacji.

## M06. Głos z Kume

**Fabuła:** U brzegów Kume Eneasz szuka Sybilli, która ma wskazać mu dalszą drogę. Rytuał w jaskini wymaga czasu, a morze wokół pilnują korsarze.

**Otwarcie:** Morze Zapomnianych, mapa około 3 × 3 km; cel 20–28 min. Aktywny bohater: Sybilla. Baza REPAIR_AND_LIGHT_BUILD. Flota: royal ×1, cutter ×1, crewDestroyer ×1, boarder ×1. Lokalnie 210 D/100 S; zasoby mapy 480 D/660 S początkowo. Łączny limit okrętów przeciwnika: 6; osobne zagrożenia: Patrole korsarzy; Zamykane okno rytuału.

**Przebieg:** Abordaż zdobywa okręt patrolowy i pozwala ograniczyć starcie, bez magicznej niewidzialności. Okręt z Sybillą stoi w strefie jaskini 45 s; przerwanie zachowuje ukończone etapy. Po wysłuchaniu wyroczni flota wychodzi przez wskazane przejście. Rytuał nie jest powtarzalną farmą nagród.

**Fazy:** P1 Otwórz podejście do jaskini (koniec warunkiem) → P2 Osłoń rytuał Sybilli (parametr czasu 45 s) → P3 Wyjdź ku Italii (koniec warunkiem). Wskazane czasy etapów to parametry celu; dokładne użycie zegara opisuje przebieg i presja, nie automatyczne przejście po odczekaniu.

**Napięcie i kontrgra:** Cztery patrolowce na mapie, dwie jednostki odwodu po alarmie. Alarm po wejściu w widoczny sektor lub rozpoczęciu rytuału, nie z wiedzy SI pod mgłą.

**Zwycięstwo — wszystkie warunki:** oracle_heard, sibyl_on_flagship, flagship_extracted. **Porażka — dowolny warunek:** flagship_lost, sibyl_lost. Nazwy flag należy podłączyć do konkretnych predicate w implementacji, nie uznawać za gotowy kod.

**Decyzja taktyczna:** Zdobyć patrol i otworzyć spokojne podejście albo przebić blokadę i uruchomić skończony odwód.

**Cel poboczny:** Przejąć patrol zamiast go zatopić; nie zatopić dwóch neutralnych statków.

**Zapis i test regresyjny:** Rytuał, alarm i manifest odwodu. Wczytanie nie powtarza rytuału ani nie tworzy drugiego odwodu.

**Nagroda i przejście:** M07; wpis o wyroczni bez stałej premii magicznej.

## M07. Ujście Tybru

**Fabuła:** Trojanie docierają do ujścia Tybru. Turnus podpala ich okręty na kotwicy, a Kybele zamienia je w morskie nimfy, zanim spłoną.

**Otwarcie:** Archipelag Egejski, mapa około 3 × 3 km; cel 24–32 min. Aktywny bohater: Eneasz. Baza REPAIR_ONLY. Flota: royal ×1, ram ×2, shipDestroyer ×1, merchant ×1. Lokalnie 210 D/90 S; zasoby mapy 360 D/660 S początkowo. Łączny limit okrętów przeciwnika: 10; osobne zagrożenia: Zapalające rajdy na kotwicowisko.

**Przebieg:** Gracz utrzymuje ujście przez łącznie 180 s i wysadza osadników z handlowca na brzeg. Rajdy Turnusa kierują się na kotwicowisko po jawnej obserwacji. Jednorazowe zdarzenie Kybele usuwa pierwszy kadłub bliski spłonięcia jako ocalały, bez zaliczania straty; nie tworzy nowych jednostek i nie jest powtarzalne.

**Fazy:** P1 Rozpoznaj ujście (parametr czasu 60 s) → P2 Utrzymaj rzekę (parametr czasu 180 s) → P3 Wysadź osadników (koniec warunkiem). Wskazane czasy etapów to parametry celu; dokładne użycie zegara opisuje przebieg i presja, nie automatyczne przejście po odczekaniu.

**Napięcie i kontrgra:** Trzy rajdy z zapowiedzianych kierunków, po 3–4 okręty, z manifestu 10 kadłubów. Brak skalowania liczby rajdów z rozmiarem floty.

**Zwycięstwo — wszystkie warunki:** river_mouth_held, settlers_landed, flagship_safe. **Porażka — dowolny warunek:** flagship_lost, aeneas_lost. Nazwy flag należy podłączyć do konkretnych predicate w implementacji, nie uznawać za gotowy kod.

**Decyzja taktyczna:** Bronić ujścia wszystkimi siłami albo odciągnąć rajdy od kotwicowiska kosztem pozycji.

**Cel poboczny:** Ukończyć misję bez utraty statku i ocalić transport.

**Zapis i test regresyjny:** Licznik utrzymania, manifest rajdów, cybele_event_done i lista osadników. Wczytanie nie przywraca zakończonego rajdu ani zdarzenia.

**Nagroda i przejście:** Zakończenie pierwszego cyklu; M08 rozpoczyna nową opowieść z odrębną flotą.

## M08. Imię zapisane na falach

**Fabuła:** Odyseusz oślepia Polifema i ucieka. Cyklop rzuca skałami; bohater ujawnia imię i ściąga na siebie klątwę.

**Otwarcie:** Archipelag Egejski, mapa około 1.5 × 1.5 km; cel 16–22 min. Aktywny bohater: Odyseusz. Baza SEA_ONLY. Flota: royal ×1, cutter ×2, ram ×1. Lokalnie 0 D/0 S; zasoby mapy 0 D/0 S początkowo. Łączny limit okrętów przeciwnika: 0; osobne zagrożenia: Polifem na klifie; Dwie strefy zbierania ocalałych.

**Przebieg:** Dwa kutry podejmują po sześciu ocalałych, którzy zajmują miejsce zgodnie z regułą pasażera. Minimalny cel 12 osób przy 16 dostępnych w dwóch strefach. Okręt Odyseusza odciąga uwagę, a gracz rozdziela punkty odbioru i zachowuje drogę ucieczki. Ujawnienie imienia następuje w scenie zakończenia i nie odbiera kontroli podczas manewrów.

**Fazy:** P1 Zabierz oddział z brzegu (parametr czasu 60 s) → P2 Przetrwaj rzuty kamieni (koniec warunkiem) → P3 Wyjdź poza zasięg (koniec warunkiem). Wskazane czasy etapów to parametry celu; dokładne użycie zegara opisuje przebieg i presja, nie automatyczne przejście po odczekaniu.

**Napięcie i kontrgra:** Rzuty co najmniej 25 s, strefa zapowiedziana 7 s, maks. 160 HP na trafienie; po utracie kontaktu kamień leci ku ostatniej znanej pozycji.

**Zwycięstwo — wszystkie warunki:** odysseus_extracted, twelve_survivors_rescued. **Porażka — dowolny warunek:** odysseus_lost, all_rescue_capacity_lost, survivor_goal_unreachable. Nazwy flag należy podłączyć do konkretnych predicate w implementacji, nie uznawać za gotowy kod.

**Decyzja taktyczna:** Odbiór blisko klifu oszczędza rejs, ale zwiększa ryzyko skał; druga zatoka wymaga rozpoznania.

**Cel poboczny:** Wyprowadzić wszystkich 16 ocalałych i oba kutry.

**Zapis i test regresyjny:** Unikalne ID uratowanych, pasażerowie, blokady miejsc odbioru i aktywny pocisk. Jednej osoby nie może podjąć kilka statków.

**Nagroda i przejście:** M09; zapis wyniku; następna misja ma własną flotę startową.

## M09. Wiatry wyrwane z worka

**Fabuła:** Towarzysze otwierają worek Eola. Uwolnione wiatry odrzucają flotę; Eol odmawia ponownej pomocy.

**Otwarcie:** Morze Burz, mapa około 3 × 3 km; cel 18–25 min. Aktywny bohater: Odyseusz. Baza SEA_ONLY. Flota: royal ×1, merchant ×2, ram ×2. Lokalnie 0 D/0 S; zasoby mapy 0 D/0 S początkowo. Łączny limit okrętów przeciwnika: 0; osobne zagrożenia: Trzy strefy wiatru o zapowiedzianych zwrotach.

**Przebieg:** Statki startują w trzech czytelnych skupiskach z drożną trasą do zborne­go akwenu. Użycie wioseł i odpowiedniego kursu pozwala wyjść spod niekorzystnego wiatru. Zwycięstwo wymaga okrętu Odyseusza i dwóch pozostałych; odmowa Eola jest sceną fabularną, nie oczekiwaniem na nieistniejące zaopatrzenie. Nie wprowadza się worka jako nowej waluty.

**Fazy:** P1 Odzyskaj spójność floty (parametr czasu 120 s) → P2 Przeprowadź rozproszone jednostki (koniec warunkiem) → P3 Opuść wyspę bez pomocy Eola (parametr czasu 60 s). Wskazane czasy etapów to parametry celu; dokładne użycie zegara opisuje przebieg i presja, nie automatyczne przejście po odczekaniu.

**Napięcie i kontrgra:** Zmiana kierunku co 75 s, zapowiedź 20 s. Wpływ wiatru/prądu respektuje limity C09, nie teleportuje statków ani nie niszczy ich losowo.

**Zwycięstwo — wszystkie warunki:** odysseus_extracted, two_other_ships_extracted. **Porażka — dowolny warunek:** odysseus_lost, rescue_quota_unreachable. Nazwy flag należy podłączyć do konkretnych predicate w implementacji, nie uznawać za gotowy kod.

**Decyzja taktyczna:** Najpierw zebrać najwolniejsze transporty albo osłonić drogę i prowadzić jednostki pojedynczo.

**Cel poboczny:** Ocalić wszystkie pięć jednostek.

**Zapis i test regresyjny:** Wektor pogody, pozostały czas do zmiany, skupiska, zaliczone bramki i wynik odmowy Eola. Seed nie może zmienić kierunku po wczytaniu.

**Nagroda i przejście:** M10; brak kary surowcowej w następnej misji za nieuratowane jednostki.

## M10. Port, który stał się pułapką

**Fabuła:** Lajstrygonowie niszczą okręty w ciasnym porcie. Odyseusz, pozostawiony poza zatoką, odcina cumę i uchodzi.

**Otwarcie:** Mglisty Archipelag, mapa około 1.5 × 1.5 km; cel 15–22 min. Aktywny bohater: Odyseusz. Baza SEA_ONLY. Flota: royal ×1, merchant ×2, ram ×3, crewDestroyer ×2. Lokalnie 0 D/0 S; zasoby mapy 0 D/0 S początkowo. Łączny limit okrętów przeciwnika: 0; osobne zagrożenia: Trzy stanowiska olbrzymów na klifach; Wąskie wyjście portowe.

**Przebieg:** Okręt Odyseusza startuje bezpiecznie poza gardłem, pozostałe statki w dwóch grupach portowych. Podstawowy cel jest uczciwie wykonalny przez odcięcie cum i odwrót. Gracz może zaryzykować czas i ruchem kolumnowym wyprowadzić część innych jednostek. Olbrzymów nie trzeba zabijać katapultami. Po 180 s zamyka się możliwość ratunku dalszych grup, ale cel główny nie zmienia się w ukryty wymóg ocalenia wszystkich.

**Fazy:** P1 Rozpoznaj zasadzkę (parametr czasu 30 s) → P2 Uwolnij zakleszczone grupy (parametr czasu 180 s) → P3 Wycofaj okręt Odyseusza (koniec warunkiem). Wskazane czasy etapów to parametry celu; dokładne użycie zegara opisuje przebieg i presja, nie automatyczne przejście po odczekaniu.

**Napięcie i kontrgra:** Pierwsza salwa dopiero po 30 s jawnej zapowiedzi; rzuty kolejnych stanowisk rozłożone co najmniej 10 s, każdy 6 s telegraph i maks. 140 HP. Liczba zagrożeń stała.

**Zwycięstwo — wszystkie warunki:** odysseus_extracted. **Porażka — dowolny warunek:** odysseus_lost. Nazwy flag należy podłączyć do konkretnych predicate w implementacji, nie uznawać za gotowy kod.

**Decyzja taktyczna:** Bezpieczny odwrót bohatera albo dodatkowe ryzyko dla ludzi w porcie.

**Cel poboczny:** Wyprowadzić co najmniej dwa inne okręty.

**Zapis i test regresyjny:** Kto odciął cumy, kolejka wyjścia, blokady korytarza i jawny zegar ratunku. Uszkodzony statek nie staje się niezniszczalnym korkiem bez możliwości wycofania.

**Nagroda i przejście:** M11 z jedną łodzią bohatera i ustalonymi posiłkami scenariusza.

## M11. Sześć głów, jeden przesmyk

**Fabuła:** Odyseusz omija wir Charybdy, lecz Scylla porywa sześciu ludzi. Kirke ostrzega, że walki ze Scyllą nie można wygrać.

**Otwarcie:** Morze Burz, mapa około 1.5 × 1.5 km; cel 16–23 min. Aktywny bohater: Odyseusz. Baza SEA_ONLY. Flota: royal ×1, cutter ×1, merchant ×1, ram ×1. Lokalnie 0 D/0 S; zasoby mapy 0 D/0 S początkowo. Łączny limit okrętów przeciwnika: 0; osobne zagrożenia: Scylla jako zagrożenie strefowe; Charybda jako wir.

**Przebieg:** Oba zagrożenia nie mają zwykłego paska HP do wyzerowania. Gracz śledzi fazę wiru i wybiera przejście, tolerując ewentualne straty załogi zamiast ryzykować kadłub całej grupy. Strefy bezpieczeństwa i promienie skrętu muszą być przetestowane, aby istniała droga do zwycięstwa. Jeden atak Scylli rozliczany raz na eventId, bez wielokrotnego pobrania ludzi z kontaktujących się colliderów.

**Fazy:** P1 Odczytaj ostrzeżenie Kirke (parametr czasu 45 s) → P2 Przejdź między zagrożeniami (koniec warunkiem) → P3 Zbierz flotę za cieśniną (parametr czasu 90 s). Wskazane czasy etapów to parametry celu; dokładne użycie zegara opisuje przebieg i presja, nie automatyczne przejście po odczekaniu.

**Napięcie i kontrgra:** Wir: 120 s cyklu, 70 s słabszego prądu, 20 s narastania, 30 s silnej fazy; nigdy bez telegraph. Scylla zapowiada strefę 6 s, pojedyncze pochwycenie maks. 6 członków załogi, cooldown 25 s.

**Zwycięstwo — wszystkie warunki:** odysseus_extracted, one_escort_extracted. **Porażka — dowolny warunek:** odysseus_lost, escort_quota_unreachable. Nazwy flag należy podłączyć do konkretnych predicate w implementacji, nie uznawać za gotowy kod.

**Decyzja taktyczna:** Bliżej Scylli — ryzyko załogi; dalej od klifu — większa siła wiru i ryzyko manewrowe.

**Cel poboczny:** Ocalić wszystkie jednostki.

**Zapis i test regresyjny:** Faza wiru, stan pochwycenia i indywidualne osoby utracone. Nie twórz jednocześnie porwanego marynarza i jego kopii na pokładzie.

**Nagroda i przejście:** M12; bez odblokowania zabijania bogów lub stałej odporności na wiry.

## M12. Przysięga pod słońcem

**Fabuła:** Flota zatrzymuje się na Trynakii. Święte stada Heliosa są objęte zakazem naruszenia, a załoga musi zgromadzić zapasy i odpłynąć w oknie pogody.

**Otwarcie:** Archipelag Egejski, mapa około 1.5 × 1.5 km; cel 18–26 min. Aktywny bohater: Odyseusz. Baza REPAIR_ONLY. Flota: royal ×1, cutter ×2, merchant ×1. Lokalnie 90 D/30 S; zasoby mapy 0 D/0 S początkowo; ładunek misji: 800 jednostek zapasów w dwóch składach, wymagane 240. Łączny limit okrętów przeciwnika: 0; osobne zagrożenia: Sztorm na podejściach do dwóch składów; Święte stada jako obiekt przysięgi.

**Przebieg:** Zapas 240 jednostek (ładunek misji, E12) trzeba rzeczywiście zebrać kutrami, dowieźć do osady i załadować na okręt wyprawowy; przy odpłynięciu jest jednokrotnie zużyty na rejs. Dwa składy i automatyczna rotacja pozwalają osiągnąć cel bez zużywania D/S, które służą tu tylko remontom. Wyspa jest bezludna, więc zapasów nie można kupić za srebro. Nie ma stałego głodu ani podatku od floty. Stada nie są legalnym automatycznym celem; ręczne naruszenie wymaga czytelnego ostrzeżenia w UI i kończy misję stanem porażki z objaśnieniem mitycznej konsekwencji. Nie wymuszaj kradzieży poprawnie grającej załodze.

**Fazy:** P1 Ustal bezpieczny zbiór (koniec warunkiem) → P2 Zgromadź zapas na rejs (parametr czasu 480 s) → P3 Opuść wyspę w oknie pogody (parametr czasu 120 s). Wskazane czasy etapów to parametry celu; dokładne użycie zegara opisuje przebieg i presja, nie automatyczne przejście po odczekaniu.

**Napięcie i kontrgra:** Pierwsze wyjście od 8:00 przez 120 s, następne dwa okna co 120 s po zamknięciu poprzedniego. Ostatnia możliwość jawnie oznaczona; pogodę i realną osiągalność sprawdzić w QA.

**Zwycięstwo — wszystkie warunki:** oath_kept, departure_supplies_committed, odysseus_extracted. **Porażka — dowolny warunek:** odysseus_lost, sacred_herd_harmed, last_departure_window_missed. Nazwy flag należy podłączyć do konkretnych predicate w implementacji, nie uznawać za gotowy kod.

**Decyzja taktyczna:** Wcześniejszy zbiór na narażonym składzie albo ostrożna praca i późniejsze okno wyjścia.

**Cel poboczny:** Wyjść w pierwszym oknie z całą flotą i nienaruszoną przysięgą.

**Zapis i test regresyjny:** Oath state, ID zużycia 240 jednostek, załadowany zapas, indeks i czas okna. Save/load nie zeruje skutków naruszenia ani nie zużywa zapasów ponownie.

**Nagroda i przejście:** Zakończenie cyklu Odyseusza i podsumowanie losów załogi.

## M13. Bóg, którego nie utrzymają liny

**Fabuła:** Piraci porywają Dionizosa. Objawienie boga zmienia ich w delfiny; rozsądny sternik zostaje ocalony.

**Otwarcie:** Archipelag Egejski, mapa około 3 × 3 km; cel 20–28 min. Aktywny bohater: brak — dowodzenie kapitanami. Baza REPAIR_AND_LIGHT_BUILD. Flota: royal ×1, boarder ×1, crewDestroyer ×2, cutter ×1. Lokalnie 210 D/120 S; zasoby mapy 540 D/660 S początkowo. Łączny limit okrętów przeciwnika: 8; osobne zagrożenia: Objawienie Dionizosa na okręcie celu.

**Przebieg:** Niszczyciele załóg przygotowują abordaż; target ma wyłączone automatyczne dobijanie kadłuba po rozpoznaniu, a ręczny atak nadal wymaga świadomej decyzji. Kontakt abordażowy i 20 s utrzymania bez zatopienia uruchamiają objawienie: liny przestają więzić boga, porywacze z okrętu celu stają się niebojowymi delfinami, sternik otrzymuje stan ocalałego. Dalsza eskorta ucieka przed dodatkowymi piratami. Przemiana nie wypłaca łupu za każdą jednostkę.

**Fazy:** P1 Rozpoznaj statek z pojmanym bogiem (koniec warunkiem) → P2 Przerwij ucieczkę porywaczy (koniec warunkiem) → P3 Osłoń ocalonego sternika (parametr czasu 180 s). Wskazane czasy etapów to parametry celu; dokładne użycie zegara opisuje przebieg i presja, nie automatyczne przejście po odczekaniu.

**Napięcie i kontrgra:** Pięć okrętów startowo wraz z celem, trzy odwodu po przechwyceniu. Statek celu rusza po 60 s ostrzeżenia; wypłynięcie za granicę trwa według nawigacji, nie ukrytego timera.

**Zwycięstwo — wszystkie warunki:** target_intercepted, epiphany_committed, helmsman_extracted. **Porażka — dowolny warunek:** helmsman_lost, target_sunk_before_epiphany, target_escaped_before_interception. Nazwy flag należy podłączyć do konkretnych predicate w implementacji, nie uznawać za gotowy kod.

**Decyzja taktyczna:** Szybszy abordaż osłabionego celu albo wcześniejsze odciągnięcie eskorty, by nie zatonąć razem z ratowanym statkiem.

**Cel poboczny:** Uwolnić sternika bez zatopienia żadnego statku z nierozpoznaną załogą.

**Zapis i test regresyjny:** Target ID, helmsman ID i epiphany_committed. Przemieniony pirat nie może ponownie zostać członkiem załogi i delfinem.

**Nagroda i przejście:** Odblokowanie M14; brak boga jako rekrutowalnego bohatera i brak nowej waluty kultu.

## M14. Andromeda: cień pod falą

**Fabuła:** Andromeda zostaje wystawiona morskiemu potworowi. Perseusz zabija bestię i uwalnia ją.

**Otwarcie:** Archipelag Egejski, mapa około 3 × 3 km; cel 28–36 min. Aktywny bohater: Perseusz. Baza REPAIR_AND_LIGHT_BUILD. Flota: royal ×1, shipDestroyer ×2, ram ×2, merchant ×2, cutter ×1. Lokalnie 300 D/150 S; zasoby mapy 720 D/1320 S początkowo. Łączny limit okrętów przeciwnika: 0; osobne zagrożenia: Ketos: jeden potwór morski; Fala przybrzeżna.

**Przebieg:** Dwa handlowce odbierają po 12 osób z 36 dostępnych cywilów. Potwora można odciągnąć na otwarty akwen zgodnie z jego jawnie obserwowanym celem. Gdy uwaga bestii jest daleko od skały, okręt Perseusza kończy interakcję uwolnienia 20 s i zabiera Andromedę. Ostrzał eskorty obniża HP do ostatnich 15%; finał wymaga interakcji Perseusza w oknie po zapowiedzianym ataku. Ten udział heroiczny zapobiega opowieści, w której bohater tylko obserwuje przypadkowe zatopienie celu. Zdarzenie finału po ewakuacji jest jedno­razowe.

**Fazy:** P1 Udrożnij drogę ewakuacji (parametr czasu 90 s) → P2 Odciągnij potwora i uwolnij Andromedę (koniec warunkiem) → P3 Dokończ starcie Perseusza (koniec warunkiem). Wskazane czasy etapów to parametry celu; dokładne użycie zegara opisuje przebieg i presja, nie automatyczne przejście po odczekaniu.

**Napięcie i kontrgra:** Bestia 4800 HP jako wartość robocza; atak stożkowy max. 160 HP co 20 s z 6 s zapowiedzią, bez natychmiastowego zabicia. Zatrzymuje atak nad zatoką osłony. Nie zjada Andromedy według ukrytego czasu.

**Zwycięstwo — wszystkie warunki:** andromeda_extracted, civilians_24_extracted, ketos_defeated, perseus_alive. **Porażka — dowolny warunek:** perseus_lost, andromeda_lost, civilian_quota_unreachable. Nazwy flag należy podłączyć do konkretnych predicate w implementacji, nie uznawać za gotowy kod.

**Decyzja taktyczna:** Połączyć ewakuację z walką w dwóch grupach albo najpierw zapewnić bezpieczny korytarz i dopiero przejść do finału.

**Cel poboczny:** Ewakuować wszystkich 36 cywilów i zachować oba handlowce.

**Zapis i test regresyjny:** Wszyscy pasażerowie, stan Andromedy, HP/faza bestii, okno finiszera i ketos_defeated. Przy równoczesnej śmierci Perseusza i potwora pierwszeństwo ma porażka.

**Nagroda i przejście:** Zakończenie antologii oraz podsumowanie decyzji i ocalonych; bez odblokowania przewagi w symetrycznych potyczkach.

## S07. Odbiór i kolejność produkcji

Najpierw wdroż wspólny runtime celów i prosty scenariusz eskortowy oparty na M04 w małej skali etapu produkcji M1. Pełna oprawa i wszystkie 14 misji nie są warunkiem prototypu. Następnie M02 (dynamiczna geometria), M03 (fizyczny przedmiot), M10 (ewakuacja), M13 (abordaż/aktor), M14 (boss), a pozostałe po walidacji wspólnych komponentów. Nie implementuj każdego celu osobnym monolitycznym skryptem bez powtórnego użycia logiki.

Wymagania minimalne: poprawne przejście celów; porażka przy utracie krytycznej jednostki; działające save/load w środku zagrożenia; brak dublowania nagród i wrogów; wykonalne trasy; odzyskiwalne przedmioty; zachowany budżet i reguły E; czytelne zagrożenia także bez audio i na każdym profilu grafiki. Testy obejmują pauzę, zmianę prędkości gry, pełny magazyn, zajęty port, zniszczenie wymaganego zbieracza i jednoczesny sukces/porażkę.

qa/SCENARIO_ACCEPTANCE.csv zawiera przypadki odbioru z NOT_RUN. qa/scenario-design.json jest wyłącznie danymi projektowymi. qa/scenario-design-check.cjs sprawdza kompletność metadanych, unikalność ID i spójność grafu etapów; nie uruchamia gry, nie potwierdza tras ani nie mierzy balansu. Raport strukturalny nigdy nie podnosi statusu gameplayTests z NOT_RUN. Dopiero rzeczywiste testy EditMode/PlayMode, build Windows i playtest 14/14 mogą dać odbiór kampanii. Hook obejmuje ten dokument i całe qa/ snapshotem, a nowe wymagania muszą mieć prawdziwe expectedTests dla danego zadania.
