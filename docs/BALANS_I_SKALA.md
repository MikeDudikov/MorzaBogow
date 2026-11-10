# Morza Bogów — balans, skala i tempo rozgrywki

Data: 2026-11-10. Gałąź: `balans-skala`. Status:
- etap 1 (profile długości, złoża, skala lądu i drzew) wdrożony;
- etap 2 (załoga i abordaż) wdrożony;
- z etapu 3 wdrożone 3a, 3b i symulacja 3c ([UZBROJENIE.md](UZBROJENIE.md)); prezentacja 3c czeka;
- etapy 4–9 zaplanowane.

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
- **Pomiar bez walki z odnawianiem** (ziarno 9, jeszcze na mapie z wyspami zaplecza): do 130 min powstaje na stronę 59–71 okrętów (Krótka), 33–43 (Standardowa) i 58–67 (Długa). Srebro chwilowo spada (Krótka: poniżej 10% w 46. min), potem odrasta. Pomiar na mapie z ograniczonym lądem: sekcja „Skala lądu i drzew”.
- **Mecze AI kontra AI** (ta sama mapa) kończą się zniszczeniem portu po 35–76 min.
- **Etap 5** doda pozostałe odnawialne źródła: ławice ryb, wraki po sztormach i konwoje.

## Naprawa w doku (uzgodnienie paczki, 2026-11-10)

Decyzja użytkownika z uzgodnienia paczki specyfikacji ([UZGODNIENIE_SPECYFIKACJI.md](UZGODNIENIE_SPECYFIKACJI.md)): kadłuby naprawia tylko dok i tylko za srebro. Zastępuje to regułę E06, która pozwalała naprawiać w każdej stoczni za drewno i srebro.

- **Dok** (`ProjectKind.Dock`, `Settlement.HasDock`): ulepszenie stoczni Miasta.
  - Warunek: Miasto ze stocznią. Miastem może zostać tylko port macierzysty z Warsztatem (3b), więc łańcuch to Warsztat → Miasto → Dok.
  - Koszt 200 D / 300 S, czas 45 s (`Rules.DockWood`, `DockSilver`, `DockSeconds`). Wartości własne, dobrane względem Miasta (300/400, 60 s) i Szkoły (200/500, 60 s).
  - Płatny jak inne budowy portu: rezerwacja, zużycie w trakcie, anulowanie jak przy zamówieniach.
- **Naprawa kadłuba** tylko w doku:
  - 25% pełnej ceny okrętu (zapłacone drewno i srebro razem) w srebrze za pełną naprawę, płatne według odbudowanego kadłuba;
  - 12 HP/s, jedno stanowisko na port, jak dawniej;
  - bez srebra naprawa czeka.
- **Naprawa modułów broni** tylko w doku: 25% wartości przedmiotu (drewno i srebro razem) razy brakująca część, w srebrze.
- **Zostaje bez doku:** przegrupowanie załogi (leczenie, zaciąg, przeszkolenie) i wymiana broni według planu działają w każdym porcie ze stocznią.
- **Rozkaz Remont.** Uszkodzony okręt płynie do najbliższego doku swojej strony, także obok bliższej stoczni (`World.RepairYard`). Rozkaz dla samego kadłuba lub modułu bez doku po swojej stronie jest odrzucany. Okręt bez rozkazu sam wchodzi do portu z powodu kadłuba tylko wtedy, gdy port ma dok.
- **AI** od 8. minuty buduje w porcie macierzystym kolejno Warsztat, Miasto i Dok (cena plus 150 S zapasu), a od 3c dalej Szkołę Mędrców i badanie broni ciężkiej.
  - W tym czasie linia okrętów odkłada cenę następnej budowy (`Opponent.BuildingSaving`), chyba że wróg jest przy porcie. Bez tego zamówienia okrętów rezerwowały całe srebro i AI nie stawiało nawet Warsztatu.
  - Kadłub naprawia dopiero wtedy, gdy ma dok. Okręty z przetrzebioną załogą wracają na przegrupowanie jak dawniej.
- **Zapis gry** ma wersję 15 (`HasDock`; bez `RepairWoodRemainder`).
- **Niezmiennik naprawy i złomowania.** Złomowanie zwraca 15% ceny razy stan kadłuba w obu surowcach, a naprawa kosztuje tylko srebro.
  - W sumie D+S naprawa przed złomowaniem nigdy się nie opłaca (test `Scrap_RepairingFirstNeverPaysBetter`).
  - W samym drewnie przestało to obowiązywać: naprawa i złomowanie zamieniają srebro na drewno. Dla taranu to ok. 1,8–1,9 S za 1 D (zależnie od podziału ceny), czyli strata przy srebrze rzadszym od drewna.
  - Dawny model ekonomii (`docs/economy-model`) sprawdzał tę pętlę osobno dla każdego surowca, przy naprawie za oba surowce.

**Pomiar z regułą doku** (simprof, AI kontra AI, 130 min, ziarna 9 i 23; „przed” to mapa z ograniczonym lądem bez reguły doku):

| Profil | Okręty wojenne bez walki (strona), przed → z dokiem | Mecze AI, zwycięstwo po, przed → z dokiem | Cel |
|---|---|---|---:|
| Krótka | 48–74 → 59–62 | 30–38 → 31–38 min | 30 min |
| Standardowa | 68–72 → 65–70 | 42–84 → 23–30 min | 60 min |
| Długa | 55–62 → 61–63 | 91–107 → 50–61 min | 90 min |

- **Budowy AI w grze bez walki:** Warsztat w 10.–16. min, Miasto w 18.–43. min, Dok w 25.–75. min.
- **Mecze Standardowe i Długie kończą się teraz przed celem.** Obecne AI traci flotę w jednym–dwóch starciach. Dawniej naprawiało kadłuby w każdej stoczni, a teraz zanim postawi dok (w meczach Długich ok. 37.–42. min), nie naprawia ich wcale. W meczach Standardowych zwycięstwo pada w 23.–30. min, zanim AI ukończy Miasto, więc koszt i czas doku nie są tu wąskim gardłem.
- **Sprawdzone warianty odkładania AI** (Standardowa, ziarna 9 / 23):
  - bez odkładania: 35 / 29 min;
  - odkładanie z wyjątkiem małej floty: 33 / 35 min;
  - odkładanie przerywane tylko zagrożeniem: 30 / 23 min.

  Został ostatni wariant, bo tylko w nim AI faktycznie dochodzi do Miasta i Doku.
- **Kosztu i czasu doku nie zmieniano.** Decyzja użytkownika z 2026-11-10: reguła doku zostaje, a długość meczów AI (cele 60/90 min) poprawi etap 7 (odwrót, obrona, fale, odbudowa). To pomiar AI kontra AI, nie playtest.

## Pomiar po etapie 3c, część 1 (2026-11-10)

simprof, AI kontra AI, 130 min, ziarna 9 i 23. „Z dokiem” to pomiar z sekcji wyżej.

| Profil | Mecze AI, zwycięstwo po: z dokiem → po 3c | Okręty wojenne bez walki (strona): z dokiem → po 3c | Cel |
|---|---|---|---:|
| Krótka | 31–38 → 13–18 min | 59–62 → 27–58 | 30 min |
| Standardowa | 23–30 → 36–38 min | 65–70 → 47–58 | 60 min |
| Długa | 50–61 → 31 min | 61–63 → 38–49 | 90 min |

Przyczyny ustalono na wariantach AI w kopii symulacji, poza projektem.

- **Długość meczów zmienia niszczyciel okrętów w cyklu AI.**
  - AI bez niego i bez Szkoły odtwarza dokładnie wyniki z dokiem: 31–38 / 23–30 / 50–61 min.
  - AI bez niego, ale ze Szkołą: 31–39 / 23–30 / 50–68 min.
  - Wyłączenie samej Szkoły i badania niczego nie zmienia, bo w meczach AI nie zdąża ich zbudować.
- **Mechanizm.**
  - AI broni się dopiero wtedy, gdy wróg podpłynie na 250 m do portu macierzystego.
  - Okręt bez rozkazu strzela tylko do wroga we własnym zasięgu, a łucznicy sięgają 100 m.
  - Balisty niszczyciela okrętów sięgają 180 m. Topią więc bezczynnych obrońców i okręty płynące do remontu bez odpowiedzi: w meczu Długim (ziarno 9) strona przegrywająca traci 10 okrętów, a zwycięska jeden.
  - Potem mury padają głównie od balist.
- **Sprawdzone szybkie poprawki nie przywracają dawnych długości** (Krótka / Standardowa / Długa):
  - balista słabsza na budynki (×0,5 zamiast ×1,25): 15–18 / 37–40 / 34 min;
  - AI odpowiada ogniem na wroga do 220 m od okrętu: 13–21 / 23–56 / 32–42 min.
- **W grze bez walki okrętów jest mniej.**
  - Szkoła i badanie kosztują razem 350 D / 800 S, a cykl z niszczycielem okrętów i bombowcem jest droższy.
  - AI stawia Szkołę w 27.–65. min. Badanie kończy w 46.–73. min, a w Krótkiej tylko raz, w 100. min.
  - Pierwszego bombowca buduje w 70.–82. min, najwyżej jednego na stronę w 130 min.
- **Placówki giną od pocisków wycelowanych w okręty.**
  - Strzał, który spada w promieniu 22 m od osady, uszkadza ją także wtedy, gdy celem był okręt przy nabrzeżu.
  - Balista zadaje murom ok. 56 punktów na strzał, więc tak burzy placówki nawet w grze bez walki. W Krótkiej jedna strona straciła 1–2 placówki.
- **Miernik czasu walki w narzędziu jest zaniżony.** Liczy tylko salwy łuków i oszczepów, taran i abordaż, więc nie widzi walki z balistami.

**Stan:** wartości klas i AI bez zmian. Decyzja o długości meczów AI czeka na użytkownika (etap 7 albo niszczyciel okrętów poza cyklem AI do etapu 7). To pomiar AI kontra AI, nie playtest.

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

**Decyzja użytkownika z 2026-11-10** (zastępuje wyspy zaplecza z etapu 1): w wersji testowej zdecydowanie ograniczyć ląd, który nie jest złożem srebra ani drzewami do wycinki; śródziemnomorski klimat antycznej Grecji z naciskiem na charakterystyczne ukształtowanie terenu; statki muszą być widoczne. Wybrane warianty: „kilka małych skał” oraz „ląd nie zasłania statków” (bez znaczników z oddali).

- **Bez wysp zaplecza.** `MapFactory` nie tworzy już `IslandKind.Hinterland` (wcześniej 11–14 dużych wysp z lasem, promień do ok. 134 m). Rodzaj i jego widok zostają tylko dla starszych zapisów gry.
- **Pięć małych skał wapiennych** (`MapFactory.AddRocks`) w miejsce pięciu dużych skał wybrzeża:
  - położenie: środek mapy, para w poprzek torów między parami centralnymi a ekspansjami, para za portami macierzystymi; pary są symetryczne punktowo;
  - rozmiar stały, niezależny od rozstawu: środek promień 22 m, pozostałe 16 m (najdalszy punkt linii wody);
  - kształt to linia wody modelu skalistego wybrzeża.
- **Warunki położenia skały** (`MapFactory.KeepsApproaches`):
  - 34 m podejścia do każdej przystani, złoża i wolnego brzegu;
  - wolne pochylnie stoczni;
  - między lądami pełny kanał 30 m albo połączenie.
  - Skała, która się nie mieści, odsuwa się od środka mapy co 5 m.
  - Przy okazji zniknęły dawne cieśniny 13–28 m między skałami za portami macierzystymi a wysepkami portów.
- **Zostają bez zmian:** wysepki portów i wolnych brzegów (budynki, przyczółki) oraz wysepki złóż (las do wycinki, kopalnia).
- **Zasłanianie strzałów.** Skały zasłaniają strzały jak dawniej wybrzeże (`Island.BlocksShots`). Na minimapie mają kolor lądu.

Powierzchnia lądu (ziarno 1, wielokąt linii wody):

| Profil | Ląd przed | Ląd po | Zmiana | Poza złożami przed / po |
|---|---:|---:|---:|---:|
| Krótka | 289 tys. m² | 31,9 tys. m² | −89% | 94% / 48% |
| Standardowa | 382 tys. m² | 31,8 tys. m² | −92% | 96% / 48% |
| Długa | 462 tys. m² | 32,3 tys. m² | −93% | 96% / 48% |

Wysepki złóż zajmują ok. 16,5 tys. m², wysepki portów i wolnych brzegów ok. 13,2 tys. m², a skały razem ok. 2,1 tys. m². Test `Map_LandIsMostlyWorkPointsAndEveryBerthStaysOpen` wymaga:
- braku wysp zaplecza;
- skał łącznie ≤ 25% lądu złóż;
- lądu poza złożami ≤ 1,5 × ląd złóż;
- podejść, kanałów i symetrii przy każdej skale.

**Drzewa.**
- **Las do wycinki** bez zmian: pinie i cyprysy w realnej skali (`TreeModels.LifeSize = 2,2`; pinia 11–18 m, cyprys 14–22 m), za stanowiskami pracy, z dala od przystani.
- **Skały** są bez drzew. Drzewa w realnej skali przerastałyby skałkę i zasłaniały przepływające kadłuby.
- **Wysepki portów i wolnych brzegów:** najwyżej 6 drzew, czyli niskie oliwki (`TreeKind.Olive`, ok. 6 m) i najwyżej 2 cyprysy, tylko po stronie odległej od przystani i pochylni.
- **Kopalnia:** oliwki i krzewy makii zamiast pinii.

**Pomiar balansu na nowej mapie** (narzędzie simprof, 130 min, ziarna 9 i 23):

| Profil | Okręty wojenne bez walki (strona), przed → po | Mecze AI kontra AI, zwycięstwo po, przed → po |
|---|---|---|
| Krótka | 70–72 → 48–74 | 25–65 min → 30–38 min |
| Standardowa | 29–37 → 68–72 | 25–35 min → 42–84 min |
| Długa | 58–67 → 55–62 | 76–125 min → 91–107 min |

- **Długość meczów AI** wypada teraz bliżej celów 30/60/90 min niż przed zmianą, więc parametrów profili nie zmieniano.
- **Wzrost w profilu Standardowym bez walki** wynika z gospodarki, nie z błędu. Na starej mapie kutry strony 0 prawie nie dowoziły drewna: do 120. min zostawało 74% złóż drewna, a w magazynie było 100–240 D przy ponad 4000 S. Wyspy zaplecza utrudniały dopływ do złóż. Teraz oba surowce schodzą równo, a srebro chwilowo spada poniżej 10% (76. min) i odrasta.
- **Walka w trybie bez ataków** (Krótka, do 2,8 min) to samoobrona przy przypadkowym spotkaniu okrętów.
- To pomiar AI kontra AI, nie playtest.

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

- ludzie tylko jako obsada okrętów: żeglarze, szermierze, oszczepnicy, łucznicy (plus bohaterowie);
- surowce w pełni odnawialne (ławice ryb, wraki po sztormach, neutralne konwoje), a ryby, bursztyn i szczątki zamieniane na D/S;
- przejęcie okrętu razem z uzbrojeniem.

Decyzje z uzgodnienia paczki specyfikacji (2026-11-10, [UZGODNIENIE_SPECYFIKACJI.md](UZGODNIENIE_SPECYFIKACJI.md)):

- **Nowe obszary w mapie drogowej**, z własnymi nazwami i liczbami: magia i świątynie, skarby i artefakty, druga cywilizacja i barbarzyńcy, potwory i czarownicy.
- **Naprawa** tylko w doku i tylko za srebro.
- **Nieudany abordaż** kosztuje atakującego całą załogę bojową, a okręt bez wojowników jest bezbronny.

Gdy wejdzie etap 5, długość gry wyznaczają cele scenariusza, a złoża z etapu 1 przestają być jedynym ograniczeniem.

| Etap | Zakres | Stan |
|---|---|---|
| 2 | Cztery klasy załogi (z oszczepnikami od 3a), składy wybierane w porcie, walka na burtach, ranni, przejęcie w całości, automatyczne przegrupowanie w porcie ([ZALOGA_I_ABORDAZ.md](ZALOGA_I_ABORDAZ.md)); porażka abordażu i bezbronność bez wojowników według uzgodnienia paczki | wdrożony |
| 3 | **3a:** stanowiska broni z obsługą, pociski z lotem i rozrzutem, moduły, pancerz, taran z zapowiedzią. **3b:** Warsztat (arsenał), Miasto, Szkoła Mędrców, badanie broni ciężkiej, wymiana broni w porcie, szablony floty. **3c:** niszczyciel okrętów i bombowiec, broń widoczna w 3D, pociski i stożki ostrzału, budynki na wyspie portu, budowa bez domyślnego uzbrojenia ([UZBROJENIE.md](UZBROJENIE.md)) | 3a, 3b wdrożone; 3c: symulacja wdrożona (klasy, budowa bez broni, AI), prezentacja po partiach A–C |
| 4 | Pełny zestaw klas: taranowiec, łowca ludzi, niszczyciel okrętów, bombowiec, abordażowiec, okręt królewski, podpalacz, okręt z wapnem; łodzie rybackie i kupcy; unikalne okręty drugiej cywilizacji i warianty barbarzyńskie (własne nazwy) | plan |
| 5 | Żywa ekonomia: migrujące ławice, bursztyn, wraki po sztormach, neutralne konwoje, szczątki potworów, polowania na lewiatany i Ketosa (nagroda w D/S, własne liczby); zagrożenia: wiry, rafy, harpie, meduzy; wrogie stwory morskie, potwory lądowe z siedzibami, chaty czarowników | plan |
| 6 | Osady: forty przybrzeżne z limitem siły na osadę, Kuźnia (pancerze i tarany: zwykłe, srebrne, złote), badania pancerza, taranu i prędkości, transport sprzętu handlowcem, sprzedaż obiektów, rozbudowa portu (dok wdrożony przy uzgodnieniu paczki) | plan |
| 7 | Bohaterowie (aury, zdolności, ratunek z wraku), skarby i artefakty; programowanie zachowania okrętów (straż, taran, abordaż, ogień na napastnika, konwój, zwiad, patrol, strefy straży i ataku), szyk eskadry, aktywna pauza, wskaźniki taktyczne; AI z falami, odbudową i obroną przed ostrzałem z dystansu, które przywróci długość meczów AI (Krótka 30, Standardowa 60, Długa 90 min; po 3c 13–18, 36–38 i 31 min); druga cywilizacja i barbarzyńcy jako strony AI (abordaż okrętu barbarzyńców go topi) | plan |
| 8 | Wiatr, prądy, żagle i wiosła; nieliniowa kampania z zachowaniem floty, weteranów i artefaktów (fabuła i misje własne) | plan |
| 9 | Świątynie morza i wojny, zaklęcia za D/S z zasięgiem od świątyni (wiry, rafy, wiatr, ogień, osłony; własny dobór i liczby) | plan |

## Granice weryfikacji

- Bramka jakości (EditMode/PlayMode) obejmuje logikę i widoki. Nie obejmuje oceny wyglądu nowych wysp ani wydajności GPU z większą liczbą drzew; potrzebny jest test dymny buildu i ocena na zrzutach.
- Pomiary balansu to model AI kontra AI, nie playtest z graczem.
