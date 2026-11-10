# Załogi okrętów

Data: 2026-11-10. Stan: wdrożenie etapami (plan w sesji „Załogi okrętów”), tylko prezentacja; symulacja bez zmian.

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

Wiarygodny jest dopiero pomiar w buildzie z kamerą gry (odrzucanie i LOD).

## Ograniczenia

- **Ray tracing.** Załogi rysowane przez `Graphics.RenderMeshInstanced` nie trafiają do odbić ray tracingu (profil Ultra).
- **Tożsamość po wczytaniu.** Zapis gry nie ma numerów ludzi, więc po wczytaniu numeracja w każdym zawodzie liczy się od zera. Wygląd jest stały dla danego zapisu, ale może różnić się od wyglądu sprzed zapisu.
- **Postacie poza okrętami.** Partie na brzegu i robotnicy stoczni (`CrewFigure`) mają jeszcze dawne postacie z prymitywów.
- **Front abordażu.** Na wąskim pomoście Ram front stoi w osi, czyli dalej od burty niż na szerokich galerach. Liczba miejsc na burtę ogranicza front obrazu, nie symulacji.
