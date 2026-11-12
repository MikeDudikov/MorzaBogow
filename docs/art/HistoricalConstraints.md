# Ramy historyczne zasobów

Rejestr referencji pomocniczych podporządkowany [nadrzędnemu planowi gry](../NOWY_PLAN_GRY.md) i [protokołowi źródłowemu](../HISTORIA_I_ZRODLA.md). Źródła w [ReferenceRegister.json](ReferenceRegister.json), oznaczane identyfikatorami `R-…`. Etykieta profilu, historyczna notatka ani źródło nie ustanawiają alternatywnej epoki produktu.

## Poziom pewności

- **ustalenie:** potwierdzone w odczytanym źródle; podany adres i to, co źródło mówi;
- **rekonstrukcja:** wniosek badaczy albo rekonstrukcja eksperymentalna, czyli materiał porównawczy, nie zachowany zabytek;
- **interpretacja:** decyzja artystyczna projektu, uzasadniona epoką;
- **anachronizm gry:** element wymagany przez mechanikę, nieznany w epoce, oznaczony jawnie;
- **mitologia:** osobna kategoria (etap 9 projektu).

**Poziom dostępu.** Książki z bibliografii startowej (Morrison, Coates i Rankov; Casson; Lawrence; Coulton) **nie zostały odczytane**: w rejestrze mają poziom „tylko metadane”. Żadne ustalenie poniżej nie powołuje się na ich strony. Odczytane źródła to strony internetowe:
- Institute of Nautical Archaeology (INA);
- Wikipedia: odczyt informacyjny, wymagający weryfikacji; nie stanowi zatwierdzenia tezy według protokołu źródłowego.

## Zakres

- **Obowiązująca kotwica:** mitologiczna Grecja IV w. p.n.e., 400–301 p.n.e., z 400 r. włącznie. E6 jest identyfikatorem technicznym, nie ograniczeniem kampanii do dat okresu stylistycznego. Każda istotna teza o technologii, budowli lub materiale wymaga zgodności z kartą misji i źródłem.
  - Region roboczy: Attyka, czyli Pireus i hangary w Zei, Laurion i Thorikos.
  - Ustalenia IV w. są w sekcji niżej.
  - Tabele dalej w pliku („Okręty”, „Załogi”, „Uzbrojenie i ubiór”, „Architektura…”) opisują dawny profil E1, czyli XII w. (LH IIIC). Są nieaktywne i zostają jako zapis historii projektu.
- **Regiony:** wybrzeża Morza Egejskiego, kontakty z Lewantem i Cyprem.

## Kotwica IV w. p.n.e. (profil E6, od 2026-11-10)

Źródła: [ReferenceRegister.json](ReferenceRegister.json). Etykieta read-web opisuje odczyt wskazanej sesji, nie potwierdza ponownej weryfikacji obecnej. Pozycje read-web-last-resort są tropami bibliograficznymi z poziomem NIEUSTALONE; do zatwierdzenia potrzebny właściwy odczyt wiarygodnego źródła. Szczegółowe świadectwo nie ustanawia automatycznie dostępności całej rodziny konstrukcji w każdym roku i regionie.

### Okręty, machiny i drewno

| Ustalenie lub decyzja | Status | Źródło | Dotyczy |
|---|---|---|---|
| Flota ateńska lat 330–320: przewaga trier; czwororzędowce od 330/29 (18, potem ok. 50); pięciorzędowce wymienione tylko w 325/4 (7); w uchwale także triakontory | ustalenie (inwentarze IG II² 1627–1631, za Gabrielsenem 2014) | R-NAVALLISTS | klasy gry: taranowiec jako triakontor, abordażowiec jako czworo- lub pięciorzędowiec |
| Klasy mają wymiary z katalogu gry (12–28 m), a triera miała ok. 37 m; większe typy rysujemy krótsze | kompromis skali gry, decyzja artystyczna | — | `GreekShips` |
| Oczy okrętów (ophthalmoi) to rzeźbione marmurowe płytki na dziobie | ustalenie (Agora, Zea, wrak Tektaş Burnu; Carlson 2009); malowanie niepotwierdzone | R-OPHTHALMOI | `GreekShips.BuildEyes`: biała płytka z ciemną źrenicą |
| Wysięgnik górnego rzędu wioseł (parexeiresia) | ustalenie (Tukidydes 4.12.1, 425 r.; LSJ: wysięgnik, skrzynia wioseł) | R-XENOPHON-HELLENICA | okręty wojenne (do zrobienia) |
| Zasłony burtowe (pararrymata); w źródle służą ukryciu, osłona przed pociskami to przypis tłumacza | ustalenie (Ksenofont, *Hellenika* 1.6.19, 406 r.) z zastrzeżeniem | R-XENOPHON-HELLENICA | okręty wojenne (do zrobienia) |
| Duże żagle zostawiano na lądzie przed walką | ustalenie (*Hellenika* 6.2.27, Ifikrates; 2.1.29, 405 r.) | R-XENOPHON-HELLENICA | żagiel zwinięty w walce (obsada już brajluje) |
| Spiżowy taran z płetwami | interpretacja dla IV w. (rzeźbione i odlewane tarany klasyczne i hellenistyczne; wzór sprawdzić w publikacji) | — | `GreekShips.BuildRam` |
| Diodor przypisuje wynalazek katapulty Syrakuzom w kontekście 399/8 r. | świadectwo literackie, Diodor 14.42.1; nie ustala dostępności wszystkich późniejszych typów | R-DIODORUS-14 | Każdy typ, montaż pokładowy, region i rok misji wymagają osobnej podstawy; konfigurację niepoświadczoną oznaczyć jako rekonstrukcję lub fikcję. |
| Machiny na okrętach przy oblężeniu Tyru, 332 r. | ustalenie (Arrian 2.21–23) | R-ARRIAN-TYRE | `ShipKiller` (miotacz bełtów), `Bomber` (miotacz kamieni) |
| Statek handlowy i znaleziska z Kyrenii jako materiał porównawczy dla konstrukcji, takielunku i ładunku | Datowanie drewna wyznacza TPQ budowy albo naprawy, a nie dokładny przedział budowy. Manning i in. 2024, sekcja A dating model oraz omówienie wyników; fazy i szczegóły konstrukcji sprawdzać osobno. | R-KYRENIA; [badanie datowania](https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0302645) | Analogia wymagająca zgodności z rokiem i regionem misji; wrak nie uzasadnia automatycznie każdej konfiguracji na całe IV w. |
| Triery i okręty długie z jodły pospolitej (elatē); statki kupieckie z peuke (sosna); kil triery z dębu; elementy gięte z morwy, jesionu mannowego, wiązu, platanu | ustalenie (Teofrast, *Historia roślin* 5.7.1–3) | R-THEOPHRASTUS-HP5 | skład drewna, pochylnia, tekstury drewna okrętu |

### Ludzie i uzbrojenie

| Ustalenie lub decyzja | Status | Źródło | Dotyczy |
|---|---|---|---|
| Reforma Ifikratesa: mniejsze tarcze, dłuższe włócznie i miecze, buty; dawni hoplici nazwani peltastami (Diodor 15.44, 374/3) | ustalenie (tekst); szczegóły sporne | R-IPHICRATES | peltaści przy stanowiskach oszczepów |
| Lniane pancerze zamiast brązowych | ustalenie tylko u Neposa (*Ifikrates* 1.3–4) | R-IPHICRATES | linothorax u części epibatów |
| Hełm pilos | ustalenie dla 425 r. (Tukidydes 4.34.3, może filcowe czapki); **powszechność w IV w. to interpretacja** (praca magisterska za Andersonem 1970) | R-PILOS | `CrewLooks`: pilos u większości epibatów |
| Tarcza aspis z godłem, hełm koryncki lub beocki u oficerów, pancerz mięśniowy | interpretacja epoki (ikonografia do odczytu) | — | dowódca, epibatai |
| Godła stron to konwencja gry (barwy stron), nie symbole miast | decyzja artystyczna | — | `ShieldPaint` |

### Budowle, srebro i krajobraz

| Ustalenie lub decyzja | Status | Źródło | Dotyczy |
|---|---|---|---|
| Domy w Olintos: fundamenty z łamanego kamienia, ściany z cegły suszonej, często piętrowe, pokoje wokół dziedzińca z portykiem (pastas) od północy, tynk malowany, dachówki | ustalenie (Robinson 1932; wykopaliska 2017); koniec 348 r. | R-OLYNTHUS | domy portu, magazyn |
| Hangary w Zei: kolumnady z wapienia, ściany, dach z dachówek; faza 3 po 375–350 r., światło ok. 5,87 m; 196 hangarów w 330/29 | ustalenie (Lovén 2011) | R-ZEA | dok i stocznia: typ, nie wymiar; okręty gry (do 10,9 m) zostają widoczne, hangar nie przykrywa pochylni |
| Laurion: galena i cerusyt; płuczki płaskie ze zbiornikiem, kanałami i osadnikami, tynk hydrauliczny, duże cysterny, stoły do kruszenia i żarna | ustalenie (Kakavoyannis 2001; Kepper 2004) | R-LAURION-WORKSHOPS | złoże srebra, `ore.washery.E5` (E5 i E6) |
| Dzierżawy kopalń od 367/6 r., ożywienie od lat 340 (ponad 80 kopalń w 342/1) | ustalenie (AIO RO 36) | R-LAURION-WORKSHOPS | wygląd czynnej kopalni IV w. |
| Wylesienie Attyki w odczuciu IV w. (Platon, *Kritiasz* 111) | opis w ramie mitycznej: postrzeganie, nie dane | R-PLATO-CRITIAS | krajobraz: wyręby i nagie zbocza obok gajów |
| Malarstwo Verginy (fryz łowiecki 5,56 × 1,16 m, 336–316 r., datowanie sporne) i mozaiki z Pelli (ostatnia ćwierć IV w.) | ustalenie (PNAS 2015; Martin 2017) | R-VERGINA-PELLA | paleta i polichromia budowli, malowane tynki |

## Okręty

| Ustalenie lub decyzja | Status | Źródło | Dotyczy |
|---|---|---|---|
| W XII w. p.n.e. galery egejskie mają jeden rząd wioseł; triera z trzema rzędami należy do V w. p.n.e. (170 wioślarzy na trzech poziomach to model klasyczny) | rekonstrukcja (Olympias) dla V w.; jeden rząd w XII w. to interpretacja epoki | R-OLYMPIAS | wszystkie klasy: `Banks = 1` |
| Kadłub z desek łączonych na pióro i wpust (pegged mortise-and-tenon), z proto-stępką | ustalenie dla wraku z Uluburun (ok. 1320 ± 15 p.n.e.) | R-ULUBURUN | kadłuby, kolejność budowy na pochylni |
| Budowa „najpierw poszycie”, potem wręgi | interpretacja wynikająca z techniki pióra i wpustu; strona INA nie używa nazwy shell-first | R-ULUBURUN | `ShipyardVisual`: poszycie 10–55%, wręgi 55–70% |
| Ładunek statku kupieckiego: sztaby miedzi w kształcie skóry wołu, cyna, dzbany kananejskie | ustalenie (Uluburun: 354 sztaby miedzi, w tym 317 w kształcie skóry wołu; około 150 dzbanów) | R-ULUBURUN | holkas: `Cargo`, wrak ze srebrem |
| Statek kupiecki tej epoki: około 15 × 5 m | rekonstrukcja z układu ładunku | R-ULUBURUN | proporcje statku kupieckiego (wymiary z katalogu gry) |
| Wrak z Kyrenii to IV w. p.n.e. (zatonął ok. 294 p.n.e.) | ustalenie z zastrzeżeniem „c.” | R-KYRENIA | **nie** używany jako wzór |
| Głowa ptaka na dziobnicy, gniazdo na maszcie, żagiel z brajlami bez dolnej rei | **niepotwierdzone w odczytanych źródłach** (strony o Medinet Habu nie opisują okrętów); przyjęte jako interpretacja na podstawie wiedzy ogólnej, do weryfikacji w publikacji Epigraphic Survey | R-MEDINET | `GreekShips`: głowa ptaka, gniazdo, żagiel |
| Drewniana ostroga na przodzie stępki zamiast brązowego taranu z trzema płetwami | interpretacja (brązowe tarany należą do okresów późniejszych) | — | klasy taranujące: `BuildSpur` |
| Smoła na kadłubie, czerwona ochra na dziobie (homeryckie „czarne okręty”, „o czerwonych policzkach”) | interpretacja literacka (eposy homeryckie; tekst nie został odczytany w tej sesji) | — | paleta `Pitch`, `Ochre` |
| Zygzak na pasie burty jak na ceramice epoki | interpretacja artystyczna | — | `GreekShips.Band` (wzór `zigzag`) |

## Warianty okrętów (wygląd jednostek, [ShipVariants.md](ShipVariants.md))

| Ustalenie lub decyzja | Status | Źródło | Dotyczy |
|---|---|---|---|
| Kamienne kotwice wożone przy dziobie | **ustalenie** dla Uluburun: 24 kotwice (22 z piaskowca, 2 wapienne), 16 przy dziobie. Kształt płyty z otworem na linę to interpretacja | R-ULUBURUN (tekst strony Wikipedii, 2026-11-10) | wyposażenie `anchor-stones`, tylko na miejscach dziobowych |
| Lina na pokładzie | **ustalenie**: lina z ostatniego rejsu Uluburun wśród datowanych próbek | R-ULUBURUN | `rope-coil`, sztagi w farbie `Rope` |
| Dzban na wodę w pierścieniu z liny | interpretacja: Uluburun wiózł dzbany kananejskie i pithoi z żywnością i żywicą; wody w dzbanach strona nie podaje | R-ULUBURUN | `water-jar` |
| Kosze, worki z zapasami, złożone płótno | interpretacja: koszy nie ma w odczytanym tekście, tylko w automatycznym streszczeniu nieodczytanej części | — | `basket`, `sacks`, `cloth` |
| Linia falista na pasie burty, obok zygzaka i pasa bez wzoru | interpretacja: motyw ceramiki przeniesiony na kadłub; strona o ceramice mykeńskiej nie wymienia motywów LH IIIC w odczytanym tekście | — | `GreekShips.Band` (`zigzag`, `wave`, `none`) |
| Trzy warsztaty: stocznia portowa, wyspiarski, skromny | decyzja gry dla spójności i rozpoznawalności flot; nazwy bez twierdzeń o regionach | — | `ShipLookLibrary.Families` |
| Żagiel szyty z pionowych pasów płótna, łaty z innego płótna w barwie strony | interpretacja | — | tekstury `canvas_*`, łaty żagla |
| Smoła w kilku odcieniach i połyskach, przetarcia do drewna, dawne naprawy | interpretacja (smołowanie kadłubów jako konserwacja) | — | odcienie `pitch.*`, ślady `repair` i `worn` |
| Barwa właściciela tylko na żaglu i łatach, bez godeł | decyzja gry: zatwierdzony zestaw to granat gracza i czerwień przeciwnika | — | `ShipPaints.Sail`, `SailPatch` |

## Załogi ([ZALOGI.md](ZALOGI.md), [CrewRoles.json](CrewRoles.json), [ShipCrewCoverage.json](ShipCrewCoverage.json))

Źródła odczytała kwerenda załóg 2026-11-10 (poziom dostępu w rejestrze). Bez numerów stron poza tymi, które były w odczytanym tekście, i bez cytatów z prac współczesnych.

| Ustalenie lub decyzja | Status | Źródło | Dotyczy |
|---|---|---|---|
| Wioślarze *e-re-ta* z tabliczek z Pylos: pobór osadników według osad, bez wzmianki o broni | ustalenie (tekst), interpretacja (status) | R-PYLOS-AN | wioślarze bez pancerzy i broni |
| Wioślarze siedzą nisko przy burtach plecami do dziobu, oburącz przy wiośle; ich torsy widać nad burtą | ustalenie (to, co namalowane: Bademgediği, Kynos), interpretacja (wysokość ławy) | R-BADEMGEDIGI, R-KYNOS | dulki na burcie, siedziska nisko na pokładzie (`GreekShips.RowerSeats`) |
| Wojownicy stoją nad wioślarzami: wąski pomost wzdłuż środka galery, wioślarze w galeriach przy burtach pod nim; pokład okrętów Ludów Morza biegnie przez całą długość, ale nie przez całą szerokość | interpretacja (Kynos A91–A92, Bademgediği; Medinet Habu według Emanuela 2014, przeczytane strony 21–45) | R-KYNOS, R-BADEMGEDIGI, R-MEDINET | `GreekShips` pomost (`WalkwayRise` 0,35 m nad burtą, galerie wioślarzy 1,25 m przy burtach); polecenie użytkownika 2026-11-10 |
| Pętla wiosła (dulka) na krawędzi burty | interpretacja (pętle ze skóry u Homera, lniane w Knossos; Kynos A92: wiosła do poziomu pokładu) | R-HOMER, R-PYLOS-AN, R-KYNOS | dulka `Sheer + 0,05 m` na galerach, w wycięciu burty łodzi |
| Sternik stoi przy trzonie wiosła sterowego, w hełmie, bez pancerza | ustalenie (Kynos A91) | R-KYNOS | sternik przy rumplach; czasem hełm |
| Dwa wiosła sterowe | interpretacja (jedno typowe w LH IIIC, dwa poświadczone później; generator ma dwa) | R-KYNOS, R-HOMER | jeden sternik między rumplami, dwóch na szerokiej galerze ciężkiej |
| Ktoś czuwa na dziobie | interpretacja (funkcja: Kynos, Od. 12.228–229; tytuł *prōireus* późniejszy) | R-KYNOS, R-HOMER | obserwator na pomoście dziobowym |
| Brak mistrza wioślarzy (*keleustes*) | jego funkcja na okręcie to praktyka klasyczna; dla XII w. brak dowodu | — | rytm podaje sternik; jeden rytm wioseł bez dodatkowej roli |
| Wojownicy na pokładzie z hełmami (jeżowe, pierzaste, rogate), tarczami okrągłymi lub klepsydrowymi, włóczniami i oszczepami | ustalenie (to, co namalowane) | R-KYNOS, R-BADEMGEDIGI, R-WARRIORVASE | stroje i broń wojowników |
| Pełny rynsztunek (napierśnik, nagolenniki, miecz Naue II) tylko dla przywódcy | interpretacja (groby wojowników LH IIIC to mała część walczących) | R-ACHAEA, R-WARRIORVASE | jeden dowódca w zbroi na okręt |
| Wioślarze-wojownicy: wolni wojownicy wiosłują w rejsie | interpretacja (Homer, Tukidydes 1.10.4); decyzja użytkownika 2026-11-10 | R-HOMER, R-THUCYDIDES | `CrewRole.WarriorRower` |
| Łucznicy na okrętach egejskich | spekulacja/interpretacja (Kynos A92, wioślarze-łucznicy u Homera); stanowiska łuczników to dana gry | R-KYNOS, R-HOMER | `CrewRole.BankArcher` |
| Strój wioślarzy (przepaska, krótka tunika, nagi tors) | spekulacja: źródła pokazują sylwetki | — | `CrewLooks`, rola wioślarza |
| Barwa strony na tunikach wojowników | konwencja gry (czytelność stron, ArtDirectionAegean #3), nie mundur | — | `CrewLooks.SideCloth` |
| Haki abordażowe | ustalenie dla floty egipskiej (Medinet Habu), spekulacja dla Egejczyków; haki to dana gry | R-MEDINET | rzut haków, liny, bez corvusa |
| Przyciągnięcie okrętów linami przy abordażu | decyzja użytkownika 2026-11-10: przesunięcie tylko obrazu | — | abordaż |
| Wzrost 1,60–1,72 m wokół 1,66 m | ustalenie zakresu dla starożytnej Grecji (159,7–170,6 cm średnio u mężczyzn) | R-KOUKLI | `CrewLooks` (wzrost) |
| Proporcje segmentów ciała | znane z literatury (Drillis i Contini, za Winterem), nieodczytane w tej sesji | B-WINTER | `CrewBody` |
| Tempo wiosłowania: około 20–30 w przelocie, około 40 w sprincie | rekonstrukcja (triera Olympias), analogia z późniejszego i większego okrętu | R-OLYMPIAS | `RowingStroke` |

## Uzbrojenie i ubiór

| Ustalenie lub decyzja | Status | Źródło | Dotyczy |
|---|---|---|---|
| Waza Wojowników z Myken: krótkie chitony, napierśniki, hełmy (z rogami po jednej stronie, typu „jeż” po drugiej), nagolenniki, włócznie, tarcze; datowanie LH IIIC, ok. 1200 p.n.e. (dyskutowane: XIII lub XII w.) | ustalenie (opis strony); numer inwentarzowy niejednoznaczny w źródłach (NAMA 1426); katalog muzeum nie został odczytany | R-WARRIORVASE | `CrewKit.Warrior` |
| Okrągła tarcza z wycięciem u dołu | **niepotwierdzone** w odczytanych źródłach; interpretacja | R-WARRIORVASE | tarcza wojownika |
| Łuki, oszczepy, groty z brązu | interpretacja epoki | — | `CrewKit.Archer`, strzały, oszczepy |
| Machiny miotające (skorpion, gastrafetes, balisty, katapulty, onager) | **anachronizm gry**: nie istniały w XII w. p.n.e.; nazwy w danych symulacji zostają | — | stanowiska Medium i Heavy, pociski |

## Architektura, porty, narzędzia

| Ustalenie lub decyzja | Status | Źródło | Dotyczy |
|---|---|---|---|
| Szopy okrętowe w Pireusie (Zea) około 40 × 6 m, zbudowane dla trier | ustalenie dla okresu klasycznego; **nie** wzór dla XII w. | R-ZEA | tylko porównanie |
| Pochylnia z płozami i podporami, narzędzia z brązu (topory, cioły, świdry) | interpretacja epoki | — | `Slipway`, `CrewTool.Axe` |
| Budowle osady (stocznia, Warsztat, Miasto, Szkoła Mędrców, dok) | **do opracowania**: dziś prymitywy; wymagają zestawów modułowych z kamienia, cegły suszonej i drewna (mury cyklopowe tylko dla cytadel) | — | `AssetManifest.json`: `building.*` |

## Zniszczenia budowli i okrętów (pionowy wycinek, [DESTRUCTION.md](DESTRUCTION.md))

Źródła tej sekcji odnotowano na poziomie search-summary. Wszystkie zależne od nich tezy mają status NIEUSTALONE do właściwego odczytu, nawet gdy ich użycie w prezentacji jest już zaimplementowane. Opis kinematycznej animacji nie stanowi fizycznej walidacji. Nie powoływać nieodczytanych stron ani cytatów.

| Ustalenie lub decyzja | Status | Źródło | Dotyczy |
|---|---|---|---|
| Trzon kolumny z bębnów i łączenia polos/empolion/anatyroza | **NIEUSTALONE** dla tego odbioru: search-summary; sprawdzić właściwy obiekt, fazę i konstrukcję | R-DRUM-JOINTS | Przed naukowym odbiorem podziału i pękania kolumny wymagany odczyt źródeł. |
| Przewrócona kolumna i układ jej bębnów | **NIEUSTALONE**: search-summary; wynik zależy od kontaktów i modelu uszkodzeń | R-OLYMPIA-QUAKE | `StructureDamageView.Rest` jest przybliżeniem prezentacji, nie zatwierdzonym pomiarem upadku. |
| Bloki architrawu i fryzu spoczywają na dwóch głowicach; tracąc jedną, spadają | **interpretacja** konstrukcyjna (statyka belki na dwóch podporach) | — | graf podparcia Szkoły |
| Ruiny ścian z cegły suszonej na kamiennym cokole | **NIEUSTALONE**: search-summary; zweryfikować konstrukcję i mechanizm zniszczenia | R-MUDBRICK-DECAY, R-OLYNTHUS | Kształt ruiny pozostaje interpretacją do odrębnego odbioru. |
| Dachówki, krokwie i układ ich pozostałości | **NIEUSTALONE**: search-summary; wymagane źródło i model podparcia/upadku | R-MUDBRICK-DECAY | CaveIn pozostaje implementacją prezentacji do odrębnego odbioru. |
| Bez ognia: ogień tylko przy zdarzeniu gry, a gra go nie ma | decyzja gry (polecenie) | — | brak zwęgleń w nowych ruinach Szkoły |
| Maszt pęka tuż nad pokładem, gdzie jest trzymany, i zostawia kikut w stępce | **interpretacja**: stępka masztu poświadczona (Kyrenia); o belkach wokół masztu źródła w tej sesji brak | R-KYRENIA-MASTSTEP | `ShipVisual.Mast` |
| Zostawianie dużych żagli na lądzie przed określonymi bitwami | **NIEUSTALONE w tej sekcji**: odczytać właściwe ustępy i nie uogólniać na wszystkie okręty; maszt wymaga osobnej oceny | R-SAILS-ASHORE | Właściwy kontekst tekstu i konfiguracja misji poprzedzają odbiór takielunku. |

## Do weryfikacji w następnym kroku

1. Okręty z Medinet Habu i Kynos: publikacje źródłowe (Epigraphic Survey; Dakoronia).
2. Katalog Narodowego Muzeum Archeologicznego w Atenach (numer inwentarzowy wazy).
3. Strony muzeum w Bodrum.
4. Odczyt bibliografii startowej. Bez odczytu nie powołujemy się na strony.
