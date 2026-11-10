# Ramy historyczne zasobów

Data: 2026-11-10. Źródła w [ReferenceRegister.json](ReferenceRegister.json), oznaczane identyfikatorami `R-…`.

## Poziom pewności

- **ustalenie:** potwierdzone w odczytanym źródle; podany adres i to, co źródło mówi;
- **rekonstrukcja:** wniosek badaczy albo rekonstrukcja eksperymentalna, czyli materiał porównawczy, nie zachowany zabytek;
- **interpretacja:** decyzja artystyczna projektu, uzasadniona epoką;
- **anachronizm gry:** element wymagany przez mechanikę, nieznany w epoce, oznaczony jawnie;
- **mitologia:** osobna kategoria (etap 9 projektu).

**Poziom dostępu.** Książki z bibliografii startowej (Morrison, Coates i Rankov; Casson; Lawrence; Coulton) **nie zostały odczytane**: w rejestrze mają poziom „tylko metadane”. Żadne ustalenie poniżej nie powołuje się na ich strony. Odczytane źródła to strony internetowe:
- Institute of Nautical Archaeology (INA);
- Wikipedia, oznaczona jako źródło ostatniej instancji, do weryfikacji w publikacjach.

## Zakres

- **Daty:** kotwica XII w. p.n.e., czyli późna epoka brązu w Egei (Late Helladic IIIC). Polecenie dopuszcza ramę XII–V w. p.n.e. Użytkownik wskazał XII w. tam, gdzie się da, więc okręty, załogi, ładunki i port idą za XII w.
- **Regiony:** wybrzeża Morza Egejskiego, kontakty z Lewantem i Cyprem (handel miedzią i cyną).
- **Okres V w. p.n.e.** (triera, szopy okrętowe w Pireusie): **nie jest kotwicą** tej wersji. Służy wyłącznie jako materiał porównawczy (R-OLYMPIAS, R-ZEA). Klasy gry nie przedstawiają trier.

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

## Do weryfikacji w następnym kroku

1. Okręty z Medinet Habu i Kynos: publikacje źródłowe (Epigraphic Survey; Dakoronia).
2. Katalog Narodowego Muzeum Archeologicznego w Atenach (numer inwentarzowy wazy).
3. Strony muzeum w Bodrum.
4. Odczyt bibliografii startowej. Bez odczytu nie powołujemy się na strony.
