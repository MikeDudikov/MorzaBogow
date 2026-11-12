# Morza Bogów — parametry do integracji i prób

Wszystkie liczby w tym pliku są **parametrami gry**, nie ustaleniami historii ani mitologii. Zestaw służy do uruchomienia spójnych prób. Nie deklaruje odbioru balansu. Normy zachowania: [plan gry](NOWY_PLAN_GRY.md) i [katalog](KATALOG_MECHANIK.md).

W czasie wdrożenia jedynym wykonawczym źródłem tych liczb staje się katalog runtime, eksportowany do raportu. Nie utrzymywać drugiej, ręcznie korygowanej tabeli wykonawczej. Zmiana parametru musi wskazać wynik, który uzasadnia zmianę.

## 1. Warunki dostępności i ceny floty

O = osada, P = przystań, S = stocznia, W = warsztat, K = kuźnia, M = ośrodek miejski, U = ośrodek wiedzy. M wymaga O+P+S+W+K. Ośrodek wiedzy wymaga M. Wymaganie dotyczy portu produkcji; badanie może działać dla całego stronnictwa zgodnie z definicją technologii.

| Rola | Budowle | Badania | Pełna cena D/S | Budowa s | Podstawa parametrów |
|---|---|---|---:|---:|---|
| N01 łódź | O+S | brak | 60/20 | 18 | obecny Cutter |
| N02 kupiec | M+S | brak | 140/60 | 28 | obecny Merchant, zdolności gospodarcze rozszerzane |
| N03 przełamujący | M+S | brak | 240/100 | 35 | obecny Ram |
| N04 osłonowy | O+S+W | brak | 180/90 | 30 | obecny CrewDestroyer |
| N05 daleki ostrzał | O+S+W | brak | 220/140 | 38 | obecny ShipKiller |
| N06 oblężniczy | M+S+W+U | ciężkie uzbrojenie i osłona II | 400/260 | 60 | obecny Bomber |
| N07 szturmowy | O+S+W+K | brak | 200/180 | 40 | obecny Boarder |
| N08 dowodzenia | O+S+W+K | organizacja dowodzenia | 300/200 | 50 | kadłub/obsada Boarder jako punkt do prób, inny plan modułów |
| N09 płomień | M+S+W+U | specjalizacja A1 | 260/180 | 45 | profil ruchu ShipKiller, moduł wbudowany |
| N10 biała mgła | M+S+W+U | specjalizacja A2 | 280/200 | 45 | profil ruchu ShipKiller, moduł wbudowany |
| N11 echo | M+S+W+U | specjalizacja B1 | 240/160 | 45 | profil ruchu CrewDestroyer |
| N12 żałobny | M+S+W+U | specjalizacja B2 | 280/220 | 50 | profil ruchu Boarder |

N01–N07 zachowują bazowe koszty i czasy katalogu rozpoznanego na commitcie 81ec638. Wymagania infrastruktury są kontraktem integracji, nie stwierdzeniem, że wszystkie te blokady już działają. Dane N08–N12 są robocze i wymagają walidacji wyceny części. Nie wolno odjąć od ceny większej wartości obsady lub wyposażenia niż koszt całego wariantu.

Bieżące wartości kadłuba, prędkości, promieni i obrotu N01–N07 pozostają w pierwszej próbie zgodne z runtime. Nie reklamujemy ich jako historycznych osiągów. Nowe warianty dziedziczą jawnie wskazany profil ruchu i otrzymują własny zestaw kosztów oraz modułów.

## 2. Zaplecze i badania

| Funkcja | Koszt D/S | Czas s | Wymaganie |
|---|---:|---:|---|
| Warsztat | 150/100 | 40 | stocznia |
| Kuźnia | 160/140 | 45 | osada i stocznia |
| Awans miejski | 300/400 | 60 | P+S+W+K |
| Ośrodek wiedzy | 200/500 | 60 | miasto |
| Dok | 200/300 | 45 | miasto i stocznia |
| Ciężkie uzbrojenie | 150/300 | 90 | ośrodek wiedzy i warsztat |
| Sanktuarium Morza | 180/300 | 60 | miasto |
| Sanktuarium Wojny | 180/300 | 60 | miasto |
| Nauka jednej mocy | 50/100 | 60 | odpowiednie sanktuarium |
| Organizacja dowodzenia | 80/120 | 45 | warsztat i kuźnia |
| Jedna specjalizacja stronnictwa | 100/180 | 60 | ośrodek wiedzy i warsztat |

Stocznia i przystań istniejące na starcie nie są opłacane ponownie. Koszt zakładania nowego portu pozostaje oparty na rzeczywiście zamawianej infrastrukturze z runtime; nie jest dopłatą pobieraną za zmianę nazwy obiektu. Wyprawa finansuje swój pełny manifest.

Kuźnia: osłona I to stan podstawowy, II dodaje 10 punktów procentowych ochrony pociskowej, III 20 względem podstawy. Limit `Rules.MaxHullReduction=0,7` dotyczy wyłącznie materialnej osłony kadłuba. Nie ogranicza ochrony magicznej M11/M12 ani niewrażliwości. Przy kolejności mnożenia z planu 100 obrażeń, M11 i brak pancerza daje 20; pancerz 15% oraz M11 daje `100×0,85×0,20=17`. Taran I to ×1, II ×1,15, III ×1,30 względem mnożnika kadłuba. Badanie II: 60D/100S i 45 s; III: 90D/160S i 60 s, wymaga U. Montaż elementu na statku opłaca jego własny przedmiot; badanie nie tworzy darmowego wyposażenia.

Dziewięć osi warsztatu ma po dwa stopnie: siła +10/+20%, zasięg +5/+10%, cykl ×0,95/×0,90. Każda rodzina liczona jest osobno. Badanie stopnia 1: 50D/80S, 40 s; stopnia 2: 80D/120S, 60 s, wymaga U. Te wartości nie są kumulowane ponownie z pełną wartością stopnia 1: wynik stopnia 2 jest całkowitą wartością końcową danej osi.

Napęd: dwa usprawnienia po +5% prędkości bazowej, wynik maksymalnie +10%; koszt odpowiednio 60D/100S i 90D/150S, czas 45/60 s, wymaga U. Niezależnie ograniczają go uszkodzenia, obsada, płycizny i efekty.

## 3. Forty — pierwszy zestaw prób

| Typ | Sama konstrukcja D/S | Budowa s | Kadłub | Pula osady |
|---|---:|---:|---:|---:|
| F01 | 180/100 | 45 | 1200 | 1 |
| F02 | 300/180 | 60 | 2200 | 2 |
| F03 | 460/280 | 90 | 3600 | 3 |

Cena wyposażonego obiektu = konstrukcja + rzeczywiście zamontowane moduły + zrekrutowana obsada. Skład zestawu jest zapisywany w Blueprint, nie ukryty w liczbie „koszt fortu”. Pierwsze układy slotów: F01 2 lekkie + 2 średnie; F02 2 lekkie + 3 średnie + 1 ciężkie; F03 2 lekkie + 3 średnie + 3 ciężkie. F02 wymaga warsztatu i kuźni, F03 także U oraz ciężkiego uzbrojenia.

Ten graf wymagań dotyczy dwóch głównych stronnictw. Niezależna flotylla ma odrębny, skończony manifest pięciu konfiguracji statków i trzech fortów oraz EncounterBuildPolicy. Nie musi budować nieobecnej w jej katalogu kuźni ani U. Polityka jawnie określa dostępne konfiguracje, limity, rzeczywistą płatność z zasobów spotkania i warunki odbudowy; nie daje darmowej, nieograniczonej produkcji.

Zasięg fortu jest zasięgiem jego modułów. Nie dodajemy darmowej broni do samego promienia budowli. Fort wyprawowy zaczyna bez modułów i ludzi, jeśli nie przywieziono opłaconego zestawu. Zewnętrzny wygląd oraz układ istniejących assetów nie wyznaczają liczby logicznych stanowisk.

## 4. Moce — liczby do pierwszego działającego wycinka

Zasięg to odległość od sanktuarium do środka legalnego celu. Promień działania jest osobną wartością. Wszystkie czasy liczone w tickach 30 Hz. „Mecz” oznacza trwałość do końca operacji lub do legalnego usunięcia źródła/efektu.

| Moc | Użycie D/S | Zasięg m | Promień m | Czas s | Odnowienie s |
|---|---:|---:|---:|---:|---:|
| M01 wir | 180/300 | 300 | 35 | 60 | 120 |
| M02 rafa | 180/300 | 300 | 25 | mecz | 120 |
| M03 wiatr/cisza | 20/40 | mapa | mapa | 45 | 60 |
| M04 trąba | 80/140 | 375 | 25 | 30 | 90 |
| M05 rozpoznanie | 0/30 | mapa | 150 | 30 | 45 |
| M06 stworzenie | 180/260 | 300 | punkt przywołania | 90 | 120 |
| M07 wzmocnienie | 220/350 | mapa | własne stronnictwo | 30 | 120 |
| M08 grom | 300/800 | 300 | 60 | uderzenie | 180 |
| M09 ognista strefa | 100/180 | 300 | 60 | 20 | 90 |
| M10 niewidzialność | 30/80 | 375 | jeden obiekt | 45 | 90 |
| M11 ochrona broni | 30/80 | 375 | jeden obiekt | 45 | 60 |
| M12 ochrona magii | 30/80 | 375 | jeden obiekt | 45 | 60 |

Zapowiedź mocy obszarowej 3 s, pojedynczej ochrony 0,5 s. Wir: wciąganie do centrum i 3% maksymalnego kadłuba/s po wejściu w rdzeń 10 m. Trąba: 2% maksymalnego kadłuba/s i zakłócenie kursu. Rafa: blokada dla za głębokiego kadłuba oraz uderzenie 15% maksymalnego kadłuba przy zdarzeniu wejścia, nie w każdym ticku; kolejny kontakt tego samego kadłuba najwcześniej po 5 s. Wariant zanurzany: cykl 5 s odsłonięcia + 5 s zanurzenia, z logiczną głębokością i zapowiedzią.

M06 przywołuje jeden określony typ z katalogu, bez zwykłej nagrody za zabicie. M07 daje ×1,5 do siły odpowiedniej broni w swoim kanale wzmocnienia. M08 niszczy legalne, niechronione cele po zapowiedzi; transformację terenu ogranicza obszar definicji i walidator celu. M09 daje 35 punktów kadłuba/s, 5 punktów obrażeń ludzi/s i stan ognia. M10 kończy się po czasie, zniszczeniu celu albo wyraźnym rozproszeniu; sam ostrzał nie musi jej kończyć. M05 daje wykrycie także obiektu objętego M10 w ujawnionym obszarze.

Limity robocze: maksymalnie 3 własne trwałe rafy i 2 aktywne wiry; próba przekroczenia odrzucana przed płatnością. Każdy efekt ma ID i ownera. M11/M12 oraz odporności stosują macierz katalogu. Najsilniejsza ochrona tej samej rodziny zastępuje słabszą, nie sumuje się z nią.

## 5. Bohaterowie, doświadczenie i przedmioty

Bohater zajmuje jedno odrębne miejsce; pojemność robocza wynosi **1 bohater na okręt wojenny**, niezależnie od 1/2 miejsc artefaktów. Ewentualne zwiększenie pojemności wymaga osobnego testu kombinacji, nie wynika z liczby miejsc przedmiotów.

- H01: pełna odporność na M01/M02 i odpowiadające im zagrożenia mapy.
- H02: siła ×1,2, zasięg ×1,1.
- H03: odrzucenie inicjowania ataku przez typy z listy CreatureNonAggression; nie chroni automatycznie przed obcym ogniem obszarowym.
- H04: ignorowanie kary wiatru do ruchu i pełna ochrona przed M04.
- H05: brak utraty kadłuba, ludzi i modułów od obrażeń; nie blokuje celów terenowych, wabienia ani utraty przez jawny warunek misji.
- H06: odnowa 2% maksymalnego kadłuba/s i 50% redukcji obrażeń stworzeń.
- H07: promień rozrzutu ×0,75, cykl ×0,8.
- H08: brak wabienia/szkody syreny i brak obrażeń harpii.
- H09: brak unieruchomienia i szkody spojrzenia gorgony.

Doświadczenie ma trzy stopnie: podstawowy, zaprawiony, weteran. Wpływ na skuteczność abordażu: ×1/×1,05/×1,10; promień rozrzutu ×1/×0,95/×0,90. Przyrost wynika z ukończonych celów i faktycznej służby przeciw wrogowi. Progi XP są częścią danych scenariusza; wymagane startowe wartości 0/100/250. Nie przyznawać XP za odnowę i celowe uszkadzanie własnych jednostek.

T01 przywraca 3% maksymalnego kadłuba/s, 4 punkty stanu modułu/s i jednego brakującego wojownika co 3 s do zapisanej liczebności preset. T10 jednorazowo odtwarza 25% stanu obsady bojowej do limitu i zużywa ładunek przedmiotu. T02/T03/T04 mają dokładny manifest, a T11 dokładne ID technologii. Pozostałe premie i odporności wynikają z rodzaju efektu, bez ukrytej kumulacji.

## 6. Pozyskanie i odzysk

Punkt początkowy prób: maksymalnie 2 odnowienia partii po partii startowej; opóźnienie 60 s bez pracy. Wartości produktu bierze się z profilu mapy. Odtworzenie zużywa jej budżet zewnętrzny. Praca nie zwiększa liczby dostępnych odnowień.

Neutralny cel ekonomiczny wymaga 60 s sumarycznej pracy własnej jednostki gospodarczej w zasięgu, z postępem zapisanym na ID celu. Rozlicza tylko jeden zwycięzca. Robocza nagroda wynosi 960D/800S i jest widoczna w opisie spotkania; wymaga próby wpływu na tempo partii.

Odzysk konstrukcji po zatopieniu: `HullWreckSilverCenti = floor(PaidConstructionSilverCenti × 5 / 100)`. Oba pola są nieujemnymi całkowitymi liczbami setnych S. Wersja bez przepełnienia mnożenia: `(P / 100) × 5 + ((P % 100) × 5) / 100`, gdzie P=PaidConstructionSilverCenti, a dzielenie jest całkowite. Przykłady: 10000 setnych (100 S) daje 500 setnych (5 S); 10001 setnych daje również 500 setnych. To 5% faktycznie opłaconego srebra za samą konstrukcję; drewno nie jest przeliczane. Podstawa nie obejmuje ludzi, modułów ani późniejszych napraw; dla obiektu przydzielonego bez opłaty wynosi zero. Wypłata następuje raz przy rzeczywistym pozyskaniu wraku i nie powiela machin odzyskanych w naturze. Ocalałe cargo i machiny zachowują aktualne reguły do czasu kontrolowanego eksperymentu.

Złomowanie w porcie ma osobną ścieżkę. Zachowuje współczynnik 15% i dotychczasowy model do wprowadzenia darmowej odnowy; P08 musi przed włączeniem odnowy oddzielić kwalifikowany stan odzysku od bieżących HP. Bezpłatne odtworzenie kadłuba/modułu nie zwiększa wypłaty ani opłaconej podstawy. Aktualny iloczyn opłaconej konstrukcji i bieżącego procentu HP sam nie spełnia tego kontraktu. Obowiązkowe próby P08: uszkodzenie→złom, uszkodzenie→bezpłatna odnowa→złom, odnowa→zapis/odczyt→utrata. Nie zmieniać teraz działającej ekonomii wyłącznie na podstawie korekty jednostek.

Wszystkie powyższe liczby podlegają pomiarom. Nie wolno zmieniać ich równocześnie bez oznaczenia eksperymentu, a następnie przypisywać wyniku jednej wybranej zmianie.

## 7. Dowodzenie, trudność i zapis — parametry do prób

Zachowanie określa [GRYWALNOSC_I_ODBIOR.md](GRYWALNOSC_I_ODBIOR.md). Poniższe wartości są hipotezami do odbioru, nie obserwacjami z gotowego buildu.

| Parametr | Wartość początkowa | Znaczenie |
|---|---:|---|
| Okno kontroli postępu ruchu | 60 ticków = 2 s | Tylko aktywne przemieszczanie; postęp po drodze, bez karania legalnego objazdu i oczekiwania. |
| Dodatkowe próby odzyskania drogi | 3 | Po limicie konkretny stan i powód; nie jest to gwarancja dotarcia w 6 s. |
| Limit kolejki zadań jednostki | 16 | Odrzucenie dopisania przed jakąkolwiek płatnością, bez usunięcia przyjętych zadań. |
| Agregacja powtarzanej informacji | 150 ticków = 5 s | Nowe krytyczne zdarzenie pozostaje natychmiastowe; nie wyciszać innego zagrożenia tym samym globalnym limitem. |
| Reakcja bojowa AI: Nauka/Standard/Weteran | 90/30/15 ticków = 3/1/0,5 s | Minimalny odstęp decyzji planera, nie częstość ruchu i strzałów symulacji. Pozostałe ceny/statystyki identyczne. |
| Rotacja autosave | 3 generacje | Osobne od zapisu ręcznego i quicksave; każda generacja jest kompletnym zestawem. |
| Okres autosave | 9000 ticków = 300 s | Czas symulacji; w pauzie licznik stoi. Checkpointy mogą zapisać się dodatkowo po określonym zdarzeniu. |

Progu postępu przestrzennego i budżetu czasu przejścia nie ustalamy jako jednej stałej dla wszystkich kadłubów. Próba zapisuje długość legalnej drogi, profil ruchu i oczekiwany zakres postępu; zerowy napęd ma osobny wynik. Licznik postępu i ponowień jest częścią stanu, nie lokalnym zegarem renderowania.

Po nauce mocy koszty użycia są ponoszone przy jej rzeczywistym przyjętym wykonaniu. Planowanie w pauzie nie rezerwuje środków na dowolnie odległą przyszłość. Powód niewykonania musi wskazać brak środków bez zgubienia innych zaplanowanych, legalnych zadań.
