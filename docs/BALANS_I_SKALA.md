# Morza Bogów — balans, skala i tempo rozgrywki

Data: 2026-11-10. Gałąź: `balans-skala`. Status: etapy 1 (profile długości, złoża, skala lądu i drzew) i 2 (załoga i abordaż) wdrożone; etapy 3–8 zaplanowane.

## Decyzja użytkownika (nadrzędna wobec dokumentów)

Polecenie z 2026-11-09, potwierdzone 2026-11-10 słowami „słuchaj mojego polecenia, a nie dokumentu akurat w tej kwestii”:

- pojedyncza rozgrywka trwa **30–90 min** zależnie od scenariusza;
- w tym czasie da się zbudować **30–55 jednostek zbalansowanej floty bojowej**, zanim złoża D i S naturalnie się wyczerpią (*zastąpione korektą z 2026-11-10: złoża się odnawiają i nie decydują o końcu gry*);
- teren, drzewa i budowle w skali względem statków;
- podział czasu gry: **~50% bitwy morskie, ~35% gospodarka i logistyka, ~15% osady i obrona**;
- mechaniki docelowe: walka wręcz na pokładach przy abordażu, uzbrojenie per okręt (katapulty, balisty, ogień grecki), załoga z wioślarzy, łuczników, mieczników i włóczników, haki abordażowe i przejmowanie okrętów w całości; ochrona kupców, szlaki zaopatrzenia, polowanie na Ketosa po dużą premię D/S, koszt napraw i leczenia rannych, odzysk z wraków; rozbudowa głównego portu, szkoła mędrców (badania: pancerz, broń oblężnicza), fortece przybrzeżne, porty jako bazy, stocznie i szkolenie załóg; bohaterowie mitologiczni z unikalnymi zdolnościami.

W tych kwestiach decyzja zastępuje dotychczasowe zapisy. Dotyczy to m.in.: E00/VIZ07 (potyczka 15–25 min), E08 (budżet 34 000 D / 12 400 S jako jedyny wzorzec), E07 (brak wypłaty za potwory, brak odnawialnego srebra), GAME_DESIGN 6.3 (brak fortec budowanych przez gracza), C04/E04 (tylko marynarze/żołnierze/specjaliści; ceny jako kompletne szablony bez zmian uzbrojenia) oraz szkoły morskiej jako jedynego miejsca badań. Pozostałe reguły E00–E12 (dwa skończone zasoby, lokalne magazyny, fizyczny ładunek, brak utrzymania floty i potrzeb mieszkańców) pozostają w mocy, dopóki użytkownik nie zdecyduje inaczej.

## Korekta 2026-11-10: złoża nie decydują o końcu rozgrywki

Użytkownik: „złoża miały nie decydować o końcu rozgrywki”. Od tej zmiany:

- **Złoża się odnawiają** (`ResourceNode.Capacity`, `World.StepRegrowth`).
  - Punkt nieeksploatowany przez 60 s odrasta do swojego stanu początkowego.
  - Tempo to 15% największej wydajności punktu (`World.NodeCap`), na przykład dla srebra w profilu Standardowym 4 × 0,53 × 0,15 ≈ 0,32 S/s.
  - Intensywnie eksploatowany punkt nadal się wyczerpuje, ale tylko na pewien czas.
  - Przyrost księga D/S liczy jako nowe zasoby (`GrownWood`/`GrownSilver`).
  - Zapis gry ma wersję 12.
- **Koniec rozgrywki wyznaczają cele:** zniszczenie portu macierzystego albo utrata całej floty bez możliwości odbudowy. Wyczerpanie złóż nie kończy gry.
- **Profile Krótka/Standardowa/Długa** ustalają teraz wielkość mapy, zasoby punktów, tempo wydobycia, mury i tempo AI. Zakładana długość 30/60/90 min wynika z tempa, nie z wyczerpania złóż.
- **Cel „30–55 okrętów, zanim złoża się wyczerpią”** jest nieaktualny. Liczbę okrętów ogranicza tempo gospodarki, linie produkcji i limit floty (100).
- **Pomiar bez walki z odnawianiem** (ziarno 9): do 130 min powstaje na stronę 59–71 okrętów (Krótka), 33–43 (Standardowa) i 58–67 (Długa). Srebro chwilowo spada (Krótka: poniżej 10% w 46. min), potem odrasta.
- **Mecze AI kontra AI** kończą się zniszczeniem portu po 35–76 min.
- **Etap 5** doda pozostałe odnawialne źródła: ławice ryb, wraki po sztormach i konwoje.

## Etap 1 — wdrożony

### Profile długości (`MatchProfile`, `Definitions.cs`)

| | Krótka | Standardowa | Długa |
|---|---:|---:|---:|
| Cel czasu | 30 min | 60 min | 90 min |
| Cel floty (strona) | 30 | 42 | 55 |
| Rozstaw mapy (`Spread`) | 1,0 | 1,3 | 1,6 |
| Odległość baz | 750 m | 975 m | 1200 m |
| Złoże przy bazie D/S | 3000/1600 | 3300/1650 | 3700/1750 |
| Para ekspansji D/S (×4) | 1700/850 | 2550/1400 | 3400/1700 |
| Para centralna D/S (×2) | 2250/1100 | 3400/1750 | 4800/2300 |
| Razem na mapie D/S | 17 300/8 800 | 23 600/12 400 | 30 600/14 900 |
| Zapas startowy D/S | 900/450 | 360/160 | 360/160 |
| Wydajność wydobycia (`Yield`) | 0,70 | 0,53 | 0,42 |
| Mury portu macierzystego | ×2,5 (4500) | ×3 (5400) | ×4,5 (8100) |
| AI: grupa ataku / kutry | 5 / 8 | 6 / 7 | 8 / 7 |

- Wydajność mnoży tempo pracy kutra i limit złoża (`World.WorkRate`, `World.NodeCap`): wyrąb i wytop trwają tyle, ile wymaga długość scenariusza. Ceny, czasy budowy i role klas okrętów są bez zmian.
- Długość wybiera się w menu „Nowa bitwa” (Krótka / Standardowa / Długa) albo argumentem `-morzaLength Short|Standard|Long`. Domyślna jest Standardowa. Długość zapisuje się w zapisie gry (wersja 9, razem z murami osad).
- Mgła wojny obejmuje 2,6 × 2,6 km (104 × 104 pola po 25 m). Kamera może oddalać się i przesuwać proporcjonalnie do rozstawu mapy.

### Skala lądu i drzew

- **Wyspy zaplecza** (`IslandKind.Hinterland`, `MapFactory.AddHinterlands`). Za wysepką portu i za wysepką każdego złoża wyrasta duża wyspa (promień do ok. 134 m), połączona z nią w jeden brzeg. Ma klify wapienne od otwartego morza i las pinii z cyprysami. Rozmiar jest największy, przy którym każda przystań, złoże i wolny brzeg zachowuje 34 m podejścia, a między lądami zostaje 30 m kanału albo pełne połączenie (bez wąskich cieśnin). Na mapie jest ich 11–14. Pochylnie stoczni nadal wychodzą na wodę (test).
- **Drzewa w realnej skali**: `TreeModels.LifeSize = 2,2`. Pinia ma 11–18 m, cyprys 14–22 m, obok kadłubów o długości 12–26 m. Rozstaw drzew w gajach to 6–9 m. Duża wyspa ma od 10 do 32 drzew (budżet wydajności).
- Wyspy zaplecza zasłaniają strzały, jak skaliste wybrzeże. Na minimapie mają kolor lądu.

### Pomiary (narzędzie: symulacja AI kontra AI w .NET, bez Unity)

Gospodarka bez walki (`Opponent.Passive`: obie strony rozwijają się i nie atakują), ziarna 9 i 23:

| Profil | Srebro < 10% | Drewno < 10% | Okręty wojenne zbudowane (strona) |
|---|---:|---:|---:|
| Krótka | 39 min | 62 min | 23–29 |
| Standardowa | 66 min | 75 min | 29–49 |
| Długa | 91 min | 101 min | 49–59 |

Mecze AI kontra AI z walką kończą się wcześniej: Krótka 11–31 min, Standardowa 32–84 min, Długa 33–48 min. Obecne AI wysyła całą flotę naraz, przegrywa ją w jednym starciu i jego gospodarka się załamuje. Długość meczu z graczem zależy od gracza i od AI, a to jest zakres etapu 6. Czas walki w meczach AI to 1–4 min na mecz, więc **podziału 50/35/15 nie osiągnięto ani nie zmierzono** i wymaga on etapów 2–6 oraz playtestów.

## Mapa drogowa (po specyfikacji użytkownika z 2026-11-10)

Decyzje użytkownika z 2026-11-10:

- ludzie tylko jako obsada okrętów: żeglarze, szermierze, łucznicy (plus bohaterowie);
- surowce w pełni odnawialne (ławice ryb, wraki po sztormach, neutralne konwoje), a ryby, bursztyn i szczątki zamieniane na D/S;
- przejęcie okrętu razem z uzbrojeniem.

Gdy wejdzie etap 5, długość gry wyznaczają cele scenariusza, a złoża z etapu 1 przestają być jedynym ograniczeniem.

| Etap | Zakres | Stan |
|---|---|---|
| 2 | Trzy klasy załogi, składy wybierane w porcie, walka na burtach, ranni, przejęcie w całości, automatyczne przegrupowanie w porcie ([ZALOGA_I_ABORDAZ.md](ZALOGA_I_ABORDAZ.md)) | wdrożony |
| 3 | Warsztat i modułowe uzbrojenie (skorpion, gastrafetes, katapulta kamienna; po badaniu balista, onager i katapulta z pociskami ołowianymi, z minimalnym zasięgiem; ogień grecki i wapno palone tylko na okrętach specjalnych), montaż na okrętach i w fortach, szablony floty, broń widoczna w 3D, karty porównawcze ze stożkami ostrzału | plan |
| 4 | Pełny zestaw klas: taranowiec, łowca ludzi, niszczyciel okrętów, bombowiec, abordażowiec, okręt królewski, podpalacz, okręt z wapnem; łodzie rybackie i kupcy | plan |
| 5 | Żywa ekonomia: migrujące ławice, bursztyn, wraki po sztormach, neutralne konwoje, szczątki potworów, polowania na lewiatany i Ketosa; zagrożenia: wiry, rafy, harpie, meduzy | plan |
| 6 | Osady: szkoła mędrców, warsztat, rozbudowa portu, forty przybrzeżne | plan |
| 7 | Bohaterowie (aury, zdolności, ratunek z wraku), programowanie zachowania okrętów, szyk eskadry, aktywna pauza, wskaźniki taktyczne; AI z falami i odbudową | plan |
| 8 | Wiatr, prądy, żagle i wiosła; nieliniowa kampania z zachowaniem floty i weteranami | plan |

## Granice weryfikacji

- Bramka jakości (EditMode/PlayMode) obejmuje logikę i widoki. Nie obejmuje oceny wyglądu nowych wysp ani wydajności GPU z większą liczbą drzew; potrzebny jest test dymny buildu i ocena na zrzutach.
- Pomiary balansu to model AI kontra AI, nie playtest z graczem.
