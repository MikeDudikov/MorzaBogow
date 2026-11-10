# Profile czasu i regionu środowiska

Profil środowiska to ustawienie prezentacji: wybiera, jak wygląda roślinność, wybrzeże, ruda i stocznie. Nie jest scenariuszem ani regułą gry. W kodzie odpowiada mu `EnvironmentProfile` (`Runtime/Presentation/Environment/EnvironmentProfiles.cs`, przestrzeń nazw `MorzaBogow.Presentation.Landscape`). Źródła twierdzeń są w [SrodowiskoZrodla.md](SrodowiskoZrodla.md), w numerowanych wierszach tabel.

**Aktywny profil:** E1 + R-LAD, czyli XII w. p.n.e. na południowym lądzie (kotwica projektu). Wyspy różnią się w obrębie profilu rodzajem, wielkością i ekspozycją na wiatr, wszystko ze stabilnego id.

## Epoki

Granice są umowne (±25–50 lat), a na Krecie fazy mają inne nazwy.

| Kod | `Era` | Lata p.n.e. | Źródło |
|---|---|---|---|
| E1 | `PostPalatial` (LH IIIC) | 1190–1070 | 1.1 (Tartaron 2008, tab. 1) |
| E2 | `EarlyIron` (SM/PG) | 1070–900, cezura ok. 1025 | 1.1–1.2 (Toffolo i in. 2013) |
| E3 | `Geometric` | 900–700 | 1.x (Weiberg i in. 2019, tab. 1) |
| E4 | `Archaic` | 700–480 | 1.x (Weiberg i in. 2019, tab. 1) |
| E5 | `Classical` | 480–400 | 1.x |

## Regiony

| Kod | `Region` | Skały | Roślinność charakterystyczna |
|---|---|---|---|
| R-CYK | `Cyclades` | marmur, łupek, granit | frygana, makia |
| R-KRE | `Crete` | wapień, kras | *Pinus brutia*, dziki cyprys, platan przy wodzie |
| R-WSCH | `EastAegean` | różne | *P. brutia*, dziki cyprys (Samos, Rodos, Kos) |
| R-LAD | `SouthMainland` | wapień; Laurion na kontakcie marmuru i łupka | *P. halepensis* |
| R-WULK | `Volcanic` | skały wulkaniczne (Thera, Melos) | bez wysp Kameni przed 197 p.n.e. |

## Co profil zmienia dziś

- **Oliwki na zamieszkanych wysepkach** (`CultivatedOliveShare`). Udział oliwek uprawnych wynosi 0,7 w E1, 0,8 w E2 i 0,9 później; resztę stanowią oliwki dzikie. Uzasadnienie: pyłek oliwki rośnie od LH III, wyraźnie po ok. 1050 p.n.e. (2.x, Weiberg i in. 2019). To interpretacja, a same liczby wybrała gra.
- **Sosna regionu** (`BrutiaPine`): *P. brutia* na Krecie i we wschodniej Egei, *P. halepensis* gdzie indziej (Atlas JRC, s. 122). Dziś tylko pole, bo sosny nie przeszły jeszcze na nowy generator.
- **Narzędzia** (`IronTools`): brąz w E1, żelazo od E2. Dotyczy stoczni i kopalni, gdy powstaną ich modele.

## Zasady stałe dla wszystkich profili

- **Bez gatunków wprowadzonych po starożytności:** eukaliptus, opuncja, agawa, robinia, ajlant (`EnvironmentManifest.Excluded`, test `Manifest_IsMadeFromTheCodeAndListsNoLaterIntroductions`).
- **Bez plantacyjnych rzędów sosen.** Masowe nasadzenia to lata 1930–1970.
- **Cyprys kolumnowy** tylko przy sanktuariach i w ogrodach. Dziki rośnie rozłożyście (JRC, s. 88–89).
- **Notch pływowy** w wapieniu leży na poziomie morza gry, bo morze gry jest morzem starożytnym (3.x).
- **Drzewa nie zasłaniają kadłubów.** Oliwki gaju mają 4,5–7 m, mniej niż maszty przy nabrzeżu (decyzja gry).

## Zestaw próbny w grze (stan 2026-11-10)

Pełna lista jest w [EnvironmentAssetManifest.json](EnvironmentAssetManifest.json), w pozycjach ze statusem `Trial`.

- **Oliwka uprawna i dzika** (`TreeGenerator`, `TreeDynamics`, `TreeGrove`):
  - 3 poziomy LOD na wspólnych kościach; wiatr meczu tylko do odczytu;
  - gaj poza kadrem kamery nic nie liczy;
  - z daleka ruszają się tylko pień i konary.
- **Wybrzeże** (`CoastFeatures`): warga notchu w sektorach klifów, mokry pas oraz płyty beachrocku na łagodnych brzegach.
- **Ruda srebra, typ Laurion, E1** (`OreOutcrop`):
  - po bokach wejścia do kopalni marmur pod łupkiem, z rdzawym kontaktem między nimi;
  - w kontakcie ziarna galenitu (jedyny metal) i biały cerusyt;
  - płytkie wyrobiska oraz hałda sortowanej rudy, która maleje ze stanem złoża.
- **Stocznia plażowa E1** (`ShipyardProps`): obok istniejących pochylni bale i deski, szałas z dachem z trzciny, stojak z narzędziami z brązu, kocioł smoły i kamienne podpórki.

**Uruchomienie pokazu:**

```
Builds/M1/MorzaBogow.exe -force-device-index 0 -morzaLandscapeShowcase <prefiks>
```

**Pomiar A/B:** `-morzaSmokeReport <plik> 60` z `-morzaPlainOlives` i bez tej flagi.

**Wynik pomiaru** (RTX 3050 Ti Laptop, 1920×1080, jeden przebieg 60 s na wariant):
- wiatr i kości 21 drzew: 0,51–0,53 ms głównego wątku na klatkę;
- GPU: Ultra 30,6 ms z nowymi oliwkami, 33,7 ms ze starymi; Low 13,5 i 14,3 ms. To różnica w granicach szumu.
- Pełnej klatki nie rozstrzygnięto: czas symulacji wahał się między przebiegami od 3 do 12 ms.

**Jeszcze nie zrobione:**
- plaża żwirowa i frygana;
- sosny, cyprysy, makia;
- przodek E5 i płuczka;
- animacje: pył i odłamki przy rudzie, praca szkutników.
