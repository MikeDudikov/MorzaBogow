# Morza Bogów — plan kontynuacji i integracji

Zakres produktu określa [NOWY_PLAN_GRY.md](NOWY_PLAN_GRY.md), funkcje [KATALOG_MECHANIK.md](KATALOG_MECHANIK.md), mapy [MAPY_I_SCENARIUSZE.md](MAPY_I_SCENARIUSZE.md), a obowiązek kwerendy [HISTORIA_I_ZRODLA.md](HISTORIA_I_ZRODLA.md). Realizujemy jedną grę mitologiczną, wyłącznie jednoosobową. Estetyka pozostaje poza zakresem przeprojektowania.

Kontrakty dowodzenia, trudności, celów i zapisu J01–J10 ustanawia [GRYWALNOSC_I_ODBIOR.md](GRYWALNOSC_I_ODBIOR.md). Ich odbiór jest częścią odpowiednich etapów, przy zachowaniu pełnego katalogu.

## 1. Punkt wznowienia

Aktualny odczyt: HEAD `012e329`, gałąź `balans-skala`, format zapisu **19**. P01 ma odbiór opisany w `docs/implementation/sessions/2026-11-12-0418-p01-a02.md`; raport `qa/reports/P01-5164392cc89342ddb7dafe9ca76b3c7b/report.json` poświadcza 404/404 EditMode i 153/153 PlayMode dla swojego snapshotu. Dodatkowy przyrost logistyki i korekty dokumentacji opisuje [audyt studia](AUDYT_PLANU_I_STUDIA_2026-11-12.md). Nowe testy celowane Unity mają 14/14; pełną bramkę tego przyrostu użytkownik przekazał Claude Code. Historyczny PASS nie certyfikuje później zmienionego drzewa. Przed wznowieniem sprawdzić stan faktyczny i nowe raporty.

Gotowe fundamenty: siedem klas, moduły broni, pociski, taran, abordaż i załoga pryzowa, cztery grupy obsady, ranni, morale, porty i projekty, fizyczny transport, odzysk, konwoje, morze, mgła wojny i zapis. Należy je rozwinąć, nie odtwarzać.

Nie powtarzać wdrożonych elementów P01: Pending, wyników komend, poprawionego dystansu ostrzału, oceny obsad AI i reguły burtowej. Ich aktualne dowody i ograniczenia odczytać z raportu odbioru. Przed rozbudową pozostają odpowiednie zadania etapów: jakość podejścia i zatory grup (P03), rezerwacja miejsca odbiorcy (P03), rozstrzygające cele/A15 (P07/P11), ceny przy nowych obsadach/katalogu oraz odroczenia wskazane w raporcie 0418. Ukończenie wybranej regresji nie oznacza wykonania całego P03.

Złomowanie statku z D/S ma już zabezpieczenie odmowy; nie należy implementować go drugi raz. Linie kodu i zakres pewności znajdują się w analizie stanu. Aktualny stan roboczy trzeba każdorazowo sprawdzić przed edycją.

## 2. Zasady mało inwazyjnej integracji

1. Nie zmieniać scen, ustawień, pakietów i oprawy przy dodawaniu definicji mechaniki, jeżeli nie jest to konieczne do jej działania.
2. Zostawić pojedynczą symulację World, aktualne komendy, księgę i katalog podstawowych klas.
3. Wspólne zachowanie realizować jednym mechanizmem efektów i relacji; różnice wpisywać do danych.
4. Zachować numery istniejących enumów. Nowe wartości dopisywać na końcu. Warianty nie mogą obchodzić limitów kadłuba.
5. Włączać kompletny podgraf funkcji. Samo wymaganie kuźni lub miasta bez możliwości ich zbudowania nie może zablokować bieżącej partii.
6. Nie zmieniać naraz całej gospodarki, AI, prędkości, katalogu obrażeń i pogody bez odrębnych porównań skutku.
7. Zapis musi obejmować wszystkie nowe stany. Zmiana binarnego formatu lub niezgodnej semantyki podnosi `SaveGame.Version` i określa sposób obsługi niezgodnego pliku.
8. Przy każdej sesji otworzyć źródła dla podstaw świata i dla realizowanego zadania; nie traktować dokumentacji projektu jako dowodu naukowego.

## 3. Etapy

### P01 — Stabilność walki, komend i zapisu

**Cel:** zabezpieczyć działający rdzeń przed dodaniem nowych typów i efektów. **Zależności:** rozpoznany stan repozytorium.

Zadania:

- odtworzyć i poprawić względny ruch abordażu; uzgodnić odległość sczepienia z kapsułowym obrysem nawigacji;
- uwzględnić wszystkie grupy wojowników w ocenie strat AI;
- wybierać dystans z aktywnego, obsadzonego uzbrojenia;
- zachować granicę stanu ticku i serializować Pending z porządkiem; zapis w pauzie nie wykonuje ticku ani nie czeka na wznowienie;
- dodać podstawowy CommandId, wynik wykonania i powód odmowy przy zachowaniu istniejących komend oraz komunikatów;
- sprawdzić istniejącą odmowę złomowania z cargo i komunikat dla gracza;
- utrzymać ciągłość konwojów i projektów po zapisie.

Odbiór: zgodne/przeciwne kursy, skrzyżowanie 90°, brak obsady, sam oszczep, sam łuk, min/max zasięg, zapis z niepustą kolejką i dokładny bilans. Istotne testy regresji oraz właściwa bramka na zamrożonym snapshotcie. Nie stroić w tym zadaniu wszystkich klas.

W P01 odbieramy istniejący bufor komend i zapis w pauzie, nie deklarujemy jeszcze kolejki kolejnych tras. Sekwencyjne zadania jednostki powstają w P03 i otrzymują własne rozszerzenie zapisu. Odmowa nie może zdradzać niezaobserwowanego stanu wroga.

### P02 — Katalog definicji i relacje stron

**Cel:** wspólne podłoże dla rozszerzeń. **Zależność:** P01.

Zadania:

- dodać definicje wariantów, technologii, fortów, zdolności, bohaterów, przedmiotów, spotkań i scenariuszy;
- rozdzielić rolę, konfigurację i wymagania dostępności;
- przygotować rozszerzalne dane zadań, wyników komend, profili AI oraz manifestów posiłków; zachować działający minimalny kontrakt P01;
- ustalić kontrakt wspólnego efektu: ID, źródło, właściciel, kanał, czas, nakładanie i serializacja; przygotować interfejs wykonawcy, bez deklarowania gotowości wszystkich skutków;
- centralnie rozstrzygać `CanControl`, `IsHostile`, `CanCapture` i `IsMainParticipant`;
- wprowadzić trzy stałe sloty właściciela: 0 człowiek, 1 główne AI, 2 niezależni aktorzy; zachować tylko dwa główne miejsca uczestników wyniku. OwnerSlotCount=3 i walidacja 0≤Owner<3 obejmują wszystkie tablice i maski, bez Owner=-1;
- rozszerzyć PriorityPorts i inne tablice zależne od właściciela, pętle widoczności i walidator zapisu; nie tworzyć Opponent(2). Slot 2 korzysta z wykonawcy spotkań i EncounterTargetPolicy;
- nie używać `1−Owner` do określenia wszystkich wrogów; koniec partii dotyczy głównych stron i celu misji, nie automatycznego wybicia całej fauny;
- przygotować eksport faktycznie używanych kosztów i wymagań do modelu i QA.

Odbiór: każdy właściciel ma legalną reprezentację, tablice i maski obejmują wszystkie dopuszczalne przypadki, mgła i rezerwacje nie wyciekają między stronami. Test dwóch głównych stron i niezależnych obiektów. Nie rozszerzać projektu do dowolnej liczby uczestników.

### P03 — Komplet gospodarki i zadań autonomicznych

**Cel:** podstawowy zakres E01–E04, E06–E12 oraz A01–A05 przy zachowaniu gotowych konwojów. **Zależność:** P02. Odbiór P03 obejmuje transport D/S, towarów i istniejącego wyposażenia; transfer obsady fortów z E09 domyka P05, a transfer artefaktów i ludzi związanych z efektami — P08. E05 (neutralny cel pozyskania) domyka P09. Pełne E06/E08 wymagają ponownego sprawdzenia po odnowie z P08. Te późniejsze podkontrakty nie są warunkiem zamknięcia podstawowego P03.

Zadania:

- umożliwić obu jednostkom gospodarczym dozwolone zadania zbioru;
- dodać wybór strefy, przechodzenie do kolejnego punktu i powrót z ładunkiem;
- dodać skończone odnowienia partii i ich zapis;
- wprowadzić towar rybny i inne towary wymieniane na D/S przy dostawie;
- domknąć rezerwację miejsca w celu oraz zwalnianie przy utracie portu, zmianie trasy i zatopieniu;
- dodać cztery flagi zachowania oraz zwiad z powrotem, patrol i strefy;
- zachować trwałe zadanie i politykę podczas pełnego celu, wyczerpania punktu i znanej blokady drogi; wznawiać bez powtarzania rozkazu;
- dodać dopisywanie kolejnych zadań, jawne zastąpienie/anulowanie i ich zapis w pauzie;
- rozszerzyć obecny planer o wykrycie braku postępu i wspólne przejście grupy: zwężenie, kolejność, zebranie. Nie zastępować całego modelu ruchu;
- weryfikować blokadę wodowania bez utraty opłaconego kadłuba.

Odbiór: pełne źródło/pełny cel, koniec partii, dwa transporty jednocześnie, manualne przerwanie, utrata eskorty, przejęcie ładunku, zmiana portu i zapis. Zasób nie pojawia się przez sam wybór rozkazu.

J01/J02/J07: mieszana grupa na legalnym przejściu, brak drogi, brak napędu i rzeczywista blokada wroga. Pełny cel → wolne miejsce oraz pusty punkt → następny/nowa partia działają bez dodatkowego identycznego rozkazu. Legalnego oczekiwania nie traktować jako awarii ruchu.

### P04 — Port, kuźnia i drzewo rozwoju

**Cel:** kompletny graf B01–B10 bez aktywnych jeszcze mocy. **Zależność:** P03.

Zadania:

- rozszerzyć istniejące projekty o kuźnię, trzy poziomy osłon i taranów, usprawnienia rodzin uzbrojenia oraz napędu;
- zdefiniować warunki awansu osady: przystań, stocznia, warsztat, kuźnia i opłata;
- zachować dok po awansie miasta jako miejsce napraw kadłuba i modułów;
- dodać sanktuaria jako obiekty z własnością i stanem, przygotowane do wykonawcy mocy;
- związać handlowca z dostępem miejskim dopiero wraz z pełnym działającym podgrafem;
- sprawdzić ceny konstrukcji, obsady i wyposażenia bez ujemnych składników.

Odbiór: można legalnie osiągnąć każdy węzeł grafu; badanie nie wymaga swojego własnego wyniku; anulowanie i zniszczenie projektu zwalnia właściwe rezerwacje; przejęcie lub utrata budowli nie kopiuje postępu.

### P05 — Trzy klasy fortów

**Cel:** działający brzegowy odpowiednik modułowego okrętu. **Zależność:** P04.

Zadania:

- dodać definicje fortów, obsadę, sloty, uszkodzenia modułów, przeładunek i jedno miejsce artefaktu;
- wdrożyć punkty 1/2/3 oraz pulę 15 na osadę;
- rozróżnić pochodzenie z lokalnej budowy i z wyprawy;
- pozwolić statkom inicjować budowę wyprawową, z jawnym płatnikiem i rzeczywistą dostawą;
- egzekwować zakaz taranowania/abordażu fortu i brak bohatera w garnizonie;
- umożliwić sprzedaż tylko obiektu, którego pochodzenie na to pozwala.

Odbiór: wszystkie legalne kombinacje punktów, zniszczenie zwalniające właściwą pulę, dozbrojenie pustego fortu, rozładunek, martwa strefa ciężkiego modułu i osłabienie ognia po stratach obsady. Forty nie mogą być ukrytym źródłem nieograniczonego srebra.

P05 odbiera pojemność i transfer miejsca artefaktu; działanie efektu przedmiotu odbieramy dopiero w P08. Graf budowy głównych stron nie jest grafem niezależnej flotylli: jej skończony manifest i EncounterBuildPolicy określamy w P09.

### P06 — Pełna flota i wyposażenie

**Cel:** katalog N01–N12 i C01–C14. **Zależność:** P04; integracja fortów po P05.

Zadania:

- dodać okręt dowodzenia, prawo flagowca i odpowiednią pojemność przedmiotów;
- dodać cztery specjalizacje stronnictw na wspólnej infrastrukturze wariantów;
- odwzorować sześć rodzin modułów miotających, zachowując siedem istniejących typów runtime, oraz dwie wbudowane bronie specjalne;
- wprowadzić doświadczenie załogi o ograniczonym wpływie na celność i abordaż;
- zachować dotkliwy skutek przegranego szturmu i uzupełnianie ludzi w porcie;
- dopracować przejęcie, samozatopienie wskazanych typów i ustawienie własnej doktryny zdobyczy.

Odbiór: 10 ról dostępnych wybranej stronie, 12 w katalogu unikalnym, brak wymontowania broni wbudowanej, brak cargo gospodarczego na jednostce wojennej, brak ofensywnego abordażu kupca. Testy mają obejmować skład floty i cel operacji, nie tylko pojedynki.

Dla każdej roli wykazać istotną korzyść i wykonalną odpowiedź w porównywalnych zadaniach J06. Porównania mają dwa osobne ograniczenia D/S i uwzględniają czas oraz straty. N08 nie zastępuje transportu gospodarczego, a niewrażliwość H05 nie usuwa ograniczeń zasięgu działania i jednoczesności celów.

P06 odbiera kadłuby, konfiguracje, sloty i warunki dostępności. Skutki specjalizacji wykorzystujące ogień, mgłę i zakłócenie odbieramy w P07; samo istnienie wariantu w danych nie oznacza sprawnej zdolności.

### P07 — Wykonawca efektów i dwanaście mocy

**Cel:** jeden mechanizm źródła, czasu, nakładania i odporności. **Zależność:** P05–P06.

Zadania:

- dodać naukę i użycie mocy, cenę, sanktuarium, cel, zapowiedź i zakończenie;
- uruchomić wykonawcę według kontraktu P02 i odebrać także efekty specjalizacji N09–N12;
- wdrożyć pełną macierz M01–M12, w tym rozróżnienie ochrony zwykłej i magicznej;
- dodać logiczną niewidzialność i ujawnienie niezależne od widoczności modelu;
- wdrożyć wir, rafy, trąbę i ognistą strefę na wspólnej osi czasu; dla M06 dodać podstawową encję przywołanego stworzenia z ruchem, celem, obrażeniami, czasem życia i zapisem. Nie odkładać tej zależności do P09;
- dodać zapisywane zmiany maski terenu bez przebudowy całej sceny; TerrainRevision unieważnia cache nawigacji i morza także po zmianie obrysu przy niezmienionej liczbie wysp;
- włączyć logiczny wpływ wiatru po odrębnym sprawdzeniu napędu i drogi. Do tego momentu nie wolno odbierać misji wymagającej tej mocy jako ukończonej.

Odbiór: zakres z sanktuarium i całej mapy, nielegalny cel, zniszczenie źródła, pauza, koniec czasu, dwie równoczesne moce, ochrona 80%, neutralizacja kanału, kontakt z rafą pod kadłubem i zapis w fazie aktywnej. Odbiór logiczny i odczytywalna prezentacja w obecnym stylu są odrębne.

### P08 — Dziewięć postaci i przedmioty

**Cel:** H01–H09, T01–T11 i R01–R09. **Zależność:** P07.

Zadania:

- unikalne ID, warunek rekrutacji, obecność na statku i przeładunek;
- pojemność artefaktów 1/2/1 dla statku, dowodzenia i fortu oraz magazyn miasta;
- odnowa kadłuba, modułów i ludzi z odrębnym pochodzeniem i bez zysku ze złomu; zachować współczynnik odzysku 15%, lecz stan kwalifikowany do odzysku oddzielić od HP odtworzonych bezpłatnie. Same PaidConstruction i bieżący procent HP nie wystarczają; zmiana stanu zapisu otrzymuje właściwą wersję i test odnowa→złom/utrata→odczyt;
- dostarczenie dokumentu jako warunek badania;
- loot wewnątrz nośnika, zrzut po zniszczeniu oraz jawne warunki przejścia kampanii;
- pełna niewrażliwość z wykonalną odpowiedzią poprzez cel operacji, a nie samą próbę odebrania HP.

Odbiór: próba duplikacji, zatopienie, przejęcie i odbicie, przekroczenie slotów, pozostawienie przedmiotu poza ewakuacją, brak krytycznego znaku, zapis podczas transferu. Pasywny efekt nie może pozostawać po odłączeniu źródła.

### P09 — Stworzenia, przybytki i niezależne flotylle

**Cel:** Z01–Z11, dwa ośrodki mocy, pięć konfiguracji jednostek i trzy klasy fortów trzeciej siły. **Zależność:** P07–P08 oraz relacje P02.

Zadania:

- ograniczone strefy pościgu i reagowanie na oba główne stronnictwa;
- odtwarzanie ze źródła, wyłączenie po jego zniszczeniu i brak powtarzanych wypłat;
- wspólny budżet spojrzenia gorgony, wabienie syreny i uszkadzanie obsady przez harpię;
- dwa ośrodki odporne na własny kanał;
- pojedynczy neutralny cel pozyskania z nagrodą i postępem;
- ośrodek flotylli łączący naprawę, przeładunek i produkcję bez pełnego drzewa badań.
- rozszerzyć encję stworzenia z P07 o pozostałe archetypy i siedziby; niezależną produkcję ograniczyć skończonym manifestem oraz EncounterBuildPolicy, zamiast wymagać nieistniejącej kuźni lub ośrodka wiedzy.

Odbiór: zniszczenie źródła w chwili odtworzenia, usunięcie aktywnego efektu, granice pościgu, równoczesna próba pozyskania przez dwie strony, samozatopienie zamiast przejęcia i zachowanie po zapisie. Źródła muszą być osiągalne lub możliwe do obejścia zgodnie z celem misji.

### P10 — Wykonawca kampanii i mapy

**Cel:** scenariusze danych, stany celu i przenoszenie kampanii. **Zależność:** P03; pełny katalog misji wymaga odpowiednich P04–P09.

Zadania:

- wprowadzić ScenarioDefinition, cele, porażkę, zdarzenia jednorazowe, manifesty i CarryPolicy;
- zdefiniować wymagane/alternatywne/ opcjonalne ObjectiveID, rozstrzygnięcie w końcu ticku, priorytet jednoczesnego sukcesu i porażki oraz terminalne rozliczenie;
- wdrożyć K01–K03 jako pierwszy kompletny wycinek z realnym przeniesieniem stanu;
- wpleść kontekstową naukę reguł J04 i informację o powodach niewykonania czynności, używając obecnego stylu;
- przygotować pozostałe K04–K14 według zależności katalogu;
- dodać trzy profile sektorów do granicy 2600 m i archetypy potyczek;
- migrować dane scenariuszy i walidator do rzeczywistego schematu wykonawcy;
- zapewnić start, wybór misji, wynik, ponowienie i zapis w obecnym interfejsie bez projektu nowego stylu.
- rozdzielić zatwierdzony postęp, rezerwę, manifest wyprawy i wynik; ponowienie przywraca snapshot wejścia, bez duplikacji nagród;
- dodać niezależne sloty ręczne/szybkie/automatyczne, atomowe generacje wraz z powiązanymi danymi wyglądu i bezpieczne checkpointy J10.

Odbiór: każdą misję da się rozpocząć i rozstrzygnąć. Wariant porażki nie pozostawia niewykrytego braku możliwości wykonania celu. Zapis nie cofa nagrody ani odtworzeń. Mapa spełnia kontrakt dróg, płycizn i zasięgów; liczba stron i objawień nie wychodzi poza dane relacji.

Odległy niepowiązany wróg i niewykonany cel opcjonalny nie blokują sukcesu. Każda misja ma konkretny predykat, dodatnie wymagane ilości i identyfikatory krytycznych obiektów. Zapis podczas pauzy, przerwanie zapisu, utrata kluczowego nośnika, ponowienie i zaakceptowana porażka są odrębnymi przypadkami odbioru.

### P11 — AI całego katalogu

**Cel:** przeciwnik wykorzystuje reguły, nie tylko podstawowy cykl statków. **Zależność:** wdrażany przyrostowo P03–P10.

AI rozdziela rozpoznanie, zbiór, eskortę, rozbudowę, obronę, szturm i odwrót. Rozwija badania, uzbraja forty, nie buduje wyłącznie jednej klasy, stosuje moce do legalnych i zaobserwowanych celów, rozpoznaje niewrażliwość, chroni transport i nie wydaje kosztów z cudzego magazynu. Zna tylko informacje jawne i własną historię obserwacji.

Odbiór: identyczne obserwacje przy różnym ukrytym stanie dają te same decyzje do chwili nowej informacji. Trudność wynika z planowania, reakcji i doboru celów. Jawny przydział scenariusza ma skończony budżet. Odbudowa nie może tworzyć ukrytego dochodu.

Profile Nauka/Standard/Weteran zmieniają wyłącznie dozwolone pola decyzji. Testy J03 sprawdzają identyczne taryfy, statystyki i historię obserwacji/RNG oraz brak ukrytego dostosowania posiłków do wyniku gracza. Fala zależna od jawnego celu pozostaje dozwolona, jeśli ma skończony manifest i zapowiedź. Trudniejsze AI nie musi zwyciężyć w każdej parze ziaren.

### P12 — Ujednolicenie danych i odbiór gry

**Cel:** kompletny katalog, jedna ścieżka startowa i rzeczywisty dowód grywalności. **Zależności:** P01–P11.

Zadania:

- powiązać każdą pozycję katalogu z kodem, testem i artefaktem;
- usunąć rozbieżności pomiędzy danymi runtime, modelem, opisem i faktyczną dostępnością;
- odebrać trzy archetypy potyczek i wszystkie operacje kampanii z wariantami utraty krytycznych obiektów;
- wykonać build oraz pełne przejścia, nie ograniczać się do walidatora JSON;
- sprawdzić gospodarkę, użyteczność pełnego katalogu, zwycięstwo i rozstrzyganie sytuacji bez zdolności kontynuowania;
- przeprowadzić playtesty z ludźmi i porównać rozkład uwagi z 40/25/15/15/5;
- odebrać pełne J01–J10, w tym samodzielne zrozumienie celu, przyczyn oczekiwania, kontrowania mocy i przenoszonego postępu;
- sprawdzić, że zmiana ustawień dźwięku nie zmienia stanu symulacji, a agregacja komunikatów nie zagłusza alarmów krytycznych;
- potwierdzić, że teksty startowe, dokumenty i kolejny przyrost funkcji nie wprowadzają wymagań gry sieciowej.

Nie trzeba zakładać usługi zewnętrznej, aby odebrać grę lokalną. Wydanie wymaga jednak właściwych testów, niezależnego review oraz dowodów z działającego buildu.

## 4. Zależności i pierwszy wycinek

Rdzeń: `P01 → P02 → P03 → P04 → P05 → P06 → P07 → P08 → P09`.

Wykonawca P10 zaczyna się po P03 i integruje ukończone systemy. AI P11 rozwija się równolegle z każdym odebranym modułem. P12 zamyka pełny zakres. Osobne prace mogą być rozdzielone między wykonawców dopiero po ustaleniu kontraktów danych i własności plików. Jeden projekt nie może mieć dwóch jednoczesnych runnerów Unity.

Pierwszy grywalny wycinek: stabilne istniejące okręty, skończone partie zasobów, automatyka, pełna podstawowa drabina portu, pierwszy fort oraz K01–K03. Nie nazywamy tego odbiorem całego katalogu; moce, bohaterowie i stworzenia otrzymują własne obowiązkowe etapy.

## 5. Procedura pojedynczego zadania

Przed zmianą: aktualny Git, otwarte źródła, zakres plików, kontrakt zachowania i test odtwarzający problem albo oczekiwany wynik. Po zmianie: odpowiednie testy EditMode/PlayMode, kontrola kompilacji przy zmianie C#, sprawdzenie zapisu i niezmienników właściwych dla zadania, niezależne review oraz raport na zamrożonym snapshotcie.

Nie zmieniać polityki bramki dla uzyskania PASS. Nie przepisywać wyników, nie liczyć skipped jako sukcesu i nie uruchamiać syntetycznego runnera jako testu gry. Nie dodawać testów odtwarzających tylko strukturę implementacji. Dokumenty planistyczne kontrolujemy pod kątem spójności, a działania gry dowodzimy uruchomieniem.

## 6. Matematyka odbioru i granice dowodów

Katalog: 12 unikalnych ról głównych, 10 dostępnych wybranemu stronnictwu; 3 klasy fortów; 6 rodzin modułów miotających, 7 istniejących typów runtime i 2 bronie specjalne; 3 poziomy osłony i 3 taranu; 12 mocy; 9 specjalizacji bohaterów; 11 kategorii użytkowych przedmiotów + 9 znaków rekrutacyjnych; 11 archetypów zagrożeń + 2 przybytki; 5 konfiguracji flotylli; 14 operacji. Każda liczba ma zdefiniowany mianownik i nie jest procentem gotowości.

Pierwsza matryca balansu: 3 strategie, wszystkie 3 nieuporządkowane pary, 20 ziaren i zamiana stron: `3×20×2 = 120` przebiegów na wybrany zestaw parametrów i jedną mapę. Dwie gry tej samej pary ziarna nie są niezależnymi obserwacjami. Wynik oceniamy przez cel operacji, straty, dostawy, czas i użycie dostępnych odpowiedzi, nie wyłącznie saldo D+S.

Nie istnieje wiarygodny procent ukończenia wyliczony z liczby plików, commitów lub przechodzących testów. `NOT_RUN`, `BLOCKED`, `FAIL`, `SKIPPED` i `INCONCLUSIVE` są odrębnymi stanami. Ukończenie dokumentacji nie oznacza ukończenia implementacji.
