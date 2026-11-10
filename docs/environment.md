# Środowisko projektu

Stan z 2026-11-09. Dokument zawiera wyłącznie wartości odczytane z maszyny.

## Sprzęt deweloperski

| Składnik | Wartość |
|---|---|
| System | Windows 11 Home, build 26200 |
| CPU | Intel Core i7-11390H, 3,40 GHz |
| RAM | 15,8 GB |
| GPU | NVIDIA GeForce RTX 3050 Ti Laptop, 4096 MiB VRAM, sterownik 621.01 |
| Drugi GPU | Intel Iris Xe Graphics |
| Bieżący tryb obrazu | 1920×1080 |

To sprzęt deweloperski, nie wymaganie gry. Gra nie ma wymagań sprzętowych (V10). Sprawdzenie fizycznego wyświetlania 2560×1440 wymaga zewnętrznego monitora (V13).

## Narzędzia

| Narzędzie | Wersja | Stan |
|---|---|---|
| PowerShell | 7.6.9 | zainstalowany |
| Node.js | 26.13.0 | zainstalowany |
| Git | 2.56.0.windows.4 | zainstalowany |
| Unity Hub | 3.22.5, instalacja ze Sklepu Microsoft | uruchomiony |
| Unity Editor projektu | 6000.3.28f1 (8f082d2fe476) | zainstalowany; Windows Build Support tylko Mono, bez IL2CPP |
| Unity 6000.6.7f1 | 6000.6.7f1 | zainstalowany, nieużywany w tym projekcie |
| HDRP, Shader Graph, VFX Graph | 17.3.0 | zablokowane w `Packages/packages-lock.json` |
| Unity Test Framework | 1.6.0 | zablokowany |
| Input System | 1.20.1 | zablokowany |

Grafika: Direct3D 12, feature level 12.2, Linear. Backend: Mono.

## Kontrole wykonane

| Polecenie | Wynik | Zakres |
|---|---|---|
| `node qa/scenario-design-check.cjs` | STRUCTURE_VALID, 14 misji, 613 kontroli | tylko struktura danych |
| `pwsh -NoProfile -File qa/tests/test-gate.ps1` | 27/27 PASS | tylko mechanizm bramki, syntetyczny runner |

| Unity EditMode, batch | 21/21 PASS, 0 skipped | logika rozdzielczości i profili |
| Unity PlayMode, batch | 8/8 PASS, 0 skipped | konfiguracja profili i przełączanie w Editorze |
| Build Windows64 Mono, batch | Success, 144985365 B, 0 błędów, 0 ostrzeżeń, 12,6 min | scena M0_Probe |

## Sonda w buildzie (informacyjna)

Scena M0_Probe: woda, niebo, kula testowa, bez statków. Okno 1920×1080, 180 klatek rozgrzewki, 600 klatek pomiaru. Czas klatki jest ograniczony synchronizacją do około 16,7 ms, więc P50 nie porównuje kosztu profili. Spadki P95 i P99 mogą pochodzić z rozgrzewania shaderów; ich przyczyny nie ustalono. Są to czasy całej klatki, nie GPU.

| Profil | RT aktywny | SSR | SSGI | Wolumetria | SSS | P50 ms | P95 ms | P99 ms |
|---|---|---|---|---|---|---:|---:|---:|
| Low | nie | nie | nie | nie | nie | 11,815 | 24,720 | 202,896 |
| Medium | nie | nie | nie | tak | nie | 15,177 | 87,369 | 182,785 |
| High | nie | tak | nie | tak | tak | 16,679 | 83,338 | 216,660 |
| Extra High | nie | tak | tak | tak | tak | 16,667 | 33,351 | 250,007 |
| Ultra | tak | tak | tak | tak | tak | 16,677 | 100,001 | 166,668 |

GPU zgłasza DXR. Tryby monitora obejmują w zakresie V13 wyłącznie 1920×1080.

## Etap M1

| Kontrola | Wynik | Zakres |
|---|---|---|
| Bramka uruchomiona ręcznie zdarzeniem TaskCompleted | PASS, EditMode 125/125, PlayMode 46/46 | gospodarka D/S, logika i integracja, raport w `qa/reports` |
| Testy bramki | 45/45 PASS | sam mechanizm, syntetyczny runner |
| Build Windows64 Mono, scena M1_Battle | Success, 261900216 B, 0 błędów, 0 ostrzeżeń | |
| Build, 300 s rozgrywki SI kontra SI | 2 przyczółki, 1 stocznia na przyczółku, 2 handlowce, 993 zdarzenia walki, mecz nierozstrzygnięty | jeden przebieg; nie dowód balansu ekspansji |
| Mecz SI kontra SI, seed 9, 18000 kroków, test | Bilans D i S stały w każdym kroku, magazyny nieujemne, 2 przyczółki, oba ze stocznią | jeden seed |
| Mecz SI kontra SI, seed 9, 6000 kroków, test | Żaden statek nie wszedł na ląd, punkty pracy nadal działają | sprawdzane co krok dla kół kolizji, jeden seed |
| Build, 130 s rozgrywki SI z próbkowaniem wody | 118645 odczytów, 8 błędów, przechył do 11,96°, wznoszenie do 0,98 m | odczyty liczbowe, ruch na ekranie nie był oceniany |
| Kolor wody w buildzie, profil Ultra, widok domyślny | RGB 42 71 84, 0% przepaleń; zaakceptowana wersja perspektywiczna 34 68 75 | średnia z wycinka zrzutu, inny kadr niż we wzorcu |
| Nachylenie 80°, obrót −40°, profil Ultra | 0% przepaleń | jeden zrzut, pozostałe kąty policzone, nie zmierzone |
| Pojedynczy kuter, trasa 70 m | 108 D/min | model: do 124 D/min przy pełnym wykorzystaniu trasy |
| Mecz SI kontra SI, seed 9 | Rozstrzygnięty po 20 min, pierwsza walka po 148 s | jeden przebieg, nie dowód balansu |
| docs/economy-model/economy-model.py | Wykonany ponownie, CSV zgodne z pakietem | model nie obejmuje walki ani Unity |

Coast Rocks 02: 4 siatki, 2363290 trójkątów LOD0, Mesh LOD do 64 trójkątów na siatkę, FBX zaimportowany bez zmian, jeden materiał HDRP Lit. Na mapie wyspa ma około 102 × 7 × 110 m. Modele okrętów, osad i węzłów to oznaczone modele zastępcze.

Kamera: rzut ortogonalny patrzący na mapę pod kątem, domyślnie nachylenie 45° i obrót 30°. Zoom kółkiem, obrót Q/E, nachylenie 25–80° PageUp/PageDown, obrót i nachylenie także środkowym przyciskiem myszy, Home przywraca widok. Sterowanie klawiszami i myszą nie było uruchamiane; zrzuty ustawiają kąt z kodu.

Obraz: stała ekspozycja EV 12,25, mapowanie tonów Neutral, nasycenie +35, słońce 100000 lx. Słońce obraca się z widokiem (przesunięcie 120°) i obniża się z 60° do 45° przy nachyleniu od 45° do 80°, bo inaczej obrót kamery zamienia morze w białe odbicie. Cienie obracają się więc razem z kamerą, co trzeba będzie rozwiązać przy cyklu dobowym z C09. Na Ultra oświetlenie pośrednie używa SSGI zamiast RTGI, które pod kamerą ortogonalną dawało plamki; odbicia nadal używają ray tracingu. Kadłuby siedzą w wodzie na głębokości zależnej od wysokości kadłuba i zapełnienia ładowni; załadowany transport osiada stopniowo. Kołysanie to sprężyna z tłumieniem dla wznoszenia, przechyłu wzdłużnego i bocznego: kadłub podąża za wodą z opóźnieniem, przechodzi lekko za grzbiet fali i się uspokaja, a okres rośnie z długością i szerokością kadłuba. Woda jest odczytywana z deformacją, więc statek unosi się też na fali dziobowej własnej i cudzej. Ruch kadłuba liczy się w czasie rzeczywistym, jak woda, także podczas pauzy. Na profilach High, Extra High i Ultra płynący kadłub wypycha falę dziobową (deformacja wody HDRP) i spienia wodę; gdy dziób wcina się w grzbiet fali albo opada na wodę, przy dziobie rośnie piana i lecą bryzgi. Bryzgi to cząsteczki na wszystkich profilach. Piana zanika szybko (trwałość 0,3), więc nie zostawia długiego pasa. Low i Medium nie mają dekali wody. Każdy ląd ma obrys linii wody: wybrzeża skalne odczytany z modelu przy budowie sceny, wysepki surowców elipsa 19 × 14 m, osady i wolne brzegi koło 20 m. Wzdłuż obrysu biegnie zastępcze dno, od 0,5 m pod brzegiem do 13 m w odległości 44 m, z lekko falującą dalszą krawędzią, oraz przybój z nakładających się płatów piany, którego fale wędrują wzdłuż brzegu (absorpcja 8 m, kaustyki 0,8). Kolizje nadal używają koła lądu, a nie obrysu. Ukryty statek nie tworzy fal ani bryzgów, a dno i przybój pojawiają się tylko przy odkrytym lądzie. Ocenione na zrzutach z profilu Ultra; ruchu w czasie nie oceniał człowiek. Mgła atmosferyczna jest bardzo rzadka, a wygaszanie fal z odległością wyłączone, bo odległość kamery ortogonalnej nie ma znaczenia dla obrazu. Fale są wyłącznie prezentacją: kadłub odczytuje wysokość prawdziwej powierzchni wody, a pozycja logiczna, zasięgi i kolizje pozostają bez zmian.

Paski HP pojawiają się tylko po zaznaczeniu myszą, także dla wielu okrętów i widocznych wrogów. Znaczniki to cienkie, półprzezroczyste okręgi z lekko zmiękczoną krawędzią: zaznaczenie i cel ruchu mają krycie 42%, atakowany wróg czerwony okrąg o kryciu 70%. Mgła wojny jest widoczna w trzech stanach, a odkrycie należy do zapisu gry. Animacje, efekty oraz modele składu drewna i kopalni srebra to prezentacja zbudowana z prostych brył, do zastąpienia docelową grafiką.

Wdrożone z gospodarki E00–E11: drewno i srebro, skończone punkty z dwoma stanowiskami i kolejką FIFO, magazyn z rezerwacjami, port na trzy statki, osobna linia wojenna i kolejka portu, zużycie kosztu wraz z postępem, rozdział ceny szablonu, remont, złomowanie, wrak z połową ładunku, księga świata i zapis w wersji 6. Zapisy z wcześniejszych buildów nie wczytują się.

Budżety kampanii M01–M14 i ładunek misji (E12) są tylko w dokumentach i danych. Sceny misji nie istnieją.

Uproszczenia względem specyfikacji, do wdrożenia w kolejnym etapie:

- Wdrożono z drugiego etapu E00: handlowca z ładownią na oba zasoby, osiem wolnych brzegów, wyprawę na przyczółek (300 D/100 S, 35 s na miejscu), lokalne magazyny i kolejki portu, stocznię na przyczółku (200 D/100 S, 40 s), dostawy na rozkaz między portami, eskortę i rozbudowę SI. SI dowozi na przyczółek tylko brakujący koszt stoczni, a po jej budowie nie uzupełnia go dalej. Dostawa to jeden kurs, a eskorta strzela tylko łucznikami.
- Nie ma automatycznych konwojów z E03: obszaru pracy, portu priorytetowego, poziomu ryzyka, trybów stoczni, grup do trzech handlowców i minimalnego zapasu źródła. Dostawa automatyczna zostawia jedynie rezerwę 60 D/20 S. Nie ma anulowania budowy stoczni ani kolejnej stoczni w osadzie głównej.
- Nie ma pozostałych inwestycji z E05, ostrzeżeń o nasyceniu ani rezerwy gracza; rezerwy 60 D/20 S przy zamówieniach wojskowych przestrzega tylko SI. Stracony przyczółek nie zostawia łupu, a jego niewydane zapasy są niedostępne.
- Ląd w logice to wielokąt linii wody (64 promienie; wybrzeże zmierzone z modelu, wysepki jako elipsy, obrót o pół obrotu po stronie x>0). Statek trzyma od niego odstęp: połowa długości plus szerokość płycizny, której jego zanurzenie nie przejdzie (`Seabed`, zanurzenie rośnie o 65% przy pełnej ładowni). Trasy: graf widoczności wokół wielokątów, osobny dla każdego odstępu (co 0,5 m), szukanie A* z kopcem i leniwym sprawdzeniem odcinków od statku. Mecz 30 min SI liczy się w ok. 6 s w .NET (przedtem 76 s). Tylko wybrzeże blokuje strzały. Cel ruchu na lądzie przesuwa się na najbliższą wodę. Zapis zawiera kształt i obrót lądu (od v7). Nie ma grafu akwenów ani rezerwacji przejść. Szyk grupy (linia, klin) działa od 2026-11-10, opis w docs/INTERFEJS.md. Odległości mapy to linie proste, nie zmierzone trasy.
- Kadłuby to kapsuły o długości i szerokości z definicji. Trzymają 2 m wody między burtami (poza parą sczepioną abordażem), by w ukośnym rzucie kamery bliższy kadłub nie zasłaniał dalszego; nachodzenie rozpychają ważone masą, a statek z kadłubem na kursie skręca, by minąć go burtą. Eskorta płynie przy burcie transportu, nie przed jego dziobem.
- Wiosła: zasięg z reguł (`Rules.OarReach`); mijające się statki zostawiają na nie miejsce, a w prezentacji wiosła składają się wzdłuż burty po stronie, gdzie inny kadłub leży w ich zasięgu, i obie strony przy postoju.
- Stocznia: dwie pochylnie przy osadzie (linia wojenna i portowa, po bokach pomostu). Kadłub na pochylni pokazuje etap według postępu zamówienia: stępka, wręgi, poszycie, pokład, maszt (i taran), wiosła i żagiel; cieśle pracują do zdjęcia rusztowań. Gotowy statek zjeżdża po rampie do morza (rozbryzg) i dopływa do miejsca z symulacji, które leży u wylotu pochylni, dziobem w morze, w pierwszym wolnym miejscu bez nachodzenia.
- Kuter na łowisku: 3 s schodzenia załogi na ląd (bez wydobycia) i 4 s zaokrętowania przed odpłynięciem (stanowisko zajęte). Dochód testowy 108 D/min (model 124 D/min bez manewrów). Prezentacja odwołuje załogę na pokład z wyprzedzeniem według przewidywanego końca pracy. Zapis v8.
- Wygląd egejski: woda ma granatowy kolor rozproszenia i turkusową refrakcję (przejrzystość 12 m), dno mielizn, wapienny brzeg i zbocza mają tekstury CC0 z Poly Haven w mapowaniu trójpłaszczyznowym, słońce 5600 K, nasycenie +12. Głazy (CC0, uproszczone do ok. 2 tys. trójkątów na część) są rozsypane deterministycznie wzdłuż każdej linii wody i na płyciznach, łączone w kilka siatek na brzeg. Źródła: Assets/Art/PolyHaven/ZRODLA_CC0.txt.
- Krajobraz antycznej Grecji (decyzja użytkownika z 2026-11-10, docs/BALANS_I_SKALA.md „Skala lądu i drzew”):
  - **Sucha trawa.** Trawa na wysepkach to sucha letnia słoma: `ShoreArt.Parched` przy budowie sceny (`BattleSceneBuilder.Run`) robi z tekstury CC0 `aerial_grass_rock` kopię `Assets/Art/Shore/aerial_grass_rock_parched_diff.jpg`. Zieleń przechodzi w złoto, skała zostaje bez zmian, a źródło pozostaje nietknięte.
  - **Oliwki.** Na wysepkach portów, wolnych brzegów i kopalni rosną niskie oliwki (`TreeKind.Olive`, liście `Placeholder_OliveLeaves`).
  - **Skałki.** Małe skały mają wapienny materiał brzegu, bez drzew.
  - **Weryfikacja.** Zrzuty z buildu 2026-11-10: mapa, port, las z kutrami, kopalnia, okręt za środkową skałą (`-morzaShowcase`). Ocena wyglądu jest wizualna. Wydajność laptopa docelowego nie była mierzona.
- Wysepki osad, wolnych brzegów i złóż mają nieregularne, seedowane linie brzegu (`LandShape.Irregular`) zamiast kół i elips; po stronie pomostu i pochylni brzeg zostaje w stałej odległości. Logika (kolizje, płycizny, trasy) używa tego samego wielokąta.
- Klify i drzewa (tylko prezentacja): wysepki mają wapienne klify do ok. 9 m po stronie otwartej, z ostrą krawędzią i niemal pionową ścianą schodzącą do -4,5 m pod wodą, niski brzeg przy pomoście i pochylniach (`ShoreShape.Cliffs`); budynki, drzewa i sztolnia stoją na terenie. Pinie i cyprysy są proceduralne (`TreeModels`), łączone w jedną siatkę na gaj; na dużych wybrzeżach rosną na 157 płaskich miejscach wyliczonych w edytorze z siatki skał. Klify nie zmieniają logiki: kolizje i płycizny dalej liczy linia wody.
- Średni czas klatki w teście dymnym (RTX 3050 Ti, Ultra, 1920×1080): 39,5–54 ms z dużym rozrzutem między przebiegami; głazy dokładają kilka do kilkunastu ms. `-morzaNoBoulders` wyłącza je do pomiarów. Optymalizacja wydajności sceny: NOT_RUN.
- Interfejs bitwy: UI Toolkit (BattleHud) według pakietu „Morza_Bogow_Interfejs” – opis w docs/INTERFEJS.md, odbiór w docs/QA_INTERFEJS.md. Fonty Cinzel SemiBold i Source Sans 3 (OFL, licencje w UI/Resources/Hud/Fonts). Kamera na strzałkach i przecinku/kropce, pauza pod Pause, spacja do alarmu (skróty zmienialne).
- Diagnoza wydajności (test dymny, Ultra 1080p): CPU główny ok. 7–10 ms, GPU 35–51 ms także na profilu Low – ograniczeniem jest karta graficzna, nie symulacja (0,9 ms) ani interfejs (0,1–0,2 ms). Optymalizacja GPU: NOT_RUN (wstrzymana).
- Załogi (tylko prezentacja, czas skrócony): drwale i górnicy schodzą z kutra trapem na pomost, ścinają drzewa (pochylenie, upadek, odrost po 25 s) lub kują w sztolni, tragarze noszą urobek; w porcie dokerzy wynoszą towar do składu. Rozładunek w porcie trwa według reguł ekonomii (60 jedn./s), więc animacja dokerów jest krótka.
- Jedyna broń to łucznicy z rzutem seedowanym, bez lotu pocisku, sektorów i wyboru wyposażenia. Wyposażenie z szablonu nie jest osobnym przedmiotem, więc 30% szansy jego przetrwania we wraku nie jest wdrożone.
- Osada łączy port i stocznię w jednym obiekcie, więc zniszczenia samego portu nie ma; łup 30% ze zniszczonej osady nie jest wdrożony, bo jej zniszczenie kończy mecz.
- SI jest prosta i zna pozycję startową wroga oraz położenie punktów pracy. Balans: NOT_RUN.

## Efekty, fale i pogoda (2026-11-10)

Partia B planu „Brakujące elementy z polecenia Senior Unity HDRP RTS”. Całość to prezentacja: symulacja, zapis gry i powtórka działają bez zmian.

- **Pula efektów** (`EffectPool`).
  - Ogólna pula obiektów prezentacji z limitem.
  - Wypożyczenie włącza obiekt i uruchamia od początku jego systemy cząsteczek oraz efekty VFX Graph (`Reinit` i `pause = false`, jak przycisk Restart w edytorze VFX Graph).
  - Zwrot zatrzymuje i czyści cząsteczki, reinicjuje i pauzuje efekty VFX, a obiekt wyłącza.
  - `Prewarm` tworzy obiekty z wyprzedzeniem, `Clear` niszczy wszystkie.
  - Cząsteczkami steruje od systemów na górze hierarchii obiektu, razem z dziećmi, więc sub-emitery odpalają tylko z cząsteczek rodzica.
  - `BattleEffects` ma po jednej puli na rodzaj efektu i nadal pokazuje najwyżej 160 efektów naraz. Obiekty efektów i tonące kadłuby giną teraz razem z bitwą.
- **Fale pod kadłubami** (`WaterBatchSampler`, `HdrpWaterHeights`).
  - W każdej klatce woda pod każdym widocznym okrętem (dziób, rufa, obie burty, środek) jest odczytywana jednym zadaniem Burst HDRP (`WaterSimulationSearchJob`, tak jak w próbce HDRP `FitToWaterSurfaceBurst`). Wcześniej odczyt obejmował do 24 okrętów na klatkę, a każdy punkt był osobnym zapytaniem `ProjectPointOnWaterSurface`.
  - Bufory natywne zostają między klatkami i tylko rosną. Zwalnia je koniec bitwy, a zwolnione źródło niczego już nie czyta ani nie alokuje.
  - Zadanie kończy się w tej samej klatce, bo HDRP aktualizuje morze przy renderowaniu.
  - Zapasowej ścieżki nie ma. `ProjectPointOnWaterSurface` czyta te same dane (`FillWaterSearchData`, `WaterSurface.cs` w HDRP 17.3), więc zawodzi w tych samych klatkach. Klatka bez danych morza zostawia kadłubom poprzednią pozę.
- **Pogoda** (`WeatherController`). Natężenie od 0 (spokój sceny) do 1 (sztorm) zmienia się płynnie w czasie rzeczywistym przez `SetWeather(natężenie, sekundy)`:
  - **morze:** wiatr dużych fal 30 → 60 km/h, chaos 0,8 → 0,95, wiatr zmarszczek 8 → 14 km/h (HDRP podaje oba wiatry w km/h). HDRP przebudowuje widmo fal przy każdej zmianie wiatru (`WaterSurface.CheckResources`), więc woda dostaje nowe wartości najwyżej 4 razy na sekundę;
  - **mgła i ekspozycja:** osobny, nakładany Volume z profilem tworzonym w pamięci, o priorytecie wyższym o 1 od Volume sceny.
    - Przy spokoju ma wagę 0, więc obraz jest dokładnie taki jak bez pogody.
    - W sztormie gęstość mgły rośnie równomiernie do drogi 250 m w warstwie 90 m, a odcień nieba w mgle zmienia się na (0,55; 0,6; 0,66). Ekspozycja otwiera się o 0,75 EV, więc obraz ciemnieje mniej, niż słabnie słońce.
    - Assety profili sceny się nie zmieniają. Przełączanie profili obrazu działa dalej, bo pogoda czyta bieżący profil sceny;
  - **słońce:** 100 000 → 35 000 lx, 5600 → 7000 K.

  W meczu pogoda jest zawsze spokojna; nowy mecz i wczytanie zapisu zaczynają się od spokoju, bo pogoda nie należy do zapisu. Ponowne wywołanie `SetWeather` z tym samym celem nie zaczyna przejścia od nowa. Etap 5 podłączy pogodę do zdarzeń gry.

| Kontrola | Wynik | Zakres |
|---|---|---|
| Testy PlayMode partii B (11), osobny przebieg | PASS 11/11, 0 pominiętych | pula (w tym obiekt zniszczony w puli i system potomny), efekty bitwy z tonącym kadłubem, punkty i pozy z zastępczego morza, źródło HDRP bez morza i po zwolnieniu, bitwa czytająca tylko widoczne kadłuby, przejście pogody i powrót, ponowne `SetWeather`, limit zmian morza, wybór Volume sceny przy dwóch bitwach, niezmienność świata przy sztormie i fali; wynik bramki M1 w commicie i w `qa/reports` |
| Build Windows64 Mono, scena M1_Battle | Success, 331674672 B, 0 błędów, 0 ostrzeżeń | |
| Showcase (`-morzaShowcase`), zrzuty `sztorm` i `po-sztormie` | sztorm: natężenie 1,00, wiatr 60 km/h, chaos 0,95, zmarszczki 14 km/h, słońce 35000 lx, 22 zmiany morza w 6 s; po powrocie dokładnie 30 / 0,8 / 8 i 100000 lx | dwie klatki z profilu Ultra; ruchu fal i przejść nie oceniał człowiek |

Pomiar testem dymnym, RTX 3050 Ti Laptop, profil Ultra, 1920×1080, 90 s rozgrywki SI, po dwa przebiegi. W obu wersjach było 16 okrętów, z czego 8 widocznych.

| Wartość | Przed (build z `ce8d4a0`) | Po (`c9dd827` + partia B) |
|---|---|---|
| Prezentacja (CPU, ms na klatkę) | 1,50 / 1,41 | 1,16 / 1,13 |
| Próbkowanie wody (ms na klatkę) | nie mierzone osobno | 0,09 / 0,10 |
| Kadłuby czytane w klatce | do 24 (widocznych było 8) | wszystkie widoczne, najwięcej 8 |
| Alokacje buforów natywnych | — | 1 / 1 |
| Średnia klatka (ms) | 44,9 / 30,5 | 39,1 / 40,4 |
| GPU (ms) | 35,4 / 28,4 | 36,9 / 38,4 |
| CPU główny (ms) | 8,83 / 9,03 | 9,47 / 9,14 |
| Najdłuższa klatka (ms) | 17250 / 267 | 2583 / 2833 |
| Przechył / wznoszenie | 12° / 1,22 i 1,18 m | 12° / 1,33 i 1,18 m |

Prezentacja zajmuje po zmianie około 0,3 ms mniej na klatkę. Całą klatkę ogranicza GPU, więc liczba klatek na sekundę się nie zmienia. Odczyty nieudane (8 przed, 40 po) to w obu wersjach jedna klatka startowa bez danych morza: dawny kod liczył pierwszy nieudany punkt każdego okrętu, nowy wszystkie punkty partii. Build „po” zawiera też etap 3c, część 1 (`c9dd827`, nowe klasy i budowy SI). W pierwszych 90 s liczba okrętów była jednak taka sama. Przebiegi 2026-11-10 uruchomiono z `-force-device-index 0`, a log pokazał RTX 3050 Ti.

## Szyk grupy i wejście na Input System (2026-11-10)

Partia C planu „Brakujące elementy z polecenia Senior Unity HDRP RTS”.

- **Szyk** (`Simulation/Formation.cs`, czysta arytmetyka).
  - Rozkaz ruchu dla grupy daje każdemu statkowi osobny rozkaz ruchu na jego miejsce. Są dwa szyki: linia burta przy burcie w poprzek kursu (najwyżej 8 w szeregu, kolejne szeregi o długość kadłuba z tyłu) albo klin z prowadzącym w punkcie.
  - Odstęp w poprzek kursu to najszerszy kadłub z wiosłami po obu stronach plus `World.HullSpacing`; wzdłuż kursu najdłuższy kadłub plus `World.HullSpacing`.
  - Punkt przy lądzie odsuwa się wzdłuż normalnej brzegu, tak jak w `World.MoveTargetToWater`, na zapas najgłębszego kadłuba grupy (`World.LandClearance` + 1 m). Miejsce bez tego zapasu albo kolidujące z innym przechodzi na najbliższe wolne, szukane w pierścieniach co pół odstępu.
  - Przydział: najpierw według położenia na boki, potem zamiany par, dopóki skracają łączną drogę. Dzięki temu kursy się nie krzyżują.
  - Kliknięcie bliżej niż odstęp od środka grupy bierze kurs prowadzącego (najniższe Id).
  - Wszystko w kolejności Id, więc wynik nie zależy od kolejności zaznaczenia.
  - Zapis i powtórka mają tylko zwykłe rozkazy ruchu.
  - Przełącznik w panelu rozkazów i klawisz G (`GameAction.Formation`, wstawiona po rozkazach, przypisania zapisane po nazwie). Wybór jest zapamiętany w PlayerPrefs (`orders.formation`). Opis w docs/INTERFEJS.md.
- **Wejście** (`Presentation/Hud/GameInput.cs`).
  - `activeInputHandler: 1` (tylko pakiet Input System). Wszystkie odczyty klawiatury i myszy idą przez jeden adapter na `Keyboard.current` i `Mouse.current`; przy braku urządzeń nic nie jest wciśnięte.
  - KeyCode odpowiada klawiszowi Input System w tym samym miejscu klawiatury, więc zapisane przypisania i etykiety się nie zmieniają.
  - Oś obrotu myszą: piksele × 0,1, jak dawne „Mouse X/Y” o czułości 0,1.
  - Kółko: domyślne `UniformAcrossAllPlatforms`, ±1 na ząbek.
  - Przy zmianie przypisania przechwytywane są tylko klawisze klawiatury, jak wcześniej, także gdy inny klawisz jest wciśnięty.
  - Zapisany klawisz, którego Input System nie obsługuje, wraca do domyślnego z ostrzeżeniem w logu. Konflikt przypisań herold zgłasza na początku bitwy.
  - Panele UI Toolkit dostają wejście od dostawcy Input System z wbudowanymi akcjami UI (projekt nie ma własnego zestawu akcji ani assetu ustawień).
  - Wpis Input System do `testables` dołączyłby setki testów pakietu, a pominięte testy blokują bramkę. Testy używają więc własnych wirtualnych urządzeń (`InputSystem.AddDevice`) i na czas testu zmieniają dwa ustawienia: fokus i tryb gry w edytorze.

| Kontrola | Wynik | Zakres |
|---|---|---|
| Testy EditMode szyku (6), osobny przebieg | PASS 6/6 | linia i odstępy, klin z dwoma skrzydłami, szeregi po 8 bez krzyżowania kursów (także układ, w którym przydział po samym położeniu bocznym krzyżował kursy), kurs prowadzącego przy kliknięciu obok grupy, niezależność od kolejności zaznaczenia, punkt na wyspie: wszystkie miejsca na wodzie z zapasem i bez stłoczenia |
| Testy PlayMode (5), osobny przebieg | PASS 5/5 | ruch grupy w linii i klinie, pojedynczy statek w punkt, przełącznik tylko dla grupy, mapa KeyCode ↔ Key, klawisze, przyciski, kółko i pozycja z wirtualnych urządzeń, klawisz szyku przez interfejs |
| Build Windows64 Mono | Success, 331723828 B, 0 błędów, 0 ostrzeżeń | z niezatwierdzoną pracą innych sesji w drzewie (modele okrętów, efekty pocisków) |
| Showcase, zrzuty `szyk-linia` i `szyk-klin` | 5 taranowców w linii, potem w klinie; przełącznik pokazuje szyk; log gracza bez błędów, Input System zainicjowany | zrzuty z profilu Ultra; wejście z prawdziwej myszy i klawiatury nie było używane |

## Znane ograniczenia

- Unity zapisuje `asserts="0"` w wynikach NUnit także dla testów z asercjami. Bramka wymaga wtedy asercji w kodzie właściwej metody testu, więc asercja wyłącznie w metodzie pomocniczej blokuje wynik. Zmiana przeszła niezależny przegląd, po którym usunięto wykryte obejścia; poprawek nie przeglądano ponownie. Analiza kodu nie dowodzi, że asercja faktycznie się wykonała.
- PreToolUse reaguje na narzędzie TaskUpdate, którego ta aplikacja nie używa. Automatyczne wywołanie hooków: NOT_RUN.
- Profile różnią się na razie funkcjami HDRP, kaskadami i ustawieniami objętości. Limity świateł, detal wody i gęstość VFX z V10 nie są wdrożone.
- Menu rozdzielczości (F10) i profili (F9) to robocze panele IMGUI.
- Jeden z 57 przebiegów w logach z 2026-11-09 wystartował na Intel Iris Xe z Direct3D 11. HDRP nie uruchomił się wtedy (`Kernel 'KDepthDownsample8DualUav' not found`), obraz był czarny, a gra działała dalej. Powtórka na RTX 3050 Ti przeszła. Przyczyny wyboru karty nie ustalono. Powtórzyło się to 2026-11-09 o 19:20 (Unity odrzuca D3D12 na zintegrowanym Intelu i przechodzi na D3D11); `-force-device-index 1` wymusza RTX 3050 Ti. Gra na samym Intel Iris Xe nie działa: BLOCKED do osobnej poprawki.
- Pierwsze uruchomienie po przebudowie bywa bardzo wolne: 68 kroków w 16 s przy drugiej działającej kopii gry. Przyczyny nie potwierdzono.
## Nieuruchomione

- Dłuższa rozgrywka człowieka: użytkownik zagrał krótko, zaznaczanie i rozkazy działały, bez błędów. Paski HP, animacje i efekty dodane po tej próbie: NOT_RUN w ręcznej ocenie.
- Ocena obrazu przez człowieka dla obecnej wody, okręgów, mgły i modeli: NOT_RUN. Sprawdzono je tylko na zrzutach z profilu Ultra w 1920×1080.
- Wizualne potwierdzenie działania odbić RT: NOT_RUN.
- Kolor wody i ekspozycja końcowych ustawień w profilach Low, Medium, High i Extra High w buildzie: NOT_RUN.
- Fizyczne wyświetlanie 2560×1440 i pozostałych trybów powyżej 1080p: BLOCKED, brak monitora.
- Pełne przełączanie rozdzielczości i profili w buildzie, 20 cykli bez wycieków: NOT_RUN.
- Pomiary GPU, VRAM oraz scen 100/200/400 statków: NOT_RUN.
- Próbkowanie fal przy dużej flocie: NOT_RUN. Zmierzono najwyżej 8 widocznych kadłubów.
- Sztorm w ruchu (fale zmieniane 4 razy na sekundę, mgła, światło) oraz na profilach innych niż Ultra, oceniany przez człowieka: NOT_RUN. Są tylko dwa zrzuty z Ultra.
- Pula efektów z efektem VFX Graph: NOT_RUN. Projekt nie ma jeszcze żadnego efektu VFX, więc restart VFX nie ma testu.
- Ręczny przegląd wejścia w buildzie po przejściu na Input System: NOT_RUN, do wykonania przez człowieka. Testy automatyczne podają wejście z wirtualnych urządzeń, nie z prawdziwej myszy i klawiatury. Do sprawdzenia:
  - kamera i przewijanie krawędzią, także gdy kursor wychodzi poza okno na drugim monitorze (do pierwszego ruchu myszy przewijanie krawędzią jest wyłączone);
  - ramka zaznaczenia, PPM i środkowy przycisk, w tym skala obrotu względem dawnej osi „Mouse X”;
  - kółko (±1 na ząbek);
  - klawisz Pause;
  - zmiana przypisania klawisza;
  - kliknięcia w panelach;
  - położenie kursora w trybie FullScreenWindow przy rozdzielczości niższej niż pulpit.
- Integracja hooków w działającym Claude Code: NOT_RUN.
- Zewnętrzne CI: NOT_RUN.
