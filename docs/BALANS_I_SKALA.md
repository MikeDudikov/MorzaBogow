# Morza Bogów — balans, skala i tempo rozgrywki

Data: 2026-11-10. Gałąź: `balans-skala`. Status: etap 1 z 6 wdrożony (profile długości, złoża, skala lądu i drzew); etapy 2–6 zaplanowane.

## Decyzja użytkownika (nadrzędna wobec dokumentów)

Polecenie z 2026-11-09, potwierdzone 2026-11-10 słowami „słuchaj mojego polecenia, a nie dokumentu akurat w tej kwestii”:

- pojedyncza rozgrywka trwa **30–90 min** zależnie od scenariusza;
- w tym czasie da się zbudować **30–55 jednostek zbalansowanej floty bojowej**, zanim złoża D i S naturalnie się wyczerpią;
- teren, drzewa i budowle w skali względem statków;
- podział czasu gry: **~50% bitwy morskie, ~35% gospodarka i logistyka, ~15% osady i obrona**;
- mechaniki docelowe: walka wręcz na pokładach przy abordażu, uzbrojenie per okręt (katapulty, balisty, ogień grecki), załoga z wioślarzy, łuczników, mieczników i włóczników, haki abordażowe i przejmowanie okrętów w całości; ochrona kupców, szlaki zaopatrzenia, polowanie na Ketosa po dużą premię D/S, koszt napraw i leczenia rannych, odzysk z wraków; rozbudowa głównego portu, szkoła mędrców (badania: pancerz, broń oblężnicza), fortece przybrzeżne, porty jako bazy, stocznie i szkolenie załóg; bohaterowie mitologiczni z unikalnymi zdolnościami.

W tych kwestiach decyzja zastępuje dotychczasowe zapisy. Dotyczy to m.in.: E00/VIZ07 (potyczka 15–25 min), E08 (budżet 34 000 D / 12 400 S jako jedyny wzorzec), E07 (brak wypłaty za potwory, brak odnawialnego srebra), GAME_DESIGN 6.3 (brak fortec budowanych przez gracza), C04/E04 (tylko marynarze/żołnierze/specjaliści; ceny jako kompletne szablony bez zmian uzbrojenia) oraz szkoły morskiej jako jedynego miejsca badań. Pozostałe reguły E00–E12 (dwa skończone zasoby, lokalne magazyny, fizyczny ładunek, brak utrzymania floty i potrzeb mieszkańców) pozostają w mocy, dopóki użytkownik nie zdecyduje inaczej.

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

## Etapy dalsze (plan)

1. **Etap 2 — załoga i abordaż.** Załoga per okręt: wioślarze (prędkość), łucznicy (ostrzał), miecznicy i włócznicy (abordaż, obrona pokładu). Walka wręcz na pokładzie zamiast jednej formuły siły: tura co 1 s, starcia par na pokładzie, haki abordażowe jako etap (zarzucenie, przyciągnięcie, przejście), przejęcie okrętu w całości z ładunkiem. Ranni: część poległych to ranni, leczeni w porcie za srebro.
2. **Etap 3 — uzbrojenie per okręt.** Gniazda lekkie, średnie i ciężkie (C04): balisty, katapulty, ogień grecki (pożar C05), montaż w porcie. Cena okrętu = kadłub + wybrane uzbrojenie + załoga.
3. **Etap 4 — logistyka.** Szlaki zaopatrzenia i konwoje kupców z eskortą, odzysk z wraków (przegląd limitów 50%/30%/15%), koszt napraw i leczenia. Ketos jako rzadkie, niebezpieczne polowanie z dużą jednorazową premią D/S (pojedyncze stado na mapę, bez odnawiania).
4. **Etap 5 — osady i obrona (~15%).** Poziomy głównego portu (magazyn, stocznia, koszary szkolenia załogi), szkoła mędrców z badaniami (pancerz, broń oblężnicza, szkolenie), fortece przybrzeżne gracza. Budynki w skali portu: zabudowa na wyspie zaplecza.
5. **Etap 6 — bohaterowie i AI.** Bohaterowie z unikalnymi zdolnościami (C07). AI z falami, odwrotem i odbudową, żeby mecz z AI trwał docelowe 30–90 min. Pomiar udziału czasu (walka / gospodarka / osady) w telemetrii meczu.

## Granice weryfikacji

- Bramka jakości (EditMode/PlayMode) obejmuje logikę i widoki. Nie obejmuje oceny wyglądu nowych wysp ani wydajności GPU z większą liczbą drzew; potrzebny jest test dymny buildu i ocena na zrzutach.
- Pomiary balansu to model AI kontra AI, nie playtest z graczem.
