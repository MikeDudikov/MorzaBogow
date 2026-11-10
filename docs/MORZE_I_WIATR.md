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

**Jeszcze nie zrobione** (prezentacja, w toku):
- `EnableMarine` przy starcie meczu;
- deformacja HDRP z tych samych składowych (compute + `WaterDecal`) i maski pasm;
- rozszerzenie `WeatherController`;
- `SeakeepingDriver` zamiast sprężyn `HullMotion`;
- piana brzegowa;
- nakładka diagnostyczna;
- pomiar GPU i czasu klatki w buildzie.

Do tego czasu gra działa jak przed zmianą: symulacja nie włącza morza sama.

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
