# Karta zgodności każdego działania z planem gry

Status: obowiązująca instrukcja wykonawcza §0 [NOWY_PLAN_GRY.md](NOWY_PLAN_GRY.md). Nie jest drugim planem produktu. Wyraźne polecenia użytkownika zachowują pierwszeństwo.

## Stała rama

**Mitologiczna Grecja IV w. p.n.e.: 400–301 p.n.e., z 400 r. p.n.e. włącznie.** Główny obszar: Attyka i pobliskie akweny egejskie. Nie narzucać kampanii jednego węższego okna. Dla misji lub assetu ustalić dokładniejszy rok/zakres, jeżeli wymaga tego weryfikacja konkretnej technologii, fazy budowli albo wyposażenia.

Obiekt starszego pochodzenia może należeć do świata IV w., jeśli uzasadniono jego ówczesne istnienie lub użycie. Manifestacja herosa lub cud jest jawną fikcją, nie pretekstem do nazywania niepotwierdzonych elementów faktami historycznymi.

## Co kontrolujemy

Kontrola dotyczy każdej czynności zmieniającej projekt: edycji, generowania, importu, zmiany parametrów, modelu, rigu, animacji, shaderów, sceny, konfiguracji, dokumentacji i danych. Odczyty oraz diagnostyka pozostają podporządkowane celowi zadania; nie wymagają osobnego wpisu dla każdej przeczytanej linii.

Przed każdą mutacją wykonawca sprawdza kartę przyrostu i własny zakres. Jedna karta może obsługiwać wiele zgodnych czynności, lecz nie zastępuje kontroli przed kolejnym wywołaniem narzędzia. Zmiana kontraktu lub plików wymaga aktualizacji przydziału i właściwego ACK. Role do odczytu nadal nie zapisują raportów samodzielnie.

## Wymagane pola karty

| Pole | Warunek |
|---|---|
| Zadanie i tryb | Cel użytkownika; ANALIZA lub autoryzowane WDROŻENIE. Odczyt promptu nie rozszerza zlecenia. |
| Podstawa | Konkretne punkty nadrzędnego planu, etap P i właściwe ID katalogu/Q/J; N/A wymaga uzasadnienia. |
| Rama świata | Zgodność z IV w. i 400 r. włącznie; region, wymagane datowanie i manifest wyposażenia. |
| Rodzaj twierdzeń | Fakt, rekonstrukcja, kalibracja fizyczna, tradycja mitologiczna, fikcja lub abstrakcja gry. |
| Dowody | Rzeczywiście odczytane źródła dla zależnych twierdzeń, z zakresem dostępu i ograniczeniami. |
| Zakres i właściciele | Dozwolone pliki/assety, chronione elementy, wykonawca, koordynator i runner. |
| Rezultat | Oczekiwane zachowanie lub widoczna zmiana, bez domyślnego dopisywania nowych mechanik. |
| Odbiór | Testy, obrazy/nagrania, pomiary, tolerancje i niezależny reviewer właściwy dla zadania. |
| Stan odniesienia | Commit, zastane zmiany i identyfikacja snapshotu. Nie przejmować cudzego autorstwa. |

Nie należy wpisywać losowej tolerancji, dopasowywać oczekiwanego wyniku do błędu ani zastępować źródła pamięcią modelu. Karta nie upoważnia sama do kosztów ani instalacji. Już udzielone upoważnienie zachowuje zakres; nie pytać ponownie o rutynowe kroki, które obejmuje.

## Wynik kontroli

- `ZGODNE`: działanie mieści się w planie, poleceniu, granicach i posiadanych dowodach; można wykonać przydzielony krok.
- `BLOCKED_PLAN_CONFLICT`: sprzeczność planu z propozycją lub dokumentem pomocniczym; zależnego działania nie wykonywać.
- `BLOCKED_RESEARCH` / `NIEUSTALONE`: brakuje wymaganej weryfikacji; nie przedstawiać tezy jako potwierdzonej.
- `NOT_RUN`, `BLOCKED`, `FAIL`, `INCONCLUSIVE`: nie oznaczają odbioru. Samo ZGODNE przed edycją także nie oznacza ukończenia.

Przy konflikcie podać oba zapisy, zakres wpływu, dowód i najmniejszą propozycję rozwiązania. Agent nie wybiera sam alternatywnej epoki, nie scala sprzecznych reguł i nie poprawia planu w celu zalegalizowania wygenerowanego wyniku. Może samodzielnie podejmować zwykłe decyzje techniczne w granicach już zatwierdzonego kontraktu. Niezależne zgodne zadania mogą trwać.

## Kontrola po działaniu i przed odbiorem

Sprawdzić diff i faktyczny rezultat, nie tylko komunikat narzędzia. Potwierdzić brak zmiany obszarów chronionych i brak cichego rozszerzenia katalogu, epoki lub reguł. W assetach sprawdzić rzeczywiste podpięcie; w kodzie właściwe testy i zapis; w dokumentacji spójność z jednym planem. Korekta opisu epoki nie zmienia numerów enumów, formatu zapisu ani wyglądu istniejących assetów.

Jakość artystyczna i wiarygodność fizyczna mają odrębne dowody: referencje i obejrzany wynik dla obrazu; model, źródła, parametry, zakres ważności i testy dla fizyki. Mitologii nie certyfikuje się jako praw fizyki. Wymagania odbioru nie mogą być cicho obniżane. Nie deklarować najwyższej jakości na podstawie nazwy modelu, liczby plików, rozdzielczości tekstury lub samego testu importu.

Przed integracją i końcowym raportem ponownie zweryfikować cały przyrost względem planu. Ślad kontroli trafia do istniejącej karty zadania, komunikacji i raportu właściwej sesji, z prawdziwym autorstwem. Nie tworzyć drugiego konkurencyjnego dziennika; nie edytować chronionego snapshotu podczas bramki.

## Granica egzekwowania

Szczegóły dowodów, kontraktów zachowania, regresji i niezależnych bramek art/fizyka określa [RYGOR_PRODUKCJI_I_ODBIORU.md](RYGOR_PRODUKCJI_I_ODBIORU.md). Ręczny walidator dowodów nie jest automatycznie zainstalowanym hookiem ani odbiorem samej gry.

To trwała instrukcja projektu, odczytywana razem z planem przez CLAUDE.md. Sama dokumentacja nie jest techniczną blokadą wszystkich narzędzi i nie daje gwarancji wykonania 100% poleceń. Istniejące hooki, uprawnienia i bramka pozostają niezmienione; nie przypisuje się im sprawdzania nowej karty, dopóki nie zostanie to osobno zaimplementowane i sprawdzone.
