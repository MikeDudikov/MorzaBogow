# Morza Bogów — wiatr, fale i siły na okręty

Data: 2026-11-10. Gałąź `balans-skala`. Plan zatwierdzony przez użytkownika: system wiatru zmiennego w przestrzeni, wzrostu i propagacji energii fal oraz sił wody i wiatru na okręty.

Decyzje użytkownika:
- **Siły.** Wszystkie siły są liczone; nurzanie, przechył i kiwanie działają od razu.
- **Ruch po mapie.** Wpływ na prędkość, dryf i skręt jest w symulacji z testami, ale za przełącznikiem `WindAffectsMovement`. Przełącznik jest domyślnie wyłączony w meczach (balans i AI w etapie 8).
- **Klimat.** Parametry odpowiadają klimatowi Morza Egejskiego i są potwierdzone źródłami.

Obowiązuje wcześniejsza decyzja: w meczu nie ma sztormu (średni wiatr ≤ 15 m/s, Beaufort 7).

Oznaczenia:
- **[Ź]** — wartość wprost ze źródła;
- **[Ź-a]** — wartość ze streszczenia;
- **[W]** — wniosek lub obliczenie z wartości źródłowych.

## Pomiar wyjściowy (przed zmianami)

- **Warunki pomiaru:**
  - build z `cc7e8f5` + `25db3f9`;
  - laptop i7-11390H, RTX 3050 Ti Laptop 4 GB, 1920×1080;
  - test dymny 60 s, flota startowa 14–16 okrętów;
  - pliki `artifacts-log/marine-baseline-{Ultra,Low}.json`.

| Profil | Średnia klatka | GPU | CPU główny | Symulacja | Prezentacja | Próbkowanie wody |
|---|---|---|---|---|---|---|
| Ultra | 36,4 ms | 34,7 ms | 7,3 ms | 0,46 ms | 0,97 ms | 0,09 ms |
| Low | 13,0 ms | 12,8 ms | 3,6 ms | 0,26 ms | 0,54 ms | 0,05 ms |

- Gra jest ograniczona przez GPU; cel 60 FPS spełnia tylko Low.
- Kołysanie (sprężyny do wysokości HDRP) osiąga klamrę 12° już przy spokojnym morzu (`maxTiltDegrees` 12,00).

## Jednostki

- 1 jednostka świata = 1 m (`MarineUnits.MetresPerUnit`). Czas w sekundach: tick = 1/30 s.
- Prędkości w m/s, masy w kg, siły w N, gęstości w kg/m³.
- Czas pogody to czas symulacji × `WeatherTimeScale`. Przyspieszanie rozwoju pogody jest jawnym parametrem (domyślnie 1).
- Fazy fal biegną zawsze w czasie symulacji, więc dyspersja nie zależy od tego parametru.

## Wybór modeli i rodzaj przybliżenia

| Element | Model | Rodzaj |
|---|---|---|
| Dyspersja | ω² = g·k·tanh(k·h) | prawo fizyczne |
| Wyporność | ρ_w·g·V zanurzone, całkowane po wręgach kadłuba | prawo fizyczne, całkowanie przybliżone |
| Wiatr synoptyczny | reżimy egejskie, procesy Ornsteina–Uhlenbecka, cykl dobowy | model parametryczny ze źródeł |
| Porywy | kinematic simulation (Fung i in. 1992), widmo von Kármána, zamrożona turbulencja Taylora | statystyczny model turbulencji, obcięty do skal ≥ 20 m |
| Strefy silniejszego i słabszego wiatru | komórki Gaussa z czasem życia, unoszone wiatrem | model parametryczny |
| Wyspy dla wiatru | ślad, spowolnienie na nawietrznej, strugi na krańcach, odchylenie kierunku, przyrost turbulencji | model parametryczny (zakresy ze źródeł, wnioski oznaczone) |
| Profil pionowy wiatru | logarytmiczny, z0 według COARE 3.5 | warstwa przyziemna, stan obojętny, z ≈ 2–50 m |
| Coriolis | pominięty: Ro = U/(f·L) ≈ 10/(9·10⁻⁵·3000) ≈ 37 | uzasadnione pominięcie |
| Morze wiatrowe | hybryda drugiej generacji: widmo kierunkowe, źródło z praw wzrostu JONSWAP zapisanych w lokalnej energii, przekształcanie do kształtu JONSWAP·TMA | model parametryczny + przybliżenie numeryczne |
| Interakcje nieliniowe | bez jawnego S_nl4; przesunięcie piku z prawa wzrostu | uproszczenie |
| Propagacja | bilans energii na siatce 52×52 × 50 m, 13 częstotliwości × 16 kierunków, upwind 1. rzędu | przybliżenie numeryczne (dyfuzja, brak dyfrakcji) |
| Wyspy dla fal | przeszkody podsiatkowe ze współczynnikami przepuszczalności | przybliżenie numeryczne |
| Dyssypacja | tarcie dna JONSWAP, whitecapping zależny od stromości, łamanie Battjes–Janssen z γ(kh) | modele parametryczne |
| Brzegi domeny | własny stan morza zewnętrznego z wiatru synoptycznego i fetchu spoza mapy | warunek brzegowy parametryczny |
| Powierzchnia | składowe liniowe (Airy) z widma, fazy z ziarna, amplitudy interpolowane | przybliżenie; fazy nie skracają się przy brzegu |
| Okręt | 3 stopnie swobody, wręgi, masa dodana i tłumienie z teorii pasowej | parametryzacja, współczynniki do kalibracji |
| Żagiel | siła nośna i opór z kąta natarcia, wiatr pozorny | model parametryczny, współczynniki do kalibracji |
| Piana | pole pokrycia: źródło z dyssypacji, unoszenie dryfem, zanik | model parametryczny; czas zaniku dobrany pod obraz |
| Grzbiet łamiący się | piana HDRP z jakobianu, maskowana polem piany | efekt czysto wizualny |

## Parametry klimatu (Morze Egejskie, lato)

### Wiatr

| Parametr | Wartość | Źródło |
|---|---|---|
| Sezon etezji | V–IX, maksimum VII–VIII | Misios i in. 2022 [Ź]; Tyrlis i Lelieveld 2013 [Ź-a] |
| Częstość dni etezyjskich VII–VIII | 35–65% | Gandham i in. 2025 [Ź]; Koletsis i in. 2009 [Ź] [W] |
| Średni U10 latem | 7–8 m/s w środkowym i płd. Egejskim, 4,5–6,7 m/s na północy | Soukissian i in. 2008 [Ź]; Badger i in. 2015 [Ź] |
| Silna etezja | 11–15 m/s, porywy 15–20 m/s | Koletsis i in. 2009/2010 [Ź]; Misios i in. 2022 [Ź] |
| Kierunek etezji | północ NE/NNE, środek N, południe NNW–NW | Badger i in. 2015 [Ź]; Koletsis i in. 2009 [Ź] |
| Cykl dobowy | U = Ū·[1 + 0,2·cos(2π(t − 14 h)/24 h)] | Prezerakos 2022 [Ź]; Koletsis i in. 2009 [Ź] [W] |
| Natężenie turbulencji | I = 0,06·(1 + 0,043·U10)·0,8 (stabilna warstwa latem) | ISO 19901-1 według Jeans 2024 [Ź]; Tombrou 2012 [Ź] [W] |
| Skala całkowa | L_u = 96 m na 10 m nad morzem, σ_v = 0,75·σ_u | EN 1991-1-4 [W] |
| Ślad wysepki | L_w = 20·H (zakres 10–30), D0 = 0,4 (0,3–0,5), strugi +15% | Koletsis i in. 2009/2010 [Ź]; Smith i in. 1997 [Ź] [W] |
| Opór powierzchni | COARE 3.5: α = 0,0017·U10 − 0,005, z0 = 0,11·ν_a/u* + max(α, 0)·u*²/g | Edson i in. 2013 [Ź] |

### Fale

| Parametr | Wartość | Źródło |
|---|---|---|
| Prawa wzrostu | gHs/U² = 1,6·10⁻³·χ^0,5; gTp/U = 0,286·χ^0,33; obowiązują w basenie śródziemnomorskim | Hasselmann i in. 1973; Alomar i in. 2011 [Ź] |
| Ostrożność | w środkowym Egejskim metody oparte na wietrze zawyżają; mnożnik kalibracji domyślnie 1 | Poulos i in. 2025 [Ź] |
| γ JONSWAP | 7,0·χ^−0,142 w zakresie 1,5–3,5 | Mazzaretto i in. 2022 [Ź-a] [W] |
| Hs i Tp latem | średnio 1,1–1,3 m; Tp ok. 6 s; etezja na otwartym morzu Hs 1–2 m, Tp 5–6,5 s | Soukissian i in. 2008 [Ź]; Barbariol i in. 2021 [Ź] |
| Fala martwa | słaba, dominuje morze wiatrowe | Barbariol i in. 2021 [Ź]; Zacharioudaki i in. 2015 [Ź-a] |
| Stromość | do ok. 0,07 | Barbariol i in. 2021 [Ź] |
| Fetch morza zewnętrznego | 60 km dla etezji (zakres 40–120 km) | Poulos i in. 2025 [Ź] (fetche 42–295 km w środkowym Egejskim) [W] |

### Woda i powietrze

| Parametr | Wartość | Źródło |
|---|---|---|
| Gęstość wody latem | 1026,3 kg/m³ (S = 39, T = 25 °C, EOS-80); zimą ok. 1029 | Poulos i in. 1997 [Ź-a]; Prezerakos 2022 [Ź] [W] |
| Gęstość powietrza latem | 1,165 kg/m³ (26 °C, 1010 hPa); zimą 1,23 | dane NOA (Mykonos) [W] |
| Lepkość kinematyczna | woda 0,94·10⁻⁶ m²/s przy 25 °C; powietrze 1,6·10⁻⁵ m²/s | wartości standardowe |
| Grawitacja | 9,80 m/s² (ok. 38° N) | wzór grawitacji normalnej |
| Pływy | ok. 10 cm; pomijalne | Pytharouli i Stiros 2012 [Ź-a] |
| Dryf wiatrowy | 3% U10 (zakres 1–6%); tylko w ruchu za przełącznikiem, prąd ≤ 20% prędkości bazowej (C09) | NOAA [Ź-a] |

**Brak danych:**
- **Wysokości wysp.** Nie ma ich w symulacji. Dodane są jako dane mapy (`Island.Height`), wzięte z obecnej geometrii prezentacji; to dane gry, nie pomiar.
- **Wartości regionalne.** Nie ma zmierzonego γ ani skal całkowych turbulencji dla Egejskiego, nie ma też szorstkości lądu wysepek.

## Stan wdrożenia (2026-11-10)

**Gotowe w symulacji** (`Runtime/Simulation/Marine`, `World.Marine.cs`; zapis v17):
- `WindField` — wiatr nad mapą:
  - tło synoptyczne;
  - komórki mezoskalowe;
  - porywy kinematic simulation w polu Matérna ν = 1/3 o ℓ = 1,339·L (wzdłuż każdej prostej to dokładnie widmo podłużne von Kármána ze skalą L), unoszone wiatrem;
  - ślady i strugi wysp;
  - COARE 3.5.
- `WaveField` — energia fal 52×52 × 50 m, 13 częstotliwości × 16 kierunków:
  - krok 4 s pogody, rozłożony na 120 ticków;
  - zapis w środku kroku odtwarzany powtórzeniem części kroku z zapisanych próbek wiatru.
- `SeaSurface` — powierzchnia fazowa: 144 składowe (λ ≥ 8 m), głębie co 10 m.
- `HullForm`, `SailForces`, `Seakeeping` — kadłub z katalogu, żagiel rejowy, 3 stopnie swobody.
- `MarineState` — przełącznik ruchu: prędkość ±25%, dryf, prąd ≤ 20% (C09), opór w falach STAWAVE-1.

**Kalibracje** (każda z uzasadnieniem w kodzie):

| Parametr | Wartość | Powód |
|---|---|---|
| κ (prędkość transportu energii / c_g(f_p)) | 0,715 | Wynika łącznie z praw JONSWAP (fetch) i SPM/CEM (czas). Przy współczynniku liczonym z kształtu (0,84) stan ustalony był 7–15% nad prawem, a wzrost w czasie 16–32% nad krzywą. |
| ShapeRate (relaksacja kształtu widma) | 10 × względne tempo wzrostu | Przy natychmiastowym przekształcaniu wzrost wiatru o 12% w 30 s zmieniał energię kubełka szczytu o 24%; teraz poniżej 2%. |
| WakeTurbulence | 1,0 | Natężenie turbulencji w cieniu wyspy rośnie ×1,5–2 (Koletsis 2009). |
| Rozkład kierunkowy | cos^2s(Δθ/2), s = 10 | Goda 2000. |

**Testy:** 33 testy EditMode w `MarineTests` (A–M ze specyfikacji i testy dodatkowe).
- Przechodzą w .NET 10 i w Unity 6000.3.28f1 (Mono): 33/33 w 13,4 s.
- Wraz z nimi 157 dotychczasowych testów symulacji w .NET przechodzi z formatem v17.

Zmierzone na modelu:

| Wielkość | Wynik |
|---|---|
| Wzrost wzdłuż fetchu (50 m, do 3 km) | 91–97% prawa JONSWAP |
| Wzrost wzdłuż fetchu (1 km, do 80 km) | 94–101% |
| Wzrost w czasie (30 min – 8 h) | 97–107% krzywej CEM |
| f_p | w granicach 4% prawa |
| Hs po 60 km przy 10 m/s | 1,2–1,3 m (średnio latem w Egejskim 1,1–1,3 m) |

**Koszt** (.NET 10, ten laptop, mapa Long, 31 wysp; na Mono w grze do zmierzenia):
- 1,75–2,5 ms na sekundę pogody, czyli średnio 0,06–0,08 ms na tick;
- najgorszy tick 0,3–0,8 ms;
- start z ustaleniem 300 s pogody: 0,7–1,2 s.

**Prezentacja** (`Runtime/Presentation`; w toku weryfikacji w buildzie):
- **Start meczu.** `World.EnableMarineForMatch()` losuje klimat z ziarna (`MarineClimate.ForMatch`): cisza 10%, bryza 20%, etezja umiarkowana 50%, silna 20%. Bez sztormu, bez użycia liczb losowych gry. Wczytana gra przynosi morze z zapisu.
- **`SeaSurfaceRenderer`** (profile High i wyżej, gdzie HDRP rysuje decale wody):
  - compute `Resources/SeaSurfaceHeights.compute` sumuje te same 144 składowe co symulacja do tekstury 512² (2 m na teksel) nad obszarem decali 1024 m wokół widoku;
  - jeden `WaterDecal` w trybie tekstury (graf HDRP „Water Decal Sample”, materiał `Resources/SeaHeightsDecal.mat`, wariant `_TYPE_TEXTURE`) dodaje ją do wody;
  - fazy liczone w podwójnej precyzji na CPU, ograniczenie 0,55·h/Hs z tekstury głębi, wygaszenie 32 m przy brzegu obszaru;
  - pasmo duże HDRP wyłączone, średnie na ⅓ (tylko faktura), zmarszczki z wiatru meczu.
- **Low i Medium** nie mają decali wody. Rysuje się tam pasmo HDRP z wiatrem i kierunkiem meczu: morze wygląda podobnie, ale grzbiety nie są tymi, na których kołyszą się kadłuby (jawne przybliżenie).
- **`SeakeepingDriver`** zastępuje sprężyny `HullMotion`:
  - model 3 stopni swobody z przechyłem i trymem od `SailForces` dla widocznych okrętów, raz na tick symulacji, pokazywany między dwoma tickami;
  - stoi w pauzie i przyspiesza z prędkością gry;
  - pasmo HDRP `timeMultiplier` dostaje prędkość gry, w pauzie 0,001 (0 psuje pianę HDRP).
- **Przybój** przy brzegu bierze kierunek fal z wiatru meczu.
- **Raport dymny:** `marineClimate`, `marineWindMs`, `marineHs`, `marineAmplitude`, `seakeepingMs`, `seakeepingShips`, `seaRenderCpuMs`, `seaCorrelation`.
  - `seaCorrelation` to korelacja wysokości HDRP odczytanej z GPU (8 punktów 250 m od widoku, z dala od kadłubów) z wysokością morza symulacji; bliska 1 potwierdza, że decal rysuje te same fale we właściwej orientacji.

**Pomiar w buildzie** (2026-11-10, ok. 12:00, RTX 3050 Ti Laptop, 1080p, test dymny 60 s, flaga `-morzaNoSea` w tym samym buildzie; pliki `artifacts-log/marine-ab2-*.json`):

| Przebieg | Klatka | GPU | CPU główny | Sym. na klatkę | Prezentacja | Kołysanie / tick (8 okrętów) | Rysowanie fal CPU | `seaCorrelation` |
|---|---|---|---|---|---|---|---|---|
| Ultra z morzem | 47,3 ms | 27,9 ms | 31,3 ms | 7,3 ms | 3,5 ms | 0,71 ms | 0,18 ms | 0,849 |
| Ultra bez morza | 45,0 ms | 38,3 ms | 17,8 ms | 2,0 ms | 2,4 ms | — | — | — |
| Low z morzem | 23,1 ms | 20,0 ms | 8,2 ms | 2,2 ms | 1,2 ms | 0,38 ms | 0 (bez decali) | 0,133 |
| Low bez morza | 27,5 ms | 20,7 ms | 13,6 ms | 2,3 ms | 2,4 ms | — | — | — |

- **Decal rysuje fale symulacji we właściwej orientacji:** korelacja 0,85 na Ultra przy 9344 porównaniach. Różnicę do 1 tłumaczą opóźnienie odczytu z GPU i dekoracyjne pasmo średnie.
- **Na Low** HDRP rysuje własne fale, które nie są falami kadłubów (korelacja 0,13): to opisane przybliżenie.
- **Czasy klatki są zaszumione.** Test dymny gra AI z prędkością 2×, więc liczba ticków na klatkę zależy od długości klatki, a przebiegi różnią się przebiegiem gry. Pojedyncze 60 s nie rozstrzyga różnic kilku ms; potrzebny jest pomiar powtórzony lub z ustaloną kamerą i prędkością.
- **Dlatego do raportu dodane jest pole `marineTickMs`** (bezpośredni koszt ticku morza) oraz koszt kołysania na tick.
- **Wcześniejsza wersja rysowania** wysyłała energię okna na GPU co klatkę i kosztowała 1,3 ms CPU. Teraz energia idzie raz na krok fal, a mieszanie robi shader.
- **Kołysanie:**
  - pomija składowe poniżej 5% najsilniejszej;
  - liczy 2 podkroki na tick;
  - przekrój kadłuba czyta z tablicy;
  - obie burty liczy w jednym przebiegu (`SeaSurface.SampleHull`: wspólne amplitudy, filtr, obrót kroku i współczynniki Smitha z pamięci według zanurzenia), dając bit w bit te same liczby co dwa wywołania `SampleLine` (test `Sea_HullSamplingEqualsTwoLineSamples`);
  - obejmuje tylko okręty w kadrze kamery z marginesem 60 m (bez kamery: do 700 m od środka widoku). Okręt, który wraca do kadru po przerwie dłuższej niż 3 ticki, zostaje najpierw ułożony na wodzie.
  - **Daleki widok jest jawnym przybliżeniem.** Poniżej 10 px/m (domyślny widok ma około 7) każdy kadłub dostaje krok co 3 ticki, z dt = 3 ticki. Kadłuby są rozłożone na ticki według id, a obraz płynie między dwoma ostatnimi krokami, z opóźnieniem 3 ticków zamiast 1. Wiatr na żagiel nadal liczy się 5 razy na sekundę.
    - Błąd względem kroku co tick (harness, silna etezja, 4 m/s, 4 klasy, 4 kursy): 7–15% RMS ruchu, czyli dla kutra około 6 cm nurzania i 0,4° przechyłu. Przy 7 px/m to poniżej piksela.
    - Testy: `Seakeeping_ThreeTickStepsStayCloseToEveryTickSteps` (próg 20%) i `SeakeepingDriverTests` (każdy kadłub raz na 3 ticki, nigdy wszystkie w jednym ticku, bez skoków obrazu, ułożenie po powrocie).
  - **Koszt.** Przed zmianami: około 17 µs na okręt na tick w .NET, 16–20 µs na Mono poza grą i około 90 µs w grze pod obciążeniem GPU. W grze wolniej działa też sama symulacja, więc różnica to środowisko, nie kołysanie. Dokładne przyspieszenia dają na Mono 15–40% mniej (pomiar zaszumiony), a krok co 3 ticki dzieli koszt dalekiego widoku przez 3.
  - **Pomiar w buildzie** (2026-11-10, 14:10, Ultra, test dymny 60 s, ten sam mecz co wcześniej; `artifacts-log/marine-ab4-Ultra-sea.json`). Kołysanie kosztuje 0,33 ms na tick przy 7 okrętach, wcześniej 0,62 ms (`marine-ab3`), czyli o 47% mniej. Średnio krokuje 58% śledzonych kadłubów (`seakeepingStepShare` 0,582): daleki widok mieszał się ze zbliżeniami do zrzutów.
    - W tym samym czasie inna sesja testowała w Unity: klatka 82,5 ms wobec 74 ms, GPU 41,5 ms wobec 33,1 ms. Liczby są więc zaszumione, a koszt na kadłub z krokiem (około 80 µs) mieści się w tym szumie.
    - Wyniki zalewania bez zmian albo lepsze: woda średnio 0,05 m nad linią wodną, ponad pokład w 0,67% z 16 012 próbek, korelacja fal 0,835.
  - **Budżet.** Przy 50 okrętach w domyślnym, dalekim widoku z tych liczb wychodzi około 1,3 ms na tick, czyli około 0,7 ms na klatkę przy 60 kl./s i prędkości 1×. To wciąż około ⅓ ponad budżet 0,5 ms, ale wcześniej było około 2,2 ms. Dalsze zejście wymagałoby liczenia kadłubów poza głównym wątkiem (osobne bufory próbkowania na wątek) albo dłuższego kroku przy największym oddaleniu. Ani jedno, ani drugie nie jest zrobione.

**Zgłoszenie użytkownika (2026-11-10): „na łodzie wdziera się woda, a czasami w 90% są pod wodą”.**

Diagnoza:
- **Ustawienie w pionie.** Kod jest spójny: `ApplyDraft` obniża model tylko o przyrost zanurzenia od ładunku, a nurzanie z modelu to odchylenie od równowagi. Nie ma podwójnego zanurzenia.
- **Rysowane fale.** Na Ultra zgadzają się z fizycznymi (korelacja 0,85).
- **Przyczyny:**
  1. HDRP rysuje płaszczyznę wody także wewnątrz otwartego kadłuba. Grzbiet krótszy niż kadłub, którego kadłub fizycznie nie nadąża śledzić (Hs 0,7–1,5 m wobec burt 0,75–0,87 m kutra), pokazuje się jako woda na pokładzie.
  2. Kutry w rezonansie przechyłu (okres około 4,6 s, fala z boku Tp około 4,5 s) przy tłumieniu ζ = 0,08 przechylały się do 20,5°, kładąc krawędź burty przy wodzie.
  3. **Pełny żagiel w silnym wietrze.** Załoga nie skracała żagla. Ustalony przechył kutra od żagla przy wietrze z boku (harness, okręt w miejscu): 5,7° przy 8 m/s, 14° przy 12,5 m/s, 20° przy 15 m/s. Porywy do 20 m/s zbliżały go do kąta zalania burty (35°). W meczu z silną etezją (12,5 m/s) kutry miały przechył RMS 11–17° i maksymalnie 30–42°: to obraz „łodzi w 90% pod wodą”.

Poprawki:
1. **Wykluczenie wody z wnętrza kadłuba.** Każdy kadłub ma `WaterExcluder` HDRP o kształcie spiczastego graniastosłupa wewnątrz linii wody, od stępki do burty. Jedzie z modelem kadłuba, tylko w obrazie.
2. **Tłumienie przechyłu 0,15 krytycznego** (było 0,08): typowe dla małych otwartych kadłubów (opór stępki, kadłuba i wirów, składowe Ikedy 0,1–0,2). Rezonansowy przechył spada mniej więcej o połowę.
3. **Załoga skraca żagiel** (`SailForces.HeelLimit`, symulacja). Gejtawy podciągają luźną stopę żagla rejowego (Casson 1995, s. 38–39; Whitewright 2018), a w porywie załoga luzuje szoty. Dzięki temu przechył od wiatru nie przekracza wytycznej IMO dla ustalonego wiatru: 16° albo 80% kąta zanurzenia krawędzi pokładu, zależnie od tego, która wartość jest mniejsza (rez. A.562(14), 1985; kodeks IS 2008). Za krawędź pokładu otwartego kadłuba przyjęta jest burta.
   - Dla wszystkich klas wychodzi 16°. To reguła zapożyczona z norm stateczności statków (interpretacja): przechył, na jaki pozwalały starożytne załogi, nie jest znany.
   - Kryterium dla łodzi otwartych (minimalna wolna burta około 400 mm; wytyczne brytyjskie, metoda Wolfson Unit) dawałoby kutrowi aż 24°, więc nie jest ostrzejsze.
   - Siła wiatru na kadłub zostaje (załoga jej nie skróci). Udział żagla jest w `WindForces.SailShare`. Przy przełączniku ruchu słabszy żagiel daje też słabszy napęd.
   - Wynik (harness, ten sam mecz): przy silnej etezji kutry mają maksymalnie 22–25,5° (było 30–42°), RMS 11–14,5°. Przy umiarkowanej bez zmian (maksymalnie 14–20°), bo przechył od żagla jest tam poniżej 16°.
   - Test: `Sails_TheCrewShortensSailToKeepTheHeelWithinTheGuide`.
4. **Pomiar w teście dymnym:**
   - woda względem pokazywanej linii wody przy rufie, burtach i środku kadłuba (`hullWaterMean`, `hullWaterRms`, `hullWaterMax`, `hullDeckAwashShare`);
   - skala fal rysowanych do fizycznych (`seaHeightRatio`).

**Weryfikacja poprawek 1, 2 i 4 w buildzie** (2026-11-10, 12:48, test dymny 60 s, etezja umiarkowana 10,5 m/s, Hs 0,82 m; `artifacts-log/marine-ab3-*`):

| Profil | `seaCorrelation` | `seaHeightRatio` | Woda nad linią wodną: średnio / RMS / maks. | Ponad pokład | Próbki | Największy przechył |
|---|---|---|---|---|---|---|
| Ultra | 0,827 | 1,02 | 0,06 / 0,42 / 1,32 m | 0,95% | 17 424 | 18,9° |
| Low | −0,06 | 1,47 | −0,02 / 0,48 / 1,49 m | 1,6% | 34 172 | 18,9° |

- **Zbliżenia z Ultra** (`marine-ab3-Ultra-sea-shot-2.png`, `-shot-5.png`): pokłady suche, bez wody wewnątrz kadłubów.
- **Kadłub nie tonie:** średnia woda leży kilka centymetrów od linii wodnej.
- **RMS 0,4 m to ruch względny wody przy burtach i rufie.** Obejmuje fale krótsze od kadłuba, przechył (punkty burt leżą na pół szerokości), falę dziobową HDRP i różnicę między falą rysowaną a fizyczną (korelacja 0,83).
- **Na Low** HDRP rysuje własne fale, o 47% wyższe od fal symulacji i w innej fazie. Kadłuby kołyszą się na falach symulacji, więc tam woda częściej sięga burty. To opisane przybliżenie profili bez decali (Low, Medium).
- **Poprawka 3** (żagiel) jest sprawdzona testami i harnessem, ale nie buildem: ten mecz ma etezję umiarkowaną, przy której skracanie żagla nie zachodzi.

**Do oceny przez człowieka (ruchomy obraz):**
- **Przechył kadłubów** po zmianie tłumienia i skracaniu żagla. Jeśli dalej wygląda przesadnie, do kalibracji są `HullForm.RollDamping`, wysokość metacentryczna (8–10% szerokości) i `SailForces.HeelLimit`.
- **Żagiel** nie jest jeszcze rysowany jako skrócony: obraz pokazuje cały żagiel także przy `SailShare` < 1.
- **Morze wokół widoku.** Fale wygaszają się na brzegu obszaru 1024 m wokół widoku (32 m). Przy największym oddaleniu i małym pochyleniu kamery dalsza woda ma tylko pasma dekoracyjne HDRP.

**Jeszcze nie zrobione:**
- nakładka diagnostyczna na ekranie (dane są w raporcie dymnym);
- piana brzegowa z łamania Battjesa–Janssena w profilu poprzecznym (przybój ma tylko kierunek z wiatru);
- kompensacja rozmycia 3×3 decala;
- pomiar GPU i czasu klatki w buildzie przed i po.

## Ograniczenia znane z góry

- Brak dyfrakcji fal. Cień za wyspą wypełnia się przez rozrzut kierunkowy i dyfuzję numeryczną.
- Refrakcja pominięta, bo szelfy (44 m) są węższe niż komórka siatki (50 m).
- Fazy powierzchni nie skracają się przy brzegu. Płycizny działają przez energię: c_g, łamanie i ograniczenie wysokości.
- Fale krótsze niż 8 m są tylko dekoracją HDRP. Fizyka okrętów ich nie widzi; różnica RMS wysokości jest raportowana.
- Kołysanie (nurzanie, przechył, kiwanie) to deterministyczny model liczony przez prezentację dla okrętów w widoku. Nie wpływa na rozgrywkę i nie jest zapisywany. Średni przechył od wiatru liczą te same funkcje co symulacja.
- Zgodność bit w bit dotyczy jednej platformy i jednego buildu. Nie twierdzę, że wyniki są takie same na różnych GPU ani platformach.

## Źródła

- **Wiatr:**
  - Badger M. i in. 2015, raport DTU I-0375 (SAR, Morze Egejskie);
  - Koletsis I. i in. 2009, NHESS 9, doi:10.5194/nhess-9-1845-2009; 2010, NHESS 10, doi:10.5194/nhess-10-1115-2010;
  - Misios S. i in. 2022, WCD 3, doi:10.5194/wcd-3-811-2022;
  - Gandham H. i in. 2025, npj Clim. Atmos. Sci., doi:10.1038/s41612-025-01072-2;
  - Prezerakos N. 2022, Atmósfera, doi:10.20937/ATM.53039;
  - Tyrlis E., Lelieveld J. 2013, J. Atmos. Sci. 70, doi:10.1175/JAS-D-13-035.1;
  - Tombrou M. i in. 2012/2014 (Aegean-GAME);
  - Jeans G. 2024, Wind Energ. Sci. 9, doi:10.5194/wes-9-2001-2024;
  - Smith R.B. i in. 1997, J. Atmos. Sci. (ślady wysp);
  - Edson J. i in. 2013, J. Phys. Oceanogr., doi:10.1175/JPO-D-12-0173.1 (COARE 3.5);
  - Fung J.C.H. i in. 1992, J. Fluid Mech. 236 (kinematic simulation).
- **Fale:**
  - Hasselmann K. i in. 1973 (JONSWAP);
  - Soukissian T. i in. 2008, Open Ocean Eng. J. 1;
  - Barbariol F. i in. 2021, Front. Mar. Sci., doi:10.3389/fmars.2021.760614;
  - Zacharioudaki A. i in. 2015, Ocean Dyn., doi:10.1007/s10236-015-0840-z;
  - Poulos S. i in. 2025, Water 17, doi:10.3390/w17101499;
  - Alomar M. i in. 2011, ICCE;
  - Mazzaretto O. i in. 2022, Ocean Eng., doi:10.1016/j.oceaneng.2022.112756;
  - Bouws E. i in. 1985 (TMA);
  - Young I.R., Verhagen L.A. 1996 (wzrost przy skończonej głębokości);
  - Battjes J.A., Janssen J.P.F.M. 1978 (łamanie głębokościowe);
  - Ruessink B.G. i in. 2003 (γ(kh));
  - Tolman H.L. 2003 (przeszkody podsiatkowe, WAVEWATCH III);
  - Snyder R.L. i in. 1981 (tempo wzrostu od wiatru).
- **Woda i pływy:**
  - Poulos S. i in. 1997;
  - Pytharouli S., Stiros S. 2012, doi:10.1179/1752270611Y.0000000035.
- **Żagiel i przechył:**
  - IMO, rezolucja A.562(14), 1985 (kryterium pogodowe: przechył pod ustalonym wiatrem 16° albo 80% kąta zanurzenia krawędzi pokładu); to samo w kodeksie IS 2008;
  - Whitewright J. 2018, „Sailing and Sailing Rigs in the Ancient Mediterranean”, IJNA 47(1): 28–44, doi:10.1111/1095-9270.12278 (gejtawy, luźna stopa żagla rejowego, za Cassonem 1995, s. 38–39);
  - Casson L. 1995, *Ships and Seamanship in the Ancient World* (nieodczytany bezpośrednio, cytowany za Whitewrightem);
  - wytyczne brytyjskie dla małych kutrów rybackich i metoda Wolfson Unit (minimalna wolna burta łodzi otwartej około 400 mm; do porównania, projekt wytycznych).
