# Audyt planu, procesu i przyrostu regresji

Data: 12 listopada 2026, Europe/Warsaw. Punkt odczytu HEAD: `012e329`, gałąź `balans-skala`, zapis v19. Autor wdrożenia tego przyrostu: Codex; niezależne przeglądy: agenci Codex do odczytu. Nie przypisuje się im modeli Claude Opus/Fable.

Zakres: nadrzędny plan, jego katalog/parametry/mapy/etapy, kontrakty J/Q i zależne normy art/tech; kod punktowo dla konwojów, transportu, zapisu i brzegu. **Nie jest to audyt całego kodu gry.** Zastane zmiany innych autorów zachowano; bez commitowania całego drzewa i bez edycji grafiki/scen/profili jakości.

## 1. Wdrożenie ośmiu działań produkcyjnych

| Działanie | Stan oparty na dowodach | Dalszy krok |
|---|---|---|
| Rzeczywiste zadanie i komunikacja | Wdrożone w P01: przy pierwszym odczycie dziennik miał 51 zdarzeń, 3 CHECKPOINT, ACK przydziałów i FREEZE/UNFREEZE. Raport 0418 opisuje SendMessage w obu kierunkach z programistą. Routing użytych ról potwierdzono także ponownym odczytem pól model w lokalnych JSONL: koordynator/reviewer Opus, programista Fable. | Weryfikować każdą nową sesję; pozostałych nieużytych ról nie uznawać za sprawdzone runtime. |
| Kontrakt zachowania | Wymagany przez kartę zgodności, protokół komunikacji i doktrynę jakości; narzędzie Acceptance sprawdza wskazane pola i dowody. | Użyć konkretnego kontraktu dla przyrostu. Sama instrukcja nie jest techniczną blokadą każdego narzędzia. |
| Kontrola zakresu testów | Istniejąca bramka wykonuje EditMode/PlayMode, weryfikuje nazwane expectedTests i źródła asercji przy zerowym liczniku Unity. Przygotowano inwentarz 404+153 nazw z poświadczonego raportu P01 i strażnik regresji. | Claude Code ma włączyć nazwy do nowego planu i uruchomić pełną bramkę. Hooków/polityki nie zmieniono. |
| Niezmienniki | Istnieją testy determinizmu, księgi i zapisu. Dodano konkretne regresje transportu, utraty praw, Pending i trasy przy brzegu. | Generowane sekwencje komend, minimalizacja seeda i szersze stress/soak pozostają osobnymi zadaniami. |
| Fragment grywalny | Istniejąca scena i pokazy oraz zrealizowany rdzeń P01 są podstawą integracji. | Pełna misja referencyjna domyka cele zgodnie z P07/P10; nie deklarować jej obecności z samego scenariusza JSON. |
| Próby z graczami | Brak potwierdzonej tury 3–5 osób. | Przygotować scenariusz, build, obserwację, nagranie i raport; człowieka nie zastępować AI-vs-AI. |
| Wydajność/czyste środowisko | Są wcześniejsze, ograniczone pomiary i harness .NET. Dla tego przyrostu brak nowego benchmarku i czystego checkoutu. | Osobne zadanie z profilem/obciążeniem, p95/p99, alokacjami i artefaktem; 33,33 ms nie jest pełnym budżetem symulacji. |
| Zwięzłe normy i odpowiedzialność | Jeden plan produktu, karta, pojedynczy dziennik, nowa doktryna jakości i dwa oddzielne wyniki odbioru. | Utrzymywać odsyłacze i usuwać rozbieżne aktywne normy, bez przepisywania historycznych dowodów. |

## 2. Wynik audytu spójności planu

Rdzeń jest spójny: jeden świat mitologiczny, rama 400–301 p.n.e. z 400 włącznie, single player, rozdział nauki/fikcji/balansu, fizyczny transport i zachowanie architektury World. Nie stwierdzono potrzeby przeprojektowania całej gry. W dokumentach zależnych znaleziono i doprecyzowano osiem konkretnych kwestii:

1. P03 wymagał pełnych E01–E12, mimo że E05 i części E09 potrzebują późniejszych P05/P08/P09. Odbiór P03 obejmuje teraz podstawowe podkontrakty z jawnym przypisaniem uzupełnień, bez cyklu zależności.
2. Zależność ceny złomu od bieżącego HP kolidowała z darmową odnową. P08 jawnie oddziela stan kwalifikowany do odzysku; zachowanie współczynnika 15% nie oznacza zachowania błędnej semantyki po włączeniu odnowy.
3. Wzór 5% odzysku używał nazwy pola w setnych jak wartości w pełnych S. Podano jednostki, dzielenie całkowite, przykład 10001→500 setnych i wersję bez przepełnienia mnożenia.
4. C09 oznaczał w różnych dokumentach ostrzał i odbiór morza. Morze ma teraz odrębne WM01–WM03, a katalog C09 zachowuje znaczenie ostrzału. Bez zmiany enumów.
5. Nieodczytane search-summary oraz Wikipedia były przedstawiane jako ustalenia. Zależne tezy destrukcji mają NIEUSTALONE do rzeczywistego odczytu; implementacja nie jest dowodem naukowym.
6. TPQ Kyrenii przedstawiono jako dokładne datowanie budowy. Korekta rozróżnia ograniczenie daty budowy lub naprawy od daty samego wykonania.
7. Przekaz Diodora o wynalazku zbyt szeroko uzasadniał cały katalog machin na pokładach. Teraz typ, zastosowanie, region i rok misji wymagają oddzielnej podstawy.
8. Współistniał sztywny cel sprzętowy i deklaracja jakości bez sprzętowej gwarancji. Kryteria wskazują konkretny pomiar i uzgodniony profil, bez uniwersalnej obietnicy FPS/rozdzielczości.

Dodatkowo doprecyzowano, że zmiana baseline wody wymaga niezależnego review i nie uprawnia do cofania cudzej pracy. Poprawne Delivered z manifestem i odbiorcą zachowano. Nie zmieniono parametrów balansu, kolejności całych etapów, zapisanych ID ani wyglądu.

## 3. Reprodukcje i wdrożenie regresji

Kod: `World.Convoy.cs`, `World.Expansion.cs`; nowe `LogisticsRegressionTests.cs` i `.meta`; komentarz w `ConvoyTests.cs` wskazuje uzupełniający zakres. Format v19 i kontrakt binarny bez zmian.

- Reprodukcja q02: źródło x=−350, front x=−150, kupcy x=−330 i y=40/80/120. Po 300 s: 0/0 dostawy; załadowany członek zatrzymuje dojście drugiego do wolnego nabrzeża. Kontrola z x=−470 dostarcza 400 D/260 S. Dowód: `work/studio-regressions/convoy-before.txt`.
- Przed poprawką nowe testy diagnostyczne .NET: 8 FAIL/3 PASS. Utrata własności portu nie zatrzymywała pobrania/wpłaty. Dowód: `work/studio-regressions/tests-before.txt`.
- Poprawka: załadowany członek grupy zwalnia podejście, pozostając przy źródle; cel i źródło są ponownie sprawdzane w toku transportu. Stan manewru wynika z zapisywanych danych.
- Niezależny review wskazał dodatkową kolejność: nowy kupiec mógł zarezerwować 180 D w przejętym źródle grupy, zanim dotychczasowy członek anulował zadanie. Dodano osłonę Owner przy dołączaniu i regresję tej kolejności. Dowód przed osłoną: `review-regression-before.txt`.
- Zapis Pending już działał. Uzupełniono połączenie pauzy ze zmianą legalności celu oraz zapis ciężkiego kadłuba z aktywną trasą omijającą brzeg. Nie zgłoszono ponownie dawnego gubienia bufora.

**Unity: 14/14 nowych przypadków Passed, 0 skipped, proces 0.** XML: `artifacts-log/studio-regressions/targeted-verified.xml`; log: `targeted-outside-sandbox.log`; kontrola check_unity_errors.py z tym logiem: kod 0. Pierwszy proces w sandboxie zakończył się awarią inicjalizacji licencji/WMI bez XML i nie jest uznany za zaliczenie.

**Pełna bramka dla tego przyrostu: NOT_RUN, na wyraźne polecenie użytkownika przekazana Claude Code w promptcie.** Brak nowego buildu, playtestu z człowiekiem, pomiaru wydajności, artystycznego/pomiarowego odbioru istniejącego morza. Zaliczenie testów logistyki nie certyfikuje dowolnej cieśniny ani całego P03. Jawny terminalny wynik zadania po utracie wcześniej przyjętego celu pozostaje P03/J02.

**Stan współbieżny przy przekazaniu:** kolejny odczyt dziennika wykazał rewizję 84 i aktywne przydziały P02, m.in. Catalog/ i Effects/. Pojawiły się również zmiany World.cs, Opponent.cs, World.Economy.cs i World.Sides.cs zapisane po targeted-verified.xml. Nie są pracą tego przyrostu i nie zostały nadpisane ani objęte deklaracją zaliczenia 14/14. Odbiór bieżącego połączonego stanu należy wykonać dopiero po integracji i FREEZE wszystkich autorów. Nie przerywać aktualnych przydziałów ani nie zastępować ich starym punktem startowym P01.

## 4. Narzędzia a rzeczywista aktywacja

Walidator art/fizyka i strażnik regresji są implementacją ręcznie wywoływaną. Zestaw 40 testów narzędzia przeszedł kontrolę: obejmuje odrzucanie m.in. braków danych, zmienionych hashy, usuniętych testów, złej sesji, nieznanej domeny, nieporównywalnych kadrów, uszkodzonego PNG, braku filmu i błędnych jednostek/progów. Nie certyfikuje piękna ani prawdziwości parametrów modelu.

Nowy MCP ma lokalną skrzynkę i publiczne referencje CMA. 17 testów modułu i 4 testy instalatora przeszły kontrolę, w tym Unicode/UTF-8 przez proces, jawne ACK, kolizje, zachowanie ISO-stringów i blokadę gate. Fixture nie udają ACK modelu Claude. Rzeczywiste HELLO `codex-hello-20261112` zapisano do skrzynki; ACK klienta pozostaje niepotwierdzony. Odczyt publicznego API rzeczywiście zwrócił kandydatów i źródłowe metadane; wyniki pozostają UNREVIEWED, w tym obiekty spoza IV w. Instalator domyślnie przygotowuje wpis. `.mcp.json`, modele, uprawnienia, hooki i polityka pozostają bez zmian do kontrolowanej aktywacji.

Kontynuacja: [PROMPT_ODBIOR_I_KONTYNUACJA_CLAUDE_CODE.md](../PROMPT_ODBIOR_I_KONTYNUACJA_CLAUDE_CODE.md). Najpierw odbiór aktualnego przyrostu; P02 i odnotowane podzadania P03 kontynuować zgodnie z rzeczywistym stanem, bez ponownego wdrażania gotowych systemów.
