# Morza Bogów — gospodarka floty

Wymagania E00–E12. Zastępują wcześniejszy model dwóch zasobów. Etapy wdrożenia: najpierw mała bitwa (port, stocznia, cztery kutry, dwa punkty pracy, ruch, przeładunek, kompletny okręt, przeciwnik), następnie druga wyspa, handlowiec, eskorta, przyczółek i lokalna produkcja. Walki nie odkłada się na koniec.

## E00. Cel rozgrywki

Gracz dowodzi flotą, walczy o akweny i osłania transporty. Gospodarka ma szybko dostarczyć pierwsze okręty, a następnie tworzyć wybory między uzupełnieniami, rozpoznaniem, eskortą i ekspansją. Pierwsze minuty obejmują równoczesne zbieranie oraz przygotowanie walki. Rozbudowana osada nie jest warunkiem wypłynięcia.

Cele dla standardowej potyczki 1 na 1: pierwszy celowy kontakt około 1:45–3:00, większe starcie 4:00–7:00, partia 15–25 minut. Wczesny kontakt może być zwiadem lub małym rajdem. Cele wymagają potwierdzenia w grze. Nie wymuszają odliczania ani okresu nietykalności.

## E01. Dwa wydawane zasoby

| Symbol | Zasób | Zastosowanie |
|---|---|---|
| D | Drewno okrętowe | Kadłuby, wiosła, maszty, zabudowania portowe, remonty i fizyczne wyposażenie |
| S | Srebro | Rekrutacja, zakontraktowane zaopatrzenie, zakup uzbrojenia i materiałów, szkolenie, badania oraz ofiary w sanktuariach |

Jednostki zasobów są umownymi pakietami gospodarczymi, bez przelicznika na historyczną masę lub liczbę monet. Dostawy srebra wizualizuj jako zapieczętowane skrzynie. Jedna jednostka D lub S zajmuje jedno umowne miejsce ładowni. To reguła czytelności transportu.

Zboże, oliwa, wino, płótno, smoła i brąz występują w oprawie, opisach wyposażenia oraz ładunkach fabularnych. Ich zakup i przygotowanie mieszczą się w D/S. Nie mają osobnych liczników, pól uprawnych, przetwórstwa ani okresowych dostaw w bazowej potyczce. Zbożowy konwój może być celem misji i korzystać z przedmiotów ładunku, bez odblokowania trzeciej waluty. Nie ma osobnej many.

Żołd i przygotowanie wyprawy opłaca się przy rekrutacji lub budowie szablonu. Nie naliczaj stałego utrzymania istniejącej floty, głodu, zużycia standardowej amunicji ani samoczynnego osłabiania okrętów po utracie przychodu. Uszkodzenia, utrata obsady i morale mają własne reguły walki.

## E02. Pozyskiwanie przy wybrzeżu

Drewno odbiera się przy składach leśnych, srebro przy nabrzeżach obsługujących okręgi górnicze. Są to punkty pracy na mapie, a nie dodatkowe własne porty. Ekipy wyrębu, wydobycia i obróbki są przedstawione przy brzegu i objęte tempem pracy punktu. Gracz nie przenosi drzewa z głębi lądu i nie steruje robotnikami górniczymi. Kuter obsługuje odbiór i dostawę w powtarzanej pętli.

Wskazanie punktu pracy i portu wystarcza do rozpoczęcia zbioru. Punkt ujawnia zapas, liczbę pracujących, oczekiwanie i spodziewany dochód po dostawie. Jedna jednostka pracuje przy jednym zasobie. Nie pobiera zasobów podczas rejsu.

| Parametr | Kuter gospodarczy | Handlowiec |
|---|---:|---:|
| Cena kompletnej jednostki D/S | 60/20 | 140/60 |
| Czas budowy | 18 s | 28 s |
| Ładownia | 60 | 480 |
| Prędkość w neutralnych warunkach | 10 m/s | 7 m/s |
| Praca przy drewnie | 6 D/s | Nie zbiera |
| Praca przy srebrze | 3 S/s | Nie zbiera |

Handlowiec służy do dostaw między magazynami, wypraw budowlanych i transportu wyposażenia. Kuter może wykonać mały transfer, lecz nie zastępuje pojemności handlowca. Jednostki gospodarcze nie inicjują abordażu i nie są tanim uzbrojonym zamiennikiem floty.

Każdy punkt ma dwa stanowiska pracy. Przy składzie drewna wspólny limit wynosi 12 D/s, przy srebrze 4 S/s. Aktywni zbieracze dzielą limit proporcjonalnie do swojego tempa. Pozostali czekają w kolejce, zamiast jednocześnie spowalniać wszystkich pracowników. Stanowisko zajmuje się podczas pracy, nie na cały rejs. Kolejność FIFO z rozstrzyganiem remisu przez stabilny identyfikator. Przy opróżnieniu punktu statek zabiera pozostały częściowy ładunek.

Oba zasoby są skończone. Nie odnawiają się po zniszczeniu budynku, zmianie właściciela, zapisie ani wczytaniu. Nie istnieje dochód za samo posiadanie portu. Nadmierna liczba zbieraczy zwiększa kolejkę; automat proponuje przeniesienie części pracy na inne odkryte wybrzeże.

## E03. Lokalne magazyny i automatyczne konwoje

Każdy portowy przyczółek ma magazyn 6000 D i 3000 S. Budynki przypisane do tej samej osady na jednej wyspie korzystają z niego bez osobnych przewozów lądowych. Między wyspami wymagany jest rzeczywisty transport. Suma w pasku frakcji nie jest zdalnym portfelem.

W UI rozdziel: dostępne, zarezerwowane i w drodze. Ładunek na statku nie jest jednocześnie zasobem magazynu. Automatyka osobno rezerwuje towar u źródła i wolną pojemność celu. Przy pełnym magazynie pozostałość zostaje na statku; system wybiera dozwolony inny cel albo pokazuje jeden alarm grupy.

Port ma trzy stanowiska przeładunku, po 60 jednostek/s. Zwykły cykl zbioru zakłada łącznie orientacyjnie 4 s manewrów przy źródle i porcie; w grze wynikają one z ruchu. Nie doliczaj ich ponownie. Remont oraz produkcja mają odrębne stanowiska. Dostawy wojskowe mają priorytet, ale po 20 s oczekiwania podnieś priorytet starej jednostki.

Gracz ustawia trzy rzeczy: obszar pracy, port priorytetowy i poziom ryzyka tras. Gotowe tryby: stocznia, naprawy, rozbudowa. Domyślny zapas stoczni to cena jednego wybranego szablonu, a nie bezlimitowe gromadzenie. Konwój może obejmować do trzech handlowców. Eskorta pozostaje z transportem i nie ściga przeciwnika poza wyznaczoną strefę.

Tryb ostrożny czeka na osłonę albo wraca; standardowy korzysta z odkrytej drogi; ryzykowny akceptuje oznaczone zagrożenie. Automat korzysta wyłącznie z rozpoznania i pamięci gracza. Nie zna niewidocznych flot. Nowa dostawa nie może obniżyć zapasu źródła poniżej wskazanego minimum bez jawnego rozkazu.

## E04. Start i produkcja floty

Start: port, jedna stocznia, cztery kutry i 360 D/160 S. Trzy kutry pracują przy drewnie, jeden przy srebrze; przydziały są natychmiast edytowalne. Punkty startowe znajdują się 70–110 m żeglownej drogi od portu.

Stocznia ma jedną linię wojenną. Port ma osobną kolejkę kutrów i handlowców. Odbudowa gospodarki nie blokuje już opłaconego okrętu wojennego. Kolejna stocznia daje jedną dodatkową linię za 200 D/100 S i 40 s. Sprawdź wolne podejście; dwa obiekty nie mogą korzystać z tego samego fizycznego stanowiska.

Domyślna rezerwa 60 D/20 S chroni możliwość budowy kutra przed automatycznymi zamówieniami wojskowymi. Jest częścią normalnego magazynu. Zakup zbieracza może z niej korzystać. Ręczny rozkaz może ją wydać po czytelnym ostrzeżeniu. Nie przyznawaj zasobów ani darmowego statku za poniesione straty.

| Kompletny standardowy szablon | D | S | Produkcja | Wymagania |
|---|---:|---:|---:|---|
| Taranowiec | 240 | 100 | 35 s | Stocznia |
| Niszczyciel załóg | 180 | 90 | 30 s | Stocznia |
| Niszczyciel okrętów | 220 | 140 | 38 s | Stocznia |
| Abordażowiec | 200 | 180 | 40 s | Stocznia |
| Bombardowiec | 400 | 260 | 60 s | Stocznia i arsenał |
| Okręt królewski | 460 | 320 | 70 s | Stocznia, arsenał i szkoła morska |
| Ogniomiot | 260 | 180 | 45 s | Stocznia i arsenał; właściwa frakcja |
| Dymowiec | 300 | 180 | 45 s | Stocznia i arsenał; właściwa frakcja |
| Okręt Nieśmiertelnych | 280 | 220 | 45 s | Stocznia i Sanktuarium Męstwa; właściwa frakcja |
| Żmija Cieśnin | 260 | 200 | 40 s | Stocznia i arsenał; właściwa frakcja |

Ceny obejmują kadłub, standardowe uzbrojenie, taran właściwy klasie i pełną zwykłą obsadę. Nie pobieraj drugi raz ceny broni i ludzi. Pierwszy taranowiec może rozpocząć budowę w chwili startu; pozostaje 120 D/60 S. Gracz może przeznaczyć je na dwa kutry, zachować na dalszą flotę albo zmienić plan.

Koszt opłaconego szablonu rozdziel na konstrukcję, istniejące przedmioty i obsadę. Cena członka obsady: marynarz 0,50 S, żołnierz 1 S, specjalista 3 S. Wojenny szablon ma ceil(25% maksymalnej załogi) marynarzy i pozostałe miejsca żołnierzy; gospodarczy ma wyłącznie marynarzy. Po odjęciu ceny obsady z tabelarycznej ceny S, przypisz konstrukcji 80% D i 40% pozostałego S, przedmiotom pozostałe 20% D i 60% S. Przykład taranowca z obsadą 36: obsada 31,50 S; konstrukcja 192 D/27,40 S; przedmioty 48 D/41,10 S. Suma wynosi dokładnie 240 D/100 S.

Indywidualna zmiana wyposażenia płaci różnicę zgodnie z cennikiem komponentów i nie kopiuje wymontowanego przedmiotu. Specjalista zastępuje istniejące miejsce obsady; nie zwiększa jej maksymalnej liczby. Przenoszenie ludzi między statkami jest rzeczywiste. Zwolnienie załogi nie zwraca srebra. Parametry obrażeń i ruchu klas wymagają oddzielnego strojenia walki.

Szkic kolejki pokazuje zapotrzebowanie bez zamrażania przypadkowej części zapasów. Pełne pokrycie ceny umożliwia rezerwację, a zużycie następuje wraz z postępem. Anulowanie przed startem zwalnia 100%; po starcie zwraca 80% niewykorzystanej części. Zmiana kolejności nie pobiera ceny ponownie. Model obliczeniowy kolejki traktuje cały opłacony koszt jako zaangażowany w chwili startu i nie symuluje anulowań.

## E05. Ekspansja i infrastruktura

Przyczółek portowy kosztuje 300 D/100 S, mieści się w ładowni handlowca i powstaje w 35 s po dotarciu. Obejmuje lokalny magazyn, port i kolejkę gospodarczą. Nie obejmuje darmowych kutrów ani wojennej stoczni. Ekipa budowy jest objęta kosztem i nie staje się dodatkową jednostką gospodarczą.

Przyczółek buduje się na odkrytym wolnym brzegu z żeglownym podejściem. Miejsce wysadzenia obsługuje wyłącznie tę wyprawę do ukończenia portu. Po anulowaniu nie staje się bezpłatnym nabrzeżem. Wysłanie dwóch już posiadanych kutrów razem z wyprawą umożliwia rozpoczęcie pracy przy obu zasobach. To relokacja i chwilowy ubytek pracy w macierzystym porcie.

Koszt portu z nowym handlowcem: 440 D/160 S. Ze stocznią: 640 D/260 S. Kompletny nowy punkt z czterema dodatkowymi kutrami i stocznią: 880 D/340 S. Są to różne zakresy inwestycji; nie przedstawiaj ceny samego portu jako ceny kompletnej ekspansji.

| Inwestycja | D/S | Czas | Działanie |
|---|---:|---:|---|
| Arsenał | 200/120 | 45 s | Ciężkie i specjalistyczne uzbrojenie |
| Sanktuarium Żywiołów lub Sanktuarium Męstwa, każde | 180/160 | 40 s | Zaklęcia swojej szkoły opłacane przez lokalny magazyn |
| Szkoła morska | 140/180 | 40 s | Szkolenie oraz zaawansowane szablony |
| Rozbudowa magazynu | 120/40 | 25 s | Jednorazowo +3000 D i +1500 S |
| Żurawie | 120/60 | 25 s | Jednorazowo +25% tempa przeładunku |
| Narzędzia drwali | 120/100 | 40 s | +10% pracy pojedynczego kutra przy D, bez wzrostu limitu punktu |
| Organizacja wydobycia | 100/120 | 40 s | +10% pracy pojedynczego kutra przy S, bez wzrostu limitu punktu |
| Wieża brzegowa | 200/140 | 40 s | Lokalna osłona podejścia, nie kontrola centralnego morza |

Ulepszenia wydobycia są pojedyncze i globalne po ukończeniu. UI pokazuje przewidywany wpływ na dostarczony dochód; nasycony punkt może nie zyskać na nich wcale. Nie umieszczaj ich w obowiązkowej ścieżce pierwszego okrętu. Nie ma rabatów kadłuba, odsetek ani bonusów do dochodu od liczby portów.

Podstawowy port wspiera jedną wieżę, arsenał odblokowuje drugą. Zakres 110 m i linia widzenia muszą pozostawiać drogę obejścia oraz przestrzeń centralną. Wieża nie naprawia floty, nie zbiera zasobów i nie powiększa magazynu. Skuteczność wieży wobec galer i ostrzału oblężniczego należy potwierdzić w walce.

## E06. Naprawy, zdobycze i odcięcie dostaw

Remont pełnej utraconej wytrzymałości kosztuje 25% konstrukcyjnej części ceny kadłuba w D/S. Remont częściowy rozlicza faktycznie odtworzony odsetek HP. Jedno stanowisko stoczni naprawia 12 HP/s, osobno od produkcji oraz rozładunku. Stanowisko może obsługiwać jeden okręt. Brak środków wstrzymuje remont. Ludzie i przedmioty wymagają osobnego uzupełnienia. Nie przywracaj wyposażenia przez samo podniesienie HP.

Złomowanie poza walką trwa 20 s i oddaje 15% rzeczywiście opłaconej konstrukcji × odsetek bieżących HP. Zdemontowane wyposażenie pozostaje przedmiotem i nie dostaje dodatkowego zwrotu. Naprawa wyłącznie dla podniesienia odzysku daje stratę: koszt remontu 25% przewyższa przyrost złomu 15%. Odzysk nie liczy drugi raz wartości ludzi ani broni. Zniżki scenariusza zapisują faktycznie opłaconą cenę, nie wartość cennikową.

Zatopiony transport pozostawia 50% rzeczywistego ładunku jako zbieralny wrak, druga połowa przepada. Zatopienie pustego kadłuba nie wypłaca surowców. Każdy istniejący przedmiot wyposażenia ma 30% szans przetrwania, losowane raz i zapisywane. Przedmioty niezbędne misji podlegają jej jawnym regułom odzyskania. Nie ma gotówki za ostatnie trafienie.

Abordaż przekazuje prawdziwy okręt, obsadę i pozostały ładunek. Nie zwraca jednocześnie transportu dawnemu właścicielowi. Zdobyty okręt zachowuje konstrukcyjny koszt i identyfikatory przedmiotów. Przekazanie między graczami nie odświeża zwrotów.

Blokada wynika z fizycznej obecności floty. Okręt, który dotrze pod ostrzałem, może się rozładować. Port nie rzuca zaklęcia zatrzymującego gospodarkę z promienia. Zniszczenie portu usuwa przeładunek; magazyn osady i opłacone prace w innych budynkach pozostają, jeśli mają działające wyjście. Zniszczenie osady pozostawia do odzyskania 30% jej niewydanych zapasów, reszta przepada. Rozlicz rezerwacje przed wyznaczeniem łupu. Towar na morzu pozostaje na statkach.

## E07. Magia i cele specjalne

Zaklęcia zużywają przeważnie S, a efekty wymagające materiałów także D. Koszt jest lokalny i naliczany raz. Przykładowe koszty do strojenia: Sprzyjający wiatr 0 D/70 S, Zew odwagi 0/80, Płomień Aresa 60/100, Naprawa Hefajstosa 120/120. Koszt nie definiuje siły efektu; zasięg, czas działania, odnowienie i możliwość przerwania wymagają testów taktycznych. Większa liczba sanktuariów nie resetuje wspólnego odnowienia zaklęcia.

Bazowa potyczka nie wymaga rynku ani polowań dla otwarcia floty. Konwoje między własnymi portami nie tworzą zysku. Neutralne kontrakty, ładunki zbożowe i skarby występują jako jawne, skończone cele scenariusza. Ich budżet musi być doliczony do mapy. Nie wprowadzaj odnawialnego źródła srebra za powtarzanie tej samej czynności. Potwór może zmieniać sytuację taktyczną bez wypłaty pieniędzy za zabicie.

## E08. Mapa i tempo

Wzorzec mapy 1 na 1: dla każdego gracza startowy punkt 3000 D/1000 S; cztery dalsze pary punktów po 4000 D/1600 S, rozmieszczone symetrycznie; dwie centralne pary po 6000 D/2000 S. Złoża zawierają łącznie 34000 D/12400 S, a początkowe magazyny obu stron kolejne 720 D/320 S. To budżet jednego wariantu mapy, nie każdej misji.

Naturalna ekspansja ma podejście 250–350 m. Droga między bazami wynosi 650–850 m dla lekkiego okrętu. Ustal odległości według rzeczywistej nawigacji i zanurzenia, nie odcinka przez wyspy. Do co najmniej jednego spornego obszaru prowadzą dwie drogi. Główna flota nie powinna jednym ustawieniem zamykać wszystkich kierunków gospodarki.

Inwestycja w okręt, nowego kutra, stocznię i przyczółek korzysta z tych samych zapasów. Flota ekonomiczna na jednym wybrzeżu ma malejące korzyści. Duży ładunek jest bardziej efektywny w przewozie, ale skupia ryzyko. Srebro ogranicza kosztowne obsady i specjalistyczne jednostki; drewno ogranicza konstrukcje oraz remonty. Nie można zastąpić kontroli obu zasobów jednym uniwersalnym źródłem dochodu.

Zwycięstwo określa cel morski lub centrum dowodzenia. Nie wymaga niszczenia wszystkich odległych składów. Wyczerpanie zasobów nie zabiera istniejącej flocie broni i zdolności działania. Remis i limit czasu muszą mieć jawne zasady, bez automatycznej wygranej najbogatszego magazynu.

## E09. Automatyka i uczciwe reguły

System proponuje uzupełnienie zbieraczy do wskazanej liczby, odbudowę eskorty i dostawy do wybranego szablonu. Nie wydaje wszystkich zapasów na badania bez zgody wynikającej z priorytetu. Grupuje alarmy konwoju. Po skonfigurowaniu dwóch baz cel ergonomii wynosi najwyżej około sześciu nieplanowanych interwencji gospodarczych podczas pięciu minut walki; decyzje dobrowolnej rozbudowy liczone osobno.

SI płaci te same koszty, przewozi prawdziwe cargo, czeka w tych samych kolejkach i korzysta ze swojego rozpoznania. Poziom trudności zmienia planowanie i reakcję. Jawne modyfikatory scenariusza nie mogą udawać zwykłej potyczki. Nie ma ukrytej nagrody za przegrywanie.

## E10. Dane i poprawność wykonania

Parametry maszynowe określa qa/economy-baseline.json. Zasoby zapisuj z dokładnością 0,01, a reszty pracy z większą precyzją. Stanowiska, osady, kontenery, rezerwacje, przedmioty i transakcje mają stabilne ID. Odczyt UI nie może sam zmieniać salda. Ponowione żądanie lub wczytanie nie pobiera ani nie wypłaca kwoty drugi raz.

Przeniesienie odejmuje źródło i dodaje cel w jednej transakcji. Rezerwacja jest częścią rzeczywistego zapasu, nie nową kopią. Zasoby w budowie pozostają zaangażowane w pracy do rozliczenia. W bilansie świata uwzględnij magazyny, złoża, cargo, wraki, materiały w produkcji, zużycie i zatopienie. Zmiana właściciela nie tworzy zasobu. Zapis obejmuje także postęp, pierwotne koszty, obsadę, cargo, limity i kolejki.

## E11. Warunki dopuszczenia gospodarki

Wymagane testy gry: pełny i częściowy rozładunek, konkurujące rezerwacje, pełny magazyn, opróżnienie złoża, kolejki, utrata transportu, przejęcie, anulowanie budowy, zniszczenie portu, powrót z wyprawy, zapis podczas każdego transferu, naprawa i złom. Każdy zachowuje bilans i rzeczywistą własność.

Balans sprawdzaj osobno dla rajdu, eskortowanej ekspansji oraz obrony z kontratakiem. Co najmniej 100 sparowanych układów map z zamianą stron dla każdej pary strategii i frakcji; podaj liczbę meczów i niepewność statystyczną. Próg około 60% wygranych jednej strategii przeciw obu pozostałym jest sygnałem do przeglądu, nie automatycznym dowodem przewagi. Dołącz playtesty ludzi.

Oceń czas kontaktu, wielkość gotowej floty, czas uwagi na morzu, straty zbieraczy, przestoje, odzyskanie dochodu po rajdzie i rzeczywiste koszty uzupełnień. Cel obserwacyjny to co najmniej 70% aktywnego czasu decyzji przy flocie, rozpoznaniu i trasach morskich; metoda obserwacji ma być jawna. Nie podnoś wyniku przez samo liczenie kamery skierowanej na wodę.

Status obliczeń ekonomicznych podaje ECONOMY_BALANCE.md. Odbiór walki, nawigacji, SI, ergonomii i czasu całych partii wymaga działającego prototypu oraz playtestów; pozostaje NOT_RUN do wykonania tych prób.

## E12. Kampania

Kampania korzysta z tych samych zasad D/S, a każda misja ma własny skończony budżet z qa/scenario-design.json. Cele potyczki E00 i wzorzec mapy E08 nie dotyczą misji. Budżety startowe i zapasy mapy przeliczono z poprzedniego modelu proporcjonalnie do cen kompletnych okrętów: D ≈ 0,3 dawnej wartości, S ≈ 0,33, z zaokrągleniem do 10. Wartości są hipotezą do playtestu każdej misji; misja, której cel staje się niewykonalny, ma status BLOCKED.

Misje SEA_ONLY nie mają zasobów ani budowy. Dwa typy zasobów pozostają jedynymi wydawanymi walutami także w kampanii.

Żywność i podobne zapasy w fabule to **ładunek misji**: przedmioty z własnymi ID, które zajmują ładownię jak inny towar, leżą w skończonym punkcie zbioru o tym samym tempie i stanowiskach co skład drewna, ale nie trafiają do magazynu D/S i nie można ich wydać na nic poza celem misji. Dotyczy to M02 (zapasy pod nalotami Harpii) oraz M12 (zapasy na rejs z Trynakii). Scenariusz podaje ich wymaganą i dostępną ilość. Złoto, runo, penaty, osadnicy i ocaleni pozostają osobnymi przedmiotami lub pasażerami. Nie kupuje się ich za srebro.
