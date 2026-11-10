# Zniszczenia budowli i okrętów

Data: 2026-11-10. Najpierw pionowy wycinek z polecenia („jedna budowla i jeden okręt kompletnie przed produkcją masową”), potem rozszerzenie na port:
- **budowla wycinka:** Szkoła Mędrców (`PortBuildings.School.cs`, `Destruction/*`);
- **okręt wycinka:** złamanie masztu na każdej klasie z masztem (`ShipVisual.Mast.cs`), obok istniejących wyłomów, strzał, rozdarć żagla, złamanych stanowisk, wygiętego taranu i tonięcia (`ShipFate`);
- **produkcja (po wycinku):** te same zasady dla magazynu i jego domu, czterech domów miasta, warsztatu i murów z wieżami (`PortBuildings.Breakables.cs`). Dok nadal pada w całości w swoją dawną ruinę.

Źródła i poziom pewności: [HistoricalConstraints.md](HistoricalConstraints.md#zniszczenia-budowli-i-okrętów-pionowy-wycinek-destructionmd) i [ReferenceRegister.json](ReferenceRegister.json) (`R-DRUM-JOINTS`, `R-OLYMPIA-QUAKE`, `R-MUDBRICK-DECAY`, `R-SAILS-ASHORE`, `R-KYRENIA-MASTSTEP`). Stan zadań: [ArtProductionManifest.json](ArtProductionManifest.json) (`Tools/visual/production_manifest.py`).

## Zasady

- **Stan gry jest jedynym źródłem.** Etap zniszczenia wynika wyłącznie z kadłuba w symulacji (`Settlement.Hull / MaxHull`, `Ship.Hull / Definition.Hull`). Trafienia (`projectile_hit_settlement`) wybierają tylko miejsce, nie ilość.
- **Prezentacja tylko czyta.** Gruz nie zadaje obrażeń, nie blokuje nawigacji, nie przesuwa okrętów i nie zmienia wyniku. Sprawdzają to `Visual_DoesNotChangeSimulationResult` i `PortViews_LeaveTheSimulationUntouched`.
- **Bez silnika fizyki.** Upadki są kinematyczne, w czasie gry (pauza je zatrzymuje). Ten sam element padający w tę samą stronę zawsze ląduje w tym samym miejscu, widziany czy nie. Nie ma mas ani dżuli; czasy upadku są dobrane wzrokowo (nie twierdzimy, że to trajektorie fizyczne).
- **Losowość deterministyczna.** `DestructionHash` (FNV-1a + SplitMix64) z identyfikatorów osady, elementu i zdarzenia. Bez generatora gry, bez `UnityEngine.Random`, bez numerów instancji, klatki i zegara.
- **Woda poza zakresem.** Żadna zmiana w shaderach, materiałach, parametrach, symulacji i interfejsach wody. Wywoływany jest tylko istniejący efekt `BattleEffects.WaterColumn`. `water_guard check`: 76 części bez zmian.
- **Bez ognia.** Gra nie ma zdarzenia ognia, więc nowe ruiny nie mają zwęgleń.

## Etapy (`DamagePlan`)

| Etap | Udział utraconego kadłuba | Budowla (Szkoła) | Okręt |
|---|---|---|---|
| `Intact` | < 8% | jedna siatka jak dotąd | bez śladów |
| `SurfaceDamage` | 8–30% | rysy przy trafieniach (do 18) | wyłomy, strzały (istniejące) |
| `LocalFailure` | 30–60% | 1–3 awarie własne i to, co na nich spoczywa | wyłomy, przechył, rozdarty żagiel od 50% |
| `PartialCollapse` | 60–100% | 4–9 awarii własnych | **maszt łamie się** (poniżej 40% kadłuba) |
| `WreckOrRuin` | osada zniszczona / `ship_sunk` | wszystko pada, w czasie gry ≤ 2,6 s | `ShipFate`: osiadanie, rozpad, wrak |
| `Settled` | po upadku | kikuty, rzędy bębnów, bloki, sterty, kopce | — |

Ta sama wartość kadłuba daje zawsze ten sam etap i tę samą liczbę awarii własnych, niezależnie od liczby strzałów (test F).

### Rozkład uszkodzeń osady na budowle (`PortBuildings.ShareOut`)

Osada ma jeden kadłub, a stoi w niej do dziewięciu budowli. Każda dostaje własny poziom uszkodzenia:
- **bez zapisanych trafień:** każda ma poziom osady;
- **z trafieniami:** każde trafienie liczy się dla najbliższej budowli (do 12 m). Udział budowli `s` to jej część trafień, wygładzona o 0,3 / n. Poziom to `d · (1 + 0,75 · (n · s − 1))`, przycięty do 0–1. Gdzie padały pociski, budowla pęka mocniej, gdzie indziej słabiej (testy O, A).
- **bez cofania:** poziom budowli nigdy nie maleje, chyba że kadłub osady rośnie (rozbudowa murów) albo port wstaje z ruin. Trafienia gdzie indziej nie podnoszą więc tego, co już leży (test P).
- **całość:** aż do upadku osady żadna budowla nie przechodzi w ruinę; najwyżej częściowe zawalenie (9 awarii własnych).

Przy upadku całego portu bryły w powietrzu dostaje najpierw Szkoła, potem miasto, warsztat i magazyn. Co przekracza budżet, od razu leży w obłoku pyłu.

## Model konstrukcyjny Szkoły

| Elementy | Liczba | Materiał | Rola | Podparcie | Odłączenie | Połączenie |
|---|---|---|---|---|---|---|
| krepidoma (3 stopnie) | 1 | kamień | fundament | — | stały | na wyrównanej skale |
| cokół sali | 1 | kamień | fundament | — | stały | ortostaty na stylobacie |
| bębny kolumn | 16 × 5 | kamień tynkowany | nośny | bęben niżej / stylobat | przewrócenie | spoina łożyska z kołkiem (R-DRUM-JOINTS) |
| głowice | 16 | kamień tynkowany | nośny | górny bęben | przewrócenie (z kolumną) | spoina łożyska |
| architrawy | 16 | kamień tynkowany | belka | dwie głowice | zsunięcie | klamry do sąsiadów |
| fryzy z gzymsem | 16 | kamień tynkowany | belka | architraw | zsunięcie (z architrawem) | na architrawie |
| ściany sali | 8 | cegła suszona | ściana | cokół (nadproże: oba węgary) | osiadanie | warstwy cegły w zaprawie glinianej |
| drzwi | 1 | drewno | detal | oba węgary | zsunięcie | w ościeżnicy |
| przęsła dachu | 2 × 4 | ceramika i drewno | dach | fryzy pod okapem i ściana pod spadkiem | zapadnięcie | dachówki na krokwiach |
| frontony | 2 | kamień tynkowany | detal | fryzy krótkiego boku | zsunięcie | płyty tympanonu na gzymsie |

- **Pozostałe budowle portu** (`PortBuildings.Breakables`):

  | Budowla | Elementy | Materiał i odłączenie |
  |---|---|---|
  | dom (magazyn, dom, domy miasta, chata przyczółka) | cokół; ściany frontu przy drzwiach i nad nimi, tył (1–2), szczyty z trójkątem; drzwi z ościeżnicą; dach 1–2 przęsła na stronę na ścianie pod okapem i szczycie | cegła suszona (osiadanie), w magazynie kamień (osiadanie w gruz i bloki); dachówki (zapadnięcie) |
  | warsztat | sprzęt na ziemi (stały); 6 słupów; kamienny tylny mur; komin; dach 2 × 2 przęsła na słupach i murze | drewno (przewrócenie), kamień (osiadanie), dachówki |
  | mury miasta | skała (stała); odcinki kurtyny po ok. 2,4 m z blankami; 6 wież; czapy wież | ciosy (osiadanie w gruz i bloki), czapy (zsunięcie i sterta dachówek) |

- **Awarie własne** Szkoły mogą mieć tylko: jeden „słaby” bęben na kolumnę (stały dla kolumny z ziarna), ściany i przęsła dachu (32 kandydatów). W pozostałych budowlach awarie własne mają ściany, słupy, komin, odcinki muru, wieże i przęsła dachu. Reszta pada tylko razem z tym, na czym spoczywa (`StructureGraph.Cascade`). `Validate` pilnuje, że każdy element spoczywa na fundamencie.
- **Kolejność:** najpierw element najbliżej trafień (odległość od środka elementu, do 1,5 m wyrównania z ziarna).
- **Kierunek upadku:** na zewnątrz budowli (do środka stoi sala, w bok sąsiednie kolumny), odchylony do 30° w stronę, w którą leciał najbliższy pocisk, plus do 10° z ziarna. Bloki i bębny dziedziczą kierunek po tym, co je niosło.

## Upadki i gruz

| Sposób | Ruch | Spoczynek |
|---|---|---|
| przewrócenie (kolumna) | górna część od pękniętej spoiny jako jedna bryła, obrót wokół krawędzi spoiny, kwadratowo przyspieszający, 0,8–1,0 s | bębny i głowica w rzędzie wzdłuż kierunku upadku, na stopniu lub zboczu pod nimi, obrócone na bok (R-OLYMPIA-QUAKE) |
| zsunięcie (bloki, fronton, drzwi) | przechył o 22° wokół zewnętrznej dolnej krawędzi, potem spadek, 0,75 s; fryz jedzie na swoim architrawie | na licu, 0,5–2 m na zewnątrz, obrót 80–105° i do 14° w poziomie |
| osiadanie (cegła) | ściana obniża się do kikuta, 1,2 s, okruchy i pył dwa razy | postrzępiony kikut z tynkiem i nagą cegłą na górze, kopce cegły po obu stronach (R-MUDBRICK-DECAY) |
| zapadnięcie (dach) | przęsło opada wokół tego, co je trzyma (ściany, gdy padła kolumnada; okapu, gdy padła ściana), 0,7 s | sterta potłuczonych dachówek z trzema połamanymi krokwiami, na zewnątrz pod okapem albo w sali |

**Gruz hybrydowy:**
- **Bryły w locie:** kilka do kilkudziesięciu obiektów z puli, każdy z jedną siatką.
- **Spoczywające:** do `MaxChunks` = 12 kawałków osobno, potem scalane w jedną siatkę gruzu.
- **Cząstki według materiału** (Shuriken, własne materiały, nie materiał wody):
  - pył: kopia materiału bryzgów w kolorze wapiennego pyłu;
  - odpryski kamienia i płatki tynku;
  - okruchy cegły;
  - dachówki;
  - drzazgi.

  Wszystkie mają limit `maxParticles` na system.
- **Trafienia w port** (`Struck`) dają efekt materiału trafionego budynku: kamień, cegła, drewno albo ziemia. Zastępują dawne drzazgi drewna przy każdym trafieniu.

**Budżety** (statyczne pola, do zmiany):
- `StructureDamageView.MaxBodiesPerStructure` = 56 i `MaxBodies` = 112 brył w powietrzu naraz. Ponad budżet element od razu leży, w obłoku pyłu.
- Pełna ruina Szkoły w spoczynku: 3 renderery (stojące, rysy, gruz), ok. 20,4 tys. trójkątów.

**Pula:**
- Bryła wraca do puli ukryta, z pustą siatką, w pozycji zerowej.
- Drugie zawalenie nie tworzy nowych obiektów (test I).
- Widok okrętu z puli (`ResetForReuse`) stawia maszt.

## Zdarzenia i zapis

**`DestructionEvent`** (zdarzenie zniszczenia):
- identyfikator;
- obiekt (id osady);
- tick gry;
- miejsce i kierunek lotu w układzie osady;
- rodzaj pocisku (`arrow`, `javelin`, `bolt`, `shot`, `stone`);
- etap po trafieniu;
- ziarno prezentacji.

**`DestructionRecord`** na obiekt:
- ostatnie 24 trafienia i 48 zdarzeń;
- awarie własne w kolejności, z kierunkiem upadku;
- rysy.

**`DestructionBook`** zapisuje rekordy obok szybkiego zapisu (`<zapis>.destruction.json`, hash zapisu), wzorem `ShipLookBook`. Format `SaveGame` się nie zmienia.
- Po wczytaniu z rekordem: te same elementy leżą w tych samych miejscach i nic nie spada ponownie (testy H).
- Bez rekordu: ten sam etap i ta sama liczba awarii, ale wybrane od nowa.

## Naprawa, widoczność, wczytanie

- **Naprawa:** kadłub osady rośnie tylko przy rozbudowie murów (`World.Arsenal`). Wtedy wracają najpierw ostatnie awarie (LIFO), razem z ich rysami (test G). Okręt dostaje nowy maszt od 50% kadłuba (naprawa w doku).
- **Widoczność:** co padło poza zasięgiem wzroku gracza, gracz zastaje leżące (`PortBuildings.Unseen`; okręt: pierwsze `SetCondition` po przerwie). Upadek nie odtwarza się po fakcie.

## Okręt: maszt

**Złamanie:**
- **Kiedy:** poniżej 40% kadłuba (`MastBreaksBelow`).
- **Gdzie:** maszt pęka 0,75–1,5 m nad stępką. Tam jest trzymany nad pokładem; to interpretacja (R-KYRENIA-MASTSTEP).
- **Upadek:** maszt pada w 1,5 s gry na burtę przechyłu, czyli stronę wyłomów. Pada do kąta, przy którym jego szczyt dotyka wody.
- **Co idzie z masztem:** reja i żagiel. Żagiel wiotczeje.

**Wrak masztu:**
- Zostają kikut z drzazgami i dwie luźne liny (sztag, achtersztag) ze zwisem.
- Wrak jest częścią modelu kadłuba: płynie, obraca się i kołysze tylko z kadłubem. Nie ma własnej prędkości, więc ruch nie jest liczony podwójnie (test D).

**Efekty i dalszy los:**
- Drzazgi przy złamaniu i słup wody, gdy szczyt wpada do morza (`BattleController.MastEffects`, raz na zdarzenie).
- Przy zatonięciu `ShipFate` nie przewraca już leżącego masztu.

## Testy (A–M z polecenia)

| Test | Stan | Gdzie |
|---|---|---|
| A, zniszczenie lokalne | zrobione | `Destruction_A_ShotsBreakTheSideTheyStruck` |
| B, materiał | zrobione | `Destruction_B_EachMaterialBreaksItsOwnWay` (w tym: kolumny padają na zewnątrz) |
| C, podparcie | zrobione | `Destruction_C_WhatRestsOnAFailedColumnFallsWithIt` |
| D, gruz okrętu w ruchu | zrobione dla masztu | `Ship_TheFallenMastMovesWithTheHullNotOnItsOwn` |
| E, wiele trafień | zrobione | `Destruction_E_MoreDamageOnlyAddsToWhatFell` |
| F, kadłub i etapy | zrobione | `Destruction_F_TheHullAloneDecidesTheStageAndHowMuchFails` |
| G, naprawa | zrobione | `Destruction_G_…`, `Ship_ADockRepairStepsANewMast…` |
| H, zapis i wczytanie | zrobione | `Destruction_H_…` (EditMode), `Destruction_AShelledPortKeepsItsRuinsThroughASave` (PlayMode, z prawdziwym ostrzałem balisty) |
| I, pula | zrobione | `Destruction_I_…`, `Ship_ADockRepairStepsANewMastAndAReusedViewHasNoWreckage` |
| J, załoga | sprawdzone w kodzie, bez testu | stanowiska załogi stoją na pokładzie i nie sięgają do olinowania (`CrewLayout.AddMast`); ludzie nie wiszą w powietrzu, ale mogą stać tam, gdzie leży maszt |
| K, woda | zrobione | `water_guard check` (76 części bez zmian) |
| L, wydajność | zrobione | `Destruction_L_AFallStaysWithinItsBudgets`; pomiar w buildzie Windows i w edytorze niżej |
| M, wielka bitwa | zrobione dla budżetu | `Destruction_M_ManyPortsFallingAtOnceShareOneBudget` |
| rozszerzenie na port | zrobione | `Destruction_N_HousesTheWorkshopAndTheWallBreakToo`, `Destruction_O_TheShotsDrawTheDamageToWhatTheyStruck`, `Destruction_P_ShotsElsewhereNeverPutBackWhatFell`, `Destruction_Q_InSightTheBuildingsAreTakenApartAFewAFrame` |

## Pomiar w buildzie Windows (RTX 3050 Ti Laptop, 1920×1080)

`MorzaBogow.exe -morzaProfile Ultra|Low -morzaCollapseReport plik.json` (`BattleController.CollapseReport`). Na wolnym brzegu obok portu macierzystego powstaje inscenizowane miasto ze wszystkimi budowlami. Kamera stoi na nim.

**Przebieg:**
- 4 s stoi;
- potem 14 trafień przy magazynie i kadłub spada do 40% (pomiar przez 6 s);
- potem upada (4 s);
- na końcu 4 s ruiny.

Rozgrzewka startu gry (12 s) nie wlicza się do pomiaru. Dwa przebiegi Ultra i jeden Low (`artifacts-log/collapse-*.json`):

| Faza | Ultra: klatka / CPU główny / GPU | Ultra: najgorsza klatka | Low: klatka / CPU główny / GPU | Low: najgorsza klatka |
|---|---|---|---|---|
| stoi | 43,5–43,8 / 7,5 / 41,6–41,9 ms | 50 ms | 19,0 / 4,8 / 17,2 ms | 22 ms |
| ostrzał do 40% | 43,2–44,2 / 8,9 / 40,4–41,5 ms | 50–67 ms | 19,4 / 5,6 / 17,1 ms | 50,5 ms |
| upadek | 43,7–44,6 / 8,5 / 40,4–41,5 ms | 67–83 ms | 19,6 / 5,3 / 17,6 ms | 27 ms |
| ruina | 43,3–44,4 / 7,6 / 41,4–42,5 ms | 50 ms | 19,7 / 4,8 / 17,8 ms | 22 ms |

**Wnioski:**
- **Średnio:** zniszczenia dokładają 0,5–1,4 ms CPU głównego wątku w trakcie ostrzału i upadku. Po spoczynku dokładają ok. 0, a GPU się nie zmienia.
- **Pojedyncze skoki:** jednorazowo 1–2 klatki. Na Low najgorsza klatka to 50 ms w chwili, gdy kadłub spada skokowo o 60% i 14 trafień przychodzi naraz. Na Ultra najgorsza to 67–83 ms przy starcie upadku.
- **Cel 60 FPS:** nie jest spełniony już bez zniszczeń. Ultra jest ograniczona przez GPU (ok. 41 ms), co mierzono wcześniej.

## Pomiary całego portu (edytor, informacyjnie)

`BuildingPreview.MeasurePortDamage`, 5 przebiegów, wszystkie budowle portu macierzystego:
- **modele elementów:** w zasięgu wzroku powstają po jednym na klatkę (`PortBuildings.ModelsPerFrame` = 1). Najpierw budowle już uszkodzone, potem reszta z wyprzedzeniem, od pierwszego trafienia. Pierwsze uszkodzenie kosztuje więc 3,5 ms zamiast 55 ms naraz. Port pierwszy raz widziany albo po wczytaniu buduje wszystko od razu.
- **start upadku całego portu:** 26,7 ms, gdy modele są gotowe; 57,8 ms, gdy port pada tuż po pierwszym trafieniu i modele budują się dopiero wtedy.
- **klatka upadku:** mediana 0,06 ms, najgorsza 4,9 ms; budżet 112 brył w powietrzu wykorzystany.
- **ruina portu w spoczynku:** 27 rendererów (9 budowli × 3), ok. 51 tys. trójkątów.

## Pomiary (edytor, procesor tego laptopa, informacyjnie)

`BuildingPreview.MeasureSchoolDamage`, 10 przebiegów, mediany:
- **zbudowanie elementów Szkoły i pierwsze rysy:** 14,6 ms. Jednorazowo, przy pierwszym uszkodzeniu osady w zasięgu.
- **lokalna awaria:** 2,3 ms.
- **start pełnego zawalenia:** 6,4 ms.
- **klatka upadku przy 60 kl./s:** mediana 0,01 ms, najgorsza 3,5 ms (scalenie gruzu).
- **ruina w spoczynku:** 3 renderery, 20 391 trójkątów.

Koszt GPU i build Windows nie zmierzone. Edytor nie potwierdza wydajności na docelowym sprzęcie.

## Ograniczenia i dalsza praca

- **Zasięg:** element po elemencie pękają Szkoła, magazyn z domem, domy miasta, warsztat i mury z wieżami. Dok (kozły) nadal pada w całości w swoją dawną ruinę. Budowle przyczółka (magazyn, chata) mają ten sam model domu.
- **Start upadku całego portu** to jednorazowy skok 27–58 ms w chwili zniszczenia osady. Do rozłożenia na klatki, jeśli pomiar w buildzie to potwierdzi.
- **Wygląd:** cegła suszona to odcień tekstury tynku, nie własna tekstura. Rysy są płaskie, nie rzeźbione.
- **Model Szkoły** powstaje w jednej klatce (ok. 15 ms): budowle powstają po jednej na klatkę, ale samej Szkoły budowa nie jest jeszcze dzielona.
- **Okręt:** brak łamanych wioseł i desek na wodzie; do tego decyzja o zwijaniu albo zostawianiu żagla w bitwie (R-SAILS-ASHORE).
- **Podgląd okrętów** jest bez powierzchni wody: część masztu pod linią wody jest tam widoczna, w grze zakrywa ją morze.
