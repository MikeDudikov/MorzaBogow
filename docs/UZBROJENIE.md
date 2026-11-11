# Morza Bogów — uzbrojenie, budowle portu i klasy okrętów (etap 3)

Data: 2026-11-10. Gałąź: `balans-skala`. Status:
- 3a i 3b wdrożone;
- 3c część 1 (symulacja: niszczyciel okrętów, bombowiec, budowa bez domyślnego uzbrojenia, AI) wdrożona;
- 3c część 2 (prezentacja: budynki na wyspie portu, budowa w toku i ruiny, zapowiedź taranu w 3D, stożki ostrzału, ikony nowych klas) wdrożona; opis niżej w „Prezentacja (3c część 2)”.

Liczby są własne Mórz Bogów. Wszystkie obliczenia należą do deterministycznej symulacji (`Runtime/Simulation`), a prezentacja tylko je czyta.

## Stanowiska i broń (3a)

Okręt wojenny ma stałą listę gniazd: lekkie, średnie albo ciężkie. Broń pasuje tylko do gniazda swojego rozmiaru (`WeaponCatalog.Fits`).

| Broń | Gniazdo | Obsługa | Zasięg m | Cykl s | Kadłub / ludzie / moduł | Łuk | Lot m/s | Rozrzut | Budynki | Warsztat D/S, czas |
|---|---|---|---|---:|---|---:|---:|---|---:|---|
| Stanowisko łuczników | lekkie | 4 łuczników | 15–100 | 2 | 2 / 3,5 / 2 | 360° | 45 | 6 | ×1 | — |
| Stanowisko oszczepników | lekkie | 4 oszczepników | 5–45 | 3 | 3 / 6 / 2 | 360° | 28 | 3 | ×1 | — |
| Skorpion | średnie | 3 | 15–140 | 3 | 12 / 8 / 6 | 270° | 70 | 4 | ×1 | 25/15, 20 s |
| Gastrafetes | średnie | 2 | 5–90 | 1,5 | 3 / 10 / 2 | 270° | 60 | 3 | ×0,5 | 15/20, 20 s |
| Balista pokładowa | średnie | 6 | 30–180 | 5 | 45 / 4 / 18 | 270° | 75 | 4 | ×1,25 | 35/30, 30 s |
| Katapulta kamienna | średnie | 6 | 40–200 | 8 | 40 / 10 / 15 | 120° | 45 | 9, odłamki 4 m, tor stromy | ×2 | 40/15, 30 s |
| Balista z pociskami ołowianymi | ciężkie | 6 | 50–240 | 6 | 70 / 4 / 25 | 180° | 80 | 4 | ×1,25 | 50/45, 40 s |
| Ciężka katapulta (ołów) | ciężkie | 8 | 60–260 | 10 | 100 / 20 / 30 | 120° | 45 | 12, odłamki 8 m, tor stromy | ×2 | 60/50, 45 s |
| Onager (ołów) | ciężkie | 8 | 100–320 | 14 | 160 / 12 / 30 | 120° | 40 | 14, odłamki 6 m, tor stromy | ×2,5 | 70/60, 55 s |

- **Obsługa** (`World.Manned`):
  - najpierw stanowiska łuczników i oszczepników, każde z czterech ludzi swojej klasy;
  - potem machiny: od szermierzy, potem z pozostałych oszczepników i łuczników;
  - żeglarze zostają przy wiosłach.

  Stanowisko bez pełnej obsługi nie strzela i się nie ładuje.
- **Ładowanie.** Cykl napełnia się w tempie 0,75 + 0,25 × morale/100. W abordażu sczepione okręty nie strzelają, ale ładunek zostaje.
- **Cel.**
  - Okręt strzela do celu z rozkazu, jeśli ten jest w zasięgu i łuku. Inaczej strzela do najbliższego widocznego wroga w zasięgu, chyba że taranuje.
  - Łuk liczy się od dziobu: połowa kąta na każdą burtę.
  - Ląd zatrzymuje płaski strzał. Pocisk o stromym torze przelatuje nad lądem.
- **Pocisk.**
  - Celowanie z wyprzedzeniem: działo mierzy w miejsce, w którym będzie cel po czasie lotu.
  - Rozrzut to koło o promieniu max(0,5; rozrzut × odległość / zasięg) × (1 + 0,25 × względna prędkość strzelającego). Punkt upadku losuje się raz z ziarna przy wystrzale.
  - Pocisk naprawdę leci: czas lotu wynika z prędkości broni, a tor stromy trwa co najmniej 1,5 s.
- **Upadek.**
  - Pocisk spadający na ląd przepada.
  - Na wodzie trafia raz każdy wrogi kadłub pod punktem upadku, a przy odłamkach każdy w ich promieniu.
  - Osada w promieniu 22 m (plus odłamki) traci tyle kadłuba, ile pocisk zadaje okrętom, pomnożone przez mnożnik budynków.
- **Trafienie** (`World.ApplyProjectile`):
  - kadłub dostaje obrażenia pomniejszone o pancerz przeciwpociskowy klasy (cała ochrona najwyżej 70%);
  - część „ludzie” zadaje obrażenia załodze; 10 punktów to jeden człowiek, a pierwsza pada najliczniejsza grupa wojowników na pokładzie;
  - część „moduł” uszkadza jedno losowe zamontowane stanowisko.
- **Moduł** ma 100 punktów. Przy zerze stanowisko milczy aż do naprawy w doku: 25% wartości przedmiotu razy brakująca część, w srebrze, najwyżej 10 s za cały moduł.

## Taran (3a)

- Taran ma każdy okręt wojenny. Kutry i handlowce go nie mają.
- **Zapowiedź.**
  - Szarżę woła się, gdy cel jest 2,5 s żeglugi od okrętu. Uderzyć wolno najwcześniej 1,2 s po zawołaniu.
  - Kontakt bez zapowiedzi nie zadaje obrażeń.
  - Kolejna szarża wymaga 8 s przerwy i odejścia na długość kadłuba (cofanie 3 s).
- **Obrażenia:** 120 × (prędkość zbliżania / 8)² × mnożnik taranu klasy × kąt × stosunek mas (0,5–1,5), najwyżej 400.
  - Kąt: w burtę 1, od rufy 0,75, czołowo 0,5, a przy błędzie kursu dziobu ponad 0,35 rad tylko 0,25.
  - Poniżej 3 m/s zbliżania uderzenie się nie udaje.
  - Atakujący sam dostaje 20% zadanych obrażeń.
  - Pancerz przeciwpociskowy nie chroni przed taranem.

## Klasy okrętów

| Klasa | Kadłub | Prędkość | Załoga | Gniazda | Domyślna broń | Pancerz | Taran | Cena D/S | Bez broni D/S | Budowa | Warunek |
|---|---:|---:|---:|---|---|---:|---:|---|---|---:|---|
| Taranowiec | 760 | 8,2 | 36 | L, Ś | łucznicy, — | 15% | ×1,5 | 240/100 | 224/86,3 | 35 s | stocznia |
| Niszczyciel załóg | 520 | 6,8 | 52 | L, L, L, L | 4 × łucznicy | 10% | ×0,6 | 180/90 | 156/72,2 | 30 s | stocznia |
| Abordażowiec | 780 | 5,2 | 90 | L, L, Ś | 2 × oszczepnicy, — | 15% | ×0,7 | 200/180 | 180/149,55 | 40 s | stocznia |
| **Niszczyciel okrętów** (3c) | 620 | 6,5 | 36 | Ś, Ś | 2 × balista pokładowa | 15% | ×0,8 | 220/140 | 187/91,17 | 38 s | stocznia |
| **Bombowiec** (3c) | 960 | 4,3 | 45 | C, C | 2 × ciężka katapulta | 20% | ×0,5 | 400/260 | 331,42/146,34 | 60 s | Warsztat w tym porcie i badanie broni ciężkiej |

L – lekkie, Ś – średnie, C – ciężkie gniazdo.

- **Ceny** są kompletnymi szablonami (E04): kadłub, załoga, broń i taran razem.
  - Udział uzbrojenia dzieli się na przedmioty wagami: lekkie 1, średnie 3, ciężkie 6, taran 2.
  - Drewno i srebro dzieli się osobno, a części sumują się dokładnie do udziału uzbrojenia (test).
- **Niszczyciel okrętów** to mały kadłub z dwiema balistami: łamie kadłuby i budynki z dystansu.
- **Bombowiec** to najcięższy i najwolniejszy okręt artyleryjski. Ciężkie katapulty z ołowiem rażą odłamkami i burzą mury. Strzelają tylko w łuku 120° od dziobu.
- **Domyślne składy załogi:** niszczyciel okrętów i bombowiec mają skład Wyważony ([ZALOGA_I_ABORDAZ.md](ZALOGA_I_ABORDAZ.md)). Obsługa machin mieści się w części bojowej.
- **Nowe klasy dopisano na końcu** `ShipClass`, bo zapis gry przechowuje klasę jako liczbę.

## Budowa bez domyślnego uzbrojenia (3c)

- **Przełącznik.** W menu zamówień linii wojennej jest przełącznik „Domyślne uzbrojenie: tak/nie”, zapamiętany w ustawieniach gracza.
- **Cena.** Kadłub bez broni kosztuje cenę szablonu minus wartość przedmiotów w gniazdach (`World.OrderPrice`). Taran zostaje i jest wliczony w cenę.
- **Wodowanie.** Okręt schodzi na wodę z pustymi gniazdami i pustym planem uzbrojenia (`BuildOrder.Unarmed`). Broń dobiera się potem planem: port montuje ją z arsenału (wymiana broni poniżej).
- **Szablon floty** zawsze buduje okręt z bronią według szablonu, więc przełącznik go nie dotyczy. Kuter i handlowiec nie mają gniazd i przełącznik ich nie zmienia.
- **Zapis gry** ma wersję 16. Zamówienie pamięta, że kadłub ma zejść bez broni, także w trakcie budowy (test zapisu w połowie budowy i księgi D/S).

## Budowle portu i badanie (3b, dok z uzgodnienia paczki)

| Budowa | Warunek | Koszt D/S | Czas | Efekt |
|---|---|---|---:|---|
| Warsztat | port ze stocznią | 150/100 | 40 s | linia machin do arsenału portu |
| Miasto | port macierzysty ze stocznią i Warsztatem | 300/400 | 60 s | mury ×1,25 |
| Szkoła Mędrców | Miasto | 200/500 | 60 s | badania |
| Badanie broni ciężkiej | Szkoła Mędrców i Warsztat w tym porcie | 150/300 | 90 s | ciężkie machiny i bombowiec dla całej strony |
| Dok | Miasto ze stocznią | 200/300 | 45 s | naprawa kadłubów i modułów, tylko za srebro ([BALANS_I_SKALA.md](BALANS_I_SKALA.md#naprawa-w-doku-uzgodnienie-paczki-2026-11-10)) |

- **Projekty.** Port prowadzi jeden projekt naraz.
- **Płatność** jak za zamówienia okrętów, z lokalnego magazynu:
  - przy zamówieniu rezerwacja całej ceny;
  - zużycie z postępem budowy;
  - po rozpoczęciu anulowanie zwraca 80% niezużytej części.
- **Warsztat** wytwarza same machiny, jedną naraz, za cenę i w czasie z tabeli broni. Stanowisk łuczników i oszczepników nie wytwarza: to obsada, nie przedmioty. Ciężkie machiny są dostępne dopiero po badaniu.
- **Plan uzbrojenia** (`SetLoadout`) można zmienić w dowolnym miejscu. Samą broń wymienia się w porcie ze stocznią przy przegrupowaniu:
  - zdjęcie broni trwa 3 s, a machina wraca do arsenału;
  - montaż trwa 5 s dla machiny średniej i 10 s dla ciężkiej;
  - zamontowana broń jest nienaładowana i ma wartość ceny Warsztatu.

  Brakującą broń port zamawia sam, a okręt na nią nie czeka.
- **Szablony floty** — do 8 na stronę. Każdy zapisuje klasę, plan uzbrojenia i skład załogi.
  - Zamówienie z szablonu kosztuje cenę klasy.
  - Port od razu zamawia brakujące bronie inne niż domyślne.
  - Zwodowany okręt dostaje plan i skład, a resztę robi przegrupowanie.
- **Przejęty okręt** zachowuje broń razem z jej stanem (test).

## AI (3c)

- **Budowy.** Od 8. minuty AI buduje w porcie macierzystym kolejno: Warsztat, Miasto, Dok, Szkołę Mędrców i badanie broni ciężkiej.
  - W tym czasie linia okrętów odkłada cenę następnej budowy (`Opponent.BuildingSaving`).
  - Odkładanie przerywa tylko wróg przy porcie.
- **Cykl okrętów:** taranowiec, niszczyciel załóg, abordażowiec, niszczyciel okrętów. Co piąty okręt jest bombowcem, jeśli port ma Warsztat, a strona ma badanie. Inaczej w tym miejscu powstaje okręt z cyklu.
- Test sprawdza, że AI dochodzi do Szkoły, badania i pierwszego bombowca.
- **Mecze AI kontra AI po 3c.** Niszczyciel okrętów je skraca (Krótka 13–18, Długa 31 min), bo balisty sięgają dalej, niż AI zaczyna się bronić. Bombowiec pojawia się w AI rzadko: pierwszy w 70.–82. min. Pomiar i przyczyny: [BALANS_I_SKALA.md](BALANS_I_SKALA.md#pomiar-po-etapie-3c-część-1-2026-11-10).

## Pojedynki i oblężenie (pomiar 3c, symulacja .NET bez Unity)

**Ustawienie:**
- dwa okręty 180 m od siebie, dziobami do siebie, z pełnymi załogami i domyślną bronią;
- każdy atakuje po swojemu: taranowiec taranuje, abordażowiec abordażuje, pozostałe strzelają;
- abordażowiec, któremu zostało mniej niż 10 wojowników, strzela;
- limit 300 s;
- każdą parę mierzono w obu ustawieniach (zamiana stron i ziarna 11/12).

W nawiasie: kadłub zwycięzcy na końcu.

| Para | Wynik |
|---|---|
| Taranowiec – taranowiec | remis bez obrażeń: okręty krążą i żaden nie wchodzi w zapowiedzianą szarżę |
| Taranowiec – niszczyciel załóg | niszczyciel załóg zatapia po 220–238 s (44–100%) |
| Taranowiec – abordażowiec | abordażowiec zdobywa po 27–29 s (94%) |
| Taranowiec – niszczyciel okrętów | niszczyciel okrętów zatapia po 65–69 s (39%) |
| Taranowiec – bombowiec | taranowiec zatapia po 158 s (30–41%) albo po 300 s bombowcowi zostaje 4% |
| Niszczyciel załóg – niszczyciel załóg | remis po 300 s (49–74%) |
| Niszczyciel załóg – abordażowiec | abordażowiec zdobywa po 39 s (91%) |
| Niszczyciel załóg – niszczyciel okrętów | niszczyciel okrętów zatapia po 31 s (91–92%) |
| Niszczyciel załóg – bombowiec | bombowiec zatapia po 22–32 s (93–96%) |
| Abordażowiec – abordażowiec | zdobyty po 30 s |
| Abordażowiec – niszczyciel okrętów | abordażowiec zdobywa po 38 s (51%) |
| Abordażowiec – bombowiec | abordażowiec zdobywa po 39 s (56%) |
| Niszczyciel okrętów – niszczyciel okrętów | zatopiony po 57–62 s (14–20%) |
| Niszczyciel okrętów – bombowiec | niszczyciel okrętów wygrywa 3 z 4 (108 s, 4–18%), bombowiec 1 z 4 (42 s, 48%) |
| Bombowiec – bombowiec | zatopiony po 73–83 s (33–42%) |

**Z utrzymywaniem dystansu.** Okręt strzelający odpływa, gdy przeciwnik jest bliżej niż max(najmniejszy zasięg + 15 m; 0,55 zasięgu), a potem zawraca i strzela, jak gracz.

| Para | Wynik |
|---|---|
| Niszczyciel załóg – taranowiec | niszczyciel załóg zatapia po 275–279 s (88%) |
| Niszczyciel załóg – abordażowiec | niszczyciel załóg zatapia po 242 s (97%) |
| Niszczyciel okrętów – taranowiec | po 300 s taranowcowi zostaje 40–50%, niszczyciel okrętów bez strat |
| Niszczyciel okrętów – abordażowiec | niszczyciel okrętów zatapia po 88 s bez strat |
| Bombowiec – taranowiec | po 300 s taranowcowi zostaje 78–89%, bombowiec bez strat |
| Bombowiec – abordażowiec | po 300 s abordażowcowi zostaje 78%, bombowiec bez strat |

**Oblężenie placówki** (mury 900). Okręt zaczyna 180 m od placówki i dostaje rozkaz ataku osady.

| Klasa | Wynik |
|---|---|
| Bombowiec | mury padają po 22 s (6 strzałów) |
| Niszczyciel okrętów | po 36 s (16 strzałów) |
| Niszczyciel załóg | po 243 s |
| Taranowiec | po 300 s zostaje 618/900 (jedno stanowisko łuczników) |
| Abordażowiec | bez obrażeń: rozkaz podprowadza okręt na 80 m, a oszczepnicy sięgają 45 m |

**Wnioski:**
- **Role nowych klas się zgadzają.**
  - Niszczyciel okrętów najszybciej topi kadłuby i burzy budynki.
  - Bombowiec najszybciej burzy mury i rozbija lekkie okręty z dystansu, ale pada łupem taranu i abordażu.

  Wartości klas nie zmieniano.
- **Na jednostkę kosztu bombowiec burzy mury tak samo jak niszczyciel okrętów:** ok. 0,06 punktu muru na sekundę na jednostkę D+S. Przewagą bombowca są zasięg 260 m, odłamki i siła pojedynczego okrętu.
- **Abordażowiec wygrywa każdy pojedynek 1:1, gdy przeciwnik sam do niego podpływa.** Rozkaz ataku okrętu albo osady (`Pursue`) prowadzi każdy okręt na 80 m, czyli 0,8 zasięgu łucznika. Dotyczy to także bombowca (zasięg 260 m) i niszczyciela okrętów (180 m). Gdy okręt strzelający trzyma dystans, wygrywa albo nie daje się złapać. Odległość podejścia zależna od broni to decyzja na później. Na razie zostaje tak, jak było w 3a.
- **Dwa taranowce w lustrzanym ustawieniu nie zadają sobie obrażeń przez 300 s.** Taranowanie celu, który manewruje, należy do zachowań okrętów z etapu 7.
- To pomiar symulacji, nie playtest.

## Interfejs (3c część 1)

- **Menu zamówień** linii wojennej pokazuje pięć klas wojennych z ceną zależną od przełącznika uzbrojenia. Bombowiec bez Warsztatu w porcie albo bez badania jest niedostępny, a podpowiedź podaje powód (`World.ShipProblem`).
- **Ikony nowych klas** zastąpiła 3c część 2 (niżej).

## Prezentacja (3c część 2)

Prezentacja tylko czyta symulację; w `Runtime/Simulation` nic się nie zmieniło. Kadłuby, broń na stanowiskach, załogi, pociski i pochylnie były już wcześniej (GreekShips, ShipVisual, Crew/*, BattleEffects, ShipyardVisual) i nie są dublowane.

### Budynki portu (`Runtime/Presentation/PortBuildings*.cs`)

Stan z 2026-11-11: budowle IV w. p.n.e. (decyzja użytkownika). Budynki stoją na lądowej połowie wyspy portu (x w stronę portu od −14,5 do 1 m, |z| < 9,5 m), tarasami na zboczu: każdy ma stopę na najniższym gruncie pod sobą. Dok stoi nad wodą. Pomost, pochylnie, punkt magazynu tragarzy i oś pomostu zostają wolne (testy).

| Stan osady | Wygląd |
|---|---|
| port macierzysty, zawsze | ciosowy magazyn i tynkowany dom na kamiennych cokołach, dachy z dachówką i antefiksami; przed nimi pithoi, amfory i kotwice |
| przyczółek, zawsze | obóz polowy: częstokół z bramą od portu, dwa namioty z lnu, łódź wyciągnięta na brzeg i położona dnem do góry na kozłach, palenisko, skrzynie, amfory i worki |
| `HasWorkshop` | ergasterion: otwarta wiata na słupach od strony linii wojennej, palenisko pod kominem, miech z dwóch skórzanych worków, kowadło, koryto do hartowania, rama katapulty na kozłach |
| `IsTown` | cztery domy na tarasach i mur z ciosów z basztami wokół działki, z bramą od portu |
| `HasSageSchool` | hala w doryckim perystylu 4 × 6 na najwyższym tarasie |
| `HasDock` | hangar okrętowy typu Zea (R-ZEA: typ, nie wymiar) obok pochylni linii wojennej i pomostu: kamienna rampa z drewnianymi płozami schodzi do morza między dwiema kolumnadami z wapienia, niski dach z dachówki, ściana od lądu; do tego kozioł na końcu pomostu nad miejscem naprawy |

- **Hangar** nie mieści okrętów gry. Światło hangarów w Zei miało 5,81 m, a okręty gry mają do 10,9 m szerokości, więc naprawa odbywa się przy nabrzeżu, pod kozłem. Hangar stoi od u = 12 m, z dala od kadłuba na pochylni z rusztowaniem (do u ≈ 10,8 m), i od z = −2,7 m, poza pomostem. Nie przykrywa pochylni ani okrętów.
- **Pomost** jest drewniany (`PortJetty`): pokład z desek na trzech podłużnicach, pale ze smołowanego drewna z oczepami i zastrzałami, krawężniki i dwa polery na głowicy. Leży w miejscu dawnego (u 14–23,5, |z| ≤ 2, wierzch 1,375 m) na każdej osadzie i wolnym brzegu.
- **Rekwizyty stoczni** (`ShipyardProps`) omijają pola z `PortBuildings.Reserved`: miejsce hangaru, front muru z bramą i stos tragarzy.
- **Budowa w toku** (`Settlement.Project`, `ProjectKind`, postęp `DoneTicks/TotalTicks`) przebiega bez skalowania (test), według progów pochylni:
  - do 15%: fundament i paliki;
  - 15–80%: warstwy murów, kolumny, odcinki muru i nogi kozła stawiane po kolei wśród rusztowań;
  - od 80%: dach;
  - rusztowanie znika przy 97%;
  - stojący budynek to jedna scalona siatka.

  `PortBuildings.WorkSpots` podaje miejsca dla przyszłych budowniczych (hangar od strony lądu) i naprawiających. Postacie dodaje osobne zadanie (ShoreMan w CrewAshore).
- **Badanie broni ciężkiej** nie stawia budynku: przed Szkołą płonie trójnóg z ogniem.
- **Zniszczenia.** Każda budowla, także dok i obóz, pęka element po elemencie z kadłubem osady ([art/DESTRUCTION.md](art/DESTRUCTION.md)):
  - kolumny hangaru i nogi kozła padają;
  - ściana od lądu osuwa się w gruz;
  - belki i przęsła dachu spadają;
  - odcinki częstokołu przewracają się na zewnątrz;
  - płótno namiotów opada.

  Zniszczona osada zostaje z wyspą i ruinami swoich elementów. Kształt gruzu wynika z numeru osady i jest taki sam na każdym systemie, niezależnie od przecinka dziesiętnego (test). Widoczność jak przy osadzie: własna zawsze, wroga w zasięgu wzroku. Wolny brzeg w tym miejscu jest ukryty, dopóki stoją ruiny, a ruiny znikają, gdy na tym brzegu stanie nowa osada.
- **Materiały:** ciosy i tynk wypalone w kodzie (`Buildings/Ashlar`, `Buildings/Stucco`), farby palety okrętów, drewno sceny.
- **Siatki** składa generator okrętów (`GreekShips.MeshBuilder`). Siatki elementów zwalniają się razem z widokiem osady.
- **Do zrobienia:** LOD budowli, dom z dziedzińcem i stoa, stocznia i pochylnie IV w. (ShipyardVisual).

### Zapowiedź taranu (`RamTelegraphView.cs`)

Od zawołania szarży (`RamPhase.Telegraph`) do uderzenia albo odskoku na morzu leży pas od dziobu taranującego do celu.
- **Lont.** Pas napełnia się jak lont: `1 − TelegraphTicks / ceil(RamTelegraphSeconds · 30)`. Pełny oznacza, że uderzenie się liczy, i wtedy pulsuje.
- **Pierścień** wokół celu bije coraz szybciej.
- **Błysk** przy dziobie oznacza chwilę zawołania.

Kolory są jak w interfejsie, czytelne dla obu stron:
- własny taran: turkus sojusznika;
- taran wroga: czerwień wroga.

Taranującego, którego gracz nie widzi, nie rysuje się wcale.

### Stożki ostrzału (`FiringArcs.cs`)

Rysowane dla zaznaczonych własnych okrętów z bronią, najwyżej 8 naraz.

**Kształt.** Każdy typ zamontowanej broni ma wycinek od najmniejszego do największego zasięgu z `WeaponCatalog`. Rozciąga się na połowę łuku na każdą burtę od dziobu i jest liczony od środka okrętu, dokładnie jak `World.InReach` (test porównuje oba w ponad tysiącu punktów).

**Paski stanowisk.** Wewnątrz dalekiej krawędzi każde zamontowane stanowisko tego typu ma własny pasek, kolejne w głąb:

| Stan (jak na karcie okrętu) | Pasek |
|---|---|
| gotowe | turkus sojusznika, ciągły |
| bez obsługi (`World.Manned`) | bursztyn, przerywany |
| zniszczone (`ModuleHp ≤ 0`) | czerwień, przerywany |

Puste gniazdo nie ma stożka.

### Ikony klas

`ship-killer` (kadłub i balista z ciężkim bełtem) i `bomber` (kadłub, ramię katapulty, kamień i jego tor) w 64 i 128 px. Rysuje je `Editor/HudClassIcons.cs` w kolorze i grubości linii ikon pakietu interfejsu: kolor zmierzony z `archer-ship-128.png`. Każda klasa ma własną ikonę (test).

## Granice weryfikacji

- **Bramka M1** (3c część 1): EditMode 180/180, PlayMode 63/63. Determinizm i księga D/S są sprawdzane w testach zapisu i meczu.
- **Pojedynki i mecze AI** zmierzono w symulacji .NET (simprof), poza Unity. To nie jest playtest z graczem.
- **Wyglądu nowych klas nie oceniono:** wejdzie w 3c części 2.
