# Morza Bogów — wskazówki z paczki systemu scenariuszy

Data: 2026-11-10.

Źródło: paczka użytkownika `MorzaBogow.zip` (z `C:\Users\noirp\Downloads`). Paczkę przejrzano w pamięci bez rozpakowywania do projektu; obejrzano tylko dwa podglądy map w katalogu tymczasowym.

**Decyzja użytkownika:** paczkę wykorzystujemy **tylko jako wskazówki**. Żadnego pliku z niej nie skopiowano do repozytorium.

## Zawartość paczki

- analiza 55 map kafelkowych 2D: obrazy w skali 64 px na pole, udział lądu i wody, liczba lądów, rozłączne akweny, wąskie przejścia;
- profil skali `MB_SCALE_1`: pole 1×1 jednostka, płaszczyzna XY, kamera ortograficzna o rozmiarze 6, czyli widok 12 pól;
- kontrakt scenariusza w JSON: siatki znaków, punkty (kotwice) i wymagane połączenia;
- walidator C# i Python, narzędzia edytora Unity, testy;
- polecenie wdrożeniowe.

## Czego nie przenosimy

| Element | Powód |
|---|---|
| Mapy, obrazy map, paleta kolorów terenu | Wyglądają na mapy i grafikę oryginalnej gry z 1998 r. Reguła użytkownika dla Mórz Bogów: własne mapy, liczby i grafika, bez kopii istniejącej gry. |
| Profil skali `MB_SCALE_1`, siatka pól, format siatek znaków, kod `ScenarioSystem` | Morza Bogów to świat 3D w metrach: płaszczyzna XZ, wyspy jako wielokąty linii wody, ciągły ruch po grafie nawigacji, kamera ukośna HDRP. Widok 12 jednostek nie pasuje do okrętów długości 12–26 m. Kod nie miałby czego obsługiwać. |
| Udział lądu z analizowanych map (20–47%) jako cel | Decyzja użytkownika z 2026-11-10 ogranicza ląd poza złożami ([BALANS_I_SKALA.md](BALANS_I_SKALA.md), sekcja „Skala lądu i drzew”). |

## Przyjęte wskazówki

| Wskazówka | Stan w Morzach Bogów | Kiedy |
|---|---|---|
| Stała skala obiektów niezależna od wielkości mapy | Zrobione: rozstaw profilu zmienia położenia, nie rozmiary okrętów, drzew i skał. | — |
| Wymagane połączenia między ważnymi punktami | Częściowo: podejście 34 m i kanał 30 m przy skałach (`MapFactory.KeepsApproaches`). Do dodania: test, że każdy port, złoże, wolny brzeg i cel są osiągalne w grafie nawigacji dla najszerszego kadłuba. | test mapy przy najbliższej zmianie mapy; pełny przy scenariuszach (etap 8) |
| Zamknięty akwen jest dozwolony; błędem jest tylko odcięcie wymaganego celu | Zasada do formatu scenariuszy. | etap 8 |
| Kontrola wszystkich plików scenariuszy przed buildem, z testami negatywnymi | Do zrobienia razem z formatem scenariuszy. | etap 8 |
| Osobny tryb podglądu całej mapy, kadr zależny od jej proporcji | Dziś jedna wysokość kamery z limitem. Dodać kadrowanie przeglądu dla map pionowych i panoramicznych. | z mapami scenariuszy (etap 8) |
| Kamera i kliknięcia uwzględniają prostokąt interfejsu | Kliknięcia pod interfejsem są blokowane (`PointerOverHud`). Kadr kamery nie uwzględnia pasa dolnego. | przy pracy nad kamerą |
| Duże mapy rysowane w kawałkach, bez jednej ogromnej tekstury | Teren to wyspy i łączone siatki. Zasada obowiązuje przy większych mapach. | etap 8 |
| Testy na mapach o różnych proporcjach (kwadrat, pion, panorama, mała) | Jest jedna mapa potyczki. Dodać przy scenariuszach. | etap 8 |

## Granice

Wskazówki to zasady projektowe, nie wdrożony kod. Nie uruchamiano skryptów ani testów z paczki.
