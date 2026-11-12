# Morza Bogów — katalog mechanik

Dokument normatywny gry jednoosobowej w mitologicznej Grecji IV w. p.n.e. Pełna specyfikacja: [NOWY_PLAN_GRY.md](NOWY_PLAN_GRY.md). Kolejność integracji: [PLAN_WDROZENIA.md](PLAN_WDROZENIA.md). Mapa statusów opisuje zakres faktycznie obecny w kodzie, a nie procent ukończenia produktu.

Wykonanie każdej funkcji podlega także kontraktom [GRYWALNOSC_I_ODBIOR.md](GRYWALNOSC_I_ODBIOR.md), J01–J10. Trwałe zadania, powody odmowy, uczciwość AI, legalne drogi i zapis w pauzie są częścią odbioru funkcji, a nie osobnym opcjonalnym dodatkiem.

Oznaczenia: **JEST** — mechanizm rozpoznany w kodzie, wymagający zachowania i odpowiedniego odbioru; **CZĘŚĆ** — użyteczny fundament bez pełnego kontraktu; **DODAĆ** — brak odrębnego systemu. Parametry bez odczytanego pomiaru są decyzją projektu, nie faktem historycznym.

## 1. Gospodarka i port

| ID | Reguła docelowa | Stan integracji | Kryterium odbioru |
|---|---|---|---|
| E01 | Dwa wydawane zasoby: D — drewno i materiały portowe, S — srebro/budżet. Brak trzeciej waluty. | JEST: `ResourceType`, lokalne magazyny | Salda, rezerwacje i koszty sumują się w setnych jednostki. |
| E02 | Dwa statki gospodarcze wykonują zbiór, transport i pozyskiwanie znalezisk; różnią się prędkością oraz pojemnością. | CZĘŚĆ: łódź i handlowiec istnieją, oba wymagają zgodnej listy zdolności | Handlowiec potrafi zbierać dozwolone partie, łódź potrafi dostarczyć swój ładunek. |
| E03 | Punkty odnawiają partię ograniczoną liczbę razy, następnie są wyczerpane. Limit i opóźnienie są danymi mapy. | CZĘŚĆ: istnieje odnowienie pojemności | `RefillsRemaining` maleje dokładnie raz na partię; wczytanie nie odnawia limitu. |
| E04 | Rybołówstwo i inne towary są ładunkiem handlowym wymienianym w porcie na D/S; nie tworzą dodatkowego salda wydatków. | DODAĆ typy towarów i wycenę dostawy | Przechwycony towar nie daje pieniędzy przed sprzedażą/odbiorczą transakcją. |
| E05 | Neutralne, nieagresywne stworzenie morskie stanowi rzadki, skończony cel pozyskania z nagrodą D/S. | DODAĆ | Cel ma ID, postęp pozyskania, skończony budżet nagrody i konkurencję obu stron. |
| E06 | Wraki dają część wartości konstrukcji, rzeczywisty ocalały ładunek i ocalałe wyposażenie. | CZĘŚĆ: cargo i machiny są; odzysk konstrukcji po zatopieniu DODAĆ, odrębnie od istniejącego złomowania | Odzysk nie powiela wartości ani przedmiotów. |
| E07 | Naprawa kadłuba i modułów tylko w doku; płatność S, bez osobnej opłaty żywnościowej. | JEST | Zwykła stocznia nie naprawia; cena zależy od uszkodzenia i opłaconej podstawy. |
| E08 | Sprzedaż statku wymaga dokowania w uprawnionym porcie. Sprzedawać można własne obiekty z wyjątkiem fortów wyprawowych. | CZĘŚĆ: złomowanie statku | Pochodzenie obiektu określa możliwość sprzedaży; cargo nie znika. |
| E09 | Osobne magazyny, porty, fizyczny załadunek i przeładunek ludzi, broni i towaru. | CZĘŚĆ: D/S i część wyposażenia są; pełny transfer ludzi, artefaktów i obsady fortu DODAĆ | Ładunek trafia do właściwego odbiorcy, także po zmianie właściciela. |
| E10 | Automatyczne dostawy uwzględniają minimum źródła, zapotrzebowanie celu i cargo w drodze. | JEST: konwoje do 3 handlowców | Brak nadmiernych zamówień i podwójnej rezerwacji; rezerwacja miejsca celu wymaga domknięcia. |
| E11 | Zbieracz może pracować na wskazanej strefie obejmującej wiele punktów i wybiera kolejny dostępny punkt. | CZĘŚĆ | Wyczerpanie jednego punktu nie kończy zadania, jeżeli w strefie jest dostępny następny. |
| E12 | Blokada miejsca wodowania wstrzymuje wodowanie, zachowując opłacony statek w kolejce. | DODAĆ/zweryfikować fizyczną integrację | Przeciwnik może blokować wyjście, lecz nie niszczy zasobów samym zablokowaniem. |

E03/E10/E11 zachowują zadanie podczas oczekiwania na partię, miejsce lub drogę, automatycznie je wznawiają i zwalniają rezerwacje zgodnie z aktualną dostawą. Brak miejsca nie wymaga powtarzania tego samego rozkazu. Wybór alternatywnego portu i tolerancja ryzyka wynikają z zapisanej polityki, nie z ukrytej wiedzy automatu.

## 2. Budowle i rozwój

| ID | Funkcja | Kontrakt |
|---|---|---|
| B01 | Osada portowa | Zakłada i finansuje obiekty, szkoli wojowników, zarządza lokalnym zapasem i listą właścicieli. |
| B02 | Przystań | Transfer ludzi, towaru, machin i przedmiotów. Nie jest nowym globalnym magazynem. |
| B03 | Stocznia | Produkcja okrętów; jawne wymagania infrastruktury i dostępności miejsca wodowania. |
| B04 | Warsztat | Wytwarzanie modułów i 9 osi usprawnień: siła, cykl, zasięg dla obsady lekkiej, machin średnich i ciężkich. |
| B05 | Kuźnia | Wytwarzanie i usprawnianie trzech stopni osłony oraz trzech stopni taranu. |
| B06 | Ośrodek miejski | Awans osady po ukończeniu przystani, stoczni, kuźni i warsztatu oraz zapłaceniu kosztu. |
| B07 | Dok | Rozbudowa stoczni po uzyskaniu ośrodka miejskiego; naprawa kadłuba i modułów. |
| B08 | Ośrodek wiedzy | Dostęp do ciężkiego uzbrojenia, zaawansowanych osłon, taranów, napędu i specjalizacji. |
| B09 | Sanktuarium Morza | Nauka sześciu mocy akwenu i prawo ich użycia w określonym zasięgu. |
| B10 | Sanktuarium Wojny | Nauka sześciu mocy walki i ochrony. |

Warsztat, awans miasta, dok, ośrodek wiedzy i badanie ciężkiego uzbrojenia mają użyteczne fundamenty w `World.Arsenal.cs`. Kuźnia, sanktuaria, rozdzielone osie ulepszeń i pełny graf wymagań potrzebują rozszerzenia. Graf badań nie może mieć cyklu, w którym odblokowanie ciężkiej broni wymaga wyposażenia dostępnego dopiero po tym odblokowaniu.

### Forty

| ID | Rozmiar | Koszt limitu osady | Przeznaczenie |
|---|---|---:|---|
| F01 | Mały posterunek | 1 | Obserwacja i bliska osłona przystani. |
| F02 | Bateria przybrzeżna | 2 | Kontrola przejścia i osłona przeładunku. |
| F03 | Twierdza portowa | 3 | Silny, wolno rozwijany punkt obrony. |

Osada ma **15 punktów** na forty budowane z jej lokalnej puli. Dla liczebności n1, n2, n3 obowiązuje `n1 + 2n2 + 3n3 ≤ 15`. Fort z wyprawy morskiej ma odrębne pochodzenie, jest finansowany dostawą i nie korzysta z tej puli. Nie można go sprzedać. Zniszczenie fortu zwalnia dokładnie tę rezerwację, z której powstał.

Budowę wyprawową może zainicjować dowolny własny statek zdolny do wydania rozkazu przy brzegu. Zasoby, wyposażenie i obsada muszą pochodzić z określonego transportu albo zasięgu obsługi portu; okręt bojowy nie zyskuje przez to zwykłej ładowni gospodarczej. Twierdza zamówiona w osadzie może otrzymać opłacony zestaw domyślny; wyprawowa rozpoczyna bez uzbrojenia, a gracz dostarcza moduły i ludzi.

**Fortów nie można taranować ani abordażować.** Niszczy się je ostrzałem, mocami, odcięciem obsługi lub celem scenariuszowym przewidującym kapitulację. Spadek sprawnej obsady i modułów ogranicza ich ogień. Nie każda bezczynna ruina wymaga dobicia, chyba że warunek misji żąda jej zniszczenia. Fort przechowuje jeden artefakt, nie przyjmuje bohatera.

## 3. Katalog floty

Każda z dwóch grywalnych stron ma **10 ról**: 2 gospodarcze, 6 wspólnych bojowych i 2 specjalizacje. Wspólny katalog to `2 + 6 + 2×2 = 12` definicji ról, nie 20 odrębnych podstawowych kadłubów. Typ roli, profil kadłuba i wariant wyposażenia są rozdzielone.

| ID | Rola | Integracja / właściwości |
|---|---|---|
| N01 | Łódź przybrzeżna | `Cutter`; szybki zbiór, rozpoznanie, mały ładunek. |
| N02 | Statek kupiecki | `Merchant`; duży ładunek, pełny transfer i budowa wyprawowa, dostęp po awansie miasta. |
| N03 | Okręt przełamujący | `Ram`; manewr i taran, wyjście z kontaktu. |
| N04 | Okręt osłonowy | `CrewDestroyer`; ostrzał ludzi, ochrona jednostek gospodarczych. |
| N05 | Okręt dalekiego ostrzału | `ShipKiller`; kadłuby i moduły, utrzymanie właściwego dystansu. |
| N06 | Okręt oblężniczy | `Bomber`; silny przeciw umocnieniom, powolny, ograniczony minimalnym zasięgiem. |
| N07 | Okręt szturmowy | `Boarder`; silny oddział abordażowy, mała mobilność. |
| N08 | Okręt dowodzenia | DODAĆ konfigurację; uniwersalna obsada, bohaterowie, dwa miejsca artefaktów i prawo flagowca. |
| N09 | Nosiciel Płomienia | Specjalizacja A: wbudowane, niewyjmowalne stanowisko ognia o statusie fikcji mitologicznej. |
| N10 | Okręt Białej Mgły | Specjalizacja A: czasowe obezwładnienie statku lub budowli; jedno stanowisko wbudowane i możliwość dodatkowego modułu. |
| N11 | Okręt Echa | Specjalizacja B: zakłócenie rozkazów i wabienie w strefie; parametry wymagają odbioru. |
| N12 | Okręt Żałobny | Specjalizacja B: duża odporność na abordaż i wyraźna podatność na taran. |

A i B są identyfikatorami dwóch fikcyjnych stronnictw portowych, opisanych w planie gry. N11–N12 są kompletnymi decyzjami projektu wymagającymi balansu; nie przypisujemy im historycznej prawdziwości.

Statki gospodarcze nie mają taranu ani zwykłych stanowisk ofensywnych i nie rozpoczynają abordażu. Przewożona obsada może się bronić. Okręty bojowe nie zbierają zwykłych zasobów i nie przewożą masowego cargo D/S; osobno dopuszczają wyposażenie, bohaterów, przedmioty i ludzi ratowanych zgodnie z rolą. Jednostki gospodarcze mają zdolność przejścia przez płycizny określoną zanurzeniem; nie unieważnia to suchego lądu ani zbyt małej głębokości.

## 4. Ludzie, broń i walka

| ID | Mechanika | Reguła |
|---|---|---|
| C01 | Trzy profesje bojowe | Łucznicy: skuteczni na dystans; oszczepnicy: dystans i abordaż; miecznicy: walka na pokładzie. Żeglarze pozostają odrębną rolą techniczną. |
| C02 | Doświadczenie | Wpływa na celność i skuteczność abordażu; nie powstaje z farmienia własnych napraw. |
| C03 | Sześć rodzin modułów miotających | Lekki miotacz bełtów, łuk mechaniczny, miotacz kamieni, ciężki miotacz bełtów, ciężki miotacz jednoramienny, ciężki miotacz pocisków. Siedem istniejących typów machin mapujemy na te rodziny; DeckBallista i LeadBallista pozostają wariantami rodziny ciężkich bełtów, z zachowaniem ich ID i danych. |
| C04 | Dwa moduły specjalne | Ogień i obezwładniająca mgła, zamontowane tylko na przypisanych wariantach. Nie można ich zdjąć i przenieść na inny kadłub. |
| C05 | Trzy stopnie taranu | Każdy ma własny koszt i mnożnik obrażeń; stopnie nie oznaczają pełnych taranów wykonanych ze szlachetnych metali. |
| C06 | Trzy stopnie osłony | Każdy ma własny koszt i redukcję określonego kanału obrażeń. Magia i taran mają osobne kanały. |
| C07 | Domyślne wyposażenie | Przełącznik produkcji z gotowym zestawem albo bez niego; brak modułów zmniejsza koszt, nie tworzy darmowej broni. |
| C08 | Przeładunek modułów i ludzi | W przystani, stoczni, doku oraz do twierdzy. Zajmuje czas i wymaga fizycznej dostępności. |
| C09 | Ostrzał | Pociski, łuki, lot, rozrzut, obrażenia kadłuba, obsady i modułu; odrębne działanie przeciw budynkom. |
| C10 | Taran | Wymaga prędkości zbliżania, kursu, kontaktu i odzyskania miejsca po uderzeniu. Nie działa na forty. |
| C11 | Abordaż | Kontakt burt, mała względna prędkość, sczepienie, rundy walki, utrata ludzi i zdobycie albo porażka. |
| C12 | Przegrany szturm | Atakujący traci skierowaną do szturmu załogę bojową, pozostając z żeglarzami i bez zdolności ponownego szturmu do uzupełnienia. Jest to ostra reguła gry, nie uniwersalny fakt historyczny. |
| C13 | Zdobycz | Zmienia właściciela z wyposażeniem, stanem i przedmiotami; wymaga załogi pryzowej i ponownego ustawienia doktryny. |
| C14 | Rozdzielenie napędu i walki | Nie zwiększamy samego MaxCrew; minimalna obsada i ceny mają być nieujemne. Reprezentacja ludzi pozostaje abstrakcją do czasu pełnego modelu. |

Macierz podstawowych kanałów: profesje bojowe i ich zwykły ostrzał uszkadzają **ludzi**, nie kadłub ani moduł. Machiny używają jawnego pakietu ludzi/kadłuba/modułu z definicji. Taran działa na kadłub i wybrane skutki kontaktu. Moce i ogień używają własnych kanałów. Przy migracji istniejących ArcherBank i JavelinBank nie wolno zostawić ich niewielkich obrażeń konstrukcyjnych przez przypadek; test katalogu sprawdza zero w tych polach.

W świecie materialnym mechaniczne machiny oceniamy według daty i miejsca poświadczenia. Konfiguracje niepoświadczone, broń nadprzyrodzona i niemożliwe efekty są oznaczone jako element fikcyjny. Taki status nie odbiera im dostępności w grze mitologicznej i nie tworzy fałszywego świadectwa historycznego.

## 5. Autonomia jednostek

Cztery niezależne flagi: **obrona ogniowa**, **zgoda na taran**, **zgoda na abordaż**, **odpowiedź ogniem na agresora**. Wyłączenie obrony ogniowej blokuje automatyczną odpowiedź ogniową, lecz nie samą możliwość wykonania ręcznego, legalnego rozkazu strzału.

Pięć zadań przestrzennych:

| ID | Zadanie | Warunek końca i granice |
|---|---|---|
| A01 | Eskorta grupy | Osłania wskazane jednostki, nie ściga bez końca poza strefą ochrony. |
| A02 | Zwiad z powrotem | Dopływa do wskazanego obszaru i wraca do pozycji wyjściowej; po napotkaniu ataku może przerwać i wrócić. |
| A03 | Patrol | Cykliczna lista punktów z jawną kolejnością i polityką reakcji. |
| A04 | Obrona obszaru | Pozostaje w zadanym prostokącie/poligonie, reaguje na wykryte cele. |
| A05 | Atak w obszarze | Wybiera legalne cele w strefie według wyposażenia i doktryny. |

Ręczny rozkaz ma pierwszeństwo. Rozstrzygnięcie kilku flag musi być deterministyczne: najpierw ochrona celu zadania, następnie legalność i użyteczność metody, potem preferencja gracza. Skład ustawień zapisujemy wraz z zadaniem, trasą i pozycją powrotu. Osobno zachowujemy gotowe konwoje transportowe; eskorta grupy nie jest ich synonimem.

## 6. Dwanaście mocy

Każda moc wymaga nauki, kosztu, legalnego celu, dostępnego sanktuarium, ewentualnego zasięgu, czasu trwania i zapisanej fazy. „Cała mapa” oznacza zasięg użycia, a nie automatyczne ujawnienie celu spod mgły.

| ID | Nazwa | Efekt | Zasięg |
|---|---|---|---|
| M01 | Wir Posejdona | Tworzy niebezpieczny wir w wybranym akwenie. | lokalny od sanktuarium |
| M02 | Zęby Morza | Tworzy rafę stałą albo okresowo zanurzaną. | lokalny |
| M03 | Oddech Boreasza | Wskazuje kierunek wiatru albo ciszę na określony czas. | cała mapa |
| M04 | Kolumna Nereusa | Tworzy przemieszczającą się trąbę wodną o ograniczonym życiu. | lokalny, większy od M01 |
| M05 | Spojrzenie Ateny | Ujawnia wybrany obszar na określony czas. | cała mapa |
| M06 | Strażnik Głębin | Przywołuje stworzenie atakujące wskazane wrogie cele. | lokalny |
| M07 | Żar Aresa | Czasowo wzmacnia siłę własnego uzbrojenia. | cała mapa |
| M08 | Grom Zeusa | Niszczy niechronione cele przeciwnika w strefie i może zmienić maskę terenu. | lokalny |
| M09 | Deszcz Hefajstosa | Utrzymuje strefę kolejnych ognistych uderzeń przez określony czas. | lokalny |
| M10 | Zasłona Hekate | Czyni wskazany obiekt niewidocznym dla zwykłej obserwacji na czas efektu. | lokalny |
| M11 | Aigida | Redukuje obrażenia zwykłej broni o 80% w określonym czasie. | lokalny |
| M12 | Pieczęć Hermesa | Chroni przed wskazanymi oddziaływaniami magicznymi według macierzy odporności. | lokalny |

Lokalne zasięgi są liczone w metrach/krokach siatki rozgrywki, nie w nieokreślonych „polach”. Wszystkie parametry znajdują się w jednym katalogu. Nie ustalamy jednego promienia dla wszystkich mocy, jeżeli różne zadania wymagają odmiennej przestrzeni reakcji.

Macierz M12: neutralizuje Grom, Żar oraz szkodliwy wpływ gorgony; śpiew syreny nadal przyciąga, ale jego kanał obrażeń jest blokowany. Obrażenia przywołanego stworzenia, ognistej strefy i trąby są zredukowane o 80%. **Rafy i wiry pozostają groźne.** Wzmocnienie M07 korzysta z kanału, którego dodatkową część M12 może zneutralizować; zwykła broń nadal działa.

M11 i M12 chronią różne kanały. Dwie ochrony 80% nie stają się 160% ani nie leczą. Do tego samego kanału stosujemy najsilniejszą aktywną ochronę tej samej rodziny. Oddziaływania na posiadacza pełnej niewrażliwości bohatera rozstrzyga macierz priorytetów opisana w planie gry.

## 7. Dziewięć specjalizacji bohaterów

Bohater jest unikalną postacią lub mityczną manifestacją, przypisaną do okrętu wojennego. Nie jest zwykłym modułem kopiowanym przy wodowaniu. W kampanii wymaga zdobycia właściwego znaku i wykonania warunku przyłączenia. Nie można go przechowywać w forcie ani na jednostce gospodarczej.

| ID | Specjalizacja | Dosłowny efekt w świecie gry |
|---|---|---|
| H01 | Pilot Przesmyków | Okręt nie wpada w wir i nie otrzymuje uderzenia rafy. |
| H02 | Strateg Ateny | Wzmacnia siłę i zasięg uzbrojenia obsadzonego okrętu. |
| H03 | Pieśniarz Delfinów | Naturalne i mityczne stworzenia z określonej listy nie inicjują ataku na jego okręt. |
| H04 | Posłaniec Boreasza | Uniezależnia prędkość od niekorzystnego wiatru i chroni przed trąbą wodną. |
| H05 | Heraklida | Czyni okręt niewrażliwym na obrażenia; nadal nie zapewnia teleportacji, przejścia przez ląd ani automatycznego wykonania celów. |
| H06 | Cieśla Hefajstosa | Odnawia stan kadłuba i wzmacnia odporność przeciw stworzeniom. |
| H07 | Łuczniczka Artemidy | Poprawia celność i cykl strzału. |
| H08 | Tłumacz Pieśni | Chroni przed syrenami i harpiami według macierzy celów. |
| H09 | Nosiciel Lustra | Chroni przed spojrzeniem gorgony. |

Dokładne imiona, dialogi i przypisanie do mitów otrzymają osobne karty fabularne. Powyższe są funkcjami projektu; nie przedstawiamy ich jako jednolitego kanonu religijnego epoki.

## 8. Przedmioty i rekrutacja

Jedenaście kategorii użytkowych:

| ID | Kategoria | Funkcja |
|---|---|---|
| T01 | Znak Odnowy | Automatycznie odtwarza stan kadłuba, modułów i obsady do zapisanych limitów. |
| T02 | Depozyt towarowy | Jednorazowo dostarcza określony ładunek D/S po podjęciu i rozliczeniu. |
| T03 | Skrzynia uzbrojenia | Dostarcza określony moduł, nie losuje go ponownie po wczytaniu. |
| T04 | Naczynie mocy | Pozwala użyć wskazanej zdolności w granicach posiadanych ładunków albo katalogowej reguły. |
| T05 | Pieczęć płomienia | Ochrona przed kanałem ognia. |
| T06 | Znak osłony | Zwiększa osłonę w określonym kanale. |
| T07 | Znak szlaku | Poprawia prędkość i skuteczność taranu. |
| T08 | Więzy pokładu | Ochrona przed przejęciem w abordażu. |
| T09 | Maska strażnicza | Odporność na ogień i oddziaływanie gorgony. |
| T10 | Czara życia | Przywraca stan załogi bojowej zgodnie z regułą odnowy. |
| T11 | Dokument warsztatowy | Odblokowuje technologię dopiero po fizycznym dowiezieniu do uprawnionej osady. |

Osobno istnieje **9 unikalnych znaków rekrutacyjnych R01–R09**, po jednym dla specjalizacji H01–H09. Przedmiot rekrutacyjny może być kluczem zadania, nie pasywną premią. Przedmioty mogą znajdować się na wodzie, na statku, w forcie, osadzie albo u strażnika; zniszczenie nośnika stosuje jawny przepis zrzutu.

Pojemność: zwykły statek **1**, okręt dowodzenia **2**, fort **1**, miasto bez limitu wynikającego z reguły rozgrywki. Ograniczenia zwykłego cargo są niezależne. Przenoszenie między misjami dotyczy przedmiotów w uprawnionym flagowcu i ewakuowanych magazynach osad; rzeczy pozostawione na pozostałych jednostkach nie przechodzą automatycznie. Ekran zakończenia pokazuje listę przenoszonych i traconych przedmiotów przed zatwierdzeniem.

## 9. Zagrożenia, strażnicy i ośrodki odtwarzania

| ID | Archetyp | Zachowanie i kontrgra |
|---|---|---|
| Z01 | Amazonka-strażniczka | Razi załogi łukiem; element mitologicznego spotkania, nie opis wszystkich kobiet epoki. |
| Z02 | Cyklop | Miota kamienie na zbliżające się cele; zniszczenie siedziby kończy odtwarzanie. |
| Z03 | Byczy eidolon | Atak pociskami/bliski ostrzał; zniszczenie siedziby usuwa źródło zagrożenia. |
| Z04 | Syrena | Przyciąga ku skałom i wirom; kontrą jest trasa, ochrona i przerwanie ekspozycji. |
| Z05 | Gorgona | Czasowe unieruchomienie; wspólny budżet spojrzenia rozdzielany między cele. |
| Z06 | Harpia | Atakuje ludzi i odtwarza się, dopóki istnieje gniazdo. |
| Z07 | Drakon | Ruchome źródło przywołań na lądzie i wodzie; źródłem trwałości jest określona siedziba. |
| Z08 | Krokodyl mityczny | Zagrożenie wodne; nie twierdzimy, że jest zwykłą fauną Attyki. |
| Z09 | Wielki miecznik | Atak na kadłub jako zjawisko fantastyczne, nie zoologiczny opis ryby. |
| Z10 | Rekin-strażnik | Obszarowy drapieżca z ograniczoną strefą pościgu. |
| Z11 | Wąż morski | Ciężki strażnik akwenu, cel do ominięcia albo pokonania. |

Dwa dodatkowe ośrodki mocy: **Przybytek Wiatru** tworzy niekorzystny wiatr i trąby, **Przybytek Ognia** strefy ognistych uderzeń. Każdy jest odporny na własny kanał, działa według zasięgu i kończy oddziaływanie po zniszczeniu źródła. Reakcje obejmują obie strony zgodnie z katalogiem, nie ukrytą pomoc przeciwnikowi gracza.

Trzecia siła — **wolne flotylle rozbójnicze** — ma pięć konfiguracji jednostek i trzy stopnie fortów. Środkowy fort łączy administrację, przeładunek i naprawę. Nie ma pełnego drzewa technologii ani sanktuariów. Przy przegranym abordażu jej okręt zostaje samozatopiony zamiast przejęty; reguła jest widoczna przed rozpoczęciem szturmu i przypisana do typu, a nie dowolnie stosowana przez AI.

Nieograniczone odtwarzanie jest dopuszczalne tylko dla jawnie wskazanego źródła, którego zniszczenie zatrzymuje proces. Posiłki wojskowe i nagrody ekonomiczne mają odrębne, skończone manifesty. Unieszkodliwienie potwora nie może zwrócić więcej nagród po każdym odrodzeniu.

## 10. Kampania, potyczka i sterowanie

- Kampania odblokowuje kolejno budynki, konfiguracje, badania i moce. Dostępność jest stanem kampanii i może być węższa od katalogu potyczki.
- Potyczka jest lokalną grą jednego człowieka przeciw AI z opcjonalnymi flotyllami i zagrożeniami mapy. Nie ma trybu gry sieciowej.
- Grupy jednostek, pozycje zapamiętane kamery, mapa, pauza, zapis/wczytanie, alarm ostatniego ataku i regulacja tempa działają przez obecny system przypisań klawiszy.
- Skrypty misji definiują cele, porażkę, posiłki, warunki przejścia i listę przenoszonego stanu. Nie ma niewykrytej utraty niezbędnego przedmiotu.
- Ręczny zapis, pauza i zmiana tempa nie pozwalają powtórnie naliczyć badań, kosztów, nagród ani przywołań.

Pauza dopuszcza planowanie kolejnych zadań i zapis Pending bez wykonania ticku. Zwykłe zastąpienie, jawne dopisanie i anulowanie to odrębne operacje. Wynik komendy oraz powód oczekiwania są dostępne w istniejących elementach interfejsu. Sloty ręczne i automatyczne nie nadpisują się nawzajem.

Kampania zachowuje osobno zatwierdzony postęp, rezerwę i manifest wyprawy. Niewysłane zasoby rosteru pozostają w rezerwie; ponowienie porażki odtwarza snapshot wejścia misji. Odczyt nie duplikuje jej nagrody. Cele opcjonalne oraz niepowiązane jednostki nie blokują rozstrzygnięcia wymaganych ObjectiveID.

Każdy ID katalogu wymaga przypisania do implementacji, istotnego testu i artefaktu odbioru. Status **DODAĆ** nie może zostać zmieniony na **JEST** na podstawie samego opisu, makiety albo liczby przygotowanych plików.
