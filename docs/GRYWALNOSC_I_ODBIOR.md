# Morza Bogów — dowodzenie i odbiór grywalności

Obowiązujące kontrakty gry jednoosobowej. Uzupełniają [plan gry](NOWY_PLAN_GRY.md), [katalog](KATALOG_MECHANIK.md), [parametry](PARAMETRY_ROBOCZE.md), [mapy](MAPY_I_SCENARIUSZE.md) i [etapy integracji](PLAN_WDROZENIA.md). Nie opisują wykonanej implementacji. Żaden z kontraktów nie upoważnia do zmiany estetyki ani zmniejszenia pełnego katalogu funkcji.

## 1. Kontrakty

| ID | Kontrakt | Etap odbioru | Powiązane Q |
|---|---|---|---|
| J01 | Niezawodny ruch pojedynczego kadłuba i grupy | P01: geometria; P03: przejścia i stan oczekiwania; P07: teren | Q04, Q08 |
| J02 | Jednoznaczne rozkazy, kolejki i pauza | P01: wynik/Pending; P03: kolejka zadań | Q02, Q04, Q07 |
| J03 | Jawna, uczciwa trudność | P02: dane; P11: zachowanie; P12: porównanie | Q03, Q06, Q10 |
| J04 | Nauka reguł w działaniu | P03: informacja; P10: K01–K03 | Q08, Q11 |
| J05 | Rozstrzygające cele i brak zbędnego pościgu po mapie | P10 | Q07, Q08 |
| J06 | Przydatność ról i realne decyzje taktyczne | P06–P09, P11–P12 | Q01, Q03, Q05 |
| J07 | Trwałe zadanie gospodarcze bez powtarzania tego samego rozkazu | P03 | Q02, Q07, Q11 |
| J08 | Czytelność informacji przy zachowaniu oprawy | przyrostowo P01–P10, odbiór P12 | Q05, Q06, Q09 |
| J09 | Trwały, jawnie rozliczany postęp kampanii | P08: unikalność; P10: kampania | Q02, Q07, Q08 |
| J10 | Odporne zapisy i działające ustawienia podczas partii | P01: pauza; P10: sloty; P12: pełna integracja | Q07, Q09, Q12 |

### J01 — Ruch i przejścia

Droga musi być legalna dla konkretnego profilu kadłuba, zanurzenia i konserwatywnego obrysu używanego przez obecny planer. Kapsułowy kontakt statków nie oznacza automatycznie, że planer brzegów przepuszcza je przez dowolną szczelinę szerszą od belki. Wspólny kontrakt legalności obejmuje planowanie, kontakt z lądem i walidację punktu końcowego.

Rozkaz grupowy zachowuje docelowy szyk, lecz przed cieśniną dopuszcza zwężenie, przejście kolejno i zebranie po drugiej stronie. Grupa przechowuje uczestników, kolejność, fazę i punkt zebrania. Utrata lidera nie kasuje celu. Nie wprowadzamy drugiego silnika ruchu ani przenikania kadłubów.

Detektor braku postępu mierzy postęp po zaplanowanej drodze lub ukończenie jej odcinka, nie tylko prostą odległość do celu. Objazd może początkowo oddalać od celu. Pauza, naprawa, przeładunek, manewr taranu i legalne oczekiwanie nie są utknięciem. Ponowienia mają limit; po nim pozostaje konkretny stan oczekiwania lub odrzucenia z powodem. Niedopuszczalne są nieograniczone oscylacje i ukryta teleportacja.

**Odbiór:** przygotowane legalne trasy dla 2, 8 i 16 różnych okrętów; ruch w obu kierunkach, zatrzymany sojusznik, blokada wroga, brak napędu, zmiana terenu, utrata lidera i zapis w przejściu. W wykonalnym układzie bez wrogiej blokady wszystkie jednostki docierają w ustalonym dla próby budżecie czasu. Niewykonalny przypadek daje prawdziwy powód; sam stan „czeka” nie zalicza próby wykonalnej trasy.

### J02 — Wynik rozkazu i planowanie w pauzie

Komenda ma CommandId i wynik dla każdej jednostki: przyjęta, wykonywana, czekająca, zakończona, odrzucona albo anulowana. Powód rozróżnia m.in. brak zdolności, obsady, doku, środków, miejsca, obserwowanego celu lub drogi. Zbiorowe polecenie pokazuje, które jednostki je przyjęły. Podgląd interfejsu nie zastępuje ostatecznej walidacji w symulacji.

Zwykłe polecenie zastępuje dotychczasowe zadanie. Jawne dopisanie tworzy kolejkę kolejnych zadań, a Stop ją czyści i zwalnia tylko właściwe rezerwacje. Zadanie ciągłe, np. patrol, pozostaje aktywne do anulowania; kolejne zadania nie są pozornie przedstawiane jako rozpoczynające się od razu. Niedostępny trwale cel kończy dany element z powodem, bez usunięcia ocalałego cargo. Tymczasowa blokada zachowuje zadanie.

Pauza zatrzymuje ticki, ruch, projekty, AI, moce i terminy misji. Pozwala przeglądać stan i planować działania. Przyjęcie przyszłej komendy nie zużywa kosztu; walidacja i transakcja następują przy wykonaniu. Nie obiecujemy sfinansowania całej kolejki na podstawie nieaktualnego podglądu.

Zapis podczas pauzy obejmuje Pending, kolejność oraz kolejki zadań, bez wykonania dodatkowego ticku. Wczytanie oddaje kontrolę w pauzie i nie wykonuje zaplanowanych działań przed wznowieniem. Przełączenie tempa nie zmienia długości ticku symulacji.

**Odbiór:** pauza → kilka poleceń → zapis/odczyt → wznowienie daje ten sam stan co przebieg bez odczytu, przy identycznych komendach i RNG. Przyjęcie i anulowanie nie tworzy zasobów. Utrata celu, przejęcie jednostki i wyczerpanie budżetu mają jednoznaczny wynik.

### J03 — Trudność i wiedza AI

Profile Nauka, Standard i Weteran różnią się czasem reakcji, koordynacją, horyzontem planowania i skłonnością do ryzyka. Korzystają z tej samej taryfy, statystyk, rezerwacji, widoczności oraz warunków legalności co człowiek. Profil nie zmienia ukradkiem obrażeń, cen, odnowień, nagród ani budżetu źródeł.

Scenariusz może mieć asymetryczny stan początkowy i skończone posiłki. Odprawa ujawnia charakter zagrożenia i regułę jego uruchomienia; rozpoznanie określa szczegóły, których gracz jeszcze nie zna. Każda fala ma opłacony manifest, źródło, limit i zapowiedź. Zdarzenie może zależeć od jawnego celu, np. zajęcia przyczółka, lecz nie od ukrytego pomiaru „gracz wygrywa zbyt łatwo”. Zapis nie odtwarza wykorzystanego manifestu.

**Odbiór:** ta sama transakcja dla człowieka i głównego AI ma ten sam koszt i czas. Dwie historie o identycznych obserwacjach oraz RNG planera wywołują te same decyzje do pierwszej różnicy obserwowalnej. Ukryte cargo, pozycja i kolejka gracza nie wpływają na wybór AI. Trudniejsze AI nie musi wygrać każdej pary ziaren; oceniamy porównywalny zestaw prób i powody decyzji.

### J04 — Wprowadzenie do reguł

K01 uczy eskorty i fizycznej dostawy, K02 trwałego zadania zbioru, K03 wyposażenia i rozwoju portu. Podpowiedź pojawia się przy pierwszym użyciu reguły, wskazuje przyczynę i może zostać pominięta. Wyjaśnienie powraca na żądanie bez restartu. Nowe typy zagrożeń otrzymują czytelny opis przed pierwszym obowiązkowym starciem z nieodwracalnym skutkiem.

Odprawa pokazuje wymagane cele, warunki porażki, stan przenoszony, ograniczenia dostępności oraz przewidywany typ zagrożenia. Pauza, regulacja tempa i tryb Nauka nie obniżają potajemnie jakości nagrody ani postępu. Pełny katalog jest odblokowywany stopniowo przez kampanię, zgodnie z jej stanem.

**Odbiór:** nowy gracz może wskazać bieżący cel, powód zatrzymania jednostki i legalny sposób jego rozwiązania, korzystając z informacji gry. Rejestrować pomoc prowadzącego, błędne rozkazy i miejsca zatrzymania. Nie zastępować próby z człowiekiem automatycznym testem.

### J05 — Cele i wynik

Każda operacja ma wymagane ObjectiveID, jawne predykaty sukcesu/porażki, opcjonalne cele i identyfikatory rozliczeń. Dostawa oznacza faktyczny, jednokrotny odbiór; obecność oznacza wejście właściwego obiektu do określonej strefy. Wartości wymaganych ilości są dodatnimi danymi manifestu. Brak wartości jest błędem walidacji, a nie domyślnym zerem zaliczającym cel.

Sukces ocenia się w końcowej fazie ticku po śmierci, dostawach i zmianach właściciela. Krytyczna porażka ma pierwszeństwo przy jednoczesnym spełnieniu obu warunków; konkretny scenariusz może ustanowić inny jawny priorytet. Wynik jest terminalny i jednokrotny. Późniejszy wrak, odległy przeciwnik i niewykonany cel opcjonalny nie blokują sukcesu.

Nie dopisujemy obowiązku zniszczenia wszystkich wrogów po wykonaniu operacji. Cel zniszczenia może dotyczyć wskazanego portu, siedziby albo skończonej grupy i jest opisany od początku. Nieosiągalność rozpoznajemy na podstawie konkretnych warunków, np. utraty niezastępowalnego przedmiotu bez dozwolonego odzysku; nie udajemy ogólnego dowodu rozwiązania każdego możliwego świata.

**Odbiór:** dodanie niepowiązanego wroga nie opóźnia wyniku. Zniszczenie/przejęcie nośnika celu, wyczerpanie partii i utrata wyjścia uruchamiają właściwy wariant. Każde K01–K14 ma przejście zwycięskie i co najmniej jeden istotny wariant porażki, z zapisem w trakcie.

### J06 — Decyzje i przydatność ról

Ostrzał ludzi, uszkadzanie kadłuba, oblężenie, taran, abordaż, dowodzenie i transport odpowiadają na różne zadania. Dla każdej roli określamy korzyść, koszt sposobności i wykonalną odpowiedź przeciwnika. Uniwersalność konfiguracji N08 nie nadaje mu masowej ładowni, jednoczesnej obecności w dwóch miejscach ani parametrów wszystkich specjalistów naraz.

Pełna niewrażliwość H05 pozostaje pełna w swoim kontrakcie. Różne cele, odległość, przepustowość transportu i kontrola ruchu zachowują znaczenie pozostałych jednostek. Nie dodajemy ukrytego przebicia odporności. Mapa nie wymaga zabicia niewrażliwego celu bez legalnej odpowiedzi.

Ekonomia wymaga decyzji o szlaku, ekspansji, osłonie, badaniu i chwili inwestycji. Nie dodajemy waluty ani obowiązkowego systemu potrzeb mieszkańców, by zwiększyć liczbę kliknięć. Przejęcie sprawnego okrętu może dać inny wynik niż zatopienie; zaleta nie może być bezwarunkowa wobec każdego typu celu.

**Odbiór:** próby zamiany roli przy wspólnych ograniczeniach `D≤D0` i `S≤S0`, tej samej technologii i celu. Nie porównywać wyłącznie sumy D+S. Mierzyć dostawę, wykonanie celu, czas i straty. Raport może stwierdzić brak wykrytej konfiguracji dominującej w zbadanej macierzy, nie matematyczny dowód braku dominacji w całej grze. Każda rola ma przynajmniej jeden istotny przypadek użycia i jedną działającą kontrę w tej macierzy.

### J07 — Trwała automatyka gospodarcza

Zadanie gospodarcze zachowuje intencję gracza niezależnie od chwilowego stanu ruchu. Rozróżnia pracę, przewóz, przeładunek, oczekiwanie na miejsce, partię lub drogę, zakończenie i anulowanie. Pełny magazyn nie kasuje zadania ani ładunku. Wyczerpany punkt kieruje do następnego dozwolonego punktu strefy, a przy zapowiedzianej kolejnej partii pozwala czekać i wznowić pracę.

Przy pełnym celu automat wybiera dozwolony alternatywny port albo czeka według zapisanej polityki. Zwolnienie miejsca, nowa partia lub ustąpienie znanej blokady wznawia zadanie. Oczekujący statek nie zajmuje bez potrzeby nabrzeża. Rezerwacje odpowiadają aktualnej dostawie i są zwalniane po zmianie zamiaru. Ręczne polecenie ma pierwszeństwo.

Automatyka nie otrzymuje wiedzy o niezaobserwowanym wrogu i nie zmienia sama zaakceptowanej tolerancji ryzyka. Alarm dotyczy nowego problemu, nie każdego ticku oczekiwania. Wszystkie stany i polityki przechodzą przez zapis.

**Odbiór:** po jednym legalnym rozkazie układ sam wykonuje sekwencję pełny cel → wolne miejsce, pusty punkt → następny, oczekiwanie → nowa partia, przerwana droga → legalne wznowienie. Liczba dodatkowych powtórzeń tego samego rozkazu potrzebnych wyłącznie do tych przejść wynosi zero. Nowa decyzja o ryzyku lub osłonie nadal należy do gracza.

### J08 — Informacja operacyjna

Obecna karta jednostki, herold i oznaczenia pokazują rolę, własność, obsadę, sprawne stanowiska, zadanie i powód oczekiwania. Rozróżnienie ról nie zależy wyłącznie od podobieństwa modeli. Używamy istniejących nazw, ikon i miejsc informacji; nie zmieniamy modeli, palety ani stylu interfejsu.

Przed kosztem mocy gracz widzi cenę, legalny obszar i znane ograniczenie. Po trafieniu może rozpoznać rozstrzygający typ ochrony. Informacja nie zdradza ukrytej zawartości, pozycji ani zniszczenia niewidocznego celu. Odrzucenie pod mgłą opisuje brak ważnego obserwowanego celu, a nie jego tajny stan.

Komunikaty tego samego zdarzenia są agregowane. Powtarzane zaznaczenie nie zagłusza alarmu utraty flagowca lub krytycznego celu. Zmiana głośności i wyciszenie nie mogą zmieniać reguł ani czasu symulacji; jakość nagrań i muzyki pozostaje poza zakresem tego kontraktu.

**Odbiór:** rozpoznanie jednostki i przyczyny odmowy w istniejącym HUD, priorytety alarmów, brak duplikowania komunikatu oczekiwania oraz test braku przecieku wiedzy. Czytelność potwierdzić próbą z człowiekiem.

### J09 — Postęp i rozliczenie wyprawy

Stan kampanii rozdziela: zatwierdzone ukończenia i odblokowania, roster/rezerwę portową, manifest wyprawy i bieżący wynik. Jednostki pozostawione w rezerwie nie znikają przy rozpoczęciu misji. Odblokowanie wiedzy nie oznacza darmowego wyposażenia ani pominięcia lokalnych wymagań budowlanych.

Powtórka przegranej misji odtwarza jej snapshot wejścia i nie kasuje wcześniej zatwierdzonych ukończeń. Jeżeli scenariusz dopuszcza kontynuację po porażce, przyjęcie tego wyniku zatwierdza również straty. Nie można zatrzymać nagrody z zaakceptowanego wyniku i jednocześnie cofnąć jego strat.

CarryPolicy określa z góry transfer flagowca, dodatkowych kadłubów i ludzi, postaci, przedmiotów oraz magazynu. Rezerwa nie omija limitów miejsc artefaktów ani potrzeby fizycznego dowiezienia i ewakuacji. Rzeczy pozostawione na nieuprawnionym okręcie nie teleportują się do miasta. Manifest wyjścia może wskazywać ewakuowane kadłuby; nie kopiuje całego sektora.

Przed zatwierdzeniem wyniku ekran pokazuje zachowane i tracone obiekty z powodem, wykorzystując obecny styl. Rekrutacja i wybory mają sprawdzalne następstwo: dostęp do sposobu przejścia, postaci, przedmiotu albo jawnej odblokowanej operacji. Nie wymagają osobnego systemu nieograniczonych talentów.

**Odbiór:** nieudana wyprawa, ponowienie, przyjęcie dopuszczalnej porażki, utrata flagowca, postać w rezerwie, przedmiot na innym okręcie i powrót do zapisu przed rozliczeniem. Każda nagroda, rekrutacja i strata zostaje zatwierdzona najwyżej raz na TransactionId. Przejście nie wymaga nagrody, którą można uzyskać dopiero po nim.

### J10 — Ochrona zapisu i ustawień

Ręczny zapis, quicksave i autosave mają odrębne sloty. Autosave rotuje własne generacje i nie zastępuje dobrego pliku ręcznego. Korzystają z jednej serializacji świata, Pending, zadań i stanu kampanii. Komplet generacji obejmuje także powiązane haszem dane wyglądu i zniszczeń; nie zmieniamy ich treści estetycznej.

Zapis odbywa się atomowo z manifestem generacji i walidacją zgodności. Uszkodzenie pliku, brak miejsca lub błąd wymiany pozostawia wcześniejszy dobry zestaw i działającą partię. Odczyt nie łączy plików różnych generacji. Zapis w pauzie nie wymusza ticku. Automatyczne terminy liczy czas symulacji, a bezpieczne checkpointy nie wykonują się wielokrotnie po wczytaniu.

**Odbiór:** przerwanie zapisu w kolejnych fazach, niekompletna generacja, niezgodny hash, pełny dysk, odczyt kopii, pauza i aktywna moc. Test ustawień porównuje stan dla identycznych komend i RNG przy zmianie głośności; nie wymaga identycznego czasu renderowania. Błąd zapisu ma jawny komunikat i nie udaje sukcesu.

## 2. Macierz integracji i dowodów

Każdy J otrzymuje mapowanie do katalogu, kodu, istotnego testu i artefaktu. Warunek logiczny kontrolujemy automatycznie; obciążenie uwagi, zrozumienie celu i przyjemność decyzji wymagają również próby z człowiekiem. Nie narzucamy fikcyjnej liczby klatek ani procentu zwycięstw jako dowodu wszystkich aspektów grywalności.

Zestaw obejmuje mały port, cieśninę, mieszany transport i walkę, efekt terenowy, pełny magazyn, wyczerpaną partię, krytyczny przedmiot oraz zapis w pauzie. Próby porównawcze zachowują mapę, budżet, technologię, seed i jawnie wskazane zmienne. Jednoczesne przestrojenie wielu niezależnych parametrów nie pozwala przypisać wyniku jednej zmianie.

Pierwszy wycinek K01–K03 wymaga właściwych części J01–J05, J07–J10. Pełny J06 i pozostałe kombinacje efektów są odbierane wraz z całym katalogiem. Ukończenie dokumentacji nie nadaje żadnemu J statusu wdrożonego.
