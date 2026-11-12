# Kontrakty morza i wiatru

Uszczegółowienie [planu §8 i §12](NOWY_PLAN_GRY.md) i P07 z [planu integracji](PLAN_WDROZENIA.md). Identyfikatory WM01–WM03 służą odbiorowi morza. C09 w aktywnym katalogu oznacza ostrzał. Nie zmieniać numerów enumów ani zapisanych identyfikatorów.

## WM01 — stan i wpływ na mechanikę

Jedno pole wiatru/morza, jawne jednostki, czas i RNG. Stan przejściowy oraz cel przejścia podlegają zapisowi. Włączenie wpływu na ruch wymaga P07: kursy względem wiatru, żagiel/wiosła, dryf/prąd, brak podwójnych sił, porównanie AI i gracza oraz zgodna kontynuacja zapisu. Do odbioru zachować obecny domyślnie wyłączony przełącznik. Wartości ±25% zmiany prędkości i 20% udziału prądu są odczytanymi ograniczeniami implementacji; zmiana ich interpretacji wymaga kontraktu. Nie są naukowym pomiarem osiągów ateńskiego okrętu. Nowe kary sztormowe i wywracanie jednostek wymagają odrębnego zlecenia mechaniki.

## WM02 — obraz, animacja i informacja

Tafla i kadłub korzystają ze zgodnego stanu, współrzędnych i fazy. Dekoracyjny detal nie dodaje drugi raz głównej amplitudy. Limity przechyłu i opóźnienia należą do jawnego profilu z bramką artystyczną/fizyczną. Mgła środowiskowa i logiczna widoczność są rozdzielone; każdy profil zachowuje uczciwą informację taktyczną. Kilwater, piana, ptaki i cienie nie zdradzają ukrytej jednostki. Obowiązują próby pauzy, tempa, zapisu, wejścia/wyjścia z widoku i przejścia pogody.

## WM03 — wiarygodność fizyczna

Zlecenie realizmu określa model fal i odpowiedzi kadłuba, źródła, jednostki, parametry, zakres głębokości/obciążenia/wymuszenia, testy stabilności i dane kontrolne. Stanowisko pomiarowe i tolerancje mają być zapisane przed pomiarem. Rekonstrukcja kadłuba, kalibracja tłumienia i potwierdzone pomiary otrzymują odrębne etykiety. Zgodność z równaniem nie dowodzi historycznej poprawności wszystkich współczynników. Stosować [dwie bramki](RYGOR_PRODUKCJI_I_ODBIORU.md).
