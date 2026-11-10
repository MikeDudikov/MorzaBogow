# Załogi okrętów

Data: 2026-11-10. Tylko prezentacja; symulacja i zapis gry bez zmian.

**Stan.** Załogi są widoczne na wszystkich 7 klasach i biorą udział w ataku według danych gry. Commity: b8ef3c5, 1a47397, a0616a2, 3bf48db.

**Poza zakresem:**
- przejście przez relingi przy abordażu;
- nowe ciało dla postaci na brzegu i w stoczni;
- wypieczony LOD2;
- pomiar GPU na cichej maszynie.

Szczegóły w „Ograniczeniach”.

## Co jest widać

- **W rejsie** na każdej klasie widać obsadę ze stanu świata. Liczby obsady ze stanu świata, niczego nie dorysowujemy:
  - wioślarzy w jednym rytmie z wiosłami;
  - sternika przy rumplach dwóch wioseł sterowych;
  - obserwatora na dziobie;
  - dowódcę na rufie;
  - marynarzy przy gejtawach i linach;
  - obsługę stanowisk;
  - wojowników siedzących w osi pokładu.
- **W ataku** walczą tylko ci, których broń istnieje w danych (`Ship.Mounts`, `World.Manned`):
  - **łucznicy i oszczepnicy banków:** naciąg albo zamach od chwili, gdy do strzału zostaje 0,6 s (0,5 s dla oszczepu), według postępu przeładowania i tempa załogi. Zwalniają w ticku `weapon_fired`, a pociski startują z ich rąk;
  - **obsługa machin:** napina lub ściąga ramię w czasie przeładowania, ładuje i celuje. Machina obraca się w łuku broni do celu;
  - **taran:** sprint wioseł w szarży, zaparcie się wojowników w zapowiedzi, wstrząs w ticku `ram_hit`, wiosłowanie wstecz przy odbiciu.
- **Abordaż:**
  - front wojowników od strony drugiego okrętu, najwyżej tylu, ilu liczy front symulacji (`AttackerFront`, `TargetFront`);
  - na galerze front stoi na pomoście nad wioślarzami: w każdym rzędzie miejsce najbliżej tej burty. Wojownik, który już tam stoi w gotowości, tylko się odwraca;
  - haki przyciągają kadłub atakującego do burty celu. To przesunięcie wyłącznie obrazu (`CrewDirector.HaulOffset`): narasta przez 3 s haków i wraca w 4 s po abordażu; trafienia w ten kadłub dostają to samo przesunięcie.
- **Pomost i galerie.** Galery mają wąski pomost wzdłuż osi nad wioślarzami (`GreekShips.WalkwayRise`, 0,35 m nad burtą). Wioślarze siedzą w galeriach przy burtach pod nim, tak jak na kraterze z Kynos i w Medinet Habu (interpretacja, `HistoricalConstraints.md`). Na pomoście stoją wojownicy, obsługa stanowisk i marynarze.
- **Zatonięcie.** Ludzie skaczą za burtę po swojej stronie w ciągu 0,6 s i znikają w wodzie po 1,2 s, zanim kadłub się rozpadnie.
- **Bez broni** (Merchant, Cutter): żadnej roli bojowej; reakcje na trafienie i zagrożenie.
- **Pomiar A/B.** Flaga `-morzaNoCrew` uruchamia ten sam build bez załóg. Koszt załóg trafia do `Player.log` przy wyjściu.
- **Zrzuty załóg.** Flaga `-morzaCrewShowcase <prefiks>` (`BattleController.CrewShowcase.cs`) daje zrzuty, a log zapisuje liczbę postaci, LOD i wywołań rysowania przy każdym zrzucie:
  - każda klasa z bliska w rejsie;
  - taran, salwa łuczników, oszczepy, machiny i abordaż;
  - flota z kamery strategicznej.

  Zwykły `-morzaShowcase` innych sesji zostaje bez zmian.
- **Z daleka** (LOD1 i LOD2, 3–15 px na człowieka) odcienie się scalają (`CrewAboard.Coarse`):
  - 2 odcienie skóry;
  - 2 odcienie włosów;
  - 3 tkaniny niebarwione;
  - barwy stron bez zużytych wersji.

  Hełmy, tarcze, budowa i barwy stron zostają. Z bliska (LOD0) widać pełną różnorodność.
- **Tylko załogi w kadrze** decydują, co robi każdy człowiek. Pozostałe załogi utrzymują tylko stanowiska i wiosła swojego okrętu.
- **Cele stanowisk** obsługa szuka raz na tick symulacji. Test mgły (`World.CanSee`) przegląda wszystkie okręty i porty, więc pada dopiero dla celu w zasięgu i sektorze. Machiny obracają się w każdej klatce.

## Architektura

```
World (symulacja, 30 Hz) ──► BattleController.StepOnce ──► Ticked(World)   [zdarzenia tego ticku, raz na EventId]
                                                              │
                              CrewDirector.OnTicked ◄─────────┘  liczebności, upadki, wystrzały, taran, pociski→stanowisko
                                                              
BattleController.PresentFrame (kadłuby: poza z wody, Animate)
CrewDirector.LateUpdate ──► CrewAboard.Update (rola → czynność ze stanu okrętu)
                         ──► CrewPoseJob (Burst: pozy, IK dłoni i stóp, macierze kości i gniazd)
                         ──► CrewRenderer (Burst: macierze części, Graphics.RenderMeshInstanced ≤ 511 na wywołanie)
BattleEffects.SyncMissiles ──► CrewDirector.LaunchPoint (dłoń strzelca / wylot machiny)
```

- **Rozdział warstw.** Stan logiczny (rola, stanowisko, czynność) → miejsce na okręcie (stacja w przestrzeni modelu) → ruch pokładu (`view.Model`: nurzanie, przechył i kiwanie z wody) → poza (klip parametryczny) → korekty IK. Postać nie ma własnego kołysania.
- **Obsada** (`CrewManning`) odtwarza kolejność symulacji:
  - banki dostają po 4 ludzi swojego zawodu;
  - machiny dostają kolejno szermierzy, oszczepników i łuczników;
  - marynarze nie obsadzają stanowisk.

  Postaci danego zawodu nigdy nie jest więcej niż ludzi tego zawodu.
- **Tożsamość.** `CrewRoster` nadaje każdemu człowiekowi trwały numer na okręcie. Wygląd (`CrewLooks`) wynika z haszu numeru okrętu i numeru człowieka, bez `GetInstanceID` i bez losowości rozgrywki. Po wczytaniu numery liczą się od zera w każdym zawodzie.
- **Czas.** Animacje liczą czas gry (`Paused ? 0 : unscaledDeltaTime × Speed`). Woda i kadłuby ruszają się w czasie rzeczywistym, więc przy pauzie postacie stoją na falującym pokładzie.

## Pliki

| Plik | Zawartość |
|---|---|
| `Runtime/Presentation/Crew/CrewRoles.cs` | role prezentacji, zawody gry, funkcja historyczna, pewność; `docs/art/CrewRoles.json` |
| `Runtime/Presentation/Crew/CrewLayout.cs` | `CrewLayoutProfile`: stanowiska z geometrii `GreekShips`, walidacja miejsca |
| `Runtime/Presentation/Crew/CrewManning.cs` | `CrewRoster`, `CrewManning.Assign` |
| `Runtime/Presentation/Crew/CrewCoverage.cs` | macierz klas: `docs/art/ShipCrewCoverage.json` |
| `Runtime/Presentation/Crew/CrewBody.cs` | rig 17 kości, paleta (atlas), siatki części w trzech LOD |
| `Runtime/Presentation/Crew/CrewLooks.cs` | wygląd z tożsamości |
| `Runtime/Presentation/Crew/CrewPose.cs` | czynności, pozy parametryczne, IK, zadanie Burst |
| `Runtime/Presentation/Crew/CrewRenderer.cs` | instancjonowane rysowanie, zadanie macierzy części |
| `Runtime/Presentation/Crew/CrewDirector.cs` | zdarzenia z ticków, LOD, klatka |
| `Runtime/Presentation/Crew/CrewAboard.cs` | załoga jednego okrętu: obsada, czynności, upadki, abordaż |
| `Runtime/Presentation/Crew/RowingStroke.cs` | jeden rytm wioseł i wioślarzy |
| `Runtime/Presentation/BattleController.CrewShowcase.cs` | zrzuty załóg (`-morzaCrewShowcase`) |
| `Editor/CrewAssetBuilder.cs` | materiał `Resources/Crew/CrewBody.mat` (instancing), atlas, prefaby podglądu ról |
| `Editor/CrewExport.cs` | zapis macierzy i manifestu ról |

**Zmiany w plikach innych sesji** (commit b8ef3c5 i dalsze, tylko własne fragmenty):
- `GreekShips`:
  - dulki, części wewnętrzne wioseł, wiosła sterowe z rumplem, stanowiska sterników;
  - pomost nad wioślarzami (`WalkwaySpan`, `WalkwayHalfWidth`, `FloorAt`);
  - stanowiska broni na pomoście lub platformie;
- `ShipVisual`: jeden rytm wioseł, `SetOarCrewed`, `SetStroke`, `OarPivot`, ster;
- `BattleEffects`: `LaunchPoint`, salwy banków w czasie lotu;
- `BattleController`:
  - `Ticked`, `CrewDirector`, czyszczenie efektów przy wczytaniu;
  - `DeckFightersShown` z frontów prawdziwych załóg (postacie zastępcze `DeckFight` tylko bez `CrewDirector`);
  - przesunięcie przyciągniętego kadłuba w `PresentFrame` i `ShotStruck`.

## Instrukcja integracji

1. **Materiał.** Po zmianie palety albo kształtów: `-executeMethod MorzaBogow.Editor.CrewAssetBuilder.Run` (materiał, atlas, prefaby podglądu) i `-executeMethod MorzaBogow.Editor.CrewExport.Run` (macierz i manifest). Test `Coverage_TheMatrixFileIsTheGameData` pilnuje zgodności plików z kodem.
2. **Nowa klasa okrętu.** Forma w `GreekShips.Form`, wiosła i ster w `BuildOars` i `BuildSteering`, potem `CrewLayoutProfile` liczy stanowiska sam. Testy `CrewGeometryTests` i `Layout_*` sprawdzają miejsce, zasięg dłoni i przenikanie.
3. **Nowa broń.** Typ w `WeaponCatalog`. Miejsca obsługi w `CrewLayoutProfile.PostPlaces`, czynności w `CrewAboard.Machine` albo `Archer` i `Javelin`. Obsadę wyznacza zawsze `World.Manned`.
4. **Nowa czynność.** Wartość w `CrewAction` i poza w `CrewPose.Evaluate`. Dłonie na przedmiocie wymagają celu IK w `CrewAboard.Build`.
5. **Nie wolno:**
   - naliczać obrażeń ani tworzyć pocisków po stronie obrazu;
   - zmieniać liczebności;
   - losować śmierci bez spadku liczebności w ticku trafienia albo rundy abordażu.

## Pomiary i walidacja

Wyniki testów i pomiarów dopisuję tutaj po każdym etapie (sprzęt pomiarowy: laptop i7-11390H, RTX 3050 Ti Laptop, 16 GB; sprzęt docelowy użytkownika i7, 32 GB, RTX 5060 nie był dostępny).

**Testy w PlayMode.** Bitwa jest wstrzymana, żeby jej klatki nie dokładały ticków między krokami testu. Wstrzymana klatka nie daje widokom czasu gry, więc pomocnik `Tick` w `CrewPresentationTests` daje każdemu widokowi czas ticku, tak jak klatka działającej gry. Bez tego wiosła nigdy nie wychodziły i wioślarze siedzieli.

**Benchmark floty w edytorze** (`Crew_J`, informacyjny):
- bez kamery, czyli wszystkie załogi na jednym poziomie szczegółów i bez odrzucania;
- czas obejmuje decyzje, pozy, macierze części i zlecenie rysowania.

| Flota | Postacie | Części | Wywołania rysowania | CPU na klatkę (edytor) |
|---|---|---|---|---|
| 100 okrętów | 3951 | 54 171 | 335 | 19,75 ms |
| 200 okrętów | 7913 | 108 521 | 419 | 61,02 ms |
| 100 okrętów, odcienie scalone z daleka | 3951 | 54 171 | 177 | 19,58 ms |
| 200 okrętów, odcienie scalone z daleka | 7913 | 108 521 | 289 | 70,05 ms |

- **Wywołania rysowania.** Scalenie odcieni z daleka zmniejszyło ich liczbę o 47% (100 okrętów) i o 31% (200 okrętów).
- **CPU w edytorze** się nie zmienił, więc ten czas idzie gdzie indziej niż na liczbę siatek. Różnica 61 → 70 ms przy 200 okrętach to szum: obok działały edytory innych sesji.
- **Wiarygodny pomiar** daje dopiero build z kamerą gry (odrzucanie i LOD).
- **Pełny pomiar** z `-morzaBenchmark`. Bez tej flagi bramka rysuje tylko 20 okrętów raz.

Po rozbiciu czasu na etapy (`CrewDirector.Timings`) poprawiłem szukanie celów i budowę postaci. Ten sam benchmark:

| Flota | Razem | Decyzje | Budowa postaci | Pozy | Lista części | Macierze i zlecenie rysowania |
|---|---|---|---|---|---|---|
| 100 okrętów, przed | 22,48 ms | 5,56 | 9,11 | 0,82 | 0,48 | 6,08 |
| 100 okrętów, po | 16,78 ms | 2,63 | 6,36 | 0,80 | 0,49 | 6,11 |
| 200 okrętów, przed | 70,43 ms | 32,69 | 16,45 | 1,60 | 1,29 | 17,62 |
| 200 okrętów, po | 38,53 ms | 4,79 | 12,31 | 1,56 | 1,20 | 17,85 |

Decyzje rosły kwadratowo: test mgły przeglądał wszystkie okręty dla każdego stanowiska w każdej klatce. Teraz rosną liniowo.

**Build Windows** (`Builds/M1`, 2026-11-10 13:40):
- warunki: profil Ultra, 1920×1080, pełny ekran w oknie, RTX 3050 Ti Laptop (i7-11390H, 16 GB);
- w czasie przebiegów nie działał edytor Unity innych sesji, tylko usługa Unity Hub.

Koszt załóg na głównym wątku mierzy sama gra (decyzje, pozy, macierze, zlecenie rysowania), średnio na klatkę:

| Przebieg | Klatki | Średnio postaci w kadrze | Wywołania rysowania | CPU główny załóg |
|---|---|---|---|---|
| test dymny 90 s (początek meczu, kamera domyślna) | 1569 | 24 | 97 | 0,532 ms |
| zrzuty załóg (głównie zbliżenia LOD0, na końcu flota z daleka) | 1757 | 87 | 176 | 0,881 ms |

Wybrane ujęcia (log `[CrewShowcase]`; „pozy” to decyzje, budowa, pozy i lista, bez zlecenia rysowania):

| Ujęcie | Postacie / okręty w świecie | LOD | Części | Wywołania | Pozy |
|---|---|---|---|---|---|
| rejs Boarder | 90 / 8 | 0 | 1901 | 215 | 0,32 ms |
| taran | 87 / 13 | 0 | 1811 | 243 | 0,32 ms |
| oszczepy | 123 / 17 | 0 | 2581 | 253 | 0,58 ms |
| abordaż | 109 / 22 | 0 | 2274 | 270 | 1,04 ms |
| flota z daleka | 266 / 28 | 1 | 3626 | 94 | 0,74 ms |

Pomiar A/B (`-morzaSmokeReport … 90`, z `-morzaNoCrew` i bez) **jest nierozstrzygający**:

| Przebieg | Średnia klatka | CPU główny | CPU renderowanie | GPU | Symulacja | Ticki w 90 s |
|---|---|---|---|---|---|---|
| z załogami | 54,5 ms | 15,70 ms | 6,46 ms | 48,64 ms | 2,63 ms | 4869 |
| bez załóg | 53,9 ms | 27,03 ms | 10,28 ms | 38,66 ms | 5,22 ms | 4114 |

- Przebieg bez załóg miał dwa razy dłuższą symulację, która od załóg nie zależy, i mniej ticków. Laptop był wtedy obciążony inaczej: to drugi z rzędu przebieg po 5 minutach pełnego obciążenia, prawdopodobnie dławienie temperaturowe.
- Różnicy GPU (+10 ms przy 24 postaciach) nie przypisuję załogom.
- Wymagany dalej: pomiar ABBA na cichej maszynie i w scenie bitwy, przy 40 okrętach w kadrze.

**Budżet** (cel: do 1,0 ms CPU i 1,0 ms GPU przy 40 widocznych okrętach):
- W zmierzonych scenach (do ok. 270 postaci w kadrze) CPU głównego wątku załóg wynosi 0,5–0,9 ms.
- Pełna bitwa z 40 okrętami w kadrze (ok. 1500 postaci) przekroczy 1 ms. Z liniowego wzrostu w benchmarku wychodzi szacunkowo 2–4 ms. Głównym kosztem będzie wtedy budowa postaci i zlecenie rysowania.
- Koszt GPU załóg jest niezmierzony.
- Sprzęt docelowy (i7, RTX 5060) nie był dostępny.

**Zrzuty załóg** (`-morzaCrewShowcase`, build 13:40) leżą w `artifacts-log/crew-shots/`, poza repozytorium:
- `zaloga-01…07`: rejs każdej klasy z bliska;
- `08` taran, `09` salwa, `10` oszczepy, `11` machiny, `12` abordaż;
- `13` flota z kamery strategicznej.

Z bliska widać wioślarzy przy wiosłach w jednym rytmie i ludzi w barwie strony na pomoście i platformach. Z daleka (LOD1, kilka pikseli na człowieka) załoga czyta się jako ciemne rzędy na pokładzie i ruch wioseł.

## Ograniczenia

- **Ray tracing.** Załogi rysowane przez `Graphics.RenderMeshInstanced` nie trafiają do odbić ray tracingu (profil Ultra).
- **Tożsamość po wczytaniu.** Zapis gry nie ma numerów ludzi, więc po wczytaniu numeracja w każdym zawodzie liczy się od zera. Wygląd jest stały dla danego zapisu, ale może różnić się od wyglądu sprzed zapisu.
- **Postacie poza okrętami.** Partie na brzegu i robotnicy stoczni (`CrewFigure`) mają jeszcze dawne postacie z prymitywów.
- **Front abordażu.** Na wąskim pomoście Ram front stoi w osi, czyli dalej od burty niż na szerokich galerach. Liczba miejsc na burtę ogranicza front obrazu, nie symulacji.
- **Bez przejścia na drugi pokład.** Fronty walczą przez relingi ze swoich pokładów. Przejścia przez dwa relingi z kontaktem dłoni i stóp (IK) nie zrobiłem. Symulacja i tak nie przenosi ludzi między okrętami przed zdobyciem.
- **Przejścia między stanowiskami** idą po prostej z interpolacją wysokości, nie po grafie pokładu. Przy krótkich przejściach (na reling, z ławy na pomost) przenikanie nie jest widoczne z kamery gry. Z bliska może być.
- **LOD2** to te same części co LOD1 z mniejszą liczbą ścian, nie wypieczona bryła całej postaci.
