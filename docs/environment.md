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
- Ląd w logice to wielokąt linii wody (64 promienie; wybrzeże zmierzone z modelu, wysepki jako elipsy, obrót o pół obrotu po stronie x>0). Statek trzyma od niego odstęp: połowa długości plus szerokość płycizny, której jego zanurzenie nie przejdzie (`Seabed`, zanurzenie rośnie o 65% przy pełnej ładowni). Trasy: graf widoczności wokół wielokątów, osobny dla każdego odstępu (co 0,5 m), szukanie A* z kopcem i leniwym sprawdzeniem odcinków od statku. Mecz 30 min SI liczy się w ok. 6 s w .NET (przedtem 76 s). Tylko wybrzeże blokuje strzały. Cel ruchu na lądzie przesuwa się na najbliższą wodę. Zapis zawiera kształt i obrót lądu (od v7). Nie ma grafu akwenów, rezerwacji przejść ani formacji. Odległości mapy to linie proste, nie zmierzone trasy.
- Kadłuby to kapsuły o długości i szerokości z definicji. Trzymają 2 m wody między burtami (poza parą sczepioną abordażem), by w ukośnym rzucie kamery bliższy kadłub nie zasłaniał dalszego; nachodzenie rozpychają ważone masą, a statek z kadłubem na kursie skręca, by minąć go burtą. Eskorta płynie przy burcie transportu, nie przed jego dziobem.
- Wiosła: zasięg z reguł (`Rules.OarReach`); mijające się statki zostawiają na nie miejsce, a w prezentacji wiosła składają się wzdłuż burty po stronie, gdzie inny kadłub leży w ich zasięgu, i obie strony przy postoju.
- Stocznia: dwie pochylnie przy osadzie (linia wojenna i portowa, po bokach pomostu). Kadłub na pochylni pokazuje etap według postępu zamówienia: stępka, wręgi, poszycie, pokład, maszt (i taran), wiosła i żagiel; cieśle pracują do zdjęcia rusztowań. Gotowy statek zjeżdża po rampie do morza (rozbryzg) i dopływa do miejsca z symulacji, które leży u wylotu pochylni, dziobem w morze, w pierwszym wolnym miejscu bez nachodzenia.
- Kuter na łowisku: 3 s schodzenia załogi na ląd (bez wydobycia) i 4 s zaokrętowania przed odpłynięciem (stanowisko zajęte). Dochód testowy 108 D/min (model 124 D/min bez manewrów). Prezentacja odwołuje załogę na pokład z wyprzedzeniem według przewidywanego końca pracy. Zapis v8.
- Wygląd egejski: woda ma granatowy kolor rozproszenia i turkusową refrakcję (przejrzystość 12 m), dno mielizn, wapienny brzeg i zbocza mają tekstury CC0 z Poly Haven w mapowaniu trójpłaszczyznowym, słońce 5600 K, nasycenie +12. Głazy (CC0, uproszczone do ok. 2 tys. trójkątów na część) są rozsypane deterministycznie wzdłuż każdej linii wody i na płyciznach, łączone w kilka siatek na brzeg. Źródła: Assets/Art/PolyHaven/ZRODLA_CC0.txt.
- Wysepki osad, wolnych brzegów i złóż mają nieregularne, seedowane linie brzegu (`LandShape.Irregular`) zamiast kół i elips; po stronie pomostu i pochylni brzeg zostaje w stałej odległości. Logika (kolizje, płycizny, trasy) używa tego samego wielokąta.
- Średni czas klatki w teście dymnym (RTX 3050 Ti, Ultra, 1920×1080): 39,5–54 ms z dużym rozrzutem między przebiegami; głazy dokładają kilka do kilkunastu ms. `-morzaNoBoulders` wyłącza je do pomiarów. Optymalizacja wydajności sceny: NOT_RUN.
- Interfejs bitwy: UI Toolkit (BattleHud), czcionki OFL Cinzel i Alegreya Sans z Google Fonts (licencje w UI/Resources/Fonts).
- Załogi (tylko prezentacja, czas skrócony): drwale i górnicy schodzą z kutra trapem na pomost, ścinają drzewa (pochylenie, upadek, odrost po 25 s) lub kują w sztolni, tragarze noszą urobek; w porcie dokerzy wynoszą towar do składu. Rozładunek w porcie trwa według reguł ekonomii (60 jedn./s), więc animacja dokerów jest krótka.
- Jedyna broń to łucznicy z rzutem seedowanym, bez lotu pocisku, sektorów i wyboru wyposażenia. Wyposażenie z szablonu nie jest osobnym przedmiotem, więc 30% szansy jego przetrwania we wraku nie jest wdrożone.
- Osada łączy port i stocznię w jednym obiekcie, więc zniszczenia samego portu nie ma; łup 30% ze zniszczonej osady nie jest wdrożony, bo jej zniszczenie kończy mecz.
- SI jest prosta i zna pozycję startową wroga oraz położenie punktów pracy. Balans: NOT_RUN.

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
- Integracja hooków w działającym Claude Code: NOT_RUN.
- Zewnętrzne CI: NOT_RUN.
