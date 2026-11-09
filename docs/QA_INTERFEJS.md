# Odbiór interfejsu rozgrywki — raport

Data: 2026-11-09. Gałąź: `interfejs-pakiet`. Sprzęt: laptop z RTX 3050 Ti (wymuszony `-force-device-index`), monitor 1920×1080, profil Ultra. Statusy: PASSED, FAILED, BLOCKED, NOT_RUN.

## Kontrole wykonane

| Kryterium | Status | Dowód |
|---|---|---|
| Kompilacja projektu i buildu Windows | PASSED | build „0 errors, 0 warnings”; import USS bez ostrzeżeń |
| Testy EditMode / PlayMode (bramka) | PASSED | 132/132 i 54/54, w tym nowe: wycena anulowania = efekt anulowania; zestaw rozkazów kutra (5); herold ≤ 4 i łączenie alarmów; Tab zwija pas i zwalnia mapę; potwierdzenie anulowania > 50%; domyślne skróty bez konfliktów i zgłaszanie kolizji |
| Wyjątki w działającym buildzie | PASSED | 4 przebiegi testu dymnego, 0 wyjątków w logu |
| Układ 1920×1080 (pasek 44 px, pas 194 px, minimapa, zaznaczenie, kolejki, rozkazy 4×3, herold, wybór portu) | PASSED | zrzuty `artifacts-log/hudqa-hud-0/shot-*.png` |
| Krój: Cinzel w nagłówkach, Source Sans 3 w danych; polskie znaki w zrzutach („Bogów”, „załogi”, „Złomuj”) | PASSED | zrzuty; pakiet potwierdza pełny zestaw ĄĆĘŁŃÓŚŹŻąćęłńóśźż w TTF |
| Cyfry tabelaryczne (tnum) | FAILED | TextCore w UI Toolkit nie włącza cech OpenType; zastępczo liczby mają stałą minimalną szerokość i wyrównanie do prawej |
| Stany zaznaczenia: brak, jeden okręt, grupa, port | PASSED | zrzuty i testy |
| Stany zaznaczenia: wrak, podgląd przeciwnika, przyczółek | NOT_RUN | ścieżki w kodzie; nie wystąpiły w zrzutach |
| Dwie linie produkcji, etap, czas | PASSED | zrzuty; test anulowania |
| Anulowanie z potwierdzeniem i kwotami z symulacji | PASSED | `Hud_CancellingAnAdvancedOrderAsksWithTheSimulationsSettlement`, `Queue_CancelQuoteIsExactlyWhatCancellingDoes` |
| Naruszenie zapasu chronionego, brak środków | NOT_RUN | zaimplementowane (okno decyzji, brakująca kwota); brak testu |
| Przełączanie portów | NOT_RUN | w testach dymnych gracz nie miał przyczółka |
| Dostawa częściowa, pełny magazyn, śmierć i przejęcie zaznaczonej jednostki | NOT_RUN | komunikaty herolda podłączone; brak scenariusza testowego interfejsu |
| Utrata widoczności celu | PASSED | `Target_EnemyIsMarkedAfterAttackOrderAndNotBefore` (znacznik i pasek znikają) |
| Kliknięcie w UI nie wydaje rozkazu; puste obszary przepuszczają | PASSED | `Hud_PanelsTakeThePointerAndTheSeaDoesNot`, test zwijania |
| Skróty z wykrywaniem kolizji, przechwytywanie klawisza | PASSED (logika) / NOT_RUN (ręcznie) | test `Keys_*`; okno Sterowanie nieklikane ręcznie |
| Esc z każdego trybu, fokus modala, tooltip przy fokusie klawiatury | NOT_RUN | zaimplementowane; brak testu ręcznego |
| Zapis/wczytanie z menu, ponowne otwarcie sceny, 10× włączenie HUD | NOT_RUN | zapis i wczytanie mają testy logiki; brak testu przez menu |
| Skala 90/100/125% | NOT_RUN | zaimplementowana (`PanelSettings.scale`, tryb compact); brak zrzutów |
| 2560×1440 | BLOCKED | monitor 1920×1080; uruchomienie z `-screen-width 2560 -screen-height 1440` dało 1920×1080 |
| Jasne i pochmurne oświetlenie | BLOCKED | gra nie ma pogody/pochmurnego oświetlenia |
| Kontrast tekstu na rzeczywistych panelach | NOT_RUN | pakiet podaje 6,70–14,25:1 dla par z palety; tło panelu jest kryjące (0,97), ale nie zmierzono na zrzutach |
| Ustawienia dźwięku | BLOCKED | projekt nie ma kanałów miksera |
| Koszt CPU interfejsu ≤ 0,5 ms | PASSED | `interfaceMs` 0,11–0,19 ms (średnia Update HUD w buildzie) |
| Koszt GPU interfejsu ≤ 0,5 ms | NOT_RUN (nierozstrzygnięte) | GPU klatki 40,8 ms z HUD i 41,6 ms bez (`-morzaNoHud`); różnica w granicach szumu pomiaru (rozrzut przebiegów kilka ms) |
| Brak stałych alokacji GC po rozgrzaniu | FAILED | nie zmierzono profilerem, ale odświeżanie co 0,125 s używa LINQ i formatowania tekstu, więc alokuje; do usunięcia |
| Próby z nowymi graczami | NOT_RUN | wymagają ludzi |
| Multiplayer (uprawnienia pauzy i tempa) | nie dotyczy | gra jednoosobowa |

## Uwagi

- Kursory: używane warianty 32 px z punktem 16/16; warianty 48 px nie zostały podłączone.
- Ilustracja archipelagu z pakietu nie jest używana w grze (zgodnie z pakietem); portrety triery i handlowca – tak.
- Wydajność całej gry (ok. 40 ms GPU na klatkę na Ultra) to osobny, wstrzymany problem sceny (woda HDRP), niezależny od interfejsu.
