# Morza Bogów — gospodarka służąca wojnie morskiej

Wymagania E00–E16.

## E00. Cel, granice i hierarchia wymagań

Gospodarka realizuje priorytety GAME_VISION.md: tworzy cele operacji morskich i daje przestrzeń do wyboru między siłą eskadry, osłoną dostaw i ekspansją. Automatyka ma umożliwiać równoległą walkę oraz zaopatrzenie. Rozbudowa jest skuteczna wtedy, gdy gracz potrafi ochronić szlaki i wykorzystać dostarczone zapasy.

Gospodarka ma zapewnić sens bitew: eskortowanie dostaw, przechwytywanie transportów, blokowanie portów, kontrolę skupisk i odległych złóż. Zwycięża dowodzenie flotą, nie skuteczność ręcznego przenoszenia skrzyń. Nie dodajemy gospodarki miejskiej z łańcuchami potrzeb mieszkańców, podatkami, rolnictwem, handlem giełdowym i dziesiątkami półproduktów.

Zachowujemy dokładnie dwa zasoby wydawane: brąz B i racje R. Wyposażenie pozostaje przedmiotami. Pojemność magazynu, liczba stanowisk, tempo obsługi i limity wydobycia są parametrami infrastruktury, nie kolejnymi walutami. Racje nie psują się w trakcie meczu. Nie ma podatku naliczanego co sekundę od istniejącej floty, głodu załóg ani bezpłatnego samoczynnego niszczenia okrętów z powodu bankructwa. Zdobyta jednostka zachowuje znaczenie bojowe nawet po odcięciu dostaw.

Reguły E00–E16 określają gospodarkę gry. Role klas, bazowe ceny kadłubów, parametry walki i dwa zasoby obowiązują zgodnie z GAME_DESIGN.md. Parametry należy stroić podczas testów i playtestów.

Trzy ustawienia ekonomiczne gracza: **gdzie pracuje flota gospodarcza**, **który port otrzymuje priorytet dostaw**, **czy inwestować w rozbudowę zamiast kolejnego okrętu**. Resztę wykonują doktryny i automatyczne kolejki. Domyślnie zlecając budowę z brakującym lokalnie zasobem, gracz widzi cenę, czas dostawy i ryzyko trasy; nie musi tworzyć osobnych zadań na każdy transport.

## E01. Lokalne magazyny i dostępność zasobów

Każda osada ma stabilny settlementId i własne ilości B/R. Port, stocznia, warsztat, kuźnia i świątynia należą do jednej osady. Budynki w jej obszarze wpływów na tej samej wyspie używają tego samego magazynu bez symulowania wózków lądowych. Brak możliwości łączenia dwóch wysp jednym obszarem wpływów. Granice przypisań są jawne w UI. Nie można tworzyć łańcucha stykających się portów przez morze w celu pominięcia transportu.

HUD frakcji pokazuje sumę własnych zasobów, ale suma nie jest wspólnym zdalnym portfelem. Dla wybranego budynku dostępne oznacza: dostarczone do jego osady i niezarezerwowane. Ładunek, obiecana dostawa i kontrakt w drodze nie mogą być wydane drugi raz. Magazyny osad nie sumują pojemności na odległość: osada ma 5000 B i 5000 R, miasto 10000 B i 10000 R. Port przyjmuje do tego magazynu; zniszczenie samego portu nie usuwa zasobów osady.

Rezerwacje są podzbiorem stanu magazynu: available=stored−reserved. Towar w ładowni znajduje się wyłącznie na statku, nie również w stored. Przy przeładunku transakcja jednocześnie odejmuje źródło i dodaje cel. Dla dwóch jednoczesnych zleceń kolejność wyznacza tick, potem commandSequence, potem entityId. Rounding i idempotency opisuje E14.

Pojemność docelową rezerwuj osobno od zasobów, maksymalnie do faktycznie wolnego miejsca. Kilka dostaw nie może każda dostać obietnicy ostatnich 100 miejsc. Przy pełnym magazynie pozostałość zostaje na statku; nie znika i nie staje się zdalnie dostępnym zasobem. Po 15 s braku postępu automat szuka innego dozwolonego odbiorcy lub przechodzi w oczekiwanie z jednym alarmem. Śmierć, anulowanie i zmiana celu zwalniają także rezerwację pojemności.

Magia jest opłacana przez osadę wybranej aktywnej świątyni. Broń, załogi i artefakty są fizyczne przy transferze między wyspami. Nie istnieje przenoszenie środków przez zmianę przypisania budynku. Rozkaz relokacji obsady wymaga miejsca i rzeczywistego transportu; transport nie tworzy ani nie usuwa ludzi.

## E02. Pozyskiwanie i ograniczenie bezpiecznego wzrostu

Bazowe tempo podczas samej pracy przy zasobie:

| Jednostka | B/s | R/s | Ładownia | Prędkość bez ładunku |
|---|---:|---:|---:|---:|
| Kuter | 4 | 3 | 160 | 8,5 m/s |
| Handlowiec | 6 | 5 | 650 | 5,8 m/s |

To nie dochód frakcji na sekundę. Trzeba doliczyć dopłynięcie, powrót, manewr, kolejkę i przeładunek. Pozyskiwanie B i R jednocześnie przez ten sam statek jest niemożliwe. Złoże odejmuje dokładnie to, co trafi do ładowni. Przy prawie pustym zasobie kilku zbieraczy nie może pobrać jego końcówki wielokrotnie.

Złoże B ma wspólny limit pracy 12 B/s, skupisko 9 R/s, przed premiami. Przy przekroczeniu limitu udziały aktywnych zbieraczy dzielą się proporcjonalnie do ich efektywnego tempa. Przykład: cztery kutry po 4 B/s dzielą 12 B/s, więc każdy uzyskuje 3 B/s. Zbieracze w podróży nie zajmują stanowisk pracy. Liczbę pozycji i obrys dostępnej wody ustala mapa; kolejka przy złożu działa podobnie jak w porcie. Nie umieszczaj 40 kutrów w jednym punkcie.

Premie do samego wydobycia: specjalizacja maks. +10% i trzy poziomy technologii po +5%, liczone addytywnie, łącznie maks. +25%. Wspólny limit złoża skaluje się najwyżej o +15% od technologii, bez specjalizacji. Dzięki temu premie przyspieszają pojedynczy rejs, ale nie zamieniają startowej zatoki w nieograniczony generator dochodu. Bonus nie zwiększa pozostałych zapasów i nie tworzy darmowego ładunku.

Kuter ma być dobrym wyborem na początek i na płycizny. Handlowiec ma przewagę pojemności, oszczędza obsługę portową na jednostkę przewiezionego towaru i obsługuje transfery/ekspansję, ale jest droższy i skupia większą stratę w jednym kadłubie. Nie balansujemy go obowiązkowym mnożnikiem zysku z samego przepłyniętego dystansu.

## E03. Złoża, skupiska i mapa operacyjna

Brąz jest skończony w standardowej potyczce; nie odradza się po zniszczeniu portu i nie resetuje po zmianie właściciela. Złoża przy starcie zapewniają rozwój i pierwszą flotę, a dalsze wymagają żeglugi przez sporne obszary. Każde złoże ma pojemność, limit pracy i jawny stan pozostałych zapasów po odkryciu.

Skupisko: pojemność startowa K=2000 R, stan S∈[0,K]. W każdej sekundzie logicznej regeneracja wynosi g(S)=1+3·S/K R/s, gdy S<K; potem następuje pobór i ograniczenie do [0,K]. W implementacji integruj po krokach 30 Hz i nie przekraczaj K. Wydajność pracy mnoży czynnik f(S)=clamp(S/(0,35K),0,4,1). Spadek poniżej 10% K zamyka skupisko; ponowne otwarcie dopiero przy 30% K. Przy zamknięciu działa regeneracja, ale nie zbiór. Histereza zapobiega ciągłemu włączaniu/wyłączaniu kutra. Domyślna doktryna wybiera inne bezpieczne skupisko, bez przekraczania strefy operacyjnej wskazanej przez gracza.

Nie wprowadzamy ręcznego zarządzania sezonami zbioru ani niewidzialnego pogarszania jakości racji. Ikona skupiska pokazuje stan i przewidywany czas odnowy; UI ostrzega przed wysłaniem kolejnych jednostek do już nasyconego miejsca pracy.

Wzorzec mapy potyczki 1 na 1: przy każdym starcie dwa złoża po 3000 B i dwa skupiska; w strefie spornej łącznie 22000 B w czterech złożach po 5500 B oraz co najmniej dwa dalsze skupiska. Całość 34000 B na mapie plus zasoby początkowe graczy. Nie jest to sztywny budżet wszystkich 40 scenariuszy: dla każdego ustal osobny czas docelowy i policz koszty floty/infrastruktury. Dla wielu graczy utrzymuj porównywalny budżet na gracza i alternatywne kierunki ekspansji, zamiast zagęszczać wszystko w jednym porcie.

Bezpieczna część to możliwość obrony, nie nietykalna strefa. Startowy kuter powinien dopływać do najbliższego zasobu około 120–200 m, a pierwsze wartościowe szlaki sporne mieć 350–700 m. Strefy startowe nie pozwalają fortecy razić całego centralnego akwenu. Dwie drogi do co najmniej jednego ważnego obszaru zmniejszają znaczenie przypadkowego zablokowania pojedynczej cieśniny. Trasy, odległości i zanurzenie muszą być sprawdzone przez rzeczywistą nawigację.

## E04. Przeładunek, dokowanie i moduły portu

Port bazowy ma dwa stanowiska i obsługuje 40 jednostek ładunku/s na stanowisko. Manewry wejścia/wyjścia to orientacyjnie łącznie 6 s w modelu balansowym, a w grze wynikają z rzeczywistego ruchu; nie dodawaj kolejnego sztucznego opóźnienia do już wykonanego manewru. Ładunek i rozładunek używają tego samego limitu. Transfer wyposażenia również zajmuje odpowiednią pojemność/czas. Stanowiska remontowe doku są osobne od dwóch stanowisk przeładunkowych.

Kolejka obsługuje priorytety, ale po 30 s oczekiwania podnosi priorytet starego zgłoszenia, aby konwoje wojskowe nie zagłodziły kutrów. Przerywanie rozładunku nie przywraca wyładowanej części ładowni. Zmiana portu docelowego zwalnia rezerwację stanowiska i niezrealizowane dostawy. Po śmierci kapitana/okrętu rezerwacje są usuwane w tym samym kroku symulacji.

Port może kupić maksymalnie dwa różne moduły z tabeli, każdy raz:

| Moduł | Koszt B/R | Czas | Efekt i ograniczenie |
|---|---:|---:|---|
| Dodatkowe nabrzeże | 250/80 | 60 s | +1 stanowisko przeładunkowe; wymaga wolnej geometrii i podejścia |
| Żurawie | 200/60 | 45 s | przeładunek +25%; nie przyspiesza wydobycia ani budowy |
| Bezpieczne kotwicowisko | 200/80 | 45 s | cztery zarezerwowane punkty oczekiwania z priorytetem odwrotu; nie daje tarczy ani automatycznej nietykalności |

Efekt wielu portów nie sumuje się na jednym stanowisku. Specjalizacja logistyczna +15% i żurawie +25% sumują się do +40%, więc maksymalnie 56/s, a nie 40×1,25×1,15. Inwestycja w żurawie ma sens dopiero przy rzeczywistym obciążeniu portu; przy braku kolejek dodatkowy kuter lub eskorta może być lepszy.

## E05. Automatyczne dostawy i konwoje

Gracz ustawia dla osady minimum i zapas docelowy B/R oraz tryb: gospodarka, stocznia, naprawy albo ręczny. Domyślnie stocznia utrzymuje równowartość jednego wybranego szablonu okrętu, a nie nieskończony zapas. Moduł logistyki co 2 s, w rozłożonym budżecie, tworzy zamówienia między własnymi osadami. Nie zajmuje naraz całej floty gospodarczej; domyślnie co najmniej jeden kuter na osadę pozostaje przy lokalnym zbiorze, jeśli taki istnieje.

Zlecenie ma requestId, źródło, cel, typ i ilość towaru, priorytet, termin, przypisany statek i stan. Cykl: Requested → SourceReserved → Assigned → Loading → InTransit → Unloading → Delivered. Osobne stany: Paused, Cancelled, Lost. Rezerwacja u źródła istnieje tylko do załadunku; podczas transportu towar jest własnością ładowni, a u celu figuruje wyłącznie oczekiwana dostawa. Częściowe dostawy zapisują dokładną pozostałą ilość. Przy anulowaniu w drodze towar pozostaje na statku i wraca/otrzymuje nowy cel, nie teleportuje się do źródła.

Automatyka nie zleca przeciwstawnych transferów tego samego zasobu między dwiema osadami. Dostawa nie może obniżyć źródła poniżej ustawionego minimum bez ręcznego potwierdzenia rozkazu w UI gry. Przy niedoborze jest widoczny wybór: sprowadź z innej osady, wybierz inny port produkcyjny albo zbuduj transport. Priorytety domyślne: ewakuacja i istniejące zobowiązania → odbudowa zbieraczy → bieżące zlecenia militarne → rozwój → handel neutralny; gracz może je zmienić.

Szlaki mają punkty pośrednie i tryb ostrożny/standardowy/ryzykowny. Ocena zagrożenia wykorzystuje tylko własną obserwację oraz wygasającą pamięć, nigdy niewidoczny stan przeciwnika. Tryb ostrożny czeka na eskortę lub wraca, a nie wybiera bez końca coraz dłuższej trasy. Brak osiągalnego celu oznacza Paused z konkretną przyczyną. Nie każ graczowi co 10 s zatwierdzać ponowienia tego samego przewozu.

Konwój domyślnie grupuje do trzech handlowców; okręty osłony zachowują formację eskorty i ograniczenie pościgu z C02. Zmiana rozkazu transportu nie zeruje polecenia osłony bez komunikatu. Przy pojawieniu się wroga najpierw ostrzeżenie, potem doktryna. Zabezpieczenie szlaku ma pozwalać małym grupom na bitwy inne niż jednorazowe starcie całych flot.

## E06. Rozbudowa, ekspansja i specjalizacja

Ceny podstawowych budynków określa GAME_DESIGN.md. Budowa zużywa zasoby lokalnej osady; nowa wyspa nie może korzystać z oddalonego portfela. Ekspansja to wyprawa handlowca z pakietem 600 B: 400 na osadę i 200 na port. Przewóz zajmuje realną ładownię (600 z 650). Punkt budowy sprawdzany przed odpłynięciem i przy rozpoczęciu. Przerwana wyprawa zachowuje niezużyty ładunek; śmierć statku tworzy jedynie rzeczywisty odzysk E11.

W miejscu założenia jest tymczasowe stanowisko wysadzenia ekipy, wyłącznie dla tego pakietu. Nie obsługuje normalnej produkcji, napraw ani magicznego rozładunku całej floty. Osada powstaje 45 s, potem port 25 s; zasoby zużywane zgodnie z postępem. Po ukończeniu osady pozostały ładunek można przeładować do jej magazynu przez stanowisko wyprawy, a z niego zbudować port. Stanowisko znika po zakończeniu lub anulowaniu wyprawy; nie jest dodatkowym bezpłatnym portem. Mapa musi mieć dostępną linię brzegu i żeglowne podejście.

Miasto powstaje po ulepszeniu osady za 500 B w 60 s. Po ulepszeniu można wybrać jeden profil za 300 B/100 R, 60 s:

| Profil | Premia lokalna | Co zyskuje flota |
|---|---|---|
| Zaopatrzeniowy | +10% tempa zbioru statków rozładowujących w tej osadzie, w promieniu 250 m | szybsza rekrutacja i przygotowanie wypraw, bez wzrostu limitu skupiska |
| Stoczniowy | czas produkcji kadłubów ×0,9; naprawa +15% tempa | szybsze uzupełnienia, ale bez tanich kadłubów do złomowania |
| Logistyczny | +15% przeładunku, krótsza obsługa konwoju | sprawniejsze wysunięte bazy |

Przypisanie premii zaopatrzeniowej ustala deklarowany port wyładunku przed rozpoczęciem pracy; zmiana portu nie nadaje jej wstecz ani nie pozwala pobrać premii z dwóch osad. Zmiana profilu: 150 B/50 R, 60 s, bez starej premii podczas przebudowy. Profil to jedna decyzja od czasu do czasu; nie jest minigrą optymalizowaną przed każdym strzałem. Podstawowe zadania budynków nadal działają w czasie zmiany profilu.

Każda własna osada jest celem operacyjnym, ale do zwycięstwa nie trzeba niszczyć wszystkich magazynów. Standardowy tryb zniszczenia centrum dowodzenia zachowuje określony cel główny. Przeciwnik nie może bez końca przedłużać meczu jedną pustą placówką gospodarczą.

## E07. Produkcja i wybór priorytetów

Stocznia zachowuje dwa stanowiska i wspólną kolejkę. Kadłuby, broń i technologie nie tworzą jednego uniwersalnego slotu budowy: stocznia buduje kadłuby, warsztat broń, kuźnia wyposażenie, szkoła badania. Gracz planuje gotowy szablon okrętu, a system pokazuje łączne lokalne koszty i przewidywane terminy. Broń z innej wyspy wymaga dostawy; brak broni nie może być ukryty jako gotowy w pełni uzbrojony okręt.

Rozróżniaj szkic planu i zlecenie finansowane. Szkic z brakującymi zasobami nie zamraża części całego skarbca na zawsze. Po uzyskaniu pełnego zestawu zasobów zlecenie atomowo rezerwuje koszt. Zmiana kolejności nie powoduje ponownej opłaty. Lokalne rezerwy na ratunek/naprawy mogą blokować automat, ale gracz może jawnie nadać ręcznemu zleceniu wyższy priorytet.

Przed startem anulowanie zwalnia 100% rezerwacji. Po starcie pozostała niezużyta część kosztu wraca w 80%, a 20% tej części trafia do jawnego odpływu strat organizacyjnych. Zużyta część nie wraca. Przykład: koszt 100 B, postęp 40% → zużyte 40, rezerwacja 60 → zwrot 48, strata 12. Nie naliczaj kary także od całych 100. Zniszczenie budynku automatycznie zwalnia niezużytą rezerwację do nadal istniejącego magazynu osady; zniszczenie samej osady obsługuje E10 i nie tworzy zwrotu w innej bazie.

Nie ma stałych dostaw amunicji jako dodatkowego obowiązku. Standardowa broń walczy bez wożenia pocisków. Koszt mają nowe okręty, montaż wyposażenia, szkolenie, rzeczywiste remonty i zaklęcia. Pozwala to walczyć po rajdzie gospodarczym zamiast natychmiast przegrywać przez pusty magazyn amunicji.

## E08. Technologie ekonomiczne i ograniczenie narastania przewagi

Technologie wojskowe z C04 pozostają osobną gałęzią: +8% na poziom, koszty 250/500/900 B, czasy 30/60/90 s. Nie stosuj ich premii automatycznie do wydobycia, ładowni lub cen. Technologie ekonomiczne:

| Gałąź | Poziomy | Koszt B/R kolejno | Czas | Efekt |
|---|---|---|---|---|
| Narzędzia pracy | 3 | 200/50; 350/100; 550/150 | 45/60/90 s | +5% tempa wydobycia i wspólnego limitu złoża na poziom |
| Organizacja ładowni | 2 | 250/80; 450/150 | 60/90 s | +10% pojemności na poziom, addytywnie maks. +20% |
| Kartografia szlaków | 1 | 300/100 | 60 s | odblokowuje dwa alternatywne warianty trasy automatu i porównanie ETA; bez wiedzy pod mgłą |

Kartografia nie jest warunkiem ręcznego wyznaczania punktów ani podstawowego omijania przeszkód. Badania frakcyjne są globalnymi odblokowaniami raz kupionymi w lokalnej szkole; wiedzy nie trzeba przewozić. Urządzeń i zasobów nie teleportują. Utrata szkoły nie odbiera ukończonych badań. Jedna aktywna technologia danej gałęzi w całej frakcji; dwie szkoły nie płacą za ten sam poziom osobno.

Brak ekonomicznych rabatów na koszt kadłuba, wyposażenia i zwrotu ze złomowania. Premie czasu nie przyspieszają zegara refundu, a premie do ładowni nie tworzą towaru. Globalne premie do wydobycia mają cap z E02. Nie ma oprocentowania zapasów ani pasywnego dochodu zależnego od liczby już istniejących portów. Rozwój nadal wymaga statków, celu pracy i bezpiecznego akwenu.

## E09. Handel neutralny jako opcja, nie główna strategia zwycięstwa

W standardowej potyczce handel jest opcjonalnym obiektem mapy, aktywnym najwcześniej od 6:00. Nie ma natychmiastowego globalnego rynku. Neutralny port publikuje proste kontrakty na dostawę B lub R i wypłatę drugiego zasobu. Statek musi dowieźć realny towar i odebrać realną wypłatę do ładowni. Powrót z ładunkiem nadal wymaga osłony. Bez automatycznego zysku za samo krążenie między własnymi portami.

Każdy gracz ma jawny, osobny limit kontraktów na mecz: maks. 1200 B oraz 1800 R wypłat w tym samym neutralnym węźle/zbiorze jego ofert; scenariusz może ograniczyć bardziej. Są to skończone rezerwy przypisane do gracza, nie bonus odradzający się po zapisie. Dostawy wpłacone przez gracza nie odnawiają tych limitów. Łączne limity wszystkich neutralnych węzłów na gracza nie mogą przekroczyć tych wartości w bazowym trybie. Brak wyścigu, w którym pierwszy gracz kupuje cały neutralny budżet i odcina drugiego; nadal można przechwycić jego statek w drodze.

Ceny netto wszystkich aktywnych ofert, już po prowizji: 1 R → 0,45–0,60 B; 1 B → 1,10–1,40 R. Opcjonalna premia ryzykownej wyprawy maks. 8% wypłaty, tylko z tej samej skończonej rezerwy. Nawet najlepszy cykl daje 0,60×1,40×1,08²≈0,9798 początkowego towaru przed zaokrągleniem; nigdy dodatni arbitraż. Sprawdź wszystkie pary ofert wszystkich neutralnych portów, nie tylko pojedynczy sklep. Właściwa oferta musi respektować limit cyklu ≤0,98 po wszystkich bonusach; nie zwiększaj premii na podstawie długości trasy.

Wypłata zaokrąglana w dół do 0,01 jednostki; łączna wypłata kontraktu ustalana przed wysłaniem. Podział dostawy na porcje rozdziela tę samą zatwierdzoną wypłatę, nie nalicza premii ponownie. Gracz rezerwuje jeden aktywny kontrakt na węzeł na maks. 180 s, drugi może być szkicem. Rezerwacja zmniejsza dostępną pulę ofert, dostawa zatwierdza zużycie. Po wygaśnięciu tylko niezrealizowana część rezerwacji wraca. Wypłata częściowa nie może zostać wykonana przy pełnej ładowni bez zwolnienia odpowiedniej pojemności przez dostarczony towar; w przeciwnym razie czeka w escrow.

Neutralny węzeł nie jest do zdobycia w bazowej potyczce, nie ma nieskończonego łupu i nie liczy się do zwycięstwa. Wariant kampanii może ustanowić inne zasady wyłącznie jako jawne reguły scenariusza. SI ma takie same limity, ceny i informacje o ofertach. Gra nie wymaga handlu do stworzenia pierwszej floty.

## E10. Blokady, zniszczenie i odbudowa

Blokada jest wynikiem rzeczywistej obecności floty i oceny zagrożenia, nie promieniem magicznie zatrzymującym rozładunek bez kontaktu. Port może nadal działać, jeśli statek fizycznie dotrze. Konwój podejmuje decyzję według doktryny; można zaryzykować wejście pod ostrzałem.

Przejmować można okręty; budynki podlegają niszczeniu. Zniszczenie portu usuwa jego stanowiska i rezerwacje, ale magazyn osady pozostaje. Kolejne dostawy trafiają do alternatywnego własnego portu tej samej osady lub czekają. Lokalna produkcja może wykorzystać wcześniej zgromadzony zapas, o ile budynek ma żeglowne wyjście; nie otrzymuje nowych odległych zasobów z powietrza.

Przy zniszczeniu osady anuluj wszystkie jej zlecenia i zwolnij rezerwacje przed podziałem zapasów. 30% niewydanych B/R staje się odzyskiwalnym łupem przy ruinie, 70% jest księgowanym odpływem zniszczenia. Nie tworzy się drugi raz zwrotu rezerwacji. Towar już w drodze pozostaje na jednostkach i szuka nowego odbiorcy. Przypisane budynki tracą usługi wymagające magazynu do odbudowania osady; nie znikają bezwarunkowo. Łup przy brzegu wymaga 10 s przygotowania zbieracza i przeładunku 20/s; nie wpada do portfela wroga od zadania ostatniego trafienia.

Ratunkowa odbudowa: doktryna trzyma w lokalnej osadzie opcjonalny, domyślnie włączony priorytetowy zapas 200 B na kuter, nie oddzielną nietykalną walutę. Ręczny rozkaz może go wydać po czytelnym ostrzeżeniu w UI gry. Gdy nie ma stoczni, działający port raz na 180 s może zbudować tylko kuter za normalne 200 B w 40 s. Ta sama cena i lokalny magazyn, brak darmowej jednostki i brak bonusu za celowe zatapianie kutra. Operacja ratunkowa zajmuje jedno stanowisko portu. Zniszczenie portu jej nie zwraca jako gotowego statku.

W kampanii scenariusz może mieć jawne wsparcie awaryjne, ale nie ukryte w standardowej potyczce. Brak ukrytego catch-up dochodu, premii za przegrywanie, resetu kosztów po przejęciu lub obowiązkowego cofnięcia silniejszej floty.

## E11. Naprawy, złomowanie i przejmowanie

Naprawa kadłuba zachowuje koszt 20% bazowej ceny kadłuba w B/R pomnożony przez przywracaną część HP. Przywrócenie pełnych 760 HP taranowca: 160 B/60 R. Naprawa 25% HP: 40 B/15 R. Dok naprawia 10 HP/s na jednym stanowisku remontowym; profil stoczniowy zwiększa do 11,5 HP/s. Opłaty naliczane wraz z rzeczywistym postępem, bez powtórnego zaokrąglania co tick. Brak zasobów wstrzymuje remont, nie pogarsza stanu. Podniesienie HP max nie jest darmową naprawą wsteczną. Załoga i uzbrojenie wymagają osobnego uzupełnienia.

Nowy kadłub zawiera minimalną obsadę marynarzy równą ceil(25% maksymalnej załogi), opłaconą w jego cenie. Resztę rekrutuje się jawnie: żołnierz 3 R, specjalista 10 B/5 R. Przykładowe ceny klas nie obejmują pełnej bojowej obsady i broni; UI pokazuje pełny szablon. Brak przenoszenia obsady do nowego statku przy zachowaniu tej samej osoby na starym. Rozwiązana obsada nie daje zwrotu racji. Marynarzy można przenieść; okręt bez nich zachowuje stan niesprawnego napędu z C04.

Dobrowolne złomowanie wymaga doku, 30 s poza walką i wolnego stanowiska. Zwraca 15% faktycznie zapłaconego kosztu kadłuba × bieżąca frakcja HP; nie wartość cennikową po rabacie. Zwrot zawiera obie zapłacone waluty. Zdobyty okręt zachowuje pierwotny koszt i identyfikator kadłuba, ale nie zwielokrotnia księgi wartości. Wyposażenie można zdemontować jako istniejący przedmiot przed złomowaniem, bez dodatkowego zwrotu walut za to samo wyposażenie. Nie ma osobnej sprzedaży załogi.

Wrak statku nigdy nie wypłaca walut za samo istnienie kadłuba. Zawiera 50% realnego transportowanego B/R (drugie 50% to zatopienie), wyposażenie rozlosowane według zapisanego seeda oraz artefakty zgodnie z misją. Szansa przeżycia każdego istniejącego przedmiotu to 30%, rozstrzygana raz przy zatonięciu i zapisana; to wartość oczekiwana, a nie twardy limit 30% liczby przedmiotów w pojedynczym wraku. Nie losuj ponownie po wczytaniu. Nie zachowuj równocześnie całego itemu na zdobytym statku i we wraku. Artefakty fabularne nie przepadają i nie wygasają.

Przejęcie okrętu z ładunkiem oddaje realną pozostałą zawartość, a nie jej kopię lub dodatkową premię. Niedokończony transport zmienia stan na Lost dla poprzedniego właściciela. Rezerwacja u źródła już nie istnieje, więc nie zwracaj mu przewiezionych zasobów. Brak premii pieniężnej za ostatni cios, którą można farmić sojuszniczymi jednostkami.

## E12. Wyprawy nurkowe i nagrody

W bazowej potyczce zatopiony skarbiec jest opcjonalną wyprawą od 8:00, maks. jeden wspólny cel na mapie 1 na 1, nigdy obowiązkowym źródłem startowych zasobów. Handlowiec potrzebuje wyposażenia nurkowego za 200 B/100 R (montaż 15 s), zachowanego jako rzeczywisty przedmiot. Operacja: 90 s w promieniu 30 m, przerywana przy wyjściu poza strefę lub zniszczeniu wyposażenia. Postęp maleje o 10 punktów procentowych na każde 5 s przerwy, zamiast natychmiast resetować całą wyprawę; nie można podwójnie składać postępu z kilku operacji. Atakujący może przerwać wyprawę albo próbować przejąć zdobycz.

Nagroda standardowa 600 B/300 R, fizycznie w miejscu ukończenia. Więcej niż podstawowa ładownia handlowca, więc potrzebny drugi rejs albo transportowiec — jawny koszt logistyki i okno przechwycenia. Nagroda nie jest przyznawana za wielokrotne odkrycie/kliknięcie. Skarbiec znika po rozstrzygnięciu, a zasób ma jedno globalne ID. Przedmiot nagrody może przejąć przeciwnik, jeśli pierwszy dowiezie statek. Rzadki wariant 900 B/450 R tylko w oznaczonych scenariuszach, wyłączony w bazowej potyczce.

Nie dodawaj losowych skrzyń z kwotą umożliwiającą natychmiastowe podwojenie floty. Nagrody misji i neutralne wypłaty mają osobną pozycję w bilansie oraz skończony budżet. Premie bohaterów i artefaktów nie mnożą łupu i wypłat neutralnych; istniejące premie bojowe/magiczne zachowują znaczenie.

## E13. SI, ergonomia i pierwszeństwo walki

SI korzysta z tych samych magazynów, rezerwacji, wydobycia, ograniczeń kontraktów i kolejek. Nie otrzymuje zasobów za utratę kutra. Zmiana trudności wpływa na planowanie, koordynację i czas reakcji, a nie potajemnie na budżety. Jawny handicap można ustawić tylko jako osobny parametr scenariusza.

Planer ekonomiczny ma dostarczać flotę zgodną z aktualną strategią. Ocenia korzyść z nowego zbieracza względem zatłoczenia zasobu, kolejki portu, zagrożenia i czasu odzyskania inwestycji. Rozbudowa bez dodatkowego źródła dochodu nie jest automatycznie dobra. Zabezpiecza podstawową możliwość zbioru, unika rezerwacji wszystkich R pod badania i zapewnia eskortę wartościowym transferom. Po rajdzie próbuje odtworzyć dochód oraz przegrupować flotę, nie wyłącza całej walki do odbudowania perfekcyjnej bazy.

Domyślne szablony: najpierw pierwszy eskortowy okręt i dochód; następnie rozbudowa albo grupa bojowa zależnie od obserwacji. Wyprawa osadnicza to inwestycja konkuru­jąca z taranowcem, nie obowiązek wykonania jej przed kontaktem bojowym. Brak odblokowania podstawowych klas wojennych za długim drzewem ekonomicznym.

Panel gospodarki: lokalne dostępne/zarezerwowane/w drodze, wąskie gardło i ETA, trzy poziomy priorytetu portu, lista tras, przyczyna postoju. Jednym poleceniem można: przydzielić zbieraczy do strefy, wyznaczyć zapas stoczni, dodać eskortę i odbudować utracone transporty do ustalonego limitu. Nie pokazuj 20 osobnych alertów z jednego przerwanego konwoju; jeden alarm obejmuje grupę i proponuje punkt dowodzenia na minimapie.

Kryterium ergonomii do playtestu: po skonfigurowaniu dwóch osad w pięciominutowym odcinku aktywnej walki gracz wykonuje najwyżej około sześciu nieplanowanych interwencji gospodarczych. To cel do pomiaru, nie twarde ograniczenie liczby kliknięć i nie dowód z modelu. Decyzje rozbudowy z własnej inicjatywy liczymy osobno. Jeżeli system regularnie odciąga od bitwy, popraw automat zamiast zwiększać nagrody za mikrozarządzanie.

## E14. Kontrakt implementacyjny i zapis

Warstwa gospodarki jest częścią czystej symulacji 30 Hz. Transakcje zasobów używają fixed-point z jednostką 0,01, akumulatory pracy mają większą precyzję reszty. Ceny nie są odejmowane przez zmiennoprzecinkowe mnożenie liczby klatek. Koszt naprawy/produkcji naliczaj od całkowitego postępu i już pobranej sumy, nie od osobno zaokrąglonego kosztu pojedynczej klatki.

Minimalne dane: SettlementInventory, Reservation, EconomicOrder, Shipment, Route, HarvestNode, TradeContract, EquipmentItem, RefundProvenance i EconomyLedger. Nazwy to zakres odpowiedzialności, nie wymóg konkretnego wzorca klas. Każda transakcja ma transactionId, tick, sourceContainer, destinationContainer lub jawny sink/source, settlementId, ownerId, resourceType, amount i reasonCode. Idempotentny commit, niezależnie od powtórnego żądania lub odtworzenia zdarzenia. UI otrzymuje wynik, nie zmienia walut bezpośrednio.

Bilans całego świata per B/R: stan początkowy wszystkich kontenerów i węzłów + regeneracja + jawne nowe źródła = bieżąca suma wszystkich kontenerów i węzłów + zużycie + zniszczenie + odpływ poza świat. Zbiór, przeładunek i handel istniejącym zapasem są transferami, nie nowymi źródłami. Osobny bilans frakcji: początkowe własne zapasy + pozyskanie z węzłów + import/zdobycz/nagrody = własne zapasy + wydatki + eksport/utrata. Nie mieszaj obu perspektyw w jednym liczniku. Neutralne pule i escrow mają kontenery; nagroda pojawia się raz z jawnym reasonCode. Zmiana właściciela jest transferem własności, nie stworzeniem waluty. Rezerwacja nie jest nowym kontenerem z kopią zasobu.

Zapis obejmuje stan S/K skupisk, flagę zamknięcia, reszty akumulatorów, rezerwacje i ich źródła, postęp zleceń, przypisania osad, cargo, kontrakty/limity, stan wyprawy, pierwotny zapłacony koszt kadłuba i już wykonane transakcje. Save/load nie odradza nagrody, nie wydłuża escrow, nie zeruje limitu handlu i nie dubluje transportu. Brakujące ID blokuje odtworzenie zobowiązania i daje diagnostykę, zamiast losowo zwracać całą cenę.

## E15. Cele balansu i pomiary

Zależności liczbowe i cele balansu określa ECONOMY_BALANCE.md. Parametry gospodarki przechowuje qa/economy-baseline.json. Odbiór balansu wymaga rzeczywistych bitew, pomiarów i playtestów.

Cele dla zwykłej potyczki 1 na 1: pierwszy celowy kontakt bojowy ok. 3–5 min, pierwsza sensowna wyprawa ekspansyjna ok. 5–8 min, pierwsze większe starcie 8–12 min, mecz ok. 25–35 min. Są to hipotezy testowe dla mapy referencyjnej, nie narzucony zegar ani przeszkoda dla wcześniejszego rajdu. Start: osada, port, stocznia, dwa kutry, 1400 B/600 R. Po skompletowaniu szablonu pierwszego okrętu ma pozostać możliwość zbudowania kutra lub kontynuowania zbioru; nie wymuszaj 6 min bez walki w oczekiwaniu na handel.

Mierzyć: B/R dostarczone/min, koszt armii faktycznie gotowej do walki, udział inwestycji gospodarczych, czas przestojów portu, straty transportów, koncentrację zasobów, odsetek dochodu z neutralnych źródeł, liczbę interwencji gracza i czas odbudowy po rajdzie. Zwiększenie floty gospodarczej bez nowego akwenu ma z czasem dawać mniejszy przyrost dochodu; nie wprowadzaj ukrytej kary silniejszemu graczowi.

Plan testów balansu: co najmniej 100 sparowanych seedów map (zamiana stron), po trzy strategie: wczesny rajd, eskorta/ekspansja, oszczędna obrona. Dla każdej pary strategii i pary frakcji test obu stron; raportować liczbę meczów, przedziały ufności i długość gry, nie sam procent wygranych. Dodatkowo ludzkie playtesty ergonomii, podejmowania ryzyka i czytelności. Zbyt silna obrona: najpierw koszt/pozycja/tempo budowy obrony, nie darmowe dochody atakującego. Zbyt silny rajd: drogi obejścia, powrót konwoju, odbudowa i eskorty, nie nietykalny transport.

Cel diagnostyczny: żadna z trzech strategii nie powinna utrzymywać >60% zwycięstw przeciw obu pozostałym w symetrycznych testach; traktować jako alarm do oceny, nie matematyczny dowód równowagi. Partie SI nie zastępują testów przeciw człowiekowi. Wynik finansowy modelu nie wyznacza siły bojowej tarana, abordażu i magii.

## E16. Etapy wdrożenia i odbiór

M0: schematy danych, księga i testy zachowania sum; poprawne local available. M1: zbiór, rozładunek, lokalna produkcja, jeden transfer, eskortowalny szlak i kompletna mała bitwa; bez obowiązku tworzenia rynku lub specjalizacji przed grywalnością. M2: moduły, specjalizacje, automatyczne zapasy, neutralne kontrakty i odbudowa. M3: kampania, SI ekonomiczna, profilowanie i sparowane playtesty. Ekonomia nie może przesunąć na koniec implementacji nawigacji i trzech form walki.

qa/ECONOMY_ACCEPTANCE.csv to macierz do przypisania konkretnych testów EditMode/PlayMode i dowodów. Wszystkie pozycje startują z NOT_RUN. Testy obowiązkowe obejmują nie tylko formuły: konkurujące rezerwacje, zniszczony port podczas wyładunku, osadę z pełnym magazynem, zdobyty transport w drodze, zatrzymaną wyprawę, zachowanie identyfikatorów po zapisie, blokadę i zmianę trasy oraz ponowne odtworzenie tego samego zdarzenia.

Hook obejmuje ECONOMY_DESIGN.md, ECONOMY_BALANCE.md i parametry w qa/ snapshotem. Sam hook nadal uruchamia wymagane zestawy Unity, nie ocenia sam jakości strategicznej i nie wymusza całej kampanii testów balansu. Odbiór ekonomii wymaga dodatkowego raportu playtestów i niezależnej kontroli. Nie wpisuj PASS na podstawie samej obecności JSON z obliczeniami.
