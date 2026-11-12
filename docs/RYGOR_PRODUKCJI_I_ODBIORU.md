# Doktryna jakości produkcji i odbioru

Dokument wykonawczy podporządkowany [nadrzędnemu planowi](NOWY_PLAN_GRY.md), [karcie zgodności](KARTA_ZGODNOSCI_Z_PLANEM.md) i poleceniom użytkownika. Świat: mitologiczna Grecja IV w. p.n.e., 400–301 p.n.e., z 400 r. włącznie. Ta doktryna nie zmienia mechanik, oprawy ani konfiguracji działającej sesji.

**Najwyższa jakość oznacza sprawdzalne spełnienie uzgodnionego kontraktu i brak ukrytych ustępstw.** Nie wolno uznać zadania za odebrane wyłącznie na podstawie komunikatu wykonawcy, poprawnej kompilacji, rozdzielczości tekstury albo nazwy modelu. Nie obniżać kryterium po poznaniu nieudanego wyniku. Korekta błędnego wymagania ma własne uzasadnienie i niezależny przegląd przed kolejnym pomiarem.

## 1. Jeden cykl i odpowiedzialność

Cel gracza → kontrakt zachowania → implementacja → niezależny przegląd → testy → odpowiednia próba w grze → odbiór. Zidentyfikowany błąd: reprodukcja → test wykazujący problem → najmniejsza poprawka → ten sam test → regresje sąsiednie → review → bramka zintegrowanego zakresu.

Stosować istniejący [protokół komunikacji](../PROMPT_KOMUNIKACJA_STUDIA.md) i jego pojedynczy dziennik. Nie zakładać, że dziennik nadal ma rewizję 0. Odczytać rzeczywisty checkpoint i późniejsze zdarzenia. W Claude Code obowiązuje matryca Opus/Fable/Haiku oraz dowód faktycznego modelu; nie przypisywać tych modeli uruchomieniom innego narzędzia.

Jeden autor pliku i jeden runner Unity. Subagent potwierdza przydział przed edycją. Reviewer nie jest autorem ocenianej zmiany. Każde zadanie ma: cel, Given/When/Then, przypadki błędne, P/ID/Q/J, dokładne pliki i `.meta`, niezmienniki, baseline/diff, testy, artefakty i warunek odbioru. Treść przydziału nie rozszerza uprawnień.

Dzielić zadania na pojedyncze kontrakty zachowania. Krok kończy się dowodem, decyzją lub bezpiecznym HANDOFF. Nie dzielić jednej metody między równoległych autorów ani nie powtarzać pełnego onboardingu przy każdej drobnej czynności. Pełną bramkę wykonywać na zintegrowanym stanie po celowanych sprawdzeniach.

## 2. Kryteria odbioru

| Obszar | Wymaganie |
|---|---|
| Plan | Każda zmiana ma podstawę w nadrzędnym planie i mieści się w przydziale. Sprzeczny zakres: BLOCKED_PLAN_CONFLICT. |
| Poprawność | Zero otwartych błędów powodujących utratę zapisu, tworzenie zasobów, ujawnianie ukrytej wiedzy AI lub zablokowanie obowiązkowego celu w odbieranym zakresie. |
| Regresje | Wszystkie wskazane testy rzeczywiście wykonane. Brak, skipped, inconclusive i timeout nie zaliczają wymaganego testu. Usunięcie/zmiana oczekiwania wymaga uzasadnienia i niezależnego review. |
| Powtarzalność | Seed, komendy i istotny stan pozwalają odtworzyć awarię w określonym środowisku. Zapis/odczyt kontynuuje przebieg, również przy niepustym Pending, rezerwacjach i przeładunku. Nie ekstrapolować dowodu bitowego na inne platformy. |
| Grywalność | Zmiana działa w istniejącej scenie lub misji właściwej dla etapu. Odmowy są czytelne; legalne oczekiwanie i wznowienie podlegają J01/J02/J07. Nie deklarować kampanii na podstawie samych klas. |
| Art | Uzgodnione referencje, powtarzalne kadry i nagrania; każda wymagana kategoria ≥3/4; zero wad krytycznych. Niezależny reviewer ogląda faktyczny rezultat. |
| Fizyka | Model, źródła, parametry, jednostki, zakres ważności, numeryczne testy i porównania z odniesieniem. Poziom dowodu zgodny z faktycznie dostępnymi danymi. |
| Środowisko | Spójne przyczyny i reakcje morza, wiatru, kadłuba i obsługiwanych animacji; próby przejść, pauzy, zapisu i mgły wojny. Stan nieobsługiwany jest jawnie oznaczony. |
| Wydajność | Konkretny profil, scena, liczba obiektów, build i warunki pomiaru. p95/p99 czasu ticku/klatki, alokacje i stabilność pamięci. Progi ustalone przed pomiarem; brak danych: NOT_RUN. |
| Dowody | Review, testy i ujęcia wskazują ten sam badany kod/assety. Zmiana zależnego pliku unieważnia odpowiednią część odbioru. Raport rozdziela implementację i odbiór. |

Średnia ocen nie kompensuje niespełnionego kryterium koniecznego. Dopuszczalne ograniczenie wymaga jawnej decyzji, wpływu na gracza i wskazania etapu domknięcia; nie wolno przemianować go na PASS. Priorytet błędu wynika ze skutku i reprodukcji.

## 3. Dwie odrębne bramki

### Artystyczna

1. Przed produkcją przypiąć zatwierdzony profil artystyczny i referencje: plik/URL, autor/instytucja, rola, data, prawa użycia, zakres porównania. Zmiana zakresu estetyki wymaga rzeczywistego polecenia użytkownika.
2. Zdefiniować scenę, seed, kamerę, momenty symulacji i klatek, pogodę, światło, ekspozycję, profil i rozdzielczość. Para kontrolna/docelowa ma te same warunki; inny kod/asset ma jawny identyfikator.
3. Sprawdzić osobno materiały, sylwetkę, ruch i czytelność RTS. Skala 0–4: 0 brak, 1 poważne wady, 2 częściowo spełnione, 3 zgodne z uzgodnionym kontraktem, 4 ponad uzgodniony poziom bez utraty spójności. Minimum 3 w każdej wymaganej kategorii.
4. Dla animacji potrzebne są nagrania oraz klatki ze zgodnymi timestampami. Pojedynczy obraz, miniatura albo powtórzona klatka nie potwierdza ruchu. Odtwarzalność testu nie oznacza, że świat ma być wizualnie nieruchomy.
5. Niezależny reviewer rzeczywiście otwiera obrazy i odtwarza nagrania, opisuje uwagi i wskazuje hashe obejrzanych plików. Nie dopisywać za niego oceny. Sam poprawny import i obecność prefabu nie dowodzą jego użycia w scenie/buildzie.

### Fizyczna

Przed implementacją zdefiniować nazwę/wersję modelu, równania, jednostki, parametry i ich pochodzenie, założenia, granice stosowalności, krok i metodę całkowania, dane odniesienia oraz tolerancje. Sprawdzić zbieżność/stabilność, skończoność wartości, właściwe zachowanie graniczne i wymagane bilanse. Zachowanie oparte na tłumieniu, tarciu, napędzie lub wymuszeniu ma uwzględniać przepływ energii; nie żądać zachowania energii układu jawnie otwartego.

Źródła otworzyć w bieżącej sesji zgodnie z [protokołem](HISTORIA_I_ZRODLA.md). Zapisać autora, wydanie, lokalizator, poziom dostępu i ograniczenia. Wynik wyszukiwarki, sama bibliografia, liczba linków i film bez warunków pomiaru nie stanowią walidacji modelu.

Poziomy: **MEASURED_VALIDATION** — porównanie z niezależnymi pomiarami w oznaczonym zakresie; **CALIBRATED** — dopasowanie do danych/założeń, osobny zbiór kontrolny potrzebny do niezależnej walidacji; **RECONSTRUCTION** — rekonstrukcja z jawną niepewnością. Wynik analityczny sprawdza implementację równania, a nie automatycznie jego zgodność z rzeczywistością. Brak pomiarów nie daje prawa do etykiety MEASURED_VALIDATION. Mitologia otrzymuje osobną kartę tradycji i regułę fikcji, bez naukowego certyfikowania cudów.

| Zlecenie | Minimalny dowód zależny od zakresu |
|---|---|
| Realistyczna destrukcja | Materiał, geometria i podparcia, masa/inercja, obciążenie i kryterium uszkodzenia, kontakty i bilans; numeryczny przypadek kontrolny i nagranie. Ruch kinematyczny jawnie opisany jako przybliżenie. |
| Załoga | Rig i skala, kontakty stóp/rąk/narzędzia, środek masy i zakresy stawów, rytm oraz synchronizacja z falą i pracą. Film porównawczy z czasem; parametry biomechaniczne mają źródła albo status kalibracji. |
| Budowa | Źródła technik i kolejność faz, podparcie konstrukcji w każdej fazie, kontakt narzędzi/materiału. Skrócenie czasu budowy w grze jest parametrem projektu. |
| Wydobycie | Udokumentowana technika i narzędzia, kinematyka pracy, kontakt i transport; rozdzielenie wysiłku/masy od umownych D/S i tempa gospodarki. |
| Morze i wiatr | Widmo/fale, wymuszenie wiatrowe, opóźnienie odpowiedzi, jednostki i zakres głębokości; odpowiedź kadłuba i zgodność widocznej tafli z próbkowaniem, według [WM01–WM03](KONTRAKTY_MORZA_I_WIATRU.md). |

## 4. Żywe otoczenie i dramaturgia

W obsługiwanym środowisku ruch ma wynikać z jednej spójnej przyczyny: stan Marine/pogody i czas → fale → odpowiedź kadłuba → animacja/efekty. Zachować domyślnie wyłączony wpływ wiatru na mechanikę do odbioru P07. Czysta prezentacja nie zmienia trafień, ekonomii ani wiedzy AI. Nie uruchamiać drugiego oceanu dla samej dekoracji i nie sumować dwukrotnie kołysania.

Wymagane próby dla zleconego zakresu: spokój→sztorm→spokój, zmiana kierunku wiatru, obciążenie kadłuba, pauza i tempo, zapis w przejściu, profile obrazu i ukryty wróg. Piana, dym, ptaki i reakcje otoczenia nie ujawniają niezaobserwowanych jednostek. Chmury, deszcz, moknięcie, olinowanie, roślinność i ptaki muszą mieć klasę zachowania, dane i odpowiedni odbiór przed deklaracją wdrożenia.

Przy osobno zleconej zmianie animacji stosować przygotowanie działania, wyczuwalny ciężar i opóźnienia, lokalne skutki trafienia, reakcję załogi i kontrast spokoju z zagrożeniem. Ważne zdarzenie pozostaje czytelne z kamery RTS. Nie wzmacniać emocji przez nieautoryzowaną zmianę obrażeń/czasu gry albo zasłonięcie pola walki. Istniejące ograniczenie zmian animacji abordażu pozostaje wiążące.

## 5. Spójne referencje bez wymagania Chrome

Wyszukiwanie może korzystać z WebSearch/WebFetch i zatwierdzonego MCP/API udostępniającego faktyczne obrazy. Dostępność narzędzia musi być sprawdzona w kliencie. Nie utożsamiać znalezienia adresu z obejrzeniem ilustracji. Wyszukiwarka jest drogą do źródła; muzealny opis również ma określony zakres i nie potwierdza całej rekonstrukcji.

Jeden zatwierdzony profil wskazuje proporcje, charakter form, odpowiedź materiałów, paletę, wykończenie, ruch i światło. Każdy zestaw ma niezależny przegląd spójności. Referencja otrzymuje rolę **ART_GUIDE**, **FACT_ONLY** albo **AVOID**. Sprzeczna wskazówka stylistyczna nie trafia do briefu jako równorzędny cel. Zdjęcia zabytków mogą różnić się warunkami fotografii; ich oświetlenia nie kopiować automatycznie do stylu gry. Nie narzucać wszystkim assetom jednakowego wieku ani zużycia powierzchni.

Karta referencji zapisuje URL obiektu i obrazu, autora/instytucję, datowanie z niepewnością, geograficzny kontekst, rolę, cechę wspieraną, ograniczenia, prawa i rzeczywisty poziom dostępu. Materiały z innego okresu mogą uzasadniać wyłącznie jawnie określoną analogię, rekonstrukcję lub trwały przedmiot poświadczony dla misji.

Film z YouTube: autor/kanał, URL, dokładny przedział czasu, tempo odtwarzania i rodzaj obserwacji. Tytuł, transkrypcja i miniatura mają status METADATA_ONLY. Do analizy animacji potrzebny faktycznie obejrzany odcinek albo legalnie dostępne klatki z timestampami. Nie deklarować pomiarów sił/masy na podstawie efektownego montażu.

## 6. Egzekwowanie w Unity

Poniższe kontrole edytorowe są **zadaniami do integracji**, nie zainstalowanymi funkcjami:

- UQ01: walidacja importu według konkretnego briefu: geometria, skala, osie, UV, rig, LOD, role map PBR i sRGB/Linear. 4K wymagane tam, gdzie zamówione; globalne podnoszenie wszystkich importów do 4K nie stanowi odbioru.
- UQ02: walidator scen/prefabów: GUID, missing scripts/references/shaders, faktyczne wiązanie assetu i zatwierdzony profil.
- UQ03: blokada buildu w `IPreprocessBuildWithReport` dla wymaganych niespełnionych kontroli; powiązanie z artefaktami bieżącego stanu. Nie nadpisywać istniejącego build runnera.
- UQ04: powtarzalny zapis kadrów i nagrań w istniejącej scenie; odnotowanie Unity/HDRP, zegara, kamery i ustawień. Niezależna ocena obrazu pozostaje wymagana.
- UQ05: benchmarki z rozgrzewką i określonym obciążeniem. Przy 30 Hz odstęp ticków to 1000/30≈33,33 ms; nie jest to cały budżet CPU symulacji. Budżet potrzebuje zapasu dla pozostałych zadań i pomiaru na wskazanym środowisku.
- UQ06: kontrola granic symulacji i prezentacji, prawidłowego wyjścia z awarii, zatrzymania i powrotu obiektu, wyczerpania puli oraz długiego przebiegu.

Problem wydajności rozwiązywać pomiarem i optymalizacją. Obniżenie uzgodnionego poziomu wymaga jawnej decyzji; zachować prawo gracza do istniejących profili i brak cichego fallbacku. Nie obiecywać dowolnego FPS/rozdzielczości na każdym urządzeniu. Komunikat o nieobsługiwanej funkcji jest wymaganym zachowaniem.

## 7. Dostępne narzędzia i granice ich dowodu

[Sprawdz-OdbiorStudia.ps1](../Sprawdz-OdbiorStudia.ps1) i [verify.py](../Tools/studio_quality/verify.py) zapewniają ręcznie wywoływane kontrole:

- Regression: sprawdzenie rzeczywistych XML i logów z raportu istniejącej bramki, ich hashy/liczników i kompletności inwentarza testów. Bez `-Historical` wrapper wymaga aktualnego snapshotu. Historyczny raport nie certyfikuje zmienionego drzewa.
- Acceptance: zgodność kontraktu, bieżącej sesji, snapshotu i hashy plików; osobne wyniki art/fizyka; dane CSV, RMSE i maksymalny błąd. SessionId pochodzi z metadanych klienta lub QUALITY_GATE_SESSION_ID. Brak dowodu oznacza BLOCKED. N/A wymaga niezależnie ocenionej klasyfikacji zakresu i uzasadnienia. Dla zerowych liczników asercji Unity potrzebny jest kompletny, zweryfikowany raport istniejącej bramki z tym samym XML, snapshotem i sesją; sam zerowy licznik nie jest dowodem.

Wzorce JSON w `qa/studio/` są niewykonanymi szablonami, zablokowanymi przez `template:true`. Wyniki przechowywać w `work/` albo `qa/reports/`. Narzędzie jest walidatorem dowodów, nie wykonawcą testów, dekoderem dowolnego wideo, uwierzytelnieniem autorstwa ani certyfikatem artystycznym/naukowym. Przegląd rzeczywistego wyniku jest obowiązkiem ludzi i uprawnionych reviewerów. Lokalne JSON i hashe nie zapobiegają sfałszowaniu wszystkich danych przez podmiot mający do nich prawo zapisu.

Nie zastępuje to istniejących hooków, pełnych EditMode/PlayMode, buildu ani playtestu. Automatyczna integracja zamykania zadań wymaga odrębnego adaptera i testów: zachować stary runner, sesję, blokadę i snapshot; dopełnić plan wymaganymi regresjami przed FREEZE. Nie instalować wrappera, który równolegle uruchamia drugi Unity albo oczekuje na raport, którego poprzedni hook jeszcze nie utworzył.

## 8. Autonomia dostępu i raportowanie

Zatwierdzony odczyt API/MCP i już udzielony dostęp mogą działać bez pytania przy każdym zapytaniu. Nowa autoryzacja, rozszerzenie uprawnień, koszt albo licencja wymaga właściwego upoważnienia; agent nie tworzy fikcyjnej zgody. Korzystać z sekretów przez skonfigurowany magazyn/zmienne, bez ich ujawniania. Nie uruchamiać całego warsztatu jako administrator dla rutynowych prac projektu.

Raport końcowy podaje osobno: kod, kompilację, testy, build, playtest, art, fizykę, wydajność i badania. Dla każdego: PASS/FAIL/BLOCKED/NOT_RUN/N/A, dowód, zakres i pozostały krok. Wynik testów narzędzia z fixture nie jest wynikiem gry. Odbiór jednego przypadku przy brzegu nie certyfikuje wszystkich cieśnin.

Źródła metody: [Claude Code — instrukcje i deterministyczne kontrole](https://code.claude.com/docs/en/best-practices#deterministic-and-non-deterministic-instructions), [hooki](https://code.claude.com/docs/en/hooks), [Unity Profiler 6000.3](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/profiler), [ITTC 2024 — Seakeeping Experiments](https://www.ittc.info/media/11888/75-02-07-021.pdf). Szczegółowe dowody dostępu znajdują się w raporcie źródłowym bieżącej sesji.
