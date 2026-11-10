# Morza Bogów — załoga, abordaż i przegrupowanie w porcie (etap 2)

Data: 2026-11-10. Gałąź: `balans-skala`. Decyzje użytkownika:
- ludzie istnieją wyłącznie jako obsada okrętów;
- trzy klasy ludzi: żeglarze, szermierze i łucznicy;
- skład załogi wybiera się w porcie presetem, a szablony floty z etapu 3 będą go zapisywać;
- liczebność w dotychczasowej skali, 36–90 ludzi;
- ceny okrętów bez zmian.

## Klasy ludzi

| Klasa | Rola |
|---|---|
| Żeglarze | Sterują, wiosłują i zbierają surowce. Prędkość zależy wyłącznie od nich: mniej niż ¼ załogi spowalnia okręt proporcjonalnie, a 40% (skład Żegluga) daje +8% prędkości. Wręcz walczą tylko wtedy, gdy na pokładzie nie zostało wojsko. |
| Szermierze | Walka wręcz: niosą abordaż, odpierają go i przecinają liny abordażowe wroga. |
| Łucznicy | Ostrzał załóg okrętów w zasięgu 15–100 m, gdzie strzela każdy łucznik; w abordażu wsparcie z dystansu i obrona własnego pokładu. |
| Ranni | Dwóch na pięciu poległych. Nie walczą i nie wiosłują, zostają na pokładzie do przegrupowania w porcie. |

Bohaterowie mitologiczni wchodzą w etapie 7.

## Składy (`CrewPreset`, `ShipDefinition.PlanFor`)

| Skład | Żeglarze | Szermierze w części bojowej | Łucznicy w części bojowej |
|---|---|---|---|
| Wyważony | 25% | 55% | 45% |
| Abordaż | 25% | 80% | 20% |
| Ostrzał | 25% | 25% | 75% |
| Żegluga | 40% | 55% | 45% |

Domyślne składy klas:

| Klasa | Skład | Żeglarze / szermierze / łucznicy |
|---|---|---|
| Taran | Wyważony | 9 / 15 / 12 |
| Niszczyciel załóg | Ostrzał | 13 / 10 / 29 |
| Abordażowiec | Abordaż | 23 / 54 / 13 |
| Kuter, handlowiec | — | sami żeglarze |

Każdy domyślny skład ma tylu żeglarzy i wojowników, ilu zakłada cena E04, więc ceny szablonów się nie zmieniły (test).

## Ostrzał

- Każdy łucznik strzela co 2 s z celnością 0,75, z karą za ruch jak dawniej.
- Trafienie zadaje 0,55 obrażeń załodze i 0,28 kadłubowi.
- 29 łuczników niszczyciela załóg zadaje tyle co dawne 4 stanowiska (test).
- Na salwę wypada jedno losowanie z ziarna.
- Okręt bez łuczników nie strzela. Okręty sczepione w abordażu nie prowadzą zwykłego ostrzału.
- Strzały trafiają najpierw ludzi na pokładzie, liczniejszą z dwóch grup bojowych. Żeglarze przy wiosłach padają na końcu.

## Abordaż

1. **Haki (3 s).** Liny zrywa odpłynięcie celu albo różnica prędkości, jak dawniej. Szermierze obrońcy tną liny w tempie 0,008 na sekundę za każdego szermierza ponad połowę szermierzy atakującego. Abordażowiec przecina tak haki taranu w około 2,7 s. Między równymi okrętami liny trzymają.
2. **Walka na burtach, runda co 1 s:**
   - Front: walczy 4 + ⌊szerokość węższego pokładu w m⌋ ludzi na stronę. Najpierw stają szermierze, potem łucznicy, a żeglarze dopiero, gdy wojska zabraknie.
   - Siła: atak lub obrona ludzi we froncie (tabela poniżej), plus wsparcie łuczników spoza frontu (0,3 w ataku, 0,4 w obronie), razy (0,5 + morale/100).
   - Straty: każda strona traci w ludziach 0,15 × siła przeciwnika. Ułamki przechodzą na następną rundę. Padają ludzie z frontu, najpierw z liczniejszej grupy.
   - Morale: spada o 120 × poległe/pełna załoga.

   | Klasa | Atak | Obrona |
   |---|---|---|
   | Szermierz | 1,2 | 1,1 |
   | Łucznik | 0,4 | 0,9 |
   | Żeglarz | 0,25 | 0,35 |

3. **Kapitulacja.** Załoga bez wojska albo z morale poniżej 20 przez 3 s kontroli poddaje okręt (`ship_surrendered`, `ship_captured`).
4. **Przejęcie w całości.**
   - Kadłub, ładunek, zapłacony koszt i wyposażenie zostają.
   - Żeglarze wiosłują dla nowego właściciela, ranni wroga przepadają.
   - Na pokład przechodzi załoga pryzowa: min(wojownicy atakującego, ¼ załogi okrętu), najpierw szermierze. Gdy zdobyczy brakuje żeglarzy, część pryzowych siada do wioseł.

Pojedynki zmierzone w symulacji (pełne załogi):

| Atak → cel | Wynik |
|---|---|
| Abordażowiec → taran | Zdobyty po ok. 14 s haków i walki; abordażowiec traci ok. 25 szermierzy. |
| Abordażowiec → niszczyciel załóg | Poddaje się po ok. 17 s; abordażowiec traci ok. 44 szermierzy. |
| Abordażowiec → abordażowiec | Zdobyty po ok. 20 s; atakujący prawie bez wojska. |
| Taran → niszczyciel załóg | Odparty po 10 s. |
| Taran → abordażowiec | Liny przecięte po 2,7 s. |
| Abordażowiec → handlowiec | Zdobyty po 6 s. |

AI wchodzi w abordaż, gdy `World.BoardingOdds` przekracza 1,2. Wskaźnik to siła pierwszej rundy razy liczba wojowników, którymi strona może zasilać front.

## Przegrupowanie w porcie

**Automatyczne.** Okręt bez rozkazu, do 80 m od własnego portu ze stocznią, sam wchodzi do doku, gdy:
- ma uszkodzony kadłub,
- ma rannych,
- albo jego skład różni się od planu, a port ma srebro na przegrupowanie.

Rozkaz Remont (P) robi to samo na żądanie.

**Kolejność w doku:**
1. Kadłub: 25% kosztu budowy, 12 HP/s, jak w E06.
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

Zapis i wczytanie w trakcie walki daje identyczny dalszy przebieg (test).

## Granice weryfikacji

- Testy EditMode i PlayMode obejmują logikę, interfejs i obecność figurek.
- Wyglądu walki na burtach nie oceniono na zrzucie, bo wymaga to okna gry.
- Balans AI kontra AI bez walki mieści się w zakresach etapu 1. Przy walce mecze AI nadal kończą się szybciej niż cel, co należy do zakresu etapu 7.
