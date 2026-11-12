# Morza Bogów — plan gry

**Status:** jedyny nadrzędny plan produktu, specyfikacja do realizacji. **Tryb:** wyłącznie jednoosobowy. **Świat:** mitologiczna Grecja IV w. p.n.e. — 400–301 p.n.e., z 400 r. p.n.e. włącznie. **Obszar główny:** Attyka i pobliskie akweny Morza Egejskiego.

Dokumenty wykonawcze: [katalog mechanik](KATALOG_MECHANIK.md), [parametry robocze](PARAMETRY_ROBOCZE.md), [mapy i scenariusze](MAPY_I_SCENARIUSZE.md), [dowodzenie i odbiór grywalności](GRYWALNOSC_I_ODBIOR.md), [plan integracji](PLAN_WDROZENIA.md), [zasady badań](HISTORIA_I_ZRODLA.md). Stan rozpoznanej implementacji opisuje [ANALIZA_STANU.md](ANALIZA_STANU.md). Plan nie jest potwierdzeniem wykonania opisanych funkcji.

## 0. Jedyny plan nadrzędny i rygor zgodności

Ten dokument jest jedynym nadrzędnym źródłem założeń świata, zakresu i zachowania gry. Bieżące, wyraźne polecenia użytkownika zachowują pierwszeństwo; ich skutki trzeba spójnie odzwierciedlić w planie. Katalog, parametry, mapy, etapy, dokumenty obrazu, profile artystyczne, manifesty assetów, prompty i raporty są podporządkowanymi uszczegółowieniami. Mogą dostarczać zgodnych szczegółów wykonawczych, ale nie ustanawiają alternatywnej epoki, innego zakresu produktu ani własnych reguł sprzecznych z planem. Kod i raport opisują stan implementacji, nie zastępują wymagań docelowych.

**Obowiązuje rygor sprawdzania każdego działania.** Przed każdą czynnością zmieniającą kod, dane, dokumentację, konfigurację, assety, animację lub obraz oraz przed wysłaniem zadania do generatora wykonawca:

1. wskazuje właściwy punkt tego planu, etap P, ID katalogu i kontrakt Q/J, o ile dotyczą działania;
2. potwierdza zgodność ze światem mitologicznej Grecji IV w. p.n.e., z uwzględnieniem 400 r. p.n.e.;
3. rozdziela fakt materialny, rekonstrukcję, fizyczną kalibrację, tradycję mitologiczną i abstrakcję gry, a wymagane źródła rzeczywiście odczytuje;
4. sprawdza upoważnienie, zakres plików, ich właścicieli i chronione niezmienniki;
5. określa oczekiwany wynik, kryteria sprawdzenia oraz artefakty odbioru;
6. po działaniu weryfikuje jego faktyczny rezultat i diff, a przed integracją i odbiorem ponawia kontrolę zgodności całego przyrostu.

Działanie bez zgodnego uzasadnienia nie jest wykonywane. Rozbieżność oznacza `BLOCKED_PLAN_CONFLICT` dla zależnego zakresu: wskazać oba zapisy, dowód i najmniejszą propozycję rozstrzygnięcia. Nie wybierać wygodniejszej wersji, nie mieszać profili i nie zmieniać samodzielnie planu, aby zalegalizować wynik. Niezależne, zgodne zadania mogą być kontynuowane. Zwykłe wybory implementacyjne mieszczące się w zatwierdzonym kontrakcie nie wymagają ponownego pytania użytkownika.

Praktyczny zapis kontroli określa [KARTA_ZGODNOSCI_Z_PLANEM.md](KARTA_ZGODNOSCI_Z_PLANEM.md). Żaden autor nie uznaje własnego zapewnienia, samego planu, generowanego pliku ani raportu innego snapshotu za dowód odbioru. Wymagana jakość nie może być cicho obniżana; brak narzędzia, źródła lub dowodu pozostaje jawnym ograniczeniem. Fikcja mitologiczna nie jest przedstawiana jako naukowo potwierdzona fizyka.

## 1. Obietnica gry i zakres

Gracz dowodzi flotą, rozbudowuje porty, uzbraja poszczególne okręty, pozyskuje zasoby i zdobywa panowanie nad akwenami, na których działają ludzie, bogowie oraz mityczne stworzenia. Okręt jest ruchomym zestawem kadłuba, napędu, obsady, broni, osłony, taranu, doktryny i ewentualnej postaci lub artefaktu. Ten sam kadłub po zmianie wyposażenia może pełnić inną funkcję.

Podstawą walki jest współdziałanie ostrzału, taranu i abordażu. Ekonomia daje cele do osłony i przechwycenia. Umocnienia kontrolują wybrzeża, ale nie zastępują floty. Automatyczne zadania ograniczają powtarzalne klikanie, pozostawiając graczowi wybór składu, pozycji, celu i chwili działania. Moce i artefakty wprowadzają dosłowne, nadprzyrodzone skutki.

Gra zawiera kampanię i lokalne potyczki przeciw AI. **Nie projektujemy gry sieciowej**, serwerów, synchronizacji klientów, sieciowego rankingu ani komunikacji między graczami. Powtarzalność symulacji pozostaje potrzebna dla testów, zapisu i analizy przebiegu.

**Estetyka nie jest objęta zmianą kierunku.** Zachowujemy istniejące modele, materiały, światło, wodę, animacje, układ stylistyczny interfejsu i profile jakości. Nowa mechanika może wymagać prezentacji jej stanu za pomocą obecnego języka wizualnego. Brakujące przedstawienie nowego obiektu jest osobnym zadaniem integracyjnym, a nie zgodą na przeprojektowanie oprawy.

**Wyjątek z polecenia użytkownika z 12 listopada 2026 r. („Grzywy i porywy”).** Wygląd wody może się zmienić wyłącznie w zakresie:
- białych grzyw wyznaczanych z pola piany symulacji i lokalnego wiatru;
- obrazu porywów: plam zmarszczek i spokojniejszej zawietrznej;
- narzędzi powtarzalnych ujęć i pomiaru fizycznego potrzebnych do ich odbioru (K0–K3).

To wyłącznie prezentacja stanu Marine. Nie zmienia reguł gry, wyniku symulacji (Q09), formatu zapisu ani składowych fal odczuwanych przez okręty (WM02). Każdy przyrost przechodzi bramkę artystyczną i fizyczną. Nowy baseline ochrony wody wymaga niezależnego przeglądu rzeczywistych ujęć. Pozostała oprawa wody — kolor, rozpraszanie, połysk, światło, profile jakości i gęstość widma fal — pozostaje bez zmian.

## 2. Świat materialny i mitologia

Obowiązująca rama świata i kampanii to **mitologiczna Grecja IV w. p.n.e.**, czyli 400–301 p.n.e., z **400 r. p.n.e. włącznie**. Nie narzucamy całej kampanii jednego węższego przedziału lat. Opisane wyprawy są fikcyjnymi scenariuszami, nie deklaracją udokumentowanych wydarzeń.

Każda misja ma kartę zgodności z epoką i manifest wyposażenia. Jeśli datowanie wpływa na dostępność technologii, fazę zabudowy lub inne istotne twierdzenie, karta określa właściwy rok albo uzasadniony zakres w IV w. p.n.e. Brak dokładnej daty nie daje prawa zakładać, że każde rozwiązanie było dostępne przez całe stulecie. Starszy obiekt może wystąpić, jeżeli uzasadniono jego istnienie lub użycie w świecie IV w.; czas jego wykonania nie jest automatycznie czasem akcji. Zjawiska i postacie mitologiczne zachowują jawny status fikcji.

Świat ma trzy odrębnie opisane warstwy:

1. **Materialna:** geografia, żegluga, kopalnie, drewno, praca portu, rzemiosło, organizacja ludzi, architektura i technika poświadczona w epoce.
2. **Mityczna:** bogowie, stworzenia, manifestacje herosów, czary, niewidzialność, niewrażliwość, zaklęte przedmioty i nadprzyrodzona przemiana akwenu. Działają naprawdę w fikcji gry.
3. **Reguły rozgrywki:** punkty kadłuba, umowne jednostki D/S, limity, czasy, zasięgi i kompresja skali. Nie są pomiarami historycznymi.

Nie ma osobnego obowiązkowego przełączania całej gry między historią i fantastyką. Każda misja ma jawny manifest dozwolonych obiektów i zjawisk. Misja portowa może obywać się bez objawienia bóstw, a inna korzystać z pełnego repertuaru mocy.

Artyleria pokładowa i większe okręty wymagają precyzyjnego datowania. Badania floty ateńskiej wskazują na różnice katalogu między kolejnymi dekadami IV w.; poświadczenie urządzenia pod Tyrem nie jest dowodem jego powszechności w Attyce. Dane dla typów i funkcji sprawdzamy w [badaniach floty Pireusu](https://tidsskrift.dk/pdia/article/download/24130/21168/55831) oraz źródłach przypisanych w rejestrze. Konfiguracje niepoświadczone mogą istnieć jako jawny element mitologicznej fikcji. Nie dopisujemy im fałszywego uzasadnienia naukowego.

Opowieść o herosie nie oznacza, że człowiek żyjący w epoce heroicznej był historycznie obecny w IV w. Postacie nadprzyrodzone pojawiają się jako manifestacje, patroni lub figury fikcyjnej opowieści; współcześni dowódcy i mieszkańcy mają własne tożsamości.

## 3. Pętla gry i podział uwagi

Rozpoznanie → przydział zasobów → uzbrojenie floty → osłona lub atak → wykorzystanie mocy i pozycji → zdobycz, naprawa lub odwrót → wykonanie celu. Wszystkie działania przebiegają równolegle w czasie rzeczywistym. Kampania dodaje rekrutację postaci, zdobywanie znaków, wybory celu i przenoszenie uprawnionego stanu.

Roboczy podział uwagi: walka i taktyka **40%**, ekonomia **25%**, porty i badania **15%**, eksploracja/RPG/mitologia **15%**, dowodzenie i informacja **5%**. Suma wynosi 100%. Działające równocześnie systemy nie oznaczają podwójnego liczenia aktywności gracza. W pomiarze przypisujemy każdej czynności jeden dominujący cel.

Potyczka standardowa ma robocze okno **30–50 minut**. Scenariusze używają własnych terminów wynikających z celów. Nie rozciągamy partii wyłącznie przez podnoszenie wytrzymałości portu. Liczba jednoczesnych jednostek, całkowita produkcja i limit floty są osobnymi parametrami. Bieżący limit 100 jednostek na stronę nie jest obietnicą, że gracz musi ręcznie dowodzić setką okrętów.

### Dowodzenie i nauka reguł

Grupa zachowuje cel i docelowy szyk, a w legalnej dla jej kadłubów cieśninie może przejść kolejno i zebrać się po drugiej stronie. Brak drogi, fizyczna blokada i brak napędu są odrębnymi stanami. Ograniczone ponawianie nie może tworzyć niekończących się oscylacji. Nie ma ukrytej teleportacji ani wyłączania kolizji.

Komendy mają wynik i powód dla każdej jednostki. Pauza taktyczna zatrzymuje czas symulacji i pozwala dopisywać kolejne zadania. Zwykły rozkaz zastępuje zadanie; jawne dopisanie zachowuje kolejkę; Stop ją anuluje. Zapis w pauzie obejmuje komendy oczekujące oraz kolejki bez wykonania ticku. Przyjęcie przyszłego rozkazu nie zużywa przedwcześnie zasobów.

K01–K03 uczą kolejno dostawy/eskorty, trwałego zbioru i wyposażenia/portu. Podpowiedzi są kontekstowe i pomijalne, a wyjaśnienia reguł dostępne na żądanie. Istniejące karty, herold i oznaczenia pokazują rolę, obsadę, zadanie i przyczynę oczekiwania, bez przebudowy oprawy. Szczegółowe warunki J01–J10 ustanawia dokument odbioru grywalności.

## 4. Ekonomia oparta na istniejącym rdzeniu

Zachowujemy kodowe zasoby **Wood/D** i **Silver/S**, lokalne magazyny, fizyczny ładunek, rezerwacje, budowę zużywającą opłacony koszt, wraki oraz konwoje. Nie zmieniamy ich nazw technicznych tylko w celu realizacji fabuły.

Punkty pracy przechowują partię produktu. Po wyczerpaniu mogą otrzymać skończoną liczbę dalszych partii z zaplecza. Dotyczy to dostaw drewna, przydziałów budżetowych i produktu kopalń; nie oznacza odrastania rudy ani dojrzałego lasu w czasie bitwy. Dane mapy określają pojemność, liczbę partii, opóźnienie i źródło. Po wyczerpaniu całego limitu punkt przestaje dostarczać zasób.

Rybołówstwo oraz towary specjalne są zadaniami zbioru i handlu, a nie dodatkowymi walutami. Katalog definiuje ich ładunek, cenę skupu, port rozliczenia i wynik D/S. Nie wolno zamienić połowu na środki przed rzeczywistą dostawą. Rzadki neutralny stwór dostarcza jednorazową, z góry określoną nagrodę; jego ponowne przywołanie nie odtwarza puli nagrody.

Handlowiec i łódź mogą wykonywać dozwolone zadania gospodarcze. Handlowiec wymaga awansu osady do ośrodka miejskiego, a blokada jego dostępności musi być respektowana przez UI, AI, szablony i skrypty. Okręty wojenne nie dostają masowej ładowni gospodarczej; wyposażenie, artefakty i ludzie są osobnymi kategoriami przewozu.

Naprawa kadłuba i modułów odbywa się w doku za S. Korzysta z opłaconej podstawy i stopnia uszkodzenia; nie pobiera osobno towaru żywnościowego. Zachowujemy działającą implementację ceny do czasu kontrolowanego strojenia. Zwykła naprawa, boska odnowa i odzysk konstrukcji muszą mieć różne pochodzenie księgowe, aby darmowa odnowa nie zwiększała wypłaty przy sprzedaży lub zatopieniu.

### 4.1. Kontrakt księgi

Dla każdego zasobu r:

`Q_r + Spent_r + Lost_r − Returned_r − External_r = Q_r(start)`.

Q obejmuje punkty, porty, przewożony ładunek i wraki. External obejmuje zdefiniowane przydziały, partie i nagrody. Rezerwacja jest częścią zasobu: `Available = Stock − Reserved`, a nie dodatkowym zapasem. Wartość zamontowanego modułu odzyskiwanego w naturze nie może zostać równocześnie wypłacona jako pieniężna wartość tego samego przedmiotu.

Transfery i przejęcia zmieniają miejsce lub właściciela, nie tworzą globalnie towaru. Wyładunek do zniszczonego portu jest odrzucony albo kierowany do legalnego zastępstwa. Pełny magazyn nie usuwa ładunku. Istniejącą odmowę złomowania statku z D/S zachowujemy; ewentualne automatyczne rozładowanie jest osobną regułą.

Zadanie ekonomiczne pozostaje aktywne podczas oczekiwania na miejsce, nową partię lub drogę. Wznawia się po zmianie warunków, przechodzi do kolejnego dozwolonego punktu i zachowuje ustaloną politykę portów oraz ryzyka. Nie wymaga powtarzania identycznego rozkazu po każdym zapełnieniu magazynu. Zmiana zamiaru zwalnia właściwe rezerwacje; nieprzerwane oczekiwanie nie powtarza alarmu co tick.

Wydajność rejsu obliczamy przez pełną drogę: `T = czas_pracy + droga_tam/v_tam + droga_powrotna/v_powrót + przeładunek + kolejki + manewry`, a następnie `I = 60×ładunek/T`. Ten sam etap pracy nie może zostać dodany ponownie jako załadunek. Przepustowość wielu stoczni jest sumą przepustowości linii, ograniczoną dostępnymi lokalnie zasobami i ludźmi.

## 5. Flota, wyposażenie i ludzie

Dwie grywalne strony to fikcyjne **Zgromadzenie Portów Attyki** i **Przymierze Wysp Egejskich**. Nie utożsamiamy ich automatycznie z jednym rzeczywistym związkiem politycznym. Każda korzysta ze wspólnej gospodarki i sześciu podstawowych ról wojennych oraz dwóch specjalizacji. Trzecia siła to niezależne flotylle i ośrodki rozbójnicze.

Katalog zawiera 12 unikalnych ról: 2 pomocnicze, 6 wspólnych bojowych i 4 specjalizacje stron. Jednocześnie wybrana strona ma dostęp do 10. Niezależne flotylle mają pięć ograniczonych konfiguracji, budowanych na tej samej infrastrukturze definicji.

Zachowujemy identyfikatory siedmiu istniejących klas. Okręt dowodzenia dodajemy na końcu enumu albo jako jawny typ w kompatybilnym katalogu. Specjalizacje w pierwszej kolejności realizujemy przez `VariantId` i uprawnienia istniejącego typu, nie przez kopiowanie kompletnej logiki ruchu, produkcji i walki. Jeśli wariant potrzebuje odmiennej pojemności, slotów lub wbudowanego modułu, rozszerzamy definicję, a nie obchodzimy walidację.

Broń jest fizycznym wyposażeniem. Warsztat wytwarza sześć rodzin machin o różnych sile, cyklu, zasięgu i minimalnym zasięgu, z zachowaniem siedmiu istniejących typów runtime według mapowania C03. Dwie specjalne bronie są związane z dedykowanymi okrętami i niewyjmowalne. Przełącznik domyślnego uzbrojenia i plan wyposażenia pozostają. Wyposażenie na zdobyczy przechodzi wraz ze stanem.

Żeglarze obsługują napęd, a wojownicy mają trzy profesje: miecz, oszczep i łuk. Doświadczenie wpływa na celność i walkę na pokładzie. Nie zwiększamy liczby ludzi ani nie przeliczamy cen tylko przez zmianę MaxCrew. Każdy preset musi spełniać nieujemność ceny konstrukcji, obsady i wyposażenia, a ich suma ma odpowiadać zapłaconej cenie.

Reprezentacja człowieka i osiągów jest abstrakcją gry. Jeżeli katalog ma otrzymać pełną historyczną liczebność załogi triery, potrzebuje odrębnej wyceny oraz rozdzielenia napędu od pokładu. Nie wymaga to zmiany liczby widocznych postaci w zadaniu mechanik.

## 6. Walka i decyzje taktyczne

### Ostrzał

Zachowujemy pociski z lotem, jednorazowo wylosowanym rozrzutem i oddziaływaniem na kadłub, ludzi oraz moduły. Dystans podejścia wynika z **obsadzonych, sprawnych** stanowisk i preferencji gracza; nie jest zawsze równy 80 m. Ciężki miotacz ma martwą strefę blisko celu. Okręt znajdujący się w niej może uniknąć ostrzału tego modułu, ale pozostaje podatny na inne stanowiska, taran i abordaż.

Dla fortów moduły i obsada są odrębne od wytrzymałości konstrukcji. Utrata ognia może pozwolić flocie przepłynąć bez dobicia budynku. Wrogie bazy wytwarzające kolejne jednostki mogą jednak wymagać zniszczenia całej struktury.

### Taran

Uderzenie wymaga odpowiedniej prędkości zbliżania i faktycznego kontaktu. Wektor względny to `v_rel = v_a − v_b`; składowa zbliżania `max(0, dot(v_rel, n))`. Obrażenia mogą zależeć od kwadratu tej prędkości, kąta, umownej masy i poziomu taranu, z jawnym limitem. Sam wzór energii kinetycznej nie dowodzi historycznej wartości punktów obrażeń. Po uderzeniu trzeba odzyskać dystans i zdolność następnej szarży.

### Abordaż

Sczepienie wymaga małej normy `|v_rel|` i zgodnego położenia burt. Dwa statki po 5,2 m/s na przeciwnych kursach mają względną szybkość 10,4 m/s, a nie zero. Podejście oparte na odległościach środków trzeba uzgodnić z kapsułowym kontaktem istniejącej nawigacji.

Przegrany szturm usuwa skierowaną do niego obsadę bojową, pozostawiając napęd, dopóki jest sprawny. Zwycięzca przejmuje dozwolony okręt wraz z wyposażeniem, cargo specjalnym i przedmiotami; przydziela załogę pryzową. Dla określonych jednostek niezależnych flotylli przegrana obrońcy oznacza samozatopienie. Ta cecha jest widoczna przed atakiem.

Forty nie są legalnym celem taranu ani abordażu. Środkiem ich zwalczania jest ostrzał, moc, zniszczenie obsługi lub jawnie zaprojektowany cel operacyjny. Okręt dowodzenia z niewrażliwością nie staje się przez to zdolny do taranowania skały lub przejęcia budowli.

## 7. Bazy, badania i zaawansowana automatyka

Graf rozwoju: osada → przystań + stocznia + warsztat + kuźnia → ośrodek miejski → dok, ośrodek wiedzy i sanktuaria. Ciężkie moduły oraz specjalizacje wymagają określonych badań. Dopóki kuźnia nie jest zaimplementowana, nie włączamy globalnie wymogu jej ukończenia w istniejącej potyczce; aktywujemy cały spójny podgraf po odbiorze. Nie tworzymy martwego otwarcia bez możliwości zdobycia transportu lub odpowiedzi na atak.

Warsztat usprawnia siłę, tempo i zasięg trzech rodzin uzbrojenia: obsady lekkiej, machin średnich i ciężkich. Kuźnia rozwija trzy stopnie osłon i taranów. Ośrodek wiedzy odblokowuje zaawansowane konfiguracje i napęd. Koszty i czasy są w katalogu, a ulepszenie nie nalicza się ponownie przy wczytaniu.

Forty mają trzy rozmiary, koszt punktowy 1/2/3 i limit lokalnej puli osady 15. Budowa wyprawowa używa rzeczywiście dostarczonych zasobów i nie korzysta z tej puli. Pochodzenie fortu określa prawo sprzedaży. Domyślnie uzbrojony fort opłaca zarówno konstrukcję, jak wyposażenie i obsadę.

Doktryna jednostki łączy cztery flagi reakcji i jedno zadanie przestrzenne: eskortę, zwiad z powrotem, patrol, obronę strefy albo atak w strefie. Zadanie przechowuje trasę, granice pościgu i miejsce powrotu. Ręczny rozkaz ma pierwszeństwo. Zdobycie jednostki nie dziedziczy bez kontroli wrogiej doktryny.

Nie uruchamiamy osobnego, konkurencyjnego planera dla każdej nowej mocy. Najpierw wspólny wybór legalnej czynności, następnie wybór celu, potem wykonanie przez istniejącą ścieżkę komend. SI ma te same koszty i ograniczenia co gracz.

## 8. Moce i ich wspólny kontrakt

Dwanaście mocy obejmuje: wir, dwie postaci rafy w jednym zaklęciu, wiatr/ciszę, trąbę wodną, rozpoznanie, przywołanie stworzenia, wzmocnienie uzbrojenia, niszczący grom, ognistą strefę, niewidzialność, osłonę zwykłej broni i osłonę magii. Szczegółowa lista M01–M12 i macierz ochrony są w katalogu.

Każde użycie ma etapy: sprawdzenie nauki i sanktuarium → wybór legalnego celu → rezerwacja/zapłata → zapowiedź → aktywacja → oddziaływanie → wygaśnięcie. Osada, sanktuarium i rzucający są identyfikatorami stanu, nie odwołaniami do obiektów sceny Unity.

Grom może natychmiast rozstrzygać niechronione cele, ale jego przygotowanie, obszar i warunki ochrony są czytelne przed uderzeniem. Niewidzialność usuwa obiekt ze zwykłej obserwacji i wyboru celu AI; nie usuwa kadłuba z kolizji ani skutków obszarowych. Ujawnienie i wykrywanie są osobnymi źródłami obserwacji.

### Kolejność ochron

1. Sprawdź, czy cel i kanał są legalne dla zdarzenia.
2. Sprawdź pełną niewrażliwość bohatera albo pełną odporność na dany kanał.
3. Sprawdź neutralizację określonego efektu, np. gorgony przez lustro.
4. Zastosuj najsilniejszą redukcję w danej rodzinie ochrony, bez sumowania identycznych tarcz.
5. Zastosuj osłonę kadłuba do kanału, którego dotyczy.
6. Rozlicz efekt, utratę i zdarzenie dokładnie raz na ID trafienia.

Efekty nawigacyjne są odrębne od obrażeń. Niewrażliwy okręt może zostać zatrzymany lub przyciągnięty, jeżeli jego karta tego nie blokuje. Pilot chroni przed wirami i rafami, a powiernik Boreasza przed trąbą. Mapa nie może wymagać zabicia niewrażliwej jednostki bez wskazania wykonalnej kontrgry albo alternatywnego celu.

### Morze i teren

Wiatr istnieje już jako stan symulacji. Naturalny wpływ na ruch włączamy dopiero po odebraniu kontraktu dla żagla, wioseł, dryfu i AI. Moc wiatru nie jest wyłącznie animacją: jej deterministyczna warstwa modyfikuje pole używane przez ruch przez określony czas. W pierwszym wycinku dopuszczalny jest jawnie uproszczony model premii/ograniczenia kursu, zanim zostanie włączony pełen model sił.

Wir, trąba, rafa i zniszczenie terenu mają stan logiczny, widoczny obszar oraz wersjonowaną maskę nawigacji. Zmiana terenu nie przebudowuje całej sceny ani nie zmienia reguł zależnie od jakości obrazu. Powstanie przeszkody pod kadłubem wymaga jawnego rozwiązania kontaktu: zadziałanie zagrożenia albo przesunięcie do legalnego punktu według ustalonego algorytmu, nigdy losowej teleportacji do portu.

## 9. Bohaterowie, przedmioty i RPG

Dziewięć specjalizacji bohaterów ma unikalne ID, warunek przyłączenia i przypisany znak rekrutacyjny. Ich skutki obejmują ochronę nawigacyjną, poprawę ognia, ochronę przed stworzeniami, ruch mimo wiatru, niewrażliwość, odnowę kadłuba, celność/cykl, odporność na śpiew i harpie oraz ochronę przed gorgoną.

Zwykły okręt ma jedno miejsce artefaktu, okręt dowodzenia dwa, fort jedno, a miasto magazyn bez regułowego limitu miejsc. Bohater zajmuje odrębne miejsce i nie może być kopiowany przez przeładunek, zatopienie, sprzedaż ani ponowne przywołanie. Jego własny przedmiot rekrutacyjny nie musi stale zajmować miejsca artefaktu po spełnieniu warunku.

Jedenaście klas przedmiotów obejmuje odnowę, towary, uzbrojenie, możliwość użycia mocy, ochronę ognia, osłonę, ruch/taran, blokadę przejęcia, ochronę ognia i gorgony, odnowę ludzi i dokument odblokowujący technologię. Osobna lista dziewięciu znaków służy rekrutacji.

Boska odnowa może dosłownie przywrócić ludzi i moduły w fikcji. Każda taka operacja jest zarejestrowana jako odnowa nadprzyrodzona i nie wytwarza srebra, drewna ani nowej podstawy złomu. Nie zwiększa liczby ludzi ponad limit odtwarzanego preset. Nie przyznaje doświadczenia za odtwarzanie własnych strat.

Kampania zachowuje odblokowania, rekrutację, stan flagowca i określony manifest floty. Każda misja określa `CarryPolicy`; domyślnie przenosi się uprawniony flagowiec, bohaterowie i przedmioty z jego dwóch miejsc oraz ewakuowanego magazynu osady. Dodatkowe kadłuby i załogi przechodzą wyłącznie wtedy, gdy wymienia je manifest wyjścia. Nie kopiujemy automatycznie całej zawartości mapy.

Stan trwały, portowa rezerwa i manifest wyprawy są rozdzielone. Niewysłane jednostki oraz postacie pozostają w rezerwie. Powtórka przegranej operacji przywraca jej snapshot wejścia i zachowuje wcześniej zatwierdzone ukończenia. Przyjęcie dopuszczalnej porażki zatwierdza również straty; nie pozwala zatrzymać nagrody i jednocześnie cofnąć jej kosztu. Wyjściowy manifest pokazuje każdą utratę i jej powód. Odblokowana wiedza nie tworzy darmowego wyposażenia i nie pomija lokalnego grafu budynków.

Weterani i relacje są elementami RPG, ale dodatkowe rozbudowane drzewka talentów, wyczerpanie i symulacja potrzeb mieszkańców nie poprzedzają odbioru pełnego katalogu funkcji. Doświadczenie ma na początku ograniczony, jasno zdefiniowany wpływ. Nie trzeba tworzyć nowego systemu zmęczenia, aby móc odebrać podstawową kampanię.

## 10. Przeciwnicy i scenariusze

Trzy siły mają jawne relacje: człowiek i jego stronnictwo, drugie stronnictwo prowadzone przez AI oraz niezależne flotylle. Stworzenia i ośrodki mocy mogą zagrażać wszystkim. Relacja wrogości nie jest równa operacji `1−Owner` dla każdego obiektu. Stosujemy trzy stałe sloty właściciela: 0 człowiek, 1 główne AI, 2 niezależni. Tylko 0 i 1 są głównymi uczestnikami wyniku. Slot 2 korzysta z wykonawcy spotkań i polityki reakcji, bez uruchamiania dla niego głównego Opponent. Nie tworzymy modelu dowolnej liczby graczy.

Profile Nauka, Standard i Weteran zmieniają reakcję, koordynację, planowanie i ryzyko AI, zachowując te same ceny, statystyki oraz ograniczenia wiedzy co człowiek. Jawnie asymetryczna misja może przydzielić przeciwnikowi większą początkową flotę lub skończony manifest posiłków. Nie zmienia ich potajemnie w odpowiedzi na ocenę wyniku gracza. Każda fala ma źródło, limit, warunek i zapowiedź, a po zapisie nie pojawia się ponownie.

Przeciwnicy obejmują 11 archetypów: łuczniczą strażniczkę, cyklopa, bycze widmo, syrenę, gorgonę, harpię, drakona i cztery zagrożenia wodne. Dwa dalsze ośrodki tworzą wiatr/trąby oraz ogniste ataki. Zniszczenie gniazda albo siedziby wyłącza odtwarzanie danej linii. Odtwarzające się stworzenia nie dają w nieskończoność nagród gospodarczych.

Scenariusze są własnymi opowieściami rozgrywającymi się w poświadczonych miejscach albo jawnie mitycznych sektorach. Nie zmieniamy tekstu mitu w relację z realnej bitwy. Projekt miejsc i celów znajduje się w [MAPY_I_SCENARIUSZE.md](MAPY_I_SCENARIUSZE.md).

## 11. Mapy jako akweny operacyjne

Mapy składają się przede wszystkim z wody, z ograniczoną liczbą wysp, brzegów, płycizn i portów. Służą rozdzielaniu dróg żeglugi, ukrywaniu informacji, ustawianiu obrony i tworzeniu celów, a nie prowadzeniu dużej wojny lądowej. Budowle stoją na logicznie przygotowanych brzegach i miejscach budowy.

Wymiary początkowych sektorów nie przekraczają obecnej domeny **2600×2600 m**. Ta granica wynika z istniejących siatek, nie z powierzchni całej Attyki. Wykorzystujemy gotową generację wysp oraz maski i konfigurujemy ich funkcję, zachowując oprawę. Dokładne odtworzenie większego obszaru wymaga kilku sektorów lub wyraźnego oznaczenia kompresji geograficznej.

Każda mapa ma co najmniej dwie sensowne drogi do głównego celu, zatoki osłonowe, ryzyko ekspansji oraz przestrzeń dla odskoku po taranie. Płycizna jest narzędziem różnicowania zanurzenia, nie losową ścianą. Zasięg twierdzy i zaklęć nie może pokrywać każdej drogi bez dostępnej odpowiedzi gracza.

## 12. Integracja techniczna bez rozbijania działającego projektu

W punkcie startowym istnieją: 7 klas, moduły uzbrojenia, załogi i ranni, taran, abordaż, pociski, porty, projekty, lokalny transport, konwoje, morze, mgła wojny i zapis. Dwa nowe moduły przekrojowe to **katalog efektów** oraz **wykonawca scenariusza**. Korzystają z obecnego World i wspólnej ścieżki komend. Nie tworzymy drugiej symulacji ani osobnej gospodarki dla mitologii.

Nowe definicje wprowadzamy jako dane i małe moduły: `ShipVariantDefinition`, `TechnologyDefinition`, `FortDefinition`, `AbilityDefinition`, `HeroDefinition`, `ArtifactDefinition`, `EncounterDefinition`, `ScenarioDefinition`. Stany i efekty są serializowane. Rozszerzenia komend i zapisanych enumów dopisujemy na końcu; nie przestawiamy istniejących wartości.

Definicja efektu określa źródło, cel, kanał, zasięg, czas, limity, identyfikator zdarzenia, regułę nakładania i priorytet. Taki sam mechanizm obsługuje osłonę, celność, blokadę, niewidzialność, odnowę i zakłócenie. Nie wolno realizować jednej mocy przez bezpośredni skrypt animacji, a drugiej przez symulację.

Zapis zawiera identyfikator katalogu, rozszerzony stan świata, artefakty, bohaterów, aktywne efekty, rezerwacje, polityki źródeł, maski terenu, zadania i przeniesienie kampanii. Każda niezgodna zmiana binarnego kontraktu podnosi `SaveGame.Version`. Aktualna wartość w punkcie bazowym wynosi **18**. Zapis wykonujemy na granicy ticku po wykonaniu przyjętych do niego komend albo jawnie serializujemy oczekującą kolejkę.

Zapis wykonywany podczas pauzy serializuje Pending i zaplanowane zadania; nie oczekuje na przyszłe wznowienie ani nie uruchamia dodatkowego ticku. Sloty ręczne, szybkie i automatyczne są rozdzielone. Jedna generacja obejmuje także powiązane haszem dane wyglądu i zniszczeń. Błąd zapisu zachowuje wcześniejszy dobry zestaw i daje komunikat. Ustawienia dźwięku nie zmieniają wyniku ani czasu symulacji.

## 13. Warunki odbioru

Sposób wykonania i dowody reguluje [doktryna jakości produkcji](RYGOR_PRODUKCJI_I_ODBIORU.md). Bramka artystyczna i fizyczna są niezależne; żadna nie zastępuje Q01–Q12 i J01–J10. Morze podlega [WM01–WM03](KONTRAKTY_MORZA_I_WIATRU.md).

| ID | Kontrakt | Dowód |
|---|---|---|
| Q01 | Każda pozycja katalogu ma implementację i zakres dostępności | Macierz ID → kod → test → artefakt |
| Q02 | D/S i unikalne przedmioty nie powielają się | Księga po każdej transakcji i test zapisu |
| Q03 | Ceny i obsady są nieujemne i zgodne z pojemnością | Enumeracja całego katalogu |
| Q04 | Ruch, rozkazy, dystans ostrzału, taran i abordaż działają z realną geometrią symulacji | Kursy, strefy ostrzału, mieszana grupa w cieśninie, zapis kolejki w pauzie |
| Q05 | Każda ochrona i moc realizuje swoją macierz, a nie przypadkowy porządek skryptów | Testy krzyżowe kanałów i priorytetów |
| Q06 | AI korzysta tylko z legalnej wiedzy i tych samych kosztów; trudność i posiłki są jawne | Identyczne historie obserwacji/RNG, transakcje dla obu stron i skończone manifesty |
| Q07 | Zmiana właściciela i nagrody są jednokrotne na ID zdarzenia | Przejęcie, odbicie, zrzut, ponowny odczyt |
| Q08 | Cele są wykonalne, rozstrzygają wynik bez zbędnego pościgu i reagują na utratę krytycznego obiektu | Pełne przejścia, odległy niepowiązany wróg oraz warianty porażki |
| Q09 | Jakość obrazu nie zmienia wyników | Identyczne komendy i porównanie stanu |
| Q10 | Źródła i fikcja są prawidłowo oznaczone | Karty historyczne, mitologiczne i projektowe |
| Q11 | Pełny katalog nie blokuje otwarcia, a automatyka zachowuje intencję i wznawia zadania | Potyczka, K01–K03, pełny magazyn → miejsce i pusty punkt → partia bez ponawiania rozkazu |
| Q12 | Dokumenty startowe prowadzą do jednego kontraktu | Kontrola odnośników, reguł i statusów |

Najpierw odtwarzamy i naprawiamy konkretne problemy rdzenia; potem włączamy cały, wewnętrznie spójny podzbiór nowych reguł. Po przejściu testów nie rozszerzamy ich bez potrzeby, ale brakujące kategorie mechanik nie mogą zniknąć z zakresu wydania. Próby z AI służą diagnozie. Odbiór balansu i zrozumiałości wymaga również przejść z udziałem człowieka.

## 14. Obowiązek badań przy każdej sesji Claude Code

Po przeczytaniu tego planu Claude Code samodzielnie otwiera wiarygodne źródła dotyczące miejsc, daty, techniki i mitów, na których opiera aktualne zadanie. Sprawdza także bazowe ramy świata. Raport zawiera autora, wydanie/instytucję, adres, lokalizator, faktycznie odczytany fragment, ograniczenie i decyzję projektową.

Materiał mitologiczny potwierdza obecność motywu w danej tradycji, nie fizyczną prawdziwość cudu. Świadectwo archeologiczne nie uzasadnia dowolnej mechaniki zaklęcia. Brak dostępu albo sporne datowanie są jawne. Nie wolno wymyślać bibliografii, stron, parametrów ani zapewniać o przeczytaniu niedostępnej publikacji. Instrukcja startowa przypomina o tej pracy, ale nie zastępuje wykonania kwerendy.
