# Morza Bogów — balans gospodarki

Parametry gospodarki zawiera qa/economy-baseline.json. Cele balansu wymagają testów walki, nawigacji i playtestów. Kryteria odbioru określa qa/ECONOMY_ACCEPTANCE.csv.

Cele balansu określa GAME_VISION.md. Wynik ocenia się przez realizację celów morskich oraz możliwość odpowiedzi na przeciwnika. Dochód, tempo produkcji i opłacalność ekspansji należy mierzyć razem ze stratami eskort, czasem dostawy i uwagą gracza. Każdą zmianę kosztu lub wydajności ocenia się dla rajdu, eskorty z ekspansją i oszczędnej obrony.

## 1. Dochód liczony po dostawie

Dla pełnego rejsu zbieracza przy dostępnym, niezatłoczonym zasobie:

T = C/q + 2d/v + m + w + C/u

Dochód/min = 60C/T.

C to ładownia, q tempo pracy, d odległość jednej drogi po faktycznej trasie, v prędkość, m suma manewrów, w oczekiwanie, u tempo wyładunku. Bazowo m=6 s, w=0 s i u=40/s. Przy pogodzie, obrażeniach, premiach i korkach podstawia się rzeczywiste dane. Wzór nie nalicza dwa razy opóźnienia za manewr.

| Jednostka / zasób | Trasa 150 m | Trasa 250 m | Trasa 450 m | Trasa 900 m |
|---|---:|---:|---:|---:|
| Kuter, B/min | 112,55 | 88,22 | 61,58 | 36,67 |
| Kuter, R/min | 97,34 | 78,59 | 56,73 | 34,90 |
| Handlowiec, B/min | 213,92 | 179,90 | 136,48 | 88,45 |
| Handlowiec, R/min | 191,20 | 163,55 | 126,86 | 84,31 |

To długookresowe tempo pojedynczego statku, a nie wypłata równomierna co sekundę: pierwszy pełny przychód czeka do wyładunku, a kolejny może być opóźniony. Liczby przestają obowiązywać wprost po wyczerpaniu złoża, nasyceniu wspólnego limitu albo zamknięciu skupiska.

położenie portu i bezpieczeństwo trasy są ważniejsze niż mała premia do samej pracy. Na 450 m kuter zarabia mniej więcej 55% dochodu z 150 m. Nie trzeba dodawać trzeciego surowca, by morskie położenie miało znaczenie strategiczne.

## 2. Kuter i handlowiec nie powinny się zastępować w każdej roli

Na 250 m handlowiec dostarcza około 2,04 razy więcej B/min niż kuter. Kosztuje jednak 400 B/200 R, podczas gdy kuter 200 B. Nie przeliczamy R na obowiązkową stałą walutę — jej wartość zależy od ograniczeń rekrutacji i sytuacji na mapie. Handlowiec nie jest darmowym ulepszeniem kutra.

Jego 650 pojemności wobec 160 umożliwia ekspansję, przewóz uzbrojenia, większe dostawy do stoczni oraz rzadsze wejścia do portu. Kuter pozostaje tańszy, szybszy i dostępny na płyciznach. Duży transport to mniejsza liczba eskortowanych celów, ale większa jednorazowa strata przy przechwyceniu. Badania muszą sprawdzić oba wybory, a nie wymuszać zastąpienie wszystkich kutrów handlowcami.

## 3. Port i badania bez obowiązkowych inwestycji przed walką

Przy trasie 250 m kuter daje 88,22 B/min bez kolejki. Dodanie 30 s oczekiwania obniża dochód do około 69,15 B/min. Żurawie za 200 B/60 R skracają sam wyładunek 160 jednostek z 4 do 3,2 s, więc bez korków dają małą korzyść; nie są obowiązkowym zakupem na początku. Dodatkowe nabrzeże uzasadnia pomiar kolejki i dostępnego podejścia, a nie bonus finansowy za liczbę budynków.

Pełne +25% tempa pracy nie daje +25% całego dochodu: dla kutra na 250 m czas zbioru B spada z 40 do 32 s, ale rejs i obsługa pozostają. Dochód wzrasta do około 95,22 B/min, czyli o około 7,94%. Technologie nie mogą więc bez końca zastępować kontroli akwenu. Wspólny limit złoża i skończone B ograniczają dodatkowo tempo wzrostu w jednej zatoce.

## 4. Pierwszy okręt musi powstać bez długiej gry ekonomicznej

Przykład taranowca z lekką bronią, taranem I i pełną obsadą:

| Pozycja | B | R |
|---|---:|---:|
| Kadłub z 9 marynarzami | 800 | 300 |
| Taran I | 100 | 0 |
| Lekka broń | 60 | 0 |
| Dodatkowych 27 żołnierzy | 0 | 81 |
| Razem | 960 | 381 |
| Pozostałość startowego 1400 B/600 R | 440 | 219 |

Gracz może zbudować kuter za 200 B i nadal mieć rezerwę. Cena nie dowodzi czasu gotowości: potrzeba budowy, wyposażenia, rekrutacji i dopłynięcia. Kadłub 50 s, obsada w kolejce i montaż broni muszą zostać zmierzone w grze. Alternatywne otwarcie militarne ma być dostępne bez neutralnego handlu, miasta, specjalizacji i badań ekonomicznych.

Wysoka wytrzymałość i koszt taranowca nie oznaczają przewagi we wszystkich otwarciach. Przeciwnik może rozpoznać inwestycję, zaatakować jej źródła albo przygotować właściwą kontrę; wynik określają mechaniki walki.

## 5. Ekspansja a kolejny okręt

Nowa osada i port wymagają 600 B przewiezionych przez handlowiec. Do kosztu porównania dochodzą dostępność transportu, utracony czas jego pracy i eskorta. Nie wystarczy policzyć ceny dwóch budynków.

Przykład optymistyczny: dwa kutry skracają drogę do lokalnych zasobów z 450 do 150 m, po czym B są wydawane w nowej osadzie. Przyrost wynosi około 101,93 B/min, co daje dolną granicę zwrotu 600/101,93≈5,89 min od uruchomienia nowej infrastruktury. Nie obejmuje czasu wyprawy i kosztu handlowca, ani nowej stoczni. Jeżeli B nadal muszą dotrzeć do starej bazy, trzeba policzyć dodatkowy przewóz; sama bliskość magazynu nie dowodzi finansowej opłacalności.

Wysunięty port ma także wartość wojskową: krótsza droga do remontu i możliwość miejscowych uzupełnień. To uczciwy powód ekspansji na sporne wody nawet przy wolniejszym zwrocie pieniędzy. Nie przyznajemy z tego powodu zdalnego wspólnego portfela i nie ignorujemy ryzyka rajdu.

## 6. Trwałość skupisk i rotacja pracy

Regeneracja i histereza E03 ograniczają wydajność przeciążonego skupiska. Przy zbyt dużej liczbie zbieraczy stan spada, wydobycie zwalnia, a skupisko zamyka się do czasu odnowienia. Regeneracja pozostaje dodatnia także przy stanie zerowym. Automatyka kieruje zbieraczy na inne bezpieczne skupiska i ogranicza liczbę alarmów.

Warunek bilansowy dla każdej próby: stan początkowy + regeneracja − rzeczywisty zbiór = stan końcowy. W teście ciągłego obciążenia czterema kutrami przez 1800 s należy sprawdzić zamykanie, ponowne otwieranie i brak duplikacji zasobów. Osobne playtesty mają uwzględnić czas rejsów i obciążenie portów.

## 7. Handel, remonty i brak pętli zysku

Najlepszy cykl wymiany z maksymalną premią 8% po obu stronach: 1 → 1,40×1,08 → 0,60×1,08 = 0,979776 jednostki początkowej. To strata około 2,02%, przed kosztem czasu i ryzykiem. Rzeczywiste wypłaty zaokrąglają się w dół. Nowe modyfikatory cen trzeba uwzględniać w tym samym teście wszystkich węzłów. Skończone limity neutralne są dodatkowym zabezpieczeniem, nie usprawiedliwieniem dodatniego arbitrażu.

Złom: 15% opłaconej ceny × HP. Naprawa: 20% ceny × odtworzona część HP. Dla ceny C i początkowego stanu h, złom od razu zwraca 0,15Ch. Pełny remont i złom dają netto 0,15C−0,20C(1−h). Różnica to −0,05C(1−h)≤0. Nie opłaca się naprawiać wyłącznie po to, by zwiększyć odzysk netto; zakup, remont i złom nie zwracają kosztu zakupu. Osobne bronie i cargo mają własne ID i nie dostają drugiego zwrotu za ten sam przedmiot.

Wyprawa nurkowa kosztuje 200 B/100 R wyposażenia oraz transport i czas. Nagroda 600 B/300 R pojawia się fizycznie, nie jest darmowym wpływem do wszystkich osad. Nie wystarcza sama do opłacenia kompletnego taranowca z przykładu. Pojawia się dopiero po zbudowaniu podstawowej floty; nie powinna decydować o otwarciu.

## 8. Mapa, flota i granice wzrostu

Wzorcowa mapa ma 34000 B w złożach oraz 2800 B startowo u dwóch graczy. Przy równym podziale to po 18400 B przed neutralnymi nagrodami. Limit wypłat handlowych 1200 B to około 6,52% tej wartości, a standardowy cel nurkowy 600 B — około 3,26%, jeśli w całości trafi do jednego gracza. Nie jest to dowód niewielkiej przewagi chwilowej: nagroda w konkretnym momencie może mieć większe znaczenie niż jej udział w całym budżecie.

Orientacyjny budżet po jednej stronie: 15 okrętów o średnim łącznym koszcie 900 B za kadłub i uzbrojenie = 13500 B oraz 4000 B infrastruktury, razem 17500 B. To przybliżenie skali kosztów, nie skład armii ani dowód dostępności racji. Remonty, straty zbieraczy, zaklęcia i wybór ciężkich klas zmniejszają dostępną liczbę. W tym wariancie nie oczekujemy budowy 100 pełnych okrętów na gracza; limity i test 400 jednostek są granicą techniczną, a większe scenariusze wymagają innego budżetu mapy.

Standardowa broń nie potrzebuje dowozu amunicji i nie ma podatku od floty, więc wyczerpanie B nie odbiera istniejącym okrętom zdolności zakończenia bitwy. Nie wprowadzamy automatycznej wygranej przez największy magazyn. Scenariusz powinien mieć jasne cele, opcję zgody na remis i ewentualny jawny limit czasu. Awaryjna logika nie może uznawać braku obserwowanych złóż za dowód, że na całej mapie zabrakło zasobów.

## 9. Odbiór balansu i ergonomii

Następny wymagany etap: sparowane seedowane mecze z zamianą stron i rzeczywistymi flotami. Wczesny rajd, eskorta/ekspansja i oszczędna obrona mają być grywalnymi wyborami. Cel 3–5 min do kontaktu i 8–12 min do większej bitwy to hipoteza dla wzorca mapy; kontakt wcześniej nie jest sam w sobie błędem, jeżeli ma koszt i kontrgrę.

Każdy raport ma podać mapę, frakcje, opening, seed, długość, koszty strat, dostawy/min, czas bezczynności, przyczynę wyniku i przedział ufności dla agregatów. Samo 100 wygranych SI z identycznym błędem przeciwnika nie uzasadnia odbioru. Playtesty ludzi oceniają, czy logistyka generuje decyzje morskie i pozwala pozostać przy bitwie, zamiast wymuszać ciągłą obsługę okien.

Do czasu tych prób status balansu rozgrywki pozostaje **NOT_RUN**. Równania służą strojeniu parametrów oraz wykrywaniu pętli zysku; równowagę strategii potwierdzają playtesty.
