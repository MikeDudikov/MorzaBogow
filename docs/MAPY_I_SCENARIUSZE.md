# Morza Bogów — mapy i scenariusze

Ramę wszystkich operacji ustanawia wyłącznie [nadrzędny plan gry](NOWY_PLAN_GRY.md): mitologiczna Grecja IV w. p.n.e. (400–301 p.n.e., z 400 r. włącznie). Nie ma obowiązkowego węższego okna całej kampanii. Każda karta misji potwierdza zgodność z epoką i określa dokładniejszą datę tylko wtedy, gdy wymaga jej dostępność materialna lub scenariusz. Lokalne dane nie mogą ustanawiać innego okresu produktu.

Gra jednoosobowa w mitologicznej Grecji IV w. p.n.e. Materiał historyczny opisuje świat materialny; nadprzyrodzone wydarzenia są fikcją osadzoną w tradycjach greckich. Reguły wspólne: [plan gry](NOWY_PLAN_GRY.md) i [katalog mechanik](KATALOG_MECHANIK.md).

Operacje podlegają J01–J10 z [GRYWALNOSC_I_ODBIOR.md](GRYWALNOSC_I_ODBIOR.md): mają legalne przejścia, czytelne cele, jawne manifesty i jednokrotne rozliczenie kampanii.

## 1. Kontrakt przestrzeni

Mapa jest **akwenem operacyjnym**, nie całym regionem w miniaturze. Woda stanowi większość pola gry. Ląd służy jako brzeg portu, wyspa z zasobem, przyczółek, miejsce twierdzy lub siedziba strażnika. Nie budujemy odrębnego frontu lądowego. Miasta są zapleczem floty i widocznymi celami operacji.

Każdy sektor zawiera:

- osłonięte miejsca startu i wodowania;
- przynajmniej dwie drogi działania przy głównym celu;
- odcinki otwartej wody dla szyku, taranu, odwrotu i eskorty;
- płycizny dostępne jednostkom o mniejszym zanurzeniu;
- zróżnicowane zasięgi fortów, zaklęć i obserwacji;
- ograniczone punkty zbioru i nowe partie z jawnym pochodzeniem;
- miejsca znalezisk, rekrutacji i zagrożeń;
- przewidywalny sposób rozpoznania drogi, zanim gracz bezpowrotnie zaangażuje całą flotę.

Wyspy nie mogą tworzyć jednej obowiązkowej ścieżki, która pozostaje pod pełnym ogniem twierdzy i niedającym się ominąć zaklęciem bez dostępnej kontrgry. Cel może być strzeżony, ale warunek przejścia musi być wykonalny przez dostępne jednostki, moce, bohaterów albo inne działanie operacyjne.

## 2. Rozmiary i proporcje do prototypowania

Wymiary i udziały poniżej są **parametrami projektu**, a nie historycznymi pomiarami regionu. Korzystamy z obecnej domeny mgły wojny 104×104 komórek po 25 m: `104×25 = 2600 m`. Domena modelu morza również wymaga sprawdzenia względem granicy sektora; sam większy obraz nie rozszerza nawigacji.

| Profil | Bok sektora | Powierzchnia | Użycie |
|---|---:|---:|---|
| Zatoka | 1600 m | 2,56 km² | Pierwsza eskorta, walka o wejście, krótszy scenariusz. |
| Archipelag | 2200 m | 4,84 km² | Zwykła potyczka, dwie drogi, przyczółki i strażnik. |
| Operacja | 2600 m | 6,76 km² | Wieloetapowa misja z rezerwą i kilkoma ośrodkami. |

Roboczy zakres: **85–90% wody**, **10–15% lądu**. Płycizny stanowią **10–20% powierzchni wody**, a nie dodatkową część sumy. Zagrożenia są nakładką i nie powiększają powierzchni mapy.

Przykład Archipelagu przy 85% wody i płyciznach równych 15% wody:

`A_water = 4,84×0,85 = 4,114 km²`;

`A_land = 4,84−4,114 = 0,726 km²`;

`A_shoal = 4,114×0,15 = 0,6171 km²`;

`A_deep = 4,114−0,6171 = 3,4969 km²`.

W rezultacie ląd + płycizny + głęboka woda dają dokładnie 4,84 km². Odcinki brzegu dekoracyjnego nie zwiększają powierzchni lądu logicznego ponad maskę kolizji. Dostępność płycizny ustala zanurzenie i reguła bezpieczeństwa.

Nie zmieniamy rozmiaru bieżącej mapy w zadaniu stabilizacji. Nowe układy powstają jako osobne dane scenariusza, z istniejących generatorów i zasobów.

## 3. Geografia i typ reprezentacji

Każda mapa otrzymuje jeden typ:

- **Sektor geograficzny:** wycinek rzeczywistego akwenu, z udokumentowanymi brzegami i jawnie oznaczonymi niepewnościami linii brzegowej IV w.
- **Sektor schematyczny:** rozpoznawalna relacja brzegów, zatok i wysp, lecz odległości oraz rozmiary są skompresowane dla gry.
- **Sektor mityczny:** fikcyjny akwen wykorzystujący konkretny grecki motyw; nie ma fałszywych współrzędnych geograficznych.

Nie przedstawiamy całej Salaminy, trzech portów Pireusu, Delos i Sunionu jako obiektów odległych o kilkaset metrów na jednej mapie geograficznej. Przejście między takimi miejscami odbywa się między sektorami kampanii.

Rzeczywista obecność sanktuarium nie dowodzi, że w IV w. miało ono każdą później opisaną budowlę. Dotyczy to także portów i faz hangarów. Źródła dla datowania znajdują się w [HISTORIA_I_ZRODLA.md](HISTORIA_I_ZRODLA.md).

## 4. Katalog kampanii

Kampania zawiera **14 operacji** o różnych celach, zamiast czternastu wariantów niszczenia bazy. Ich przebieg jest fikcyjny. Numery określają kolejność projektową; manifest kampanii może odblokowywać operacje poboczne, nie powielając całych map dla każdej decyzji.

| ID | Miejsce / forma | Motyw | Cel operacyjny | Nowa reguła |
|---|---|---|---|---|
| K01 | Podejście do Pireusu, sektor 1600 m | Ochrona portu i przydział materiałów | Doprowadzić dwa transporty i rozładować wymagany towar | Ruch, ostrzał, przeładunek i eskorta |
| K02 | Wybrzeże Thorikos–Laurion, wycinek 2200 m | Praca zaplecza i prawo do partii srebra | Utrzymać odbiór skończonych partii i wyprowadzić budżet | Zbiór strefowy, rezerwacje i wyprawowa baza |
| K03 | Wybrane wejście zatoki Zea, sektor 1600 m | Rzemiosło i patronat Ateny | Ukończyć wyposażenie, odeprzeć atak i nie zablokować wodowania | Warsztat, kuźnia, zestaw domyślny i dok |
| K04 | Przejście koło Salaminy, sektor 2200 m | Ajas i pamięć bohatera | Zdobyć dokument z wrogiego okrętu i dowieźć go do osady | Abordaż, załoga pryzowa i przedmiot kluczowy |
| K05 | Zatoka Eleusis, sektor 2200 m | Demeter i obieg świętego zboża | Ochronić konwój przed strażnikami i dostarczyć dar | Artefakty, społeczny kontekst sanktuarium i pierwszy znak rekrutacyjny |
| K06 | Przylądek Sunion, sektor 2600 m | Posejdon i ryzyko opłynięcia przylądka | Przepłynąć przez zmieniające się zagrożenia, ocalić flagowiec i minimum floty | Sanktuarium Morza, wir, rafa i pilot |
| K07 | Podejście do Brauron, sektor 1600 m | Artemis i ochrona świętego miejsca | Odbić dar bez zniszczenia wskazanych obiektów sanktuarium | Zwiad, selekcja celów i postać łucznicza |
| K08 | Wycinek wybrzeża Eginy, sektor 2200 m | Ajakos i straż wyspy | Unieszkodliwić dwa ośrodki odtwarzania i zdobyć przyczółek | Niezależne flotylle, trzy klasy fortów i niszczenie źródła |
| K09 | Schematyczny sektor okolic Delos, 2200 m | Apollo i morska droga do sanktuarium | Rozpoznać prawdziwy szlak, zabezpieczyć eskortę poselstwa | Ujawnienie, niewidzialność, patrol i ochrona obszaru |
| K10 | Zatoka Powracających, sektor mityczny 2200 m | Syreny i próba rozpoznania zwodniczej obietnicy | Odzyskać wskazany przedmiot, nie tracąc wszystkich statków do wabienia | Śpiew, osłony i bohater odporny na wpływ |
| K11 | Brzeg Lustrzanej Skały, sektor mityczny 1600 m | Gorgona i spojrzenie pośrednie | Zabrać znak z trzech punktów albo przełamać straż lustrem | Budżet spojrzenia, rozdział celów i H09 |
| K12 | Wyspy Kowala, sektor mityczny 2600 m | Hefajstos, ogień i praca nad materią | Zniszczyć Przybytek Ognia i utrzymać transport wyposażenia | Pełna artyleria, ogień, mgła obezwładniająca i odnowa |
| K13 | Przejście Boreasza, sektor mityczny 2200 m | Wiatr, trąby i powrót z wyprawy | Wyprowadzić ruchomy cel i artefakty przed końcem okna | Wiatr logiczny, odporności i pozycja powrotu |
| K14 | Podejście do Attyki, odrębny sektor 2600 m | Konflikt stronnictw i próba utrzymania przysięgi | Zrealizować złożony cel żeglugi albo przełamać główny ośrodek przeciwnika | Cały katalog, niezależna trzecia siła i stan kampanii |

K04–K09 mają rzeczywiste nazwy miejsc i motywy wymagające lokalnych kart źródłowych. K10–K13 nie ustanawiają lokalizacji epizodów heroicznych na współczesnej mapie. Ich topografia i połączenia są fikcyjne. K14 nie jest rekonstrukcją nazwanej bitwy historycznej.

### Konstrukcja stanów początkowych

Każda operacja deklaruje: datę fikcyjnej akcji, profil sektora, pozycje portów i wyjść, początkową flotę, budżet D/S, partie zasobów, dostępny graf badań, bohaterów, przedmioty, ośrodki zagrożeń i wiedzę startową obu stron. Dane odczytuje wykonawca scenariuszy, nie rozproszone instrukcje w kodzie prezentacji.

Misje K01–K03 tworzą pierwszy kompletny wycinek kampanii. Ich odbiór nie wymaga gotowości wszystkich mitycznych stworzeń. K06–K13 muszą jednak pozostać jawnymi zadaniami pełnego zakresu, z odpowiednimi zależnościami; nie wolno uznać ich za wykonane po przygotowaniu samych opisów.

### Predykaty zakończenia operacji

Poniższe są kontraktami wykonawcy, nie gotowymi danymi poziomów. Każdy parametr ilości, obszaru, terminu, trasy i obiektu musi otrzymać wartość w ScenarioDefinition przed uruchomieniem misji. Wymagane ilości są dodatnie; brak danych blokuje start zamiast automatycznie zaliczać cel.

`Delivered(DeliveryId, RequiredManifest, RecipientId)` oznacza faktyczny odbiór całego wymaganego manifestu przez wskazanego uprawnionego odbiorcę. DeliveryId wiąże dozwolonych przewoźników i jednokrotne zapisy dostaw; manifest może określać zasoby, osoby lub unikalne przedmioty. `Exited(manifest, zone)` wymaga przekroczenia strefy właściwymi obiektami. `Resolved(wave)` dotyczy skończonej wskazanej grupy, której członkowie zostali zniszczeni, legalnie przejęci przez gracza, poddali się albo wycofali przez ustalone wyjście; nie oznacza wszystkich wrogów mapy. Przejęcie oceniamy po zmianie właściciela, bez wymagania dodatkowego zdarzenia kapitulacji. Operator AND wymaga obu warunków, OR dopuszcza którąkolwiek pełną gałąź.

| Operacja | Wymagany predykat sukcesu | Przykład krytycznej porażki |
|---|---|---|
| Pireus (K01) | Delivered(DostawaT1, ManifestT1, PortOdbioru) AND Delivered(DostawaT2, ManifestT2, PortOdbioru) | Niezastępowalny transport i jego ładunek utracone bez legalnego odzysku. |
| Laurion (K02) | Delivered(DostawaPartii, WymaganyProdukt, PortPrzeładunku) AND Exited(BudżetWyjścia, Strefa) | Pozostałe partie i odzysk nie wystarczają do wymaganej ilości. |
| Zea (K03) | InfrastructureReady(Wymagane) AND LoadoutsReady(Eskadra) AND Resolved(W1) | Utrata niezastępowalnego punktu rozwoju albo brak legalnej możliwości wyposażenia eskadry. |
| Salamina (K04) | Delivered(DostawaDokumentu, DokumentId, OsadaOdbiorcza) | Dokument bezpowrotnie utracony, bez zastępczego celu. |
| Eleusis (K05) | Delivered(DostawaKonwoju, DarId, PortSanktuarium) | Utrata wymaganego daru bez możliwości odzysku. |
| Sunion (K06) | Exited(Flagowiec, Strefa) AND Exited(MinimumFloty, Strefa) | Flagowiec utracony albo ocalały manifest nie może spełnić minimum. |
| Brauron (K07) | Delivered(DostawaDaru, DarId, PortId) AND ProtectedObjectsIntact(ZbiórId) | Zniszczenie jawnie chronionego, niezastępowalnego obiektu. |
| Egina (K08) | SourcesDisabled(S1, S2) AND OutpostOperational(P1) | Brak możliwości odtworzenia wymaganego przyczółka przy pozostałym budżecie i dostępie. |
| Delos (K09) | RouteObserved(R1) AND Delivered(DostawaPoselstwa, PoselstwoId, PortId) | Utrata niezastępowalnego poselstwa. |
| Zatoka Powracających (K10) | Delivered(DostawaPrzedmiotu, PrzedmiotId, PortWyjścia) | Utrata ostatniego legalnego nośnika lub przedmiotu bez drogi odzysku. |
| Lustrzana Skała (K11) | Delivered(DostawaZnaku, ZnakR09, PortWyjścia), zdobyty dozwoloną gałęzią | Brak znaku i brak możliwości wykonania którejkolwiek gałęzi. |
| Wyspy Kowala (K12) | Destroyed(PrzybytekOgniaId) AND Delivered(DostawaWyposażenia, Wyposażenie, PortOdbioru) | Utrata niezastępowalnego wyposażenia bez dozwolonego zastępstwa. |
| Przejście Boreasza (K13) | Exited(CelRuchomyId, Strefa) AND Exited(Artefakty, Strefa), przed Deadline | Upływ terminu albo utrata krytycznego celu bez zastępstwa. |
| Podejście do Attyki (K14) | FlagshipAlive AND ((Delivered(DostawaKonwojów, Manifest, PortOdbioru) AND Exited(Ewakuowani, Strefa)) OR Destroyed(GłównyPortCelu)) | Utrata flagowca albo konkretnie wykazana niewykonalność obu gałęzi. |

K11 ma dwie drogi do tego samego unikalnego R09: trzy punkty zadania składają znak albo dostępny wcześniej przedmiot T09 pozwala przełamać straż i go odzyskać. H09 można zrekrutować po uzyskaniu R09; nie wymagamy H09 do otrzymania jego własnego znaku. Punkty zadania nie tworzą trzech kopii artefaktu.

W K01–K03 podpowiedzi są kontekstowe i pomijalne. K03 przedstawia skład oraz skończoność W1 i nie dokłada nowych fal na podstawie ukrytej oceny siły gracza. Dla K14 obie gałęzie mają od początku pełne, widoczne warunki; nie dochodzi ukryty obowiązek pościgu za resztkami floty.

Cele opcjonalne służą zwiadowi, rekrutacji i dodatkowej zdobyczy. Ich brak nie blokuje wyniku. CarryPolicy i nieodwracalne straty są opisane przed startem. Wynik rozstrzyga się w określonej fazie ticku; niewymagany żywy przeciwnik poza celem nie przedłuża operacji.

## 5. Karty motywów i granice naukowej pewności

- **Pireus i Zea:** punktem oparcia są badania portów, pochylni i magazynowania floty. Patronat bóstwa i wydarzenia misji są warstwą opowieści.
- **Laurion–Thorikos:** źródła potwierdzają kopalnie i dzierżawy. Rozmiar partii w grze oraz jej dostępność w danej minucie nie są pomiarem antycznej wydajności.
- **Salamina:** tradycja Ajasa jest motywem heroicznej tożsamości; nie oznacza udziału Ajasa w wojnie IV w.
- **Eleusis:** hymn do Demeter opisuje mit i ustanowienie kultu. Boski wpływ na konwój jest fikcją scenariusza.
- **Brauron:** sanktuarium Artemis ma podstawę archeologiczną. Misja nie twierdzi, że wiemy, jak wyglądała każda czynność obrzędowa ani że wojownicza postać gry reprezentuje wszystkie uczestniczki kultu.
- **Egina:** tradycja Ajakosa wymaga wskazania konkretnego świadectwa; dane o późniejszych zabytkach nie są automatycznie stanem IV w.
- **Delos:** związek z Apollinem ma świadectwa literackie i archeologiczne. Konkretne rytualne wymogi misji są dopiero kartą fabularną.
- **Syreny, Gorgona, Boreasz i Hefajstos:** ich oddziaływania są dosłowne w grze. Obrażeń, promieni, odporności i czasu odnowienia nie wyprowadzamy z tekstu antycznego jako liczbowych faktów.

Literackie motywy Demeter, Apollina i Delos sprawdzamy w oznaczonym wydaniu [Hymnów homeryckich](https://www.gutenberg.org/files/348/348-h/348-h.htm). Dla Brauron podstawą materialną jest [oficjalny opis stanowiska](https://www.hh.gr/en/destinations/brauron/). Pełny rejestr i lokalizatory obejmuje dokument źródłowy; nie zastępują ich same nazwy bóstw.

## 6. Potyczki lokalne

Pierwszy zestaw obejmuje trzy archetypy map:

1. **Zatoka i dwa wyjścia:** jedna baza na stronę, boczne szlaki, ograniczone zasoby startowe; cel eskorty lub przełamania.
2. **Archipelag trzech szlaków:** dwie drogi osłonowe i otwarty środek, przyczółki, płycizny oraz jeden neutralny cel ekonomiczny.
3. **Pierścień strażników:** dwa stronnictwa, strefa niezależnej flotylli i ośrodki mitycznych zagrożeń; zadaniem jest zdobycie przewagi operacyjnej bez obowiązku zabijania każdego stworzenia.

Przed startem gracz widzi stronę, poziom AI, dostępne technologie, profil zasobów, występowanie magii i stworzeń oraz warunek zwycięstwa. To konfiguracja jednej lokalnej gry, bez usług sieciowych. Informacje o aktualnym położeniu ukrytych jednostek pozostają niewidoczne.

## 7. Zasady osiągalności

Wykonawca scenariusza sprawdza, czy pozostał legalny sposób wykonania obowiązkowego celu. Utrata przedmiotu może uruchomić wrak do odzyskania, zastępczy cel albo wykrytą porażkę. Nie zostawia gracza w misji bez rozwiązania.

Zagrożenie niewrażliwe wymaga przynajmniej jednego z warunków: można je ominąć; znika po zniszczeniu źródła; dostępna ochrona pozwala przekroczyć strefę; istnieje alternatywny cel. Obowiązkowa walka o fort wymaga dostępnego ostrzału albo mocy o odpowiednim kanale obrażeń. Misja wymagająca wiatru nie startuje, zanim logiczny wpływ wiatru nie zostanie odebrany.

Przed przeniesieniem kampanii gracz otrzymuje listę ludzi, jednostek i przedmiotów przechodzących do następnej operacji. Akceptacja nie może naliczyć nagrody dwukrotnie. Powrót do zapisu sprzed ekranu wyniku zachowuje identyfikator rozliczenia.

## 8. Testy map

- Droga dla najmniejszego i największego dozwolonego profilu kadłuba, także przy uszkodzonym napędzie.
- Zdolność wodowania bez kolizji z brzegiem, przy zachowaniu możliwości fizycznej blokady przez jednostkę.
- Dwa szlaki o mierzalnie odmiennej długości, ekspozycji i ryzyku, zamiast dwóch niemal identycznych prostych.
- Zasięgi sanktuarium i fortów sprawdzone na rzeczywistej masce mapy.
- Legalna reakcja na utworzenie rafy, wiru i usunięcie terenu; żaden cel nie wypada poza domenę siatek.
- Powtórka po zapisie w każdej fazie celu, przywołania, odtworzenia partii i zmiany właściciela.
- Odbiór rzeczywistego przejścia od startu do wyniku. Sam rysunek mapy i poprawny JSON nie są odbiorem misji.
