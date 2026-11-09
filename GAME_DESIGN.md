# Morza Bogów — specyfikacja gry

GAME_VISION.md określa doświadczenie gracza, priorytety i cele balansu. Dokument określa mechaniki, jednostki, ekonomię, walkę, magię, SI, środowisko i warunki odbioru gry. Szczegółowe reguły gospodarki zawiera ECONOMY_DESIGN.md, parametry balansu ECONOMY_BALANCE.md, prezentację obrazu VISUAL_TECH_SPEC.md, a kampanię MYTHIC_SCENARIOS.md. Parametry liczbowe należy weryfikować w grywalnych scenach i playtestach.

## Kompletna specyfikacja produkcyjna gry strategicznej czasu rzeczywistego

**Gatunek:** strategia czasu rzeczywistego skoncentrowana na bitwach morskich / mitologia grecka  
**Platforma:** Windows 11, komputery PC  
**Silnik:** Unity 6.3 LTS, High Definition Render Pipeline (HDRP)  
**Perspektywa:** strategiczna izometryczna kamera 3D z możliwością płynnej zmiany oddalenia i kąta  
**Tryby:** kampania, potyczka, scenariusze specjalne, docelowo multiplayer i edytor map  
**Języki:** polski i angielski  
**Charakter:** pełnoprawna produkcja premium, bez mikropłatności i mechanizmów pay-to-win

---

# CZĘŚĆ I. FUNDAMENTY ROZGRYWKI

## 1. Wizja gry

Morza Bogów to strategia czasu rzeczywistego o dowodzeniu flotą wojenną w świecie greckiej mitologii. Gracz zdobywa inicjatywę dzięki rozpoznaniu, manewrowi, współdziałaniu jednostek i kontroli ważnych akwenów. Porty, osady i zaopatrzenie umożliwiają prowadzenie eskort, rajdów, blokad, wypraw i operacji ratunkowych.

Najważniejszą jednostką decyzji jest okręt działający w eskadrze. Kadłub, napęd, załoga, morale, wyposażenie i gotowość określają jego rolę i bieżącą użyteczność. Ostrzał, taranowanie oraz abordaż współdziałają i wymagają odmiennych pozycji, dystansów oraz przygotowania. Liczebność floty ma znaczenie, a jej skuteczność zależy także od składu, osłony i realizowanego celu.

Gospodarka opiera się na drewnie okrętowym i srebrze, lokalnych magazynach i rzeczywistym transporcie. Automatyczne dostawy, doktryny i kolejki pozwalają kierować uwagę na sytuację na morzu. Skończone zasoby, stanowiska pracy i koszt ekspansji tworzą sporne szlaki. Rozbudowa infrastruktury jest decyzją konkurującą z przygotowaniem kolejnej eskadry.

Mitologia nadaje operacjom charakter przez bohaterów, zaklęcia, artefakty, potwory i wyjątkowe zagrożenia. Koszty, cooldowny, zapowiedzi oraz dostępna kontrgra utrzymują znaczenie dowodzenia. Każda misja ma jawne cele; zwycięstwo może oznaczać przeprowadzenie konwoju, ocalenie ludzi, wyjście z cieśniny lub utrzymanie pozycji.

Pełne zasady doświadczenia gracza określa GAME_VISION.md. Ponad 70% aktywnego czasu misji ma przypadać na statki, walkę, eskortę, nawigację i logistykę na wodzie. Jest to cel playtestów.

### 1.1. Filary projektowe

1. **Dowodzenie flotą.** Rozpoznanie, formacje, wybór celu, manewr i odwrót tworzą główne decyzje gracza.
2. **Współdziałające metody walki.** Ostrzał, taran i abordaż mają własne wymagania, koszty oraz odpowiedzi przeciwnika.
3. **Gospodarka tworząca cele.** Lokalne zapasy, fizyczne cargo i sporne akweny nadają sens eskortom, rajdom oraz blokadom.
4. **Automatyka wspierająca dowódcę.** Gracz ustala priorytety i trasę, a powtarzalne czynności wykonują doktryny oraz kolejki.
5. **Różne grywalne strategie.** Wczesny rajd, eskorta i ekspansja oraz oszczędna obrona wymagają kontroli mapy i reakcji na przeciwnika. Pojedynczy skład nie zastępuje wszystkich decyzji.
6. **Mitologiczne operacje.** Zagrożenia i zdolności zmieniają sposób wykonania celu przy czytelnej zapowiedzi i dostępnej kontrgrze.
7. **Sprawiedliwe zasady.** SI respektuje koszty, obserwację i ograniczenia transportu. Pomoc i asymetria scenariusza są jawne.
8. **Czytelne widowisko.** Woda, światło, pogoda i skutki trafień podkreślają przebieg bitwy. Profil jakości nie zmienia reguł ani informacji taktycznej.

### 1.2. Rytm i progresja

Standardowa potyczka 1 na 1 ma umożliwiać celowy kontakt około 1:45–3:00 i większą bitwę około 4:00–7:00; docelowy mecz trwa około 15–25 min. Są to cele do weryfikacji na mapach i w playtestach. Dostępna wcześniej okazja do rajdu nie jest blokowana zegarem.

Kampania obejmuje 14 misji w trzech cyklach z określonymi flotami i budżetami startowymi. Między misjami zachowuje ukończenia, medale i kronikę; nie przenosi flot ani magazynów. Cele poboczne nie dają stałej przewagi w potyczkach. Katalog 40 potyczek obejmuje różne cele operacyjne i osiem biomów.

## 2. Główna pętla rozgrywki

1. Rozpoznaj akwen, przeciwnika, drogi podejścia i cel scenariusza.
2. Przydziel zbieracze i automatyczne dostawy, wybierz priorytet portu oraz osłonę szlaku.
3. Przygotuj eskadrę i dobierz wyposażenie do rajdu, eskorty, obrony, abordażu lub ratunku.
4. Podejmij walkę na korzystnej pozycji, wiążąc przeciwnika albo wykorzystując odsłonięty cel.
5. Rozstrzygnij pościg, zabezpiecz cargo lub zdobyty okręt i oceń potrzebę odwrotu.
6. Wykorzystaj kontrolę akwenu do uzupełnień, napraw, ekspansji lub wykonania następnego celu.

Dochód, dostawy i działania wojenne przebiegają równolegle. Gospodarka nie wymaga ukończenia całego zaplecza przed pierwszą operacją. Misje z gotową flotą mogą rozpoczynać się od bezpośredniego starcia, eskorty lub ucieczki. Zwycięstwo rozstrzygają jawne reguły scenariusza.

## 3. Reguły symulacji

Świat funkcjonuje w ciągłym czasie rzeczywistym.

Logika rozgrywki powinna być aktualizowana w stałych krokach symulacyjnych, niezależnych od liczby wyświetlanych klatek. Docelowa częstotliwość symulacji to 30 aktualizacji na sekundę, z interpolacją ruchu i animacji na potrzeby renderowania.

Prędkość gry może przyjmować wartości:

- Pauza — całkowite zatrzymanie symulacji.
- 0,5× — szczegółowe dowodzenie i trudne starcia.
- 1× — standardowe tempo.
- 1,5× — przyspieszona rozgrywka.
- 2× — szybki rozwój ekonomii i transport.

W kampanii i potyczkach jednoosobowych wszystkie wartości są dostępne. W multiplayerze szybkość jest wspólna dla wszystkich uczestników.

Pauza taktyczna umożliwia wydawanie i kolejkowanie rozkazów bez upływu czasu symulacji. Gracz może podczas niej zmienić wyposażenie w portach, sprawdzić sytuację i przygotować operację, lecz zmiany wymagające czasu zaczynają być realizowane dopiero po wznowieniu gry.

Każde zdarzenie posiada jednoznaczny rezultat. Nie wolno uzależniać strat, produkcji, ruchu czy działania zaklęć od liczby klatek renderowanych przez GPU.

---

# CZĘŚĆ II. ŚWIAT, MAPY I ŚRODOWISKO

## 4. Struktura świata

Świat składa się z archipelagów, wysp, zatok, cieśnin, płytkich mórz, otwartych akwenów, skalistych wybrzeży, raf, jaskiń, gór, osad i ruin.

Każda mapa zawiera cztery warstwy informacji:

**Warstwę wizualną:** teren 3D, ocean, roślinność, skały, budynki i dekoracje.

**Warstwę nawigacyjną:** głębokość wody, przeszkody, porty, korytarze żeglugowe i miejsca możliwego manewrowania.

**Warstwę strategiczną:** zasoby, strefy kontroli, osady, przeciwników, miejsca zdarzeń oraz punkty zainteresowania.

**Warstwę środowiskową:** wiatr, prądy morskie, falowanie, pogodę, widoczność i zjawiska mitologiczne.

### 4.1. Biomy

Dostępnych jest osiem głównych środowisk:

| Biom | Charakterystyka |
|---|---|
| Archipelag Egejski | Jasne wapienne klify, turkusowe zatoki, świątynie |
| Wyspy Wulkaniczne | Czarne skały, popioły, lawa, niebezpieczne wybrzeża |
| Morze Burz | Wysokie fale, sztormy, pochmurne niebo |
| Wybrzeża Perskie | Suche wzgórza, rozbudowane porty, ciepłe morze |
| Mglisty Archipelag | Ograniczona widoczność, skały, zasadzki |
| Wyspy Syren | Urwiska, jaskinie, zdradliwe przesmyki |
| Morze Zapomnianych | Ruiny, wraki, ciemna woda, istoty mitologiczne |
| Troada | Żyzne wybrzeża, bogate osady, ruiny monumentalnych murów |

Każdy biom posiada własną kolorystykę, intensywność falowania, atmosferę, zestaw dźwięków, rodzaje roślinności i charakterystyczne zjawiska.

### 4.2. Rozmiary map

Wyróżnia się trzy skale map:

- Małe: około 1,5 × 1,5 km, dla szybkich potyczek.
- Średnie: około 3 × 3 km, standardowe scenariusze.
- Duże: około 5 × 5 km, rozbudowane bitwy i scenariusze wieloosobowe.

Wartości stanowią skalę projektową; rzeczywiste proporcje modeli muszą uwzględniać czytelność strategiczną. Świat powinien posługiwać się spójną jednostką przestrzenną, a duże mapy wykorzystywać streaming obiektów i odpowiednie poziomy szczegółowości.

### 4.3. Głębokość wody

Woda posiada logicznie zdefiniowaną głębokość, niezależną od jej wyglądu.

- 0–1,5 m: płycizny dostępne dla najmniejszych jednostek.
- 1,5–2,5 m: woda przybrzeżna dostępna dla lekkich galer.
- 2,5–5 m: woda średniej głębokości.
- Powyżej 5 m: głębokie morze dostępne dla całej floty.

Każdy kadłub ma własne zanurzenie.

Okręt nie może wpłynąć na obszar o głębokości mniejszej niż wymagane zanurzenie powiększone o margines bezpieczeństwa. Margines zależy od falowania i prędkości.

Przy wejściu na mieliznę okręt może utracić prędkość, uszkodzić kadłub lub utknąć, zależnie od warunków scenariusza i poziomu ryzyka wybranego przez gracza.

### 4.4. Mgła wojny

Mapa posiada trzy stany rozpoznania:

1. Nieodkryte obszary, których ukształtowanie jest niewidoczne.
2. Obszary odkryte, lecz obecnie nieobserwowane.
3. Obszary znajdujące się w aktualnym polu widzenia jednostek.

Po opuszczeniu obserwowanego obszaru gracz zachowuje znajomość geografii, ale nie otrzymuje bieżących informacji o ruchomych przeciwnikach.

Ostatnie znane pozycje wrogich jednostek mogą być przedstawiane jako przygaszone oznaczenia z informacją o czasie obserwacji.

Podstawowy promień obserwacji kutra wynosi 250 metrów, standardowego okrętu wojennego 200 metrów, okrętu królewskiego 275 metrów, a specjalistycznej jednostki rozpoznawczej 320 metrów.

Mgła, ulewa, noc i niektóre zaklęcia ograniczają efektywny promień obserwacji. Wysokość stanowisk obserwacyjnych i obecność latarni mogą go zwiększać.

---

# CZĘŚĆ III. GOSPODARKA

## 5. Zasoby i gospodarka morska

Dokładnie dwa zasoby: **drewno okrętowe D** na kadłuby, wiosła, maszty, zabudowania, remonty i fizyczne wyposażenie oraz **srebro S** na rekrutację, zaopatrzenie, uzbrojenie, szkolenie, badania i ofiary w sanktuariach. Oba są skończone. Nie ma dodatkowej waluty, podatku od floty, głodu załóg ani obowiązkowej dostawy amunicji. Żywność i inne materiały występują w oprawie albo jako ładunek misji. Pełne reguły: E00–E12 w ECONOMY_DESIGN.md.

### 5.1. Zbiór

Punkty pracy na wybrzeżu mają po dwa stanowiska. Kuter pracuje z tempem 6 D/s albo 3 S/s; wspólny limit punktu to 12 D/s albo 4 S/s, a kolejni zbieracze czekają FIFO. Handlowiec nie zbiera. Nie ma dochodu za samo posiadanie portu. Zasoby ładuje się fizycznie i dostarcza do portu.

### 5.2. Magazyny i dostawy

Każda osada ma magazyn 6000 D i 3000 S, wspólny dla jej budynków na jednej wyspie. Między wyspami towar musi przewieźć statek. HUD rozdziela zapas dostępny, zarezerwowany i w drodze; suma nie jest zdalnym portfelem. Port ma trzy stanowiska po 60 jednostek/s. Remont i produkcja mają odrębne stanowiska. Przy pełnym magazynie pozostałość zostaje na statku.

### 5.3. Wraki i łup

Zatopiony transport pozostawia 50% realnego ładunku jako zbieralny wrak, druga połowa przepada. Pusty kadłub niczego nie wypłaca. Każdy przedmiot wyposażenia ma 30% szans przetrwania, losowane raz i zapisywane. Zniszczenie osady pozostawia do odzyskania 30% jej niewydanych zapasów.

### 5.4. Ekspansja

Przyczółek portowy kosztuje 300 D/100 S, mieści się w ładowni handlowca i powstaje w 35 s po dotarciu. Obejmuje lokalny magazyn, port i kolejkę gospodarczą, bez darmowych kutrów i stoczni. Bazowa potyczka nie wymaga rynku ani wypraw; neutralne kontrakty i skarby to jawne, skończone cele scenariusza.

Gospodarka daje powody do eskort i rajdów. Nie jest alternatywnym trybem wygranej. Start potyczki: port, stocznia, cztery kutry (trzy przy drewnie, jeden przy srebrze), 360 D/160 S. Model tempa: ECONOMY_BALANCE.md.

---

# CZĘŚĆ IV. OSADY, PORTY I BUDYNKI

## 6. System budowy

Budynki powstają wyłącznie na odpowiednio przygotowanych obszarach wysp i wybrzeży, w obszarze wpływów osady. Podczas ustawiania gracz widzi obrys, koszt, dostęp do morza oraz kolizje. Budynki nadbrzeżne wymagają kontaktu ze strefą żeglowną. Koszt pokrywa lokalny magazyn osady.

### 6.1. Port i przyczółek

Port jest podstawowym centrum gospodarczym: magazyn 6000 D/3000 S, trzy stanowiska przeładunku, osobna kolejka kutrów i handlowców. Model wizualny ma czytelną strukturę: centralny budynek, magazyny, nabrzeże i stanowiska pracy. Rozbudowuje się go przez dobudowanie magazynów, wież, portyków i monumentalnych budowli.

Nowy port powstaje jako przyczółek za 300 D/100 S w 35 s, po dowiezieniu ładunku handlowcem. Dostępne rozkazy obejmują załadunek, rozładunek, transfer uzbrojenia, zmianę załogi, transfer bohatera, transfer artefaktu oraz organizację konwoju.

### 6.2. Stocznia

Stocznia produkuje okręty i ma jedną linię wojenną. Kolejna stocznia daje dodatkową linię za 200 D/100 S i 40 s, z własnym wolnym podejściem. Budowana jednostka ma wizualną reprezentację na pochylni: żebra, poszycie, maszt i wyposażenie.

Stocznia ma jedno odrębne stanowisko remontowe, 12 HP/s. Remont pełnej utraconej wytrzymałości kosztuje 25% konstrukcyjnej części ceny kadłuba; częściowy rozlicza faktycznie odtworzony odsetek HP. Naprawa kadłuba nie przywraca automatycznie żołnierzy ani uzbrojenia. Złomowanie poza walką trwa 20 s i oddaje 15% opłaconej konstrukcji × odsetek bieżących HP.

### 6.3. Inwestycje

| Inwestycja | D/S | Czas | Działanie |
|---|---:|---:|---|
| Arsenał | 200/120 | 45 s | Ciężkie i specjalistyczne uzbrojenie, pancerze i tarany; bombardowce i jednostki specjalne |
| Sanktuarium Żywiołów | 180/160 | 40 s | Zaklęcia wody, wiatru, widoczności i pioruna |
| Sanktuarium Męstwa | 180/160 | 40 s | Zaklęcia ognia, ochrony, morale, naprawy i przywołania |
| Szkoła morska | 140/180 | 40 s | Szkolenie, technologie floty i zaawansowane szablony |
| Rozbudowa magazynu | 120/40 | 25 s | Jednorazowo +3000 D i +1500 S |
| Żurawie | 120/60 | 25 s | Jednorazowo +25% tempa przeładunku |
| Narzędzia drwali | 120/100 | 40 s | +10% pracy kutra przy D, bez wzrostu limitu punktu |
| Organizacja wydobycia | 100/120 | 40 s | +10% pracy kutra przy S, bez wzrostu limitu punktu |
| Wieża brzegowa | 200/140 | 40 s | Lokalna osłona podejścia, zakres 110 m |

Podstawowy port wspiera jedną wieżę, arsenał odblokowuje drugą. Wieża nie naprawia floty i musi pozostawiać drogę obejścia oraz przestrzeń centralną. Fortece występują wyłącznie jako obiekty scenariuszy i baz barbarzyńców, nie są budowane przez gracza w bazowej potyczce.

Każde sanktuarium ma własny obszar wpływu. Zaklęcia o zasięgu lokalnym można rzucać tylko w skutecznym zasięgu aktywnego sanktuarium.

Technologie floty w szkole morskiej mają trzy poziomy, z wyjątkiem badań odblokowujących konkretne klasy. Kategorie: kadłuby i opancerzenie, konstrukcja taranów, napęd i żeglarstwo, uzbrojenie lekkie, uzbrojenie ciężkie, szkolenie załóg, specjalistyczne konstrukcje.

---

# CZĘŚĆ V. FRAKCJE I OKRĘTY

## 7. Frakcje

Dwie główne cywilizacje to Hellenowie i Persowie.

Każda posiada dwa typy statków gospodarczych, sześć wspólnych klas bojowych oraz dwie jednostki specjalne.

Obie cywilizacje korzystają z tego samego modelu ekonomii, nawigacji i podstawowej walki.

### 7.1. Hellenowie

Flota Hellenów stawia na wszechstronność, jakość załóg i wyspecjalizowane uzbrojenie.

Cechy wizualne: jasne drewno, białe i granatowe żagle, brązowe elementy konstrukcyjne, geometryczne ornamenty, rzeźbione dzioby i charakterystyczne tarcze na burtach.

Jednostki specjalne: ogniomiot i dymowiec.

### 7.2. Persowie

Flota Persów opiera się na mobilności, przechwytywaniu oraz wykorzystaniu taktyki oskrzydlającej.

Cechy wizualne: ciemniejsze drewno, czerwone i purpurowe żagle, złocone ornamenty, zdobione dzioby oraz charakterystyczne proporce.

Jednostki specjalne: Okręt Nieśmiertelnych i Żmija Cieśnin.

### 7.3. Barbarzyńcy

Barbarzyńcy są niezależną frakcją, występującą w kampanii oraz scenariuszach.

Posiadają pięć klas okrętów:

1. Szybki łupieżca.
2. Lekki taranowiec.
3. Okręt abordażowy.
4. Ciężki okręt korsarski.
5. Jednostka dowódcy barbarzyńców.

Mogą posiadać małe, średnie i wielkie fortece.

Nie rozwijają technologii świątynnych ani zaawansowanej magii.

Ich zachowanie opiera się na rabowaniu handlowców, przejmowaniu statków i atakowaniu słabo chronionych osad.

## 8. Budowa okrętu — struktura wewnętrzna

Każdy okręt składa się z następujących podsystemów:

**Kadłub** przechowuje punkty wytrzymałości, masę, odporność, zanurzenie, geometrię kolizji i stan zalania.

**Napęd** określa przyspieszenie, maksymalną prędkość, prędkość wiosłowania oraz charakterystykę żagli.

**Układ sterowania** przechowuje prędkość obrotu, promień skrętu, zdolność hamowania i aktualny kierunek ruchu.

**Taran** jest osobnym elementem z określoną kategorią, odpornością i współczynnikiem uderzenia.

**Opancerzenie** posiada trzy poziomy jakości i właściwości ochronne zależne od rodzaju obrażeń.

**Stanowiska broni** określają liczbę dostępnych machin, ich położenie, sektory ostrzału i rodzaje dopuszczonego uzbrojenia.

**Załoga** posiada liczebność, skład, morale, doświadczenie i gotowość bojową.

**Ładownia** przechowuje zasoby lub przedmioty zgodnie z przeznaczeniem jednostki.

**Bohaterowie i artefakty** zapewniają dodatkowe zdolności.

**Automatyzacja** określa aktualne rozkazy, preferencje celów i zachowanie w sytuacjach awaryjnych.

Każdy podsystem musi mieć jednoznacznie określony stan w danych rozgrywki.

# Katalog statków

### 4.2. Pełny katalog klas jednostek

Tabela określa parametry startowe do strojenia balansu. Prędkości są wyrażone w jednostkach świata gry na sekundę przy neutralnych warunkach.

| Jednostka | Kadłub | Prędkość | Załoga maks. | Kompletny szablon D / S | Produkcja |
|--------|--------|--------|--------|--------|--------|
| Kuter | 180 | 10,0 | 8 | 60 / 20 | 18 s |
| Handlowiec | 370 | 7,0 | 16 | 140 / 60 | 28 s |
| Taranowiec | 760 | 8,2 | 36 | 240 / 100 | 35 s |
| Niszczyciel załóg | 520 | 6,8 | 52 | 180 / 90 | 30 s |
| Niszczyciel okrętów | 620 | 6,5 | 36 | 220 / 140 | 38 s |
| Bombardowiec | 960 | 4,3 | 45 | 400 / 260 | 60 s |
| Abordażowiec | 780 | 5,2 | 90 | 200 / 180 | 40 s |
| Okręt królewski | 1150 | 6,1 | 80 | 460 / 320 | 70 s |
| Ogniomiot | 540 | 7,0 | 40 | 260 / 180 | 45 s |
| Dymowiec | 600 | 6,1 | 36 | 300 / 180 | 45 s |
| Okręt Nieśmiertelnych | 680 | 6,3 | 45 | 280 / 220 | 45 s |
| Żmija cieśnin | 560 | 8,0 | 30 | 260 / 200 | 40 s |

Ceny obejmują kadłub, standardowe uzbrojenie, taran właściwy klasie i pełną zwykłą obsadę; nie pobiera się ich drugi raz (E04). Bombardowiec, ogniomiot, dymowiec i żmija wymagają arsenału, okręt królewski także szkoły morskiej, a Okręt Nieśmiertelnych Sanktuarium Męstwa. Jednostki ostatnich czterech klas są unikatowe dla swoich frakcji: ogniomiot i dymowiec dla floty helleńskiej, pozostałe dwa dla floty perskiej. Prędkości kutra i handlowca pochodzą z modelu gospodarki; pozostałe parametry bojowe wymagają testów.

### 4.3. Kuter

Najmniejszy, najtańszy statek gospodarczy. Wąski, lekki kadłub, niewielki żagiel, widoczne wiosła, małe zanurzenie.

Ma zbierać drewno, srebro i pozostałości wraków, prowadzić rozpoznanie wybrzeża oraz przewozić małe ilości wyposażenia.

Ładowność: 60 jednostek. Może przepływać przez płycizny niedostępne dla ciężkich galer. Jego podstawową obroną jest ucieczka; może przewozić niewielką eskortę żołnierzy, lecz nie może inicjować abordażu.

W zachowaniu automatycznym unika wszystkich uzbrojonych przeciwników. Jeśli wyznaczony obszar zbioru zostanie wyczerpany, automatycznie wyszukuje kolejny bezpieczny zasób w zasięgu swojej sieci logistycznej.

### 4.4. Handlowiec

Szerszy kadłub, większe żagle, ładownia podzielona na sekcje, mniejsza zwrotność. Ładowność: 480 jednostek.

Nie zbiera zasobów. Realizuje cztery zadania: przewożenie uzbrojenia do portów, zaopatrywanie przyczółków i wypraw budowlanych, transport artefaktów oraz organizowanie automatycznych szlaków handlowych między osadami.

Handlowiec może eskortować zaopatrzenie budowanej w odległym miejscu placówki. Jeżeli przeciwnik zablokuje wybraną trasę, system ma spróbować znaleźć drogę alternatywną lub poprosić gracza o decyzję.

Nie wolno dopuścić do sytuacji, w której jeden zablokowany statek wstrzymuje całą gospodarkę.

### 4.5. Taranowiec

Wąski, opływowy okręt z charakterystycznym brązowym taranem na dziobie. Ma potężny napęd wiosłowy i ograniczoną liczbę stanowisk broni dystansowej.

Jego efektywność zależy od prędkości zderzenia, kąta trafienia i masy przeciwnika. Najbardziej skuteczny jest atak w bok ciężkiego okrętu.

Taranowiec powinien wykonywać manewr: podejście, ustawienie kursu, przyspieszenie, uderzenie, wycofanie i ponowny atak. Po nieudanym zderzeniu musi być przez chwilę narażony na kontratak.

Nie może bez końca zadawać obrażeń przez kontakt z kadłubem. Jeden manewr oznacza jedno rozstrzygnięcie uderzenia.

### 4.6. Niszczyciel załóg

Średniej wielkości jednostka z podwyższonymi platformami dla łuczników i oszczepników.

Jego celem jest zmniejszenie liczebności załóg przed abordażem, a nie zatapianie jednostek. Może prowadzić ostrzał pokładu i obsługi machin oblężniczych.

Powinien otrzymać lepszą celność przeciwko załogom niż inne okręty, ale stosunkowo niskie obrażenia kadłuba. Największą skuteczność osiąga wspólnie z abordaźowcem.

Jego załoga pozostaje odsłonięta, dlatego jest podatny na bombardowanie i ogień.

### 4.7. Niszczyciel okrętów

Zwinna platforma z machinami przeznaczonymi do uszkadzania kadłubów i wyposażenia.

Powinien wykorzystywać balisty, skorpiony lub ciężkie pociski o dużej penetracji. W walce zachowuje średni dystans, stara się ustawiać bokiem do celu i unika bezpośredniego zderzenia.

Jest naturalną kontrą wobec ciężkich jednostek abordażowych, jeśli potrafi utrzymywać dystans, ale przegrywa z szybkim taranowcem, który skutecznie wejdzie w zwarcie.

### 4.8. Bombardowiec

Największa jednostka oblężnicza, z ciężkim kadłubem i wysokimi platformami artyleryjskimi.

Bardzo wolno zmienia kurs, długo przygotowuje strzał, lecz ma znaczny zasięg. Doskonale niszczy fortece, stocznie, budynki portowe i inne nieruchome cele.

Jego słabości są równie ważne jak siła: martwa strefa bardzo blisko kadłuba, niewielka zwrotność, ograniczona obrona przed abordażem i konieczność osłaniania przez szybsze okręty.

Ciężkie pociski mają poruszać się po widocznych łukach balistycznych. Przy trafieniu w kamienną budowlę powstaje pył, odłamki oraz uszkodzenie konkretnej sekcji konstrukcji.

### 4.9. Abordażowiec

Szeroka jednostka z wysokimi burtami, pomostami, hakami, linami i dużym oddziałem piechoty.

Jego najważniejszą cechą jest maksymalna liczba żołnierzy oraz bonus do stabilności przy połączeniu dwóch jednostek. Jest powolny i stanowi łatwy cel dla bombardowców.

Atak składa się z podejścia, zrównania prędkości, zarzucenia haków, próby unieruchomienia celu, walki pokładowej oraz przejęcia albo wycofania.

Nie wolno stosować natychmiastowej zmiany właściciela po jednym kliknięciu. Wynik walki musi wynikać ze stanu załóg, morale, wyszkolenia i aktualnej sytuacji.

### 4.10. Okręt królewski

Najbardziej uniwersalna, prestiżowa jednostka floty, odpowiednia do transportu herosów.

Powinien posiadać najbardziej rozbudowany model wizualny: zdobiony dziób, większy żagiel, chorągwie, rzeźbione burty i wyraźną sylwetkę widoczną nawet przy dalekim oddaleniu kamery.

Jest odporny, dysponuje dużą załogą i może przenosić różne rodzaje uzbrojenia, ale nie jest najlepszy w żadnej jednej specjalizacji.

Wyznaczenie go na okręt flagowy odblokowuje koordynowanie formacji i dodatkowy zasięg organizacyjny floty. Utrata jednostki flagowej powoduje chwilowe pogorszenie morale i spójności ugrupowania, nie automatyczną porażkę.

### 4.11. Ogniomiot

Niewielki okręt z instalacją miotającą substancję zapalającą. Charakterystyczne zbiorniki, ceramiczne naczynia i zabezpieczone stanowisko ogniowe.

Jego rola to podpalanie wrogich kadłubów, żagli i machin oblężniczych. Ogień zadaje obrażenia przez określony czas, może rozprzestrzeniać się między blisko ustawionymi jednostkami, ale jego skuteczność zmniejsza deszcz, silne zalewanie i aktywne gaszenie przez załogę.

Ogniomiot ma ograniczony zasięg, jest podatny na przechwycenie i nie może spowodować błyskawicznego unicestwienia całej floty jednym atakiem.

### 4.12. Dymowiec

Okręt wsparcia wyposażony w machinę wyrzucającą garnce duszącego dymu.

Trafienie zmniejsza celność broni, sprawność obsługi stanowisk i tempo wykonywania rozkazów. Przy silnym efekcie może na krótko zablokować określone moduły jednostki.

Bardzo ważne: **obezwładnienie nie oznacza bezterminowego zamrożenia celu**\. Działanie jest ograniczone czasowo, a kolejne trafienia nie mogą nieustannie odnawiać pełnego czasu paraliżu.

W przeciwnym razie połączenie kilku dymowców i ogniomiotów stałoby się strategią pozbawioną kontr.

# Reguły wykonawcze

## C01. Zakres i kamienie milowe

Produkt docelowy: 14 misji kampanii, 40 scenariuszy potyczek, dziewięciu herosów, 12 zaklęć, osiem biomów, dwie frakcje grywalne i barbarzyńcy. Multiplayer do ośmiu graczy i edytor są późniejszymi etapami, wymagającymi osobnej walidacji, nie obietnicą gotowości sieciowej prototypu.

M0: audyt środowiska, zablokowane wersje Unity/HDRP/Test Framework, infrastruktura testów, scena uruchomieniowa. M1: grywalna pętla kuter–zasób–port–budowa–walka; cztery klasy (kuter, taranowiec, niszczyciel załóg, abordażowiec), jedna mapa, przeciwnik, wygrana i przegrana, zapis. M2: wszystkie klasy, wyposażenie, formacje i pełna ekonomia. M3: magia, bohaterowie, pogoda, kampanijny vertical slice z docelową wodą. M4: pełna zawartość i stabilizacja. Nie nazywaj M1 pełną grą.

Wymagana instalacja Windows 11 x64. Użyj Unity 6.3 LTS i kompatybilnego HDRP. Przed utworzeniem projektu potwierdź obsługę wymaganych funkcji. Zapisz dokładny numer edytora, pakiety i lockfile; nie wybieraj automatycznie najnowszego preview. Przed zmianą środowiska projektu sprawdź kompatybilność komponentów. Brak licencji Unity blokuje rzeczywiste testy, nie uprawnia do raportu PASS.

## C02. Jednostki, ruch i nawigacja

Jednostka świata = metr, czas symulacji = sekunda. Logika działa w 30 Hz. Każda jednostka ma trwały identyfikator; rozkaz ma identyfikator i numer sekwencji. RNG ma zapisany seed i stan. Rendering interpoluje; fizyka wizualna nie zmienia wyniku ostrzału ani ekonomii. Pauza zatrzymuje produkcję, cooldowny, pociski i pogodę logiczną. Wydawane rozkazy trafiają do kolejki i są wykonywane po wznowieniu.

Ruch jest ograniczony przyspieszeniem, hamowaniem i promieniem skrętu. Bez teleportowania i ruchu bocznego. Cofanie przy manewrach portowych do 20% prędkości bazowej. Punkt docelowy oznacza osiągalny obszar z tolerancją zależną od kadłuba, nie identyczny punkt dla całej floty.

Nawigacja: graf akwenów dla tras globalnych, lokalna siatka kosztów uwzględniająca zanurzenie, obrys i promień skrętu, unikanie dynamicznych przeszkód oraz rezerwacje wąskich przejść. Głębokość używana przez logikę jest stałym polem mapy z korektą pływu; fala wizualna sama nie otwiera drogi. Margines pod kilem: 0,25 m + 0,1 amplitudy fal. Trasa musi sprawdzać cały obrys, nie tylko środek kadłuba.

Zanurzenie startowe: kuter 0,7 m, handlowiec 1,4 m, taranowiec 1,6 m, niszczyciele 1,7 m, bombardowiec 2,8 m, abordażowiec 2,2 m, królewski 2,5 m, ogniomiot i dymowiec 1,6 m, Nieśmiertelnych 1,9 m, Żmija 1,3 m. Ładunek i zalanie zwiększają zanurzenie do +25% podstawy. Nie zwiększaj kolizyjnego zanurzenia od samego przechyłu wizualnego.

Brak postępu przez 5 s uruchamia lokalne ponowne planowanie; po 15 s jednostka wycofuje się do ostatniego bezpiecznego punktu albo zgłasza blokadę. Nie wykonuje nieskończonej pętli szukania. Port rezerwuje trzy stanowiska i bezpieczny punkt oczekiwania, zwalnia rezerwację po śmierci lub anulowaniu. Pierwszeństwo ma jednostka opuszczająca kanał; kolejka ma ochronę przed zagłodzeniem.

Formacje: linia, kolumna, klin, półksiężyc, pierścień eskorty. Odstęp minimalny = suma promieni obrysów + 3 m; przy sztormie +50%. Lider wyznacza trasę dla największego zanurzenia albo rozdziela grupę na podgrupy z komunikatem. W cieśninie automatyczne przejście do kolumny, potem odtworzenie szyku. Gracz może zablokować automatyczne przeformowanie. Prędkość grupy odpowiada najwolniejszej sprawnej jednostce; pościg ma limit dystansu.

## C03. Klasy specjalne i barbarzyńcy

Okręt Nieśmiertelnych: perska galera gwardii z łuskowymi osłonami burt, odporna na strach; obrona abordażowa ×1,5, ale nie absolutna nietykalność. Otrzymywane obrażenia tarana ×1,25. Nie tworzy darmowej załogi i nie ignoruje zniszczenia kadłuba.

Żmija Cieśnin: perski przechwytujący okręt o niskiej sylwetce; przyspieszenie ×1,2 względem taranowca, mały promień skrętu, słaby pancerz, bonus +20% obrażeń pierwszego ataku po 10 s bez walki. Bonus nie odnawia się przez chwilowe wyjście z zasięgu. Nie posiada niewidzialności; mgła wojny działa normalnie.

Barbarzyńcy używają wariantów istniejących mechanik: łupieżca (kuter, prędkość +10%, ładownia 40, broń lekka), lekki taranowiec (HP 500, prędkość 8,5, załoga 24), abordażowy (HP 600, załoga 65), korsarski (HP 850, załoga 55, dwa średnie stanowiska), dowódca (HP 1000, załoga 70, aura morale +10 w 100 m). Nie otrzymują zasobów znikąd. Budżety zdarzeń scenariusza są jawne w ustawieniach trudności.

## C04. Produkcja, ekonomia i wyposażenie

Wymagania ekonomiczne E00–E12 w ECONOMY_DESIGN.md są integralne i nadrzędne w swoim zakresie: dwa skończone zasoby D/S, lokalne magazyny, fizyczne cargo, automatyczne dostawy i brak stałego podatku od istniejącej floty. Produkcja i naprawy płacą z własnej osady, magia z osady aktywnego sanktuarium.

Jednostka zasobu zajmuje 1 pojemności. Broń lekka/średnia/ciężka: 20/40/80, artefakt 5, pasażer 1. Zamontowane wyposażenie nie zajmuje pojemności transportowej, ale ma masę i wpływ na ruch/zanurzenie. Kuter 60, handlowiec 480 pojemności, bez darmowego ładunku przy ulepszeniu. Przeładunek: 60 jednostek/s na stanowisko, żurawie +25%.

Zlecenie rezerwuje pełny lokalny koszt, a zużycie następuje wraz z postępem. Anulowanie przed startem zwalnia 100%; po starcie zwraca 80% niezużytej części. Pozostałe 20% tej części jest stratą, zużyta część nie wraca. Zniszczenie samej stoczni zwalnia niezużyte rezerwacje do osady. Przestawienie kolejki nie pobiera opłaty ponownie.

Czasy: inwestycje według 6.3, port 35 s jako przyczółek. Start potyczki: port, stocznia, cztery kutry, 360 D/160 S. Limit 100 okrętów na frakcję, 400 łącznie; edytowalny per scenariusz. Przejęcie ponad limit dopuszcza istniejącą flotę, ale blokuje nową produkcję.

Kadłub ma sloty lekkie/średnie/ciężkie: kuter 0/0/0, handlowiec 1/0/0, taranowiec 1/0/0, niszczyciel załóg 4/0/0, niszczyciel okrętów 0/2/0, bombardowiec 0/0/2, abordażowiec 2/0/0, królewski 2/2/1, ogniomiot 1 specjalny, dymowiec 1 specjalny, Nieśmiertelni 2/1/0, Żmija 2/0/0. Broń montowana wyłącznie przy porcie po zatrzymaniu, 5 s lekka, 10 s średnia, 15 s ciężka. Brak wymiany w czasie walki (10 s od obrażeń).

Standardowa obsada szablonu wojennego to ceil(25% maksymalnej załogi) marynarzy i pozostałe miejsca żołnierzy; szablon gospodarczy ma wyłącznie marynarzy. Cena członka obsady: marynarz 0,50 S, żołnierz 1 S, specjalista 3 S. Uzupełnienie po stratach: żołnierz 1 s, specjalista 3 s. Minimalna obsada ruchu = 25% maksymalnej, poniżej niej prędkość skaluje się liniowo do zera. Personel nie może być jednocześnie w dwóch rolach ani statkach.

Indywidualna zmiana wyposażenia płaci różnicę według cennika komponentów, wartości startowe do strojenia: stanowisko lekkie 12 D/16 S, średnie 20 D/30 S, ciężkie 40 D/70 S; pancerz I/II/III 30/10, 60/20, 100/40; taran I/II/III 30/10, 60/20, 100/40. Pancerz: redukcja pocisków 10/20/30%, kara prędkości 0/5/10%. Taran: mnożnik 1/1,2/1,4. Maksymalna redukcja sumaryczna 70%; premie multiplikatywne w określonej kolejności, z logiem źródeł. Technologie floty mają trzy poziomy: 80/60, 150/120, 260/220 D/S, 30/60/90 s, po +8% w danej dziedzinie, poza odblokowaniami. Tylko jedna technologia danej gałęzi naraz w całej frakcji. Te wartości to hipoteza do testów walki, nie wynik modelu gospodarki.

## C05. Walka i przejmowanie

Kolejność kroku: rozkazy i koszty → ruch → kolizje/tarany → trafienia → straty załóg → abordaż → zmiana właściciela → śmierć → wygrana. Kadłub o HP ≤0 zostaje oznaczony do zatonięcia; w tym samym kroku nie można go zdobyć. Przy równoczesnym zniszczeniu obu celów misja stosuje jawny priorytet lub remis. Wszystkie obrażenia są naliczane dokładnie raz po identyfikatorze zdarzenia.

Broń ma obsługę, sektor, przeładowanie, minimalny/maksymalny zasięg, celność, czas lotu, obrażenia kadłuba/załogi/modułu. Dane startowe na jeden strzał: łucznicy 2/4 HP, 15–100 m, 2 s; oszczepnicy 4/6, 5–45 m, 3 s; balista 45/2, 30–180 m, 5 s; ciężka katapulta 100/8, 60–260 m, 10 s, promień 8 m. Celność bazowa 0,75 dla lekkiej, 0,8 balisty, 0,65 katapulty; spada przy ruchu i fali, nigdy nie omija sektora i linii widzenia. Trafienie w obszar liczone geometrycznie; celność określa rozrzut. Nie losuj niezależnie trafienia po geometrycznej kolizji pocisku.

Rozkazy: atakuj kadłub, załogę, napęd, moduł; przechwyć; utrzymaj dystans; odwrót. Samoobrona nie goni poza strefę. Ogień sojuszniczy domyślnie wyłączony, wyjątek jawnie opisany przez scenariusz. Pociski nie strzelają przez wyspę; budynek jest przeszkodą. Cel niewidoczny można ostrzelać obszarowo ostatnią znaną pozycją, bez śledzenia.

Taran: stan Podejście → Przyspieszenie → Uderzenie → Odskok → Gotowy. Wymagany kontakt dziobowego segmentu i dodatnia prędkość zbliżania ≥3 m/s. Obrażenia = 120 × (prędkość zbliżania/8)^2 × mnożnik tarana × współczynnik kąta [0,25–1] × ograniczony stosunek mas [0,5–1,5]. Maksimum 400 HP na uderzenie. W bok współczynnik 1, w dziób 0,5, muśnięcie 0,25. Atakujący otrzymuje 20% obrażeń przed ochroną własną. Para kadłubów i identyfikator szarży blokują wielokrotny kontakt; ponowna szarża po 8 s i oddaleniu o długość kadłuba. Niskoprędkościowe kolizje nie stają się darmową bronią.

Abordaż wymaga dystansu burt ≤4 m, względnej prędkości ≤1,5 m/s i ≥10 żołnierzy. Haki przygotowują się 3 s, przeciwnik może uciec lub przeciąć połączenie. Walka rozliczana co 1 s: siła = żołnierze × wyszkolenie × (0,5 + morale/100) × bonus obrony. Straty obu stron równoczesne, bazowo 5% przeciwnej siły z ograniczeniem do realnej liczby. Morale 0–100, spada za straty i ogień, regeneruje poza walką. Po pokonaniu obrony i 3 s kontroli atomowo zmień właściciela, przenieś minimalną obsadę, zakończ stare rozkazy i zaktualizuj mgłę wojny. Dwóch atakujących nie może zdobyć tego samego statku; zwycięża pierwszy ważny commit kontroli w porządku identyfikatorów. Sojusznicza pomoc nie duplikuje żołnierzy.

Ogień ma intensywność 0–1, bazowo 12 HP/s przy 1, nie zabija załogi bezpośrednio szybciej niż 1/s. Deszcz mnoży narastanie przez 0,5, gaszenie przez 1,5. Gaszenie zajmuje część załogi i obniża gotowość. Dymowiec: -30% celności i -20% przeładowania przez 5 s, pełna blokada modułu maks. 2 s, odporność na kolejną pełną blokadę 10 s; powtórne trafienia nie wydłużają jej bez końca. Zalanie ograniczone i zależne od uszkodzeń, pompy zużywają obsadę. Przy zatonięciu usuń rezerwacje portowe, cele AI, boarding links i sloty formacji. Wrak zachowuje wyłącznie realną zawartość, 30% szans przetrwania każdego przedmiotu wyposażenia według E06, bez losowej duplikacji fabularnych artefaktów.

## C06. Magia — 12 zaklęć, dwie szkoły

Nie ma trzeciego zasobu mana. Koszt D/S pochodzi z dostępnego lokalnego magazynu osady wybranego aktywnego sanktuarium i jest naliczany raz; cooldown wspólny dla danego zaklęcia całej frakcji, aby budowa sanktuariów nie omijała ograniczeń. Zaklęcia zużywają przeważnie S, a efekty wymagające materiałów także D. Zniszczone sanktuarium nie anuluje już zastosowanego efektu, ale nie przyjmuje nowego zlecenia. Domyślny zasięg sanktuarium 800 m, cel musi być obserwowany. Koszt rezerwowany przy rozpoczęciu, pobrany przy aktywacji; utrata ważności przed aktywacją zwraca rezerwację i nie daje efektu. Czas rzucania 2 s, z wyjątkiem wyraźnie opisanym w danych. Efekty obszarowe mają widoczną zapowiedź i obrzeże. Koszty poza czterema przykładami z E07 są wartościami startowymi do strojenia.

| Szkoła | Zaklęcie | Koszt D/S | Cooldown | Działanie i kontrgra |
|---|---|---:|---:|---|
| Żywioły | Sprzyjający wiatr | 0/70 | 60 s | Stożek 120 m, 15 s, żagle +20%; nie zwiększa wioseł |
| Żywioły | Mgła Posejdona | 0/90 | 90 s | Koło 100 m, 20 s, widoczność obu stron ×0,5; bez pełnej niewidzialności |
| Żywioły | Spokojne wody | 0/80 | 75 s | 100 m, 20 s, lokalna kara fal zmniejszona o połowę |
| Żywioły | Fala odpychająca | 0/110 | 90 s | 60 m, rozdziela zwarcie do 10 m po sprawdzeniu kolizji; nie przenosi na ląd |
| Żywioły | Oko oceanu | 0/80 | 90 s | Odkrywa koło 150 m przez 10 s; bez stałego śledzenia po końcu |
| Żywioły | Błyskawica Zeusa | 0/150 | 120 s | Jeden widoczny cel, 180 HP kadłuba po 2 s, bez trafień łańcuchowych |
| Męstwo | Wezwanie trytona | 60/180 | 180 s | Jeden sojuszniczy tryton na 30 s, HP 600, atak 25/3 s, nie zdobywa zasobów |
| Męstwo | Płomień Aresa | 60/100 | 75 s | 40 m, +0,4 intensywności ognia po 2 s zapowiedzi; działa gaszenie |
| Męstwo | Egida | 40/110 | 90 s | Jeden okręt, redukcja 25% przez 12 s, respektuje limit redukcji 70% |
| Męstwo | Zew odwagi | 0/80 | 60 s | 100 m, morale +20 na 15 s, bez wskrzeszania załogi |
| Męstwo | Klątwa paniki | 0/110 | 90 s | 60 m, morale -20 na 10 s; minimalne morale 0, odporność Nieśmiertelnych |
| Męstwo | Naprawa Hefajstosa | 120/120 | 120 s | Regeneruje 150 HP przez 15 s, przerywana przez taran; bez naprawy załogi |

Takie same efekty nie sumują się; używany jest silniejszy, czas nie przekracza pierwotnego maksimum. Różne modyfikatory składają się według jawnej tabeli kolejności. Cooldown, status i rezerwacje muszą przetrwać zapis. Żaden VFX nie nadaje obrażeń. SI płaci identyczne koszty i podlega zasięgowi.

## C07. Dziewięciu bohaterów, artefakty i stworzenia

Bohaterowie: Eneasz (eskorta i formacje), Odyseusz (rozpoznanie), Achilles (abordaż), Atalanta (ostrzał załogi), Herakles (taran), Orfeusz (morale), Sybilla (magia), Tezeusz (obrona), Perseusz (walka z potworami). Każdy jest unikatowy w scenariuszu, maksymalnie jeden na statku. Nie klonuje się po transferze, zatopieniu ani wczytaniu. Domyślnie niedostępni w symetrycznej potyczce rankingowej.

Zdolności aktywne na 10 s z cooldownem 90 s: Eneasz redukuje błąd utrzymania formacji; Odyseusz daje +30% widzenia; Achilles +20% siły abordażowej; Atalanta +15% celności przeciwzałogowej; Herakles +20% tarana z zachowaniem limitu obrażeń; Orfeusz +15 morale; Sybilla obniża następny koszt zaklęcia o 15% (jedna rezerwacja); Tezeusz -15% obrażeń kadłuba; Perseusz +25% obrażeń przeciw mitologicznym stworzeniom. Pasywne bonusy nie przekraczają 10% i nie sumują identycznych aur. Poziomy 1–5 za cele i walkę, brak farmienia XP przez leczenie sojusznika. Przy zatonięciu bohater przechodzi do stanu rozbitka na 120 s, potem otrzymuje stan rannego i staje się niedostępny do końca misji; misja może jawnie wymagać jego ocalenia. Następna misja ustala własną obsadę i poziomy bohaterów zgodnie z C11.

Artefakty: Muszla (+10% widzenia), Kotwica (-10% odpychania), Żagiel (+10% napędu żaglowego), Pieczęć (+10 morale), Amfora (-10% czasu rzucania), Oko (+10% celności przeciw potworom). Jeden slot na jednostkę, dwa na królewskim; nie można nakładać identycznych artefaktów. Przedmiot ma globalny identyfikator i właściciela kontenera. Zatonięcie przenosi go do wraku, nigdy nie kopiuje. Przedmioty misji nie wygasają.

Stworzenia: syreny kuszące i obniżające morale w oznaczonej strefie (można ominąć lub przerwać śpiew); kraken jako boss atakujący zapowiadanymi mackami, odporny na abordaż; tryton jako krótkotrwały przywołaniec. Boss nie losuje natychmiastowego zabicia floty bez sygnału. Każda zdolność ma zasięg, telegraph minimum 2 s i bezpieczny obszar ucieczki.

## C08. SI i automatyczne doktryny

Warstwy: strategia (co 3 s: ekonomia, produkcja, ekspansja, obrona), operacje (co 1 s: cele eskadr i konwoje), taktyka (5 Hz: wybór celu, dystans, odwrót), sterowanie (30 Hz). Aktualizacje rozłożone między klatkami, budżety ograniczone. SI korzysta z tego samego modelu rozkazów i walidacji co gracz, nie wywołuje bezpośrednio zmiany HP lub zasobów.

Wejścia SI ograniczone własną mgłą wojny i pamięcią ostatnich obserwacji. Nie zna bieżących pozycji niewidocznych jednostek, kolejki budowy przeciwnika ani przyszłych zdarzeń. Rozpoznanie generuje hipotezy z czasem ważności. Decyzje o kontrach wynikają z obserwacji. Trudność: łatwa = wolniejsze decyzje i mniejsza koordynacja, normalna = pełne zasady, trudna = lepszy podział zadań. Brak ukrytych mnożników zasobów. Opcjonalny handicap jawny w UI i raporcie meczu.

Doktryny jednostek: bierna, samoobrona, agresywna, eskortuj, patroluj, broń obszaru, zbieraj, transportuj, napraw przy HP <30%, oszczędzaj załogę. Ręczny rozkaz ma pierwszeństwo do ukończenia lub anulowania. Awaryjna ucieczka gospodarcza nie prowadzi przez widoczny silniejszy oddział, jeśli istnieje bezpieczna droga. Brak bezpiecznej drogi oznacza zatrzymanie i alarm. Nie wolno przejąć ręcznego rozkazu bez oznaczonej przyczyny.

SI ekonomiczna realizuje E09: fizyczne dostawy, zapasy docelowe i odbudowę po rajdach według tych samych reguł co gracz. Nie zna zagrożeń pod mgłą. Przeciwnik buduje co najmniej jedną ścieżkę dochodu, eskortuje handlowce, atakuje zasoby, osłania bombardowce, osłabia załogi przed abordażem, wycofuje uszkodzone jednostki. Stosuje histerezę celu (minimum 3 s), aby nie przeskakiwał co krok. Trace diagnostyczny zapisuje obserwację, wybrany cel i przyczynę replanowania. QA sprawdza symetrię wiedzy i kosztów na seedowanych meczach.

## C09. Pogoda, woda i oświetlenie

System logiczny przechowuje wiatr (wektor), prąd, wysokość fali, widoczność, opad i stan pływu. Pogoda przechodzi między stanami łagodnie przez 30–90 s, z zapowiedzią sztormu co najmniej 20 s. Prąd dodaje do ruchu wektor ograniczony do 20% prędkości bazowej. Wiatr daje żaglom premię/karę do ±25%, nie blokuje napędu wiosłowego. Sztorm zwiększa rozrzut do +30% i zmniejsza widzenie do 60%; nie losuje śmierci sprawnego statku.

Rafy mają widoczne markery po odkryciu i logiczny obrys. Wir ma zewnętrzną strefę ostrzegawczą i wektor przyciągania; można go ominąć, opór zależy od napędu. Zasięg i siła nie wynikają z rozmiaru cząsteczek. Zdarzenia wymagane fabularnie zapowiedziane i zapisywane; po wczytaniu nie uruchamiają się drugi raz.

Ocean to HDRP Water, jeśli wybrana zablokowana wersja spełni testy kompatybilności. Oddziel daleką falę, średnie falowanie i małe detale normalnych. Barwa wynika z absorpcji, głębokości i oświetlenia: turkus przy brzegu, głębszy błękit na otwartym morzu. Wymagane: refrakcja, odbicia, piana brzegowa, wygaszanie fal przy brzegu, piana od dziobu, kilwater, smugi wioseł, rozpryski przy trafieniach i zanurzeniach. Unikaj przesadnego białego szumu piany. Kaustyka tylko na płytkim dnie, zgodna z kierunkiem światła, wyłączalna.

Kadłub wizualny pobiera wysokość i normalną powierzchni w kilku punktach; filtruje przechył z limitem 12° spokojnie i 25° sztorm. Współrzędna logiczna pozostaje stabilna, aby fala nie zmieniała zasięgu broni. Przy dużej odległości ogranicz próbkowanie i efekty. Współdziel próbki fal dla pobliskich jednostek, stosuj LOD piany i shaderów, pooling VFX. Statek nie może pływać nad powierzchnią ani znikać pod małą falą; QA obejmuje wszystkie kąty i odległości kamery.

Oświetlenie: fizyczne niebo i jedno główne słońce/księżyc; ograniczona liczba lokalnych świateł ognia i świątyń; spójne PBR drewna, brązu, tkanin i mokrych powierzchni. Cienie kontaktowe przy kadłubie i nabrzeżu. Na profilu Ultra odbicia i oświetlenie pośrednie korzystają z ray tracingu, po potwierdzeniu dostępności w zablokowanej wersji HDRP; SSR i sondy służą jako fallback. Profile Low, Medium, High i Extra High używają technik bez ray tracingu według V10 i muszą zachować czytelność oraz pełne reguły gry. Ekspozycja ograniczona, brak gwałtownych skoków po pojawieniu się ognia. Bloom nie zakrywa wskaźników. Noc klimatyczna, ale jednostki i brzegi czytelne; kolory frakcji uzupełnione symbolami.

Cykl dobowy konfigurowany per mapa, domyślnie 24 minuty. Deszcz i mgła oddziałują na oba zespoły zgodnie z tym samym polem środowiskowym. HUD ostrzega o zmianie widoczności. Ustawienia ograniczają błyski, kołysanie kamery, motion blur i cząsteczki; decyzje taktyczne nie mogą wymagać ich włączenia.

## C10. Kamera, sterowanie i UI

Kamera 3D: przesuwanie WSAD/krawędzie, obrót Q/E, zoom kółkiem, opcjonalny tilt; zakres wysokości 20–300 m, ograniczenia mapy, bez wejścia pod wodę i klify. Płynność zależna od czasu rzeczywistego, nie prędkości symulacji. Zaznaczenie kliknięciem/ramką, Shift dodaje, Ctrl+1–9 zapisuje grupy, 1–9 przywołuje, podwójne przywołanie centruje. PPM rozkaz kontekstowy z podglądem trasy, Shift kolejkuje, Escape anuluje tryb. Klawisze remapowalne. Menu zatrzymuje single player zgodnie z ustawieniem.

HUD: dostępne D/S, transport i rezerwacje oddzielnie, limit floty, minimapa z mgłą, panel zaznaczenia, cztery wskaźniki statku (kadłub, załoga, napęd, gotowość), zakładka wyposażenia i aktywnych statusów. Tooltip pokazuje koszt, cooldown, sektor, przyczynę braku dostępności. Ostrzeżenia grupowane i ograniczone częstotliwością. Kolor nie jest jedynym nośnikiem informacji. Skalowanie interfejsu 80–150%, PL/EN, brak tekstu zaszytego w obrazach. Ustawienia oferują pełną gamę rozdzielczości od 1920×1080 do 2560×1440 według V13 w VISUAL_TECH_SPEC.md; rozdzielczość nie zmienia reguł ani informacji taktycznej.

Dźwięk: osobne miksery muzyki, morza, bitwy, komunikatów i UI. Priorytet sygnałów tarana, utraty portu i alarmu konwoju. Maksymalna liczba nakładających się komunikatów; napisy rozkazów i zdarzeń fabularnych. Brak zależności mechanik od długości klipu.

## C11. Kampania i scenariusze

Kampania fabularna obejmuje czternaście misji opartych na epizodach mitologii greckiej, przełożonych na walkę, eskortę, abordaż, nawigację, ratunek i logistykę morską. MYTHIC_SCENARIOS.md definiuje początkowe floty, zasoby, cele, porażki, zdarzenia, kontrgrę, zapis oraz nagrody.

Kolejność kampanii:

1. M01 Popioły Ilionu — ewakuacja ocalałych i penatów z płonącego wybrzeża.
2. M02 Szpony nad Strofadami — załadunek zapasów pod nalotami Harpii.
3. M03 Noc odpłynięcia z Kartaginy — konwój wychodzi z portu przed pościgiem.
4. M04 Ogień na plaży Drepanum — ratowanie floty płonącej na kotwicy.
5. M05 Palinur u steru — nocna żegluga przy rafach i utrata sternika.
6. M06 Głos z Kume — osłona Sybilli podczas rytuału w jaskini nad morzem.
7. M07 Ujście Tybru — utrzymanie rzeki i kotwicowiska do wysadzenia osadników.
8. M08 Imię zapisane na falach — ewakuacja spod klifu Polifema.
9. M09 Wiatry wyrwane z worka — zebranie floty rozproszonej przez wiatry.
10. M10 Port, który stał się pułapką — odwrót z portu atakowanego przez Lajstrygonów.
11. M11 Sześć głów, jeden przesmyk — wybór drogi przy Scylli i Charybdzie.
12. M12 Przysięga pod słońcem — zaopatrzenie i odpłynięcie z Trynakii.
13. M13 Bóg, którego nie utrzymają liny — abordaż i uwolnienie Dionizosa z okrętu piratów.
14. M14 Andromeda: cień pod falą — ratunek Andromedy i walka z morskim potworem.

M01–M07 tworzą cykl Eneasza, M08–M12 cykl Odyseusza, a M13–M14 są osobnymi opowieściami. Nie przenoś między nimi floty, zasobów ani nagród.

Dane projektowe znajdują się w qa/scenario-design.json, a macierz odbioru w qa/SCENARIO_ACCEPTANCE.csv. Walidator qa/scenario-design-check.cjs sprawdza wyłącznie kompletność i strukturę danych; nie testuje nawigacji, implementacji ani balansu. Odbiór wymaga rzeczywistych testów i playtestów, które pozostają NOT_RUN do czasu wykonania.

40 potyczek: osiem biomów × pięć reguł (zniszczenie centrum dowodzenia; utrzymanie trzech punktów przez 180 s łącznie; dostarczenie trzech relikwii; eskortowanie trzech konwojów; obrona przez 20 minut). Każda kombinacja ma osobny identyfikator B01–B08/R01–R05, układ wysp, co najmniej dwie drogi operacyjne i seed. Samo przemianowanie tej samej mapy nie spełnia wymogu 40 scenariuszy. Równe odległości startowe w symetrycznych wariantach; scenariusze asymetryczne oznaczone. Konwój nie rusza bez osiągalnej trasy.

## C12. Architektura, zapis i wydajność

Warstwy: dane definicji (ScriptableObject lub równoważne), czysta symulacja, adaptery Unity, prezentacja/UI, narzędzia i testy. Rozkazy walidowane w jednym miejscu. Odpowiedzialność za jednostki i zasoby jawna, brak globalnej zmiennej zmienianej przez VFX. Oddziel asmdef dla runtime/editor/testów. Użyj stabilnych ID zamiast referencji do obiektów sceny w zapisie. Nie zakładaj deterministyczności Unity Physics między platformami; docelowy multiplayer wymaga osobnego wyboru authoritative server/lockstep po prototypie i pomiarach.

Zapis wersjonowany, atomowy (plik tymczasowy + podmiana), z kopią poprzedniego zapisu. Gospodarka przechowuje lokalne magazyny, faktycznie zapłacone ceny, transakcje, transporty, kolejki, stanowiska i wraki według E10. Zawiera jednostki, właścicieli, wyposażenie, załogi, rozkazy, kolejki i rezerwacje, RNG, czas, pogodę, mgłę wojny, misje, bohaterów, artefakty, cooldowny i boarding links. Ładowanie dwufazowe: obiekty, potem relacje; walidacja brakujących ID i bezpieczne odrzucenie niekompatybilnej wersji. Autosave co 5 min poza krytyczną operacją zapisu, trzy rotowane sloty. Wczytanie nie pobiera ponownie ceny ani nie wypłaca ponownie nagród.

Gra nie ma wymagań sprzętowych ani budżetu FPS ograniczającego jakość obrazu. Skala 400 statków, 100 pocisków i aktywnej pogody pozostaje wymogiem projektowym. Raportować informacyjnie p50/p95/p99 czasu CPU/GPU, szczyt RAM/VRAM, liczbę jednostek, ustawienia, wersję builda i sprzęt; wyniki nie obniżają jakości obrazu. Wymogami poprawności, niezależnymi od profilu grafiki, pozostają: brak narastania pamięci po 30 min oraz krok symulacji 30 Hz, który na sprzęcie testowym nie spowalnia czasu gry. Przekroczenie kroku optymalizuje się w samej symulacji, bez usuwania reguł. Pomiary bez edytora, po rozgrzaniu.

Spatial hashing dla sąsiedztwa, pooling pocisków/VFX, LOD jednostek i animacji, instancing powtarzalnych elementów, brak alokacji w gorących pętlach, rzadsza aktualizacja odległych detali. Nie implementuj rozbudowanego ECS wyłącznie z założenia; uzasadnij go pomiarami.

## C13. Kryteria odbioru i macierz testów

Każdy wymóg ma ID (numer rozdziału lub Cxx), implementację, test i artefakt dowodowy w docs/requirements.csv. Brak testu oznacza lukę; nie zapisuj PASS dla NOT_RUN, SKIPPED, BLOCKED lub INCONCLUSIVE. Testy automatyczne muszą mieć rzeczywiste asercje, a testy wizualne wymagają ręcznej oceny lub porównania o znanych ograniczeniach. Hook potwierdza automatyczne zestawy, nie jakość całej gry.

| Obszar | Obowiązkowe przypadki |
|---|---|
| Ekonomia | wszystkie E00–E12 i qa/ECONOMY_ACCEPTANCE.csv: zachowanie sum, lokalne rezerwacje, transport/utrata cargo, stanowiska i limity pracy, koniec złoża, wrak, remont, anulowanie, przejęcie, zapis i niezależne playtesty morskie |
| Nawigacja | 50 okrętów w cieśninie, dwa konwoje z przeciwnych stron, zablokowany port, różne zanurzenia, unreachable, śmierć lidera, odtworzenie formacji |
| Walka | sektory i martwa strefa, wyspa zasłaniająca cel, jeden taran na szarżę, dwa abordażowce, zatonięcie w kroku przejęcia, brak wiecznego stun |
| Magia | koszt raz, cooldown po save/load, niewidoczny cel, zniszczone sanktuarium, stacking, anulowanie rzucania, identyczne reguły SI |
| SI | brak wiedzy pod mgłą, brak darmowych zasobów, osiągalne cele, utrata dochodu, test 30-minutowy bez zakleszczeń |
| Zapis | roundtrip wszystkich stanów, uszkodzony plik, nieznana wersja, autosave przy pełnym dysku, brak duplikacji artefaktu |
| Grafika | dzień/noc/deszcz/sztorm, wszystkie zoomy, kilwater, styk brzegu, przejścia LOD, pięć profili Low–Ultra, Ultra z RT, czytelność w walce |
| Produkt | czysta instalacja builda Windows, tutorial, zwycięstwo/porażka, PL/EN, remap, UI scale, przełączanie rozdzielczości 1080p–1440p, zapis i powrót z menu |

EditMode: zasoby, obrażenia, statusy, walidacja rozkazów, serializacja i seedowane regresje. PlayMode: życie jednostki i sceny, kolizje, porty, formacje, integracja mgły wojny, UI i zapis. Osobno build Windows i smoke test uruchomionego programu. Automatyczny hook wymaga obu zestawów dla każdego zadania w domyślnej polityce; początkowo brak testów prawidłowo blokuje zamknięcie, aż pojawią się realne testy. Test konfiguracji hooka nie zastępuje testów gry.

Odbiór etapu: brak błędów kompilacji, zero failed/skipped/inconclusive w wymaganym zestawie, wszystkie wymagane testy istnieją i są wykonane, niezależny review implementacji i testów, aktualne artefakty dla niezmienionego snapshotu, znane ograniczenia ujawnione. P0/P1 otwarte blokują wydanie. Przed pełnym wydaniem przeprowadź testy kampanii 14/14 i potyczek 40/40; nie ekstrapoluj wyniku jednej mapy.
