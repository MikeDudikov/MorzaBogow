# Warianty wyglądu okrętów (profil `aegean-12c/1`)

Data: 2026-11-10.

Okręty jednej klasy mają wspólną konstrukcję i jeden model bazowy. Różnią się wykończeniem, wyposażeniem pomocniczym i śladami użytkowania. Zmiana dotyczy wyłącznie prezentacji. Bez zmian zostają:
- klasa i parametry;
- HP i uzbrojenie;
- footprint, kolizje i nawigacja;
- prędkość i zasięgi;
- właściciel w symulacji.

Ramy historyczne i źródła opisuje [HistoricalConstraints.md](HistoricalConstraints.md) w sekcji „Warianty okrętów”.

## Warstwy i kolejność składania

| Warstwa | Zawartość | Kod | Trwałość |
|---|---|---|---|
| A. Konstrukcja typu | kadłub, stanowiska, maszt, wiosła, broń, olinowanie | `GreekShips.Parts(klasa)`, siatki wspólne | kod |
| B. Rodzina (warsztat) | wzór pasa burty, zakresy odcieni, charakter wykończenia | `ShipLookLibrary.Families` | w opisie |
| C. Jednostka | odcienie, słoje, płótno, łaty, dawne naprawy, przetarcia, wyposażenie | `ShipLook` | w opisie, zapisywany obok zapisu gry |
| D. Stan bieżący | wyłomy, strzały, przechył, rozdarcia, złamania, świeże naprawy, wyposażenie zmyte za burtę, barwa właściciela | `ShipVisual` ze stanu symulacji i zdarzeń | niezapisywany, odtwarzany ze stanu |

`ShipVisual` jest jedynym właścicielem rendererów, materiałów i wartości rendererów okrętu. Nie ma osobnego, konkurencyjnego kontrolera. Warstwy składa zawsze w tej samej kolejności:
1. typ;
2. wygląd: raz, przy tworzeniu albo przez jawne `ApplyLook` — materiały palety i zużycie w wartości renderera;
3. stan: zdarzenia i `SetCondition` (świeże deski z doku mają zużycie 0);
4. barwa właściciela: `SetOwner`, tylko materiał żagla i łat.

Wilgotność i sadza (V03) mają zarezerwowane pola w wartości renderera, przed barwą właściciela. Shader je odczytuje, ale wygląd się od nich nie zmienia, bo projekt nie ma ich źródła: nie ma ognia ani mokrych kadłubów. Nic z wyglądu nie liczy się co klatkę.

## Dozwolone różnice i limity

Wybory nie są niezależne. Warsztat i wykończenie wyznaczają zakresy, z których wybiera się resztę, więc farby, drewno, płótno, ślady i wyposażenie pasują do siebie.

| Element | Warianty | Limit i zasada |
|---|---|---|
| Warsztat | stocznia portowa (zygzak, smoła świeża, jasna ochra), wyspiarski (linia falista, smoła brunatna, głęboka ochra), skromny (bez wzoru, smoła matowa, wypłowiała ochra) | strona ma warsztat główny (ok. 3/4 okrętów) i skromny jako domieszkę; okręt przejęty zachowuje swój warsztat |
| Wykończenie | świeże, sezonowane, stare | wagi 3 : 5 : 2; stary kadłub nadal wygląda na sprawny |
| Smoła | błyszcząca, matowa, brunatna, solna | tylko odcienie i połyski z zakresu warsztatu dla danego wykończenia |
| Ochra, biel | 3 i 2 odcienie | tak samo |
| Drewno | 4 tekstury słojów (różne układy i natężenia, sęki, styki desek, ślady cioła, przetarte krawędzie) × odcienie | pary odcień i słoje tylko z listy wykończenia |
| Płótno | 4 tekstury (wąskie i szerokie pasy, wzmocnienie pod reją, zabrudzona stopa) × 2–3 odcienie | kolor zawsze z barwnika strony, odcień tylko go rozjaśnia lub przyciemnia (×0,86–1,12) |
| Łaty żagla | 0 (świeże), 0–1 (sezonowane), 1–3 (stare) | najwyżej 3, nie nachodzą na siebie, z innego płótna w barwie strony |
| Ślady na kadłubie | dawne naprawy (wstawione deski w smole innego odcienia, u kupca w drewnie) i przetarcia smoły do drewna | 0–1, 1–3, 3–6 według wykończenia; nad wodą, pod pasem burty, poza czerwonymi „policzkami” dziobu, bez nakładania |
| Olinowanie | sztagi w farbie `Rope`, 2 odcienie | maszt, reja i układ bez zmian |
| Wyposażenie | dzban na wodę, worki, kosz, zwój liny, złożone płótno, kamienne kotwice | galera 1–3, kupiec 2–4, łódź 0–2; każdy przedmiot najwyżej raz |

**Oznaczenia właściciela:** tylko barwa żagla i łat z zatwierdzonego zestawu (granat gracza, czerwień przeciwnika). Bez godeł i bez losowania znaków między flotami.

## Opis wariantu `ShipLook`

**Pola:**
- id okrętu, profil z wersją, seed prezentacji, klasa, strona budownicza;
- warsztat i wykończenie;
- id odcieni (smoła, ochra, biel, drewno, lina, len) i id tekstury słojów;
- wzór pasa, płótno żagla i płótno łat;
- łaty (u, v, szerokość, wysokość);
- ślady (rodzaj, s, wysokość, burta, wymiary, kąt);
- wyposażenie (przedmiot, nazwane miejsce, obrót).

Wszystko jest zapisane identyfikatorami, nie pozycjami w bibliotece. Opis nie zawiera kopii tekstur ani siatek.

**Seed:** FNV-1a 64 z `profil|klasa|id`. Każdy aspekt (warsztat, wykończenie, farby, płótno, łaty, ślady, wyposażenie) ma osobny strumień SplitMix64, więc zmiana jednej listy nie przesuwa wyborów w innej. Kompozytor nie korzysta z:
- RNG gry;
- `UnityEngine.Random`;
- `GetInstanceID`;
- kolejności tworzenia, czasu ani klatek.

**Zapis:**
- `ShipLookBook` trzyma opisy meczu i składa opis przy pierwszym pojawieniu się id.
- `QuickSave` zapisuje opisy obok zapisu gry (`<zapis>.looks.json`) razem z hashem zapisu (`SaveGame.Hash`). `QuickLoad` przyjmuje plik tylko dla tego samego zapisu.
- Bez pliku opisy składają się od nowa z id. Daje to ten sam wygląd z jednym wyjątkiem: okręt przejęty dostałby warsztat nowej strony.
- Format `SaveGame` się nie zmienia.

**Profil `plain`:** wygląd klasy bez wariantów (materiały sceny, zygzak, bez śladów i wyposażenia). `ShipVisual.Create` bez opisu daje właśnie ten wygląd. Bitwa podaje opis z `ShipLookBook`. Flaga `-morzaPlainShips` pokazuje wszystkie okręty bez wariantów, co służy do pomiaru A/B w buildzie.

## Wyposażenie: miejsca i reguły

**Nazwane miejsca** (`ShipLookLibrary.PlacesOf`), lewa (L) i prawa (P) burta, przy łodzi środek (S):
- galera:
  - pomost rufowy za sternikiem;
  - pokład między pomostem rufowym a wioślarzami;
  - pokład między wioślarzami a pomostem dziobowym;
  - przód pomostu dziobowego;
- kupiec: rufa, śródokręcie za ładunkiem, dziób;
- łódź: rufa i dziób na linii środka.

**Reguła miejsca** (`ShipLookLibrary.Blocked`). Przedmiot nie może stać:
- na stewach ani poza pokładem lub pomostem;
- w pasie przy relingu, gdzie stoją abordażyści (`RailNear`) i schodzą załogi (`DeckToward`);
- na ławach wioślarzy (od pierwszej do ostatniej dulki, z marginesem 0,4 m);
- w przejściu środkiem między pomostami;
- przy maszcie i wiosłach sterowych;
- przy stanowiskach broni (0,8 m dla obsługi);
- wyżej niż 1 m.

Miejsce zajmowane przez przedmiot wynika z jego siatki: okrąg dla każdego obrotu i wysokość.

**Zachowanie przedmiotu:**
- **Zasięg widoczności:** `LODGroup` na modelu. Od 5% wysokości ekranu widać wszystkie detale, od 1,5% łaty i większe przedmioty, poniżej tylko pas burty i farby.
- **Uszkodzenia:** worki, kosze i płótno znikają za burtą poniżej 30% kadłuba i wracają po naprawie.
- **Tonięcie:** dzban, worki, kosz i płótno odpływają przez `BattleEffects.FloatOff`; kotwice i lina idą na dno.
- **Rozpad kadłuba:** detale wyglądu chowają się razem z kadłubem, a sekcje zachowują farby okrętu.

Żaden przedmiot nie przypomina broni ani zbroi (bez brązu).

## Materiały i tekstury HDRP

- **Wspólna paleta:**
  - każdy odcień profilu (z teksturą słojów lub płótna) to jeden materiał;
  - tworzy się go raz, jako klon materiału farby okrętu (`Resources/ShipLook/ShipPaint.mat`, Shader Graph `MorzaBogow/ShipPaint`), i współdzielą go wszystkie okręty z tym odcieniem; nazwa zostaje „materiał sceny [odcień]”;
  - bez materiału farby (brak zasobu albo shader nieobsługiwany) klonuje się materiał sceny dla danej farby, jak przed PBR (tylko kolor);
  - nie ma materiału na okręt ani na klatkę, ani wywołań `renderer.material`; to, czym różnią się okręty jednego odcienia (zużycie), niesie wartość renderera;
  - dla 40 okrętów wszystkich klas powstało 36 materiałów, limit wynosi 48;
  - wygląd `plain` dalej rysują materiały sceny HDRP/Lit.
- **Siatki:**
  - typ korzysta ze wspólnych siatek;
  - na okręt przypadają najwyżej trzy małe siatki własne: dawne naprawy, przetarcia i łaty żagla; świeże naprawy z bitwy dochodzą do nich jako stan;
  - kadłub nie jest skalowany ani podmieniany;
  - generator liczy styczne (`RecalculateTangents` z UV w metrach), więc mapy normalnych leżą wzdłuż desek, drzewców i pasów płótna.
- **Tekstury** (`Editor/ShipTextureBaker`, deterministyczne, bez pobierania): 12 tekstur po 3 mapy, wszystkie z tych samych receptur, więc szwy, słoje, splot i skręty pokrywają się w kolorze, rzeźbie i masce.
  - Rozmiary: słoje 4 × 512², płótno 4 × 512², przetarta smoła 2 × 256², lina i wiklina po 256²; mapy normalnych i maski mają rozmiar koloru.
  - Kolor `<nazwa>.png`: sRGB, kompresja wysokiej jakości (na Windows BC7).
  - Normalne `<nazwa>_normal.png`: przestrzeń styczna, +Y wzdłuż v (OpenGL), import jako normal map (na Windows BC5). Liczone z rzeźby różnicami centralnymi z zawinięciem tam, gdzie tekstura się powtarza.
  - Maska `<nazwa>_mask.png`, pakowanie HDRP Lit, liniowo, alfa z pliku: R metallic = 0 (nic nie jest metalem), G okluzja (szwy, styki, szczeliny, rowki lin), B maska detalu = gdzie widać zużycie (krawędzie desek, grzbiety słojów, szwy płótna, brudna stopa, odsłonięte drewno, wierzchy skrętów i wici), A połysk względny 0,3–1 mnożony przez połysk odcienia (w HDRP/Lit: `_SmoothnessRemapMax` = połysk odcienia).
  - Rzeźba: drewno — rowki szwów i styków, zaokrąglone krawędzie, deski wystające lub cofnięte, wypukłe słoje (mocniej w `weathered`), ślady cioła, sęki; płótno — grzbiety szwów, wzmocnienie pod reją z wpuszczonym ściegiem, splot, marszczenie; przetarcia — smoła wyżej niż odsłonięte drewno; lina — okrągłe skręty z ostrym rowkiem; wiklina — okrągłe wici nad i pod.
  - Przetarcia mają zawijanie `Clamp`, reszta `Repeat`; wszystkie mapy mają mipmapy i filtr anizotropowy 4.
  - Pamięć: ok. 3 MB na rodzaj map, razem ok. 9 MB. Liczba wynika z rozmiarów i formatu, nie została zmierzona.
- **UV w metrach** (generator): deski i pasy poszycia biegną wzdłuż kadłuba, drzewca wzdłuż osi, a płótno ma u w metrach i v od 0 do 1. Kierunku słojów i splotu nic nie obraca. Normalne, maska i kolor dzielą kafelkowanie mapy koloru (`_BaseColorMap_ST`), jak w HDRP/Lit.

### Farba okrętu (Shader Graph) i wartość renderera

- **Graf** `Art/Ships/Shaders/ShipPaint.shadergraph`: HDRP Lit, nieprzezroczysty, jednostronny jak materiały sceny, odbiera decale i SSR, z przebiegami ray tracingu. Całą farbę liczy jedna funkcja `ShipPaint.hlsl` (Custom Function z pliku).
- **Właściwości** nazwane jak w HDRP/Lit (`_BaseColor`, `_BaseColorMap`, `_NormalMap`, `_NormalScale`, `_MaskMap`, `_Metallic`, `_Smoothness`) oraz `_WearStrength`. Paleta ustawia je tak samo jak dawniej w HDRP/Lit.
- **Bez słów kluczowych dla map.** Normalne i maska są zawsze próbkowane (bez tekstury: płaska normalna i biała maska), więc build nie może ich wyciąć. Każdy materiał palety ma zestaw słów kluczowych materiału farby, a ten leży w `Resources`, więc build kompiluje dokładnie ten wariant.
- **Wartość renderera** (`Renderer.SetShaderUserValue`, `ShipSurface`): 32 bity, pola po 4 bity (0–15 czytane jako 0–1):

| Bity | Pole | Źródło | Stan |
|---|---|---|---|
| 0–3 | zużycie | wykończenie i seed wyglądu: świeże 1–4, sezonowane 5–9, stare 10–13; `plain` 0; świeże deski z doku 0 | wdrożone |
| 4–7 | wilgotność (V03) | brak: nie ma deszczu ani bryzgów na kadłubach | zarezerwowane, odczytywane, bez wpływu na obraz |
| 8–11 | sadza (V03) | brak: nie ma ognia | zarezerwowane, odczytywane, bez wpływu na obraz |
| 12–31 | — | — | zera |

- **SRP Batcher zostaje:** HDRP trzyma wartość renderera w `unity_RenderingLayer.y` bufora `UnityPerDraw`, czyli w danych rysowania, nie materiału. Okręty o różnym zużyciu dzielą materiały palety.
- **Zużycie w obrazie:** `w = zużycie × B maski × _WearStrength` (odcienie z teksturą 1, bez tekstury 0,35, więc smoła i farby płowieją tylko trochę i równo). Kolor zbliża się do szarości tej samej jasności (do 30%) i jaśnieje (do 8%), połysk spada (× 1 − 0,45 w), rysunek rzeźby się pogłębia (siła normalnej × 1 + 0,5 w). Nigdy nie dodaje metalu. Stary kadłub nadal wygląda na sprawny.
- **Wartość się nie zapisuje:** wynika z opisu wyglądu (wykończenie i `Seed`), więc zapis i format `SaveGame` się nie zmieniają, a wczytany okręt ma to samo zużycie.

## Pomiar: paleta czy MaterialPropertyBlock

**Warunki pomiaru:**
- `ShipLookBenchmark` z flagą `-morzaBenchmark`;
- 100 taranowców pod kamerą strategiczną;
- edytor batch, profil Ultra, obraz 1920×1080;
- RTX 3050 Ti Laptop;
- 120 klatek po 30 klatkach rozgrzewki.

Wyniki pochodzą z edytora, więc mają charakter informacyjny.

| Tryb | Renderery na okręt | Materiały | Bloki MPB | Draw calls | SetPass | Główny wątek | Tworzenie okrętu |
|---|---|---|---|---|---|---|---|
| bez wariantów (`plain`) | 24 | 7 | 0 | 6714 | 35 | 13,4 ms | 0,66 ms |
| **paleta (wdrożone)** | 26,9 | 34 | 0 | 6870 | 35 | 14,0 ms | 2,36 ms |
| MaterialPropertyBlock | 26,9 | 10 | 3704 | 6870 | 2859 | 40,8 ms | 5,80 ms* |

\* z przepisaniem materiałów na bloki w teście.

**Wniosek: zostaje paleta.**
- Przy palecie SetPass się nie zmienia, draw calls rosną o 2,3%, a główny wątek o 0,6 ms.
- MPB wyłącza SRP Batcher dla tych rendererów: SetPass rośnie 82 razy, a główny wątek o 27 ms.
- Alokacje na klatkę są takie same we wszystkich trybach. 29 KB w każdej klatce pochodzi z edytora i testu.
- Test budżetu zmierzył 0 B na 30 klatek animacji i stanu przy wariantach i bez nich.

**Tworzenie okrętu z wyglądem:** ok. 2,4 ms wobec 0,66 ms bez wariantów, raz przy budowie, przejęciu lub wczytaniu.

**Pomiar wyjściowy przed zmianami:** 23 renderery na okręt, 6 materiałów, 6314 draw calls, SetPass 35, 11,4 ms. Inny przebieg edytora, a pas burty nie był jeszcze osobnym rendererem.

### Farba okrętu wobec palety HDRP/Lit (PBR, 2026-11-10)

**Warunki:** jak wyżej (`ShipLookBenchmark` z `-morzaBenchmark`, 100 taranowców, edytor batch, Ultra, 1920×1080, 120 klatek po 30 klatkach rozgrzewki), karta RTX 3050 Ti Laptop wymuszona `-force-device-index 0` (bez niej Windows dał edytorowi raz Intel Iris Xe).

**Zmiana w samym teście:** placeholdery i kopie HDRP/Lit przechodzą `HDMaterial.ValidateMaterial`, jak materiały sceny z `BattleSceneBuilder`. Mają więc kolejkę 2225 (odbierają decale) i wchodzą do depth prepassu, tak jak w grze i jak farba okrętu. Tabela wyżej powstała z niewalidowanymi materiałami testu (kolejka 2000), dlatego draw calls są tu wyższe i obu tabel nie porównuje się wprost.

| Tryb | Shader farb | Renderery na okręt | Materiały | Bloki MPB | Draw calls | SetPass | Główny wątek | Tworzenie okrętu |
|---|---|---|---|---|---|---|---|---|
| bez wariantów (`plain`) | materiały sceny HDRP/Lit | 26 | 7 | 0 | 10 614 | 42 | 14,16 ms | 0,73 ms |
| paleta HDRP/Lit, sam kolor (`lit`, jak 9ab5039) | HDRP/Lit | 28,9 | 34 | 0 | 10 848 | 42 | 15,10 ms | 2,17 ms* |
| paleta HDRP/Lit z mapami (`litmaps`) | HDRP/Lit, `_NORMALMAP`, `_MASKMAP` | 28,9 | 34 | 0 | 10 848 | 45 | 14,41 ms | 2,42 ms* |
| **paleta na farbie okrętu (wdrożone)** | `MorzaBogow/ShipPaint` + wartość renderera | 28,9 | 34 | 0 | 10 848 | 42 | 14,45 ms | 2,30 ms |
| MaterialPropertyBlock | HDRP/Lit | 28,9 | 10 | 3904 | 10 848 | 4719 | 43,98 ms | 8,48 ms* |

\* z przerobieniem materiałów w teście.

**Wnioski:**
- **SRP Batcher zostaje.** Farba okrętu z wartościami rendererów (każdy okręt ma swoje zużycie) ma SetPass 42, jak paleta HDRP/Lit i okręty bez wariantów. Draw calls się nie zmieniają.
- **Koszt CPU** jest taki jak HDRP/Lit wykonującego tę samą pracę: 14,45 ms wobec 14,41 ms (`litmaps`) i 15,10 ms (`lit`). Różnice mieszczą się w rozrzucie przebiegów edytora (pojedynczy przebieg, inne sesje w tle).
- **HDRP/Lit z mapami** potrzebuje osobnych wariantów (SetPass 45) i słów kluczowych, które przy materiałach robionych w biegu build mógłby wyciąć. Farba okrętu próbkuje mapy zawsze, więc tego problemu nie ma.
- **Paleta zostaje.** Wariant „odcienie w shaderze” (indeksy odcieni w wartości renderera zamiast materiału na odcień) nie miałby czego poprawić: przy palecie ani draw calls, ani SetPass nie rosną z liczbą materiałów. Taki był też wybór użytkownika z 2026-11-10.
- **GPU** test w edytorze nie mierzy; build opisuje sekcja „Farba okrętu w buildzie”.
- **Alokacje na klatkę:** 29 KB we wszystkich trybach (edytor i test).

### Farba okrętu w buildzie (2026-11-10)

**Build:** `BattleSceneBuilder.BuildWindows` ze stanu drzewa z 13:40 (log `artifacts-log/crew-build2.log`): Build Succeeded, 0 błędów.
- Shader `MorzaBogow/ShipPaint` jest w buildzie (2,3 MB, d3d11) z wariantami po wycinaniu: GBuffer 4, DepthOnly, ShadowCaster i MotionVectors po 1, ray tracing (GBufferDXR 16, IndirectDXR 16, ForwardDXR 4, VisibilityDXR 2, PathTracingDXR 8). Przebieg Forward wycina profil HDRP (tylko deferred), jak dla HDRP/Lit.
- W buildzie są `ShipPaint.mat` i wszystkie 36 tekstur: 24 × 341,5 KB (512²) i 12 × 85,5 KB (256²), razem ok. 9,2 MB, zgodnie z oszacowaniem.

**Uruchomienie:** `Builds/M1/MorzaBogow.exe -force-device-index 0 -morzaProfile Ultra -morzaSmokeReport … 90 -morzaSmokeShots …`, 1920×1080, RTX 3050 Ti Laptop.
- Raport: `lookShader` = `MorzaBogow/ShipPaint`, `lookShaderSupported` = `true`, 31 materiałów i 39 rendererów wyglądu dla 16 okrętów.
- Player.log bez komunikatów o nieobsługiwanym shaderze ani brakujących wariantach.
- Zrzuty (kuter z bliska, flota przy porcie) pokazują okręty w farbach wyglądu, bez różowego i czarnego materiału: smoła, ochra, drewno pokładu, pasy płótna na żaglu.
- Czasu GPU nie porównuję z A/B powyżej: build zawiera też nowe morze i załogi z innych sesji, a w tle działały edytory innych sesji.

### A/B w buildzie (2026-11-10)

**Warunki:**
- `Builds/M1/MorzaBogow.exe -morzaSmokeReport … 90`, z flagą `-morzaPlainShips` i bez niej;
- ten sam build i ta sama mapa, profil Ultra, 1920×1080, pełny ekran w oknie;
- RTX 3050 Ti Laptop;
- w trakcie działały edytory Unity innych sesji: 1 przy przebiegu z wariantami, 2 bez wariantów.

| Przebieg | Okręty | Renderery wyglądu | Materiały wyglądu | Średnia klatka | Najgorsza klatka | CPU główny | CPU renderowanie | GPU | Prezentacja |
|---|---|---|---|---|---|---|---|---|---|
| z wariantami | 16 | 53 | 32 | 42,7 ms | 7850 ms | 13,75 ms | 6,78 ms | 37,49 ms | 1,44 ms |
| bez wariantów | 16 | 16 (sam pas burty) | 0 | 39,1 ms | 2600 ms | 10,63 ms | 7,02 ms | 36,65 ms | 1,44 ms |

**Wnioski:**
- **GPU** z wariantami: +0,84 ms (+2,3%).
- **Prezentacja** (praca na klatkę po stronie gry): bez różnicy, bo wygląd niczego nie liczy co klatkę.
- **Średnia klatka i CPU** nie nadają się do porównania. W przebiegu z wariantami była jednorazowa przerwa 7,8 s, a obciążenie tłem przez inne edytory było różne.
- **Wymagane dalej:** czysty pomiar bez innych procesów oraz w późniejszej fazie meczu, przy większej liczbie okrętów w kadrze. Ten przebieg trwał 90 s, kadr pokazywał 16 okrętów, nie było jeszcze walki.

## Cykl życia i pula

**Pula:** widoki okrętów nie są pulowane, bo powstają rzadko: przy budowie, przejęciu i wczytaniu. Warstwa wyglądu jest gotowa na pulę.
- `ApplyLook` zdejmuje tylko elementy wyglądu i nakłada nowy opis, razem z jego wartością renderera na wszystkich częściach okrętu. Uszkodzenia i barwa właściciela zostają.
- `ResetForReuse` przygotowuje widok dla innego okrętu tej samej klasy. Nie zostawia wyłomów, strzał, rozdarć, świeżych napraw, złamań, opuszczonego żagla, piany, znaczników, barw, wyglądu ani zużycia poprzedniej jednostki.
- Elementy zdejmowane wychodzą z hierarchii od razu, a własne siatki zwalnia `OnDestroy`.
- Części rysowane poza widokiem okrętu (rozpad kadłuba w `BattleEffects`) dostaje się przez `ShipVisual.AttachPiece`: farby i wartość renderera okrętu.

**Zdobycie okrętu:** żagiel opada i wraca w barwie nowej strony, w tym samym płótnie i z łatami w tych samych miejscach. Warsztat, drewno, ślady, wyposażenie i zużycie się nie zmieniają.

**Naprawa w doku:** zdjęte wyłomy zostawiają świeże, niesmołowane deski (stan, najwyżej 3, zużycie 0). Wygląd nie losuje się od nowa.

## Testy

| Wymaganie | Test | Wynik 2026-11-10 |
|---|---|---|
| A. ten sam id, ten sam wariant | `ShipVariantRulesTests.Looks_SameIdGivesTheSameLook` | PASS |
| B. kolejność tworzenia bez znaczenia | `Looks_CreationOrderDoesNotChangeAnyShipsLook` | PASS |
| C. bez RNG gry i Unity | `Looks_UseNeitherTheGamesNorUnitysRandom` | PASS |
| D. zapis i wczytanie | `ShipVariantTests.Variant_SaveAndLoadKeepEveryShipsLook`, `Looks_SavedDescriptionSurvivesALibraryInAnotherOrder` | PASS |
| E. ponowne użycie bez śladów poprzedniej jednostki | `Variant_ReapplyingLeavesNothingOfThePreviousShip` | PASS |
| F. nowy właściciel zmienia tylko barwy | `Variant_ANewOwnerChangesOnlyTheColours` | PASS |
| G. naprawa i HP nie losują wyglądu | `Variant_RepairAndDamageNeverRerollTheLook` | PASS |
| H. statystyki, kolizje i punkty interakcji bez zmian | `Variant_ChangesNoStatsPointsOrColliders` | PASS |
| I. wyposażenie poza przestrzenią załogi | `Looks_GearKeepsClearOfCrewStationsPassagesOarsAndPosts`: niezależna geometria widoku, wszystkie klasy, 4 obroty | PASS |
| J. LOD zachowuje istotne cechy | `Variant_FarAwayKeepsTheEssentialLook` (konfiguracja LOD i materiały, nie obraz) | PASS |
| K. flota różnorodna i spójna | `Looks_AFleetDiffersShipToShipYetKeepsItsWorkshopAndColours` | PASS |
| L. budżet | `Variant_StaysInItsMeasuredBudget` oraz `ShipLookBenchmark` | PASS |
| limity, wygląd to nie uszkodzenie | `Looks_StayWithinTheirLimitsAndNeverLookLikeDamage` | PASS |
| UV, pas burty, tekstury | `GreekShipsTests.Ships_UvsRunAlongPlanksAndSpars`, `Ships_TheWaleBandIsItsOwnMeshInEveryApprovedPattern`, `Textures_BakerIsDeterministicAndImportSettingsFitTheirUse` | PASS |
| PBR: mapy z bakera, import jako dane | `ShipPaintRulesTests.Textures_PbrMapsAreBakedAndImportedAsData` (24 pliki = baker bajt w bajt; normal map, sRGB off, alfa maski z pliku, zawijanie, rozmiar, Resources) | PASS |
| PBR: pakowanie HDRP Lit, kafelkowanie | `Textures_MasksAndNormalsFollowTheHdrpLitPackingAndTile` (R = 0, zakresy G/B/A, normalne jednostkowe, krawędź kafla nie większa niż szew wewnątrz) | PASS |
| wartość renderera: układ bitów | `Surface_ValuePacksWearAndKeepsWetnessAndSootReserved` | PASS |
| zużycie tylko z wyglądu | `Surface_WearFollowsTheFinishFromTheLookAlone` (zakresy wykończeń, zapis i wczytanie, bez `UnityEngine.Random`) | PASS |
| shader: kompilacja, mapy bez słów kluczowych, SRP Batcher | `Paint_TheGraphCompilesWithoutMapKeywordsAndKeepsTheSrpBatcher` (kod zgodności SRP Batchera 0 po jednym renderze) | PASS |
| materiały palety w buildzie | `Paint_PaletteMaterialsComeFromThePaintTheBuildCarries` (shader i słowa kluczowe materiału farby z `Resources`, tekstury z `Resources/ShipLook`) oraz log buildu | PASS |
| styczne siatek | `Ships_MeshesCarryTangentsForTheirNormalMaps` | PASS |
| wartość na każdym rendererze okrętu | `ShipPaintTests.Paint_EveryRendererOfAShipCarriesItsWear` (walka, naprawa w doku, przejęcie, rozpad, ponowne użycie) | PASS |
| shader czyta wartość renderera | `Paint_TheRendererValueChangesThePicture` (render: zużycie zmienia obraz o 0,071, dwa równe — 0, pełna wilgoć i sadza — 0) | PASS |
| pomiar wobec HDRP/Lit | `ShipLookBenchmark.Benchmark_LooksFromTheLitPalette`, `Benchmark_LooksFromTheLitPaletteWithMaps` | PASS |

Wyniki z 9ab5039: przebiegi batch w edytorze, EditMode 34/34 (zestawy wariantów, generatora i potoku) i PlayMode 98/98 (cały zestaw). Wyniki PBR (2026-11-10, przebiegi batch z filtrem): EditMode 22/22 (`ShipPaintRulesTests`, `ShipVariantRulesTests`, `GreekShipsTests`), PlayMode `ShipPaintTests` 2/2 oraz `ShipLookBenchmark`, `ShipVariantTests` i `ShipLookTests` 19/19. Bramkę M1 opisuje raport commita.

## Porównanie grupy

Taranowce z jednego modelu bazowego, w tym samym świetle podglądu edytora (nie w oświetleniu sceny gry):
- [ram_floty_kamera_strategiczna.png](warianty/ram_floty_kamera_strategiczna.png): 8 okrętów gracza i 8 przeciwnika z kamery strategicznej, kamera ortograficzna i LOD jak w grze;
- [ram_zblizenie.png](warianty/ram_zblizenie.png): trzy okręty gracza z bliska, ze wszystkimi detalami;
- [ram_bez_wariantow.png](warianty/ram_bez_wariantow.png): te same miejsca w wyglądzie bez wariantów, dla porównania;
- [ram_zblizenie_pbr.png](warianty/ram_zblizenie_pbr.png): to samo zbliżenie z farbą okrętu (2026-11-10): szwy i styki desek w normalnych i okluzji, pasy płótna. Zawiera też pomost nad wioślarzami i wiosła z rękojeścią z commitów załóg (b8ef3c5, 1a47397), więc nie wszystkie różnice wobec `ram_zblizenie.png` pochodzą z PBR.

Obrazy tworzy `-executeMethod MorzaBogow.Editor.AIAssets.AIAssetBatch.PreviewLooks -aiOut <folder> -aiShip Ram`.

## Ograniczenia

- **Tekstury:** kolor, normalne i maska z jednego bakera; brak map wysokości i szczegółu (detail map). Ślady to geometria, nie maski w shaderze. Odcienie bez tekstury (smoła, ochra, biel, len, kamień, terakota) mają płaską normalną.
- **Wilgotność i sadza:** pola w wartości renderera są zarezerwowane i odczytywane przez shader, ale bez wpływu na obraz, bo nie mają źródła (deszcz, bryzgi, ogień).
- **Ray tracing:** odbicia śledzone promieniami używają przebiegów DXR tej samej farby; czy widać w nich zużycie (wartość renderera w danych instancji DXR), nie sprawdzono.
- **Pomiary:** liczby pochodzą z edytora. Rozstrzygające jest A/B w buildzie (`-morzaSmokeReport` z `-morzaPlainShips` i bez) na tym laptopie.
- **Kampania z przenoszeniem floty (etap 8):** opis wyglądu musi przejść razem z okrętem.
- **Pochylnia:** okręt w budowie nie ma jeszcze id, więc pokazuje wygląd bez wariantów.
