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

`ShipVisual` jest jedynym właścicielem rendererów i materiałów okrętu. Nie ma osobnego, konkurencyjnego kontrolera. Warstwy składa zawsze w tej samej kolejności:
1. typ;
2. wygląd: raz, przy tworzeniu albo przez jawne `ApplyLook`;
3. stan: zdarzenia i `SetCondition`;
4. barwa właściciela: `SetOwner`.

Wilgotność i nadpalenie mają zarezerwowane miejsce przed barwą właściciela. Nie są wdrożone, bo projekt nie ma ich źródła: nie ma ognia ani mokrych kadłubów. Nic z wyglądu nie liczy się co klatkę.

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
  - tworzy się go raz, jako klon materiału sceny dla danej farby, i współdzielą go wszystkie okręty z tym odcieniem;
  - nie ma materiału na okręt ani na klatkę, ani wywołań `renderer.material`;
  - dla 40 okrętów wszystkich klas powstało 36 materiałów, limit wynosi 48.
- **Siatki:**
  - typ korzysta ze wspólnych siatek;
  - na okręt przypadają najwyżej trzy małe siatki własne: dawne naprawy, przetarcia i łaty żagla; świeże naprawy z bitwy dochodzą do nich jako stan;
  - kadłub nie jest skalowany ani podmieniany.
- **Tekstury** (`Editor/ShipTextureBaker`, deterministyczne, bez pobierania):
  - słoje 4 × 512², płótno 4 × 512², przetarta smoła 2 × 256², lina i wiklina po 256²;
  - format sRGB z mipmapami;
  - przetarcia mają zawijanie `Clamp`, reszta `Repeat`;
  - kompresja wysokiej jakości (na Windows BC7).
  - Pamięć to ok. 3 MB. Liczba wynika z rozmiarów i formatu, nie została zmierzona.
- **UV w metrach** (generator): deski i pasy poszycia biegną wzdłuż kadłuba, drzewca wzdłuż osi, a płótno ma u w metrach i v od 0 do 1. Kierunku słojów i splotu nic nie obraca.
- **Tylko mapa koloru bazowego.** Normal i mask map wymagają słów kluczowych shadera, które build mógłby wyciąć. Odkładam je do biblioteki PBR, razem z ewentualnym shaderem na `Renderer.SetShaderUserValue`.

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
- `ApplyLook` zdejmuje tylko elementy wyglądu i nakłada nowy opis. Uszkodzenia i barwa właściciela zostają.
- `ResetForReuse` przygotowuje widok dla innego okrętu tej samej klasy. Nie zostawia wyłomów, strzał, rozdarć, świeżych napraw, złamań, opuszczonego żagla, piany, znaczników, barw ani wyglądu poprzedniej jednostki.
- Elementy zdejmowane wychodzą z hierarchii od razu, a własne siatki zwalnia `OnDestroy`.

**Zdobycie okrętu:** żagiel opada i wraca w barwie nowej strony, w tym samym płótnie i z łatami w tych samych miejscach. Warsztat, drewno, ślady i wyposażenie się nie zmieniają.

**Naprawa w doku:** zdjęte wyłomy zostawiają świeże, niesmołowane deski (stan, najwyżej 3). Wygląd nie losuje się od nowa.

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

Wyniki pochodzą z przebiegów batch w edytorze: EditMode 34/34 (zestawy wariantów, generatora i potoku) i PlayMode 98/98 (cały zestaw). Bramkę M1 opisuje raport commita.

## Porównanie grupy

Taranowce z jednego modelu bazowego, w tym samym świetle podglądu edytora (nie w oświetleniu sceny gry):
- [ram_floty_kamera_strategiczna.png](warianty/ram_floty_kamera_strategiczna.png): 8 okrętów gracza i 8 przeciwnika z kamery strategicznej, kamera ortograficzna i LOD jak w grze;
- [ram_zblizenie.png](warianty/ram_zblizenie.png): trzy okręty gracza z bliska, ze wszystkimi detalami;
- [ram_bez_wariantow.png](warianty/ram_bez_wariantow.png): te same miejsca w wyglądzie bez wariantów, dla porównania.

Obrazy tworzy `-executeMethod MorzaBogow.Editor.AIAssets.AIAssetBatch.PreviewLooks -aiOut <folder> -aiShip Ram`.

## Ograniczenia

- **Tekstury:** tylko kolor bazowy, bez normal i mask map. Ślady to geometria, nie maski w shaderze.
- **Wilgotność i nadpalenie:** niewdrożone, bo nie mają źródła.
- **Pomiary:** liczby pochodzą z edytora. Rozstrzygające jest A/B w buildzie (`-morzaSmokeReport` z `-morzaPlainShips` i bez) na tym laptopie.
- **Kampania z przenoszeniem floty (etap 8):** opis wyglądu musi przejść razem z okrętem.
- **Pochylnia:** okręt w budowie nie ma jeszcze id, więc pokazuje wygląd bez wariantów.
