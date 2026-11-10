# Morza Bogów — załoga, abordaż i przegrupowanie w porcie (etap 2, oszczepnicy od 3a)

Data: 2026-11-10. Gałąź: `balans-skala`. Decyzje użytkownika:
- ludzie istnieją wyłącznie jako obsada okrętów;
- trzy klasy ludzi: żeglarze, szermierze i łucznicy, a od etapu 3a czwarta: oszczepnicy (decyzja z 2026-11-10);
- skład załogi wybiera się w porcie presetem, a szablony floty z etapu 3 będą go zapisywać;
- liczebność w dotychczasowej skali, 36–90 ludzi;
- ceny okrętów bez zmian.

## Klasy ludzi

| Klasa | Rola |
|---|---|
| Żeglarze | Sterują, wiosłują i zbierają surowce. Prędkość zależy wyłącznie od nich: mniej niż ¼ załogi spowalnia okręt proporcjonalnie, a 40% (skład Żegluga) daje +8% prędkości. Nie walczą na pokładzie (od uzgodnienia paczki 2026-11-10). |
| Szermierze | Walka wręcz: niosą abordaż, odpierają go i przecinają liny abordażowe wroga. Jako pierwsi obsadzają machiny. |
| Oszczepnicy | Obsadzają stanowiska oszczepników (po czterech, rzut na 5–45 m). Na pokładzie walczą za szermierzami, a spoza frontu wspierają jak łucznicy. |
| Łucznicy | Obsadzają stanowiska łuczników (po czterech, 15–100 m); w abordażu wsparcie z dystansu i obrona własnego pokładu. |
| Ranni | Dwóch na pięciu poległych. Nie walczą i nie wiosłują, zostają na pokładzie do przegrupowania w porcie. |

Bohaterowie mitologiczni wchodzą w etapie 7.

## Składy (`CrewPreset`, `ShipDefinition.PlanFor`)

| Skład | Żeglarze | Szermierze / oszczepnicy / łucznicy w części bojowej |
|---|---|---|
| Wyważony | 25% | 40% / 25% / 35% |
| Abordaż | 25% | 60% / 30% / 10% |
| Ostrzał | 25% | 15% / 20% / 65% |
| Żegluga | 40% | 40% / 25% / 35% |

Domyślne składy klas (od 3a z oszczepnikami):

| Klasa | Skład | Żeglarze / szermierze / oszczepnicy / łucznicy |
|---|---|---|
| Taranowiec | Wyważony | 9 / 11 / 7 / 9 |
| Niszczyciel załóg | Ostrzał | 13 / 6 / 8 / 25 |
| Abordażowiec | Abordaż | 23 / 40 / 20 / 7 |
| Niszczyciel okrętów (3c) | Wyważony | 9 / 11 / 7 / 9 |
| Bombowiec (3c) | Wyważony | 12 / 13 / 8 / 12 |
| Kuter, handlowiec | — | sami żeglarze |

Każdy domyślny skład ma tylu żeglarzy i wojowników, ilu zakłada cena E04, więc ceny szablonów się nie zmieniły (test).

## Ostrzał

Od etapu 3a strzelają stanowiska broni ([UZBROJENIE.md](UZBROJENIE.md)):
- **Stanowisko łuczników.** Obsadza je czterech łuczników. Strzela co 2 s na 15–100 m, a trafienie zadaje kadłubowi 2 i załodze 3,5 (10 punktów to jeden człowiek). Cztery stanowiska niszczyciela załóg ranią załogi tak jak dawniej jego 29 łuczników (test).
- **Pocisk.** Każdy pocisk leci naprawdę. Punkt upadku losuje się raz z ziarna przy wystrzale.
- **Kiedy nie strzela.** Stanowisko bez pełnej obsługi nie strzela. Okręty sczepione w abordażu nie prowadzą zwykłego ostrzału.
- **Kto pada.** Pociski trafiają najpierw ludzi na pokładzie, z najliczniejszej grupy bojowej. Żeglarze przy wiosłach padają na końcu.

## Abordaż

1. **Haki (3 s).** Liny zrywa odpłynięcie celu albo różnica prędkości, jak dawniej. Szermierze obrońcy tną liny w tempie 0,012 na sekundę za każdego szermierza ponad połowę szermierzy atakującego. Abordażowiec przecina tak haki taranowca w około 2,4 s. Między równymi okrętami liny trzymają.
2. **Walka na burtach, runda co 1 s:**
   - Front: walczy 4 + ⌊szerokość węższego pokładu w m⌋ ludzi na stronę. Najpierw stają szermierze, potem oszczepnicy, potem łucznicy. Żeglarze nie walczą na pokładzie (uzgodnienie paczki, 2026-11-10): okręt bez wojowników nie stawia oporu.
   - Siła: atak lub obrona ludzi we froncie (tabela poniżej), plus wsparcie oszczepników i łuczników spoza frontu (0,3 w ataku, 0,4 w obronie), razy (0,5 + morale/100).
   - Straty: każda strona traci w ludziach 0,15 × siła przeciwnika. Ułamki przechodzą na następną rundę. Padają ludzie z frontu, najpierw z liczniejszej grupy.
   - Morale: spada o 120 × poległe/pełna załoga.

   | Klasa | Atak | Obrona |
   |---|---|---|
   | Szermierz | 1,2 | 1,1 |
   | Oszczepnik | 0,9 | 1,0 |
   | Łucznik | 0,4 | 0,9 |

3. **Kapitulacja.** Załoga z morale poniżej 20 przez 3 s kontroli poddaje okręt (`ship_surrendered`, `ship_captured`). Okręt bez wojowników nie walczy wcale: po 3 s kontroli zostaje przejęty bez strat atakującego. Dotyczy to też kutrów i handlowców bez wojowników.
4. **Porażka atakującego** (decyzja użytkownika z uzgodnienia paczki, [UZGODNIENIE_SPECYFIKACJI.md](UZGODNIENIE_SPECYFIKACJI.md)).
   - Gdy atakującemu skończą się wojownicy na wrogim pokładzie (`boarding_repelled`), traci całą załogę bojową: poległych i rannych z tego abordażu (`BoardingLink.AttackerWoundedAtStart`).
   - Ranni sprzed ataku zostają. Żeglarze wracają z okrętem do portu.
   - Do przeszkolenia nowych wojowników w porcie okręt nie może abordażować (minimum 10 wojowników) i nie obroni własnego pokładu.
   - Przecięcie lin przed walką nie jest taką porażką: nikt nie przeszedł na wrogi pokład.
5. **Przejęcie w całości.**
   - Kadłub, ładunek, zapłacony koszt i wyposażenie zostają.
   - Żeglarze wiosłują dla nowego właściciela, ranni wroga przepadają.
   - Na pokład przechodzi załoga pryzowa: min(wojownicy atakującego, ¼ załogi okrętu), najpierw szermierze. Gdy zdobyczy brakuje żeglarzy, część pryzowych siada do wioseł.

Pojedynki zmierzone w symulacji: pełne załogi z oszczepnikami (pomiar 3c), czas od zarzucenia haków. Abordażowiec ma 67 wojowników. Po zdobyciu część z nich przechodzi na zdobycz jako załoga pryzowa.

| Atak → cel | Wynik |
|---|---|
| Abordażowiec → taranowiec | Zdobyty po ok. 12 s. Abordażowiec traci 12 wojowników (4 z nich ranni), a 9 przechodzi na zdobycz. |
| Abordażowiec → niszczyciel załóg | Poddaje się po ok. 14 s. Abordażowiec traci 25 wojowników (10 z nich ranni), a 13 przechodzi na zdobycz. |
| Abordażowiec → abordażowiec | Zdobyty po ok. 20 s. Atakującemu nie zostaje żaden wojownik, a 22 jest rannych. |
| Taranowiec → niszczyciel załóg | Odparty po 10 s. Taranowiec traci całą załogę bojową. |
| Taranowiec → abordażowiec | Liny przecięte po 2,4 s. |
| Abordażowiec → handlowiec | Zdobyty po 6 s. |

Pojedynki wszystkich klas wojennych z ostrzałem i taranem: [UZBROJENIE.md](UZBROJENIE.md#pojedynki-i-oblężenie-pomiar-3c-symulacja-net-bez-unity).

AI wchodzi w abordaż, gdy `World.BoardingOdds` przekracza 1,2. Wskaźnik to siła pierwszej rundy razy liczba wojowników, którymi strona może zasilać front.

## Przegrupowanie w porcie

**Automatyczne.** Okręt bez rozkazu, do 80 m od własnego portu ze stocznią, sam wchodzi na stanowisko, gdy:
- ma uszkodzony kadłub lub moduł broni, a port ma dok (uzgodnienie paczki 2026-11-10: kadłub i moduły naprawia tylko dok),
- ma rannych,
- albo jego skład różni się od planu, a port ma srebro na przegrupowanie.

Rozkaz Remont (P) robi to samo na żądanie. Uszkodzony okręt płynie do najbliższego doku swojej strony, a bez doku do najbliższej stoczni, tylko po przegrupowanie i wymianę broni (`World.RepairYard`).

**Kolejność na stanowisku:**
1. Kadłub, tylko w doku: 25% pełnej ceny okrętu (drewno i srebro razem) w srebrze za pełną naprawę, 12 HP/s ([BALANS_I_SKALA.md](BALANS_I_SKALA.md#naprawa-w-doku-uzgodnienie-paczki-2026-11-10)).
2. Ranni: 0,25 S za człowieka, czterech na sekundę. Wyleczony wraca do klasy, której najbardziej brakuje.
3. Zaciąg: 0,5 S za żeglarza i 1 S za wojownika według cen E04, 1 s na człowieka.
4. Przeszkolenie nadwyżki jednej klasy na brakującą: 0,5 S, 2 s.

Port pobiera opłatę na początku każdego kroku z lokalnego magazynu. Bez srebra czeka i niczego nie traci. Cena podana w interfejsie (`World.MusterCost`) wykonuje te same kroki na kopii załogi, więc równa się zapłaconej (test). Koniec przegrupowania emituje `ship_restored`.

**Plan.** `SetCrewPlan` (preset albo liczby) zmienia sam plan, w dowolnym miejscu. Ludzie zmieniają się tylko w porcie.

## Interfejs

- **Karta okrętu.** Pokazuje żeglarzy, szermierzy i łuczników. Przy własnym okręcie liczba ma postać „stan/plan”, jeśli różni się od planu. Do tego ranni i morale.
- **Przycisk „Skład: …”.** Otwiera wybór czterech składów z liczbami i ceną przegrupowania.
- **Wiersz abordażu.** Faza: haki z postępem cięcia lin albo walka na burtach z frontem. Dwa paski siły ostatniej rundy.
- **Herold.** Komunikaty: haki wroga, przecięte liny, odparty abordaż, kapitulacja, zdobycie i przegrupowanie.
- **Walka na burtach.** Figurki w barwach stron, jedna na dwóch ludzi frontu, do ośmiu na stronę, z animacją ciosu. Widać je tylko w fazie walki i tylko przy widocznych okrętach. W fazie haków lecą liny (`BattleEffects.Rope`).

## Zapis

Wersja 10 zapisu zawiera:
- klasy ludzi, rannych, plan, ułamek rannych i licznik przegrupowania;
- postęp cięcia lin i ułamki strat obu stron abordażu.

Wersja 14 dopisuje rannych atakującego z chwili zarzucenia haków (`BoardingLink.AttackerWoundedAtStart`).

Zapis i wczytanie w trakcie walki daje identyczny dalszy przebieg (test).

## Granice weryfikacji

- Testy EditMode i PlayMode obejmują logikę, interfejs i obecność figurek.
- Wyglądu walki na burtach nie oceniono na zrzucie, bo wymaga to okna gry.
- Balans AI kontra AI bez walki mieści się w zakresach etapu 1. Przy walce mecze AI nadal kończą się szybciej niż cel, co należy do zakresu etapu 7.
