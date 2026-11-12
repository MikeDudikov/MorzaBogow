# Morza Bogów — badania świata i scenariuszy

Zakres wyznacza wyłącznie [nadrzędny plan gry](NOWY_PLAN_GRY.md): mitologiczna Grecja IV w. p.n.e., 400–301 p.n.e., z 400 r. p.n.e. włącznie, głównie Attyka i wybrane akweny egejskie. Nie obowiązuje jedno węższe okno chronologiczne całej kampanii. Karta misji ustala dokładniejszą datę, gdy wymaga jej weryfikacja materialna. Cuda działają dosłownie w fikcji; baza materialna i tradycje mitologiczne wymagają osobnych dowodów. Dawny raport lub etykieta profilu nie zmienia tej ramy.

## 1. Obowiązek każdej sesji Claude Code

Po odczytaniu planu **samodzielnie otwórz źródła w bieżącej sesji** i sprawdź bazowe ramy świata oraz wszystkie tezy, od których zależy wybrane zadanie. Nie uznawaj pamięci modelu, notatki projektu ani raportu innego agenta za własną weryfikację.

1. Odczytaj plan gry, katalog, parametry, mapy, plan integracji i aktualny stan kodu.
2. Wypisz osobno: tezy materialne, motywy mitologiczne oraz reguły gry.
3. Dla realiów ustal rok, region, rodzaj świadectwa i zakres niepewności. Daty poświadczenia nie zamieniaj w dokładną datę wynalezienia.
4. Dla mitu podaj autora/tradycję, wydanie i lokalizator. Odróżnij warianty mitu oraz datę tekstu od daty fikcyjnego wydarzenia.
5. Dla nowych lub spornych tez szukaj niezależnego potwierdzenia. Jedno zachowane świadectwo jawnie oznacz; nie wymyślaj drugiego.
6. Zapisz raport w `docs/research/sessions/YYYY-MM-DD-HHMM-<zadanie>.md` przed implementacją zależnej mechaniki.
7. Po implementacji sprawdź, czy zachowanie nie zostało opisane jako prawda naukowa tylko dlatego, że działa w kodzie.

Raport: data/strefa, commit, teza, URL, autor/instytucja, wydanie, dokładny fragment, faktyczny poziom dostępu, ograniczenia, decyzja i status. Statusy: **POTWIERDZONE_W_ZAKRESIE**, **REKONSTRUKCJA**, **TRADYCJA_MITOLOGICZNA**, **REGUŁA_GRY**, **NIEUSTALONE**, **BLOCKED_RESEARCH**.

Brak internetu, paywall i uszkodzony tekst nie dają prawa do fikcyjnego cytatu lub numeru strony. Kontynuuj niezależną pracę techniczną, ale nie zatwierdzaj nieodczytanej tezy jako naukowej. Przypomnienie startowe nie wykonuje tej pracy za agenta i nie stanowi certyfikatu prawdziwości.

## 2. Rozdzielenie dowodów

- **Historia i przyroda:** inskrypcje z komentarzem, archeologia, badania żeglugi i techniki, krytycznie czytane teksty antyczne.
- **Mitologia:** teksty antyczne w oznaczonym wydaniu, ich warianty i świadectwa lokalnych kultów. Potwierdzają obecność opowieści, nie fizyczne istnienie zjawiska.
- **Projekt:** koszty D/S, czasy, obrażenia, poziomy, zasięgi, wymiary sektora i premia bohatera. Mają być spójne i przetestowane, nie „udowodnione” przypisem do mitu.

Nie używaj Wikipedii, sklepu z modelem ani niepodpisanego streszczenia jako zatwierdzenia tezy. Popularna strona może wskazać bibliografię. Sama domena uniwersytetu nie zmienia pracy studenckiej lub katalogu bibliotecznego w pełne opracowanie naukowe.

## 3. Rejestr odczytu

Przegląd wykonano 11 listopada 2026. Poniższy poziom dostępu jest rzeczywisty. Nie oznacza przeczytania całej publikacji ani wszystkich dzieł wymienionych w jej bibliografii.

| ID | Źródło | Odczytany zakres | Wniosek i ograniczenie |
|---|---|---|---|
| S01 | Vincent Gabrielsen, [The Piraeus and the Athenian Navy](https://tidsskrift.dk/pdia/article/download/24130/21168/55831) | PDF, szczególnie s. 37–43, tabela s. 39 | Datowane inwentarze i zaplecze portu. Tetrery i pentery mają różne daty poświadczeń; nie udostępniać całego katalogu w każdym roku IV w. |
| S02 | Danish Institute at Athens, [Zea Harbour Project](https://diathens.gr/en/projects/piraeus) | opis badań i funkcji portów | Pochylnie, hangary i chronione bazy. Nie ustala fazy każdego modelowanego obiektu w roku misji. |
| S03 | AIO, Lambert/Ashwell/Osborne, [Agora XIX P5 / RO 36](https://www.atticinscriptions.com/inscription/RO/36) | rachunek 367/6, w. 40–94 i komentarz | Laurion, dzierżawy i zaplecze. Nie wyznacza wydajności ani kosztu budowy statku w grze. |
| S04 | AIO, Stephen Lambert, [IG II³ 1 370](https://www.atticinscriptions.com/inscription/IGII2/1629) | dekret 325/4 zawarty w inwentarzach i komentarz | Wymienia triery, tetrery i triakontory oraz ochronę żeglugi i dostaw. Nie jest pełnym inwentarzem floty; sam nie dowodzi liczby penter ani faktycznej realizacji kolonii. |
| S05 | Arrian, tłum. E. J. Chinnock, [Anabasis of Alexander](https://www.gutenberg.org/files/46976/46976-h/46976-h.htm) | księga II, rozdz. 19–23 | Brander, machiny, blokada i usuwanie przeszkód w Tyrze. Jest to świadectwo z innego teatru działań, nie dowód standardowego wyposażenia Attyki. |
| S06 | Thierry Lucas, [Early Greek Catapults and First-Generation Artillery Towers](https://www.efa.gr/th-lucas-publication-d-article-early-greek-catapults-and-first-generation-artillery-towers/) | **abstrakt**, Historia 71.2 (2022), s. 130–149 | Sygnalizuje spór o typologię i chronologię wież. Pełnego wywodu nie odczytano; nie wyciągać z abstraktu konkretnych parametrów broni. |
| S07 | Pausanias, tłum. W.H.S. Jones/H.A. Ormerod, [Perseus/Tufts TEI](https://raw.githubusercontent.com/PerseusDL/canonical-greekLit/master/data/tlg0525/tlg001/tlg0525.tlg001.perseus-eng2.xml) | 1.35.1–3; 2.29.1–11; 1.19.5; fragment 1.1.1 | Salamina/Ajas, Egina/Ajakos, Boreasz/Orejtyja i motywy wybrzeża. Autor pisze znacznie później niż IV w.; jego zabytki nie są automatycznie stanem epoki gry. |
| S08 | H.G. Evelyn-White, [Hymny homeryckie i teksty hesiodowe](https://www.gutenberg.org/files/348/348-h/348-h.htm) | Hymn II, szczególnie w. 90–112 i 473–495; Hymn III, w. 1–164; Hymn XX, w. 1–8; Hymn XXII, w. 1–7; nagłówki/wydanie | Demeter/Eleusis, Apollo/Delos, Hefajstos/rzemiosło i Posejdon/żegluga. To tradycja literacka. Nie przejmować bez krytyki datowań i tez wstępu wydania z 1914. |
| S09 | Homer, Odyseja, tłum. Samuel Butler, [tekst wydania](https://www.gutenberg.org/files/1727/1727-h/1727-h.htm) | księga XII, śpiew syren, związanie i zamknięcie słuchu | Podstawa motywu i kontrgry narracyjnej. Nie przyjmujemy kontrowersyjnej geografii ze wstępu tłumacza jako naukowej lokalizacji wysp. |
| S10 | Hellenic Heritage, [Brauron](https://www.hh.gr/en/destinations/brauron/) | opis sanktuarium, datowanie i tradycja założenia | Podstawa materialna dla rozkwitu w V–IV w. oraz lokalnego motywu Artemis. Nie jest pełnym raportem wykopalisk. |
| S11 | Hellenic Heritage, [Sounion](https://www.hh.gr/en/destinations/sounion/) | opis świątyni, przylądka, portu i tradycji | Połączenie przylądka, Posejdona i funkcji żeglugi; geometria sektora nadal wymaga osobnego opracowania. |
| S12 | Ephorate of Antiquities of the Cyclades, [Delos](https://cyclades.culture.gov.gr/en/location/the-archaeological-site-and-museum-of-delos/) | opis miejsca, sanktuarium i warstwy mitologicznej | Podstawa miejsca i związku z Apollinem. Nie przenosić całej późniejszej zabudowy portowej do IV w. |
| S13 | [Demostenes]/Apollodoros, tłum. A.T. Murray, [Against Polycles, Perseus](https://raw.githubusercontent.com/PerseusDL/canonical-greekLit/master/data/tlg0014/tlg050/tlg0014.tlg050.perseus-eng2.xml) | §§10–20, 25, 33–38 | Znaczenie płac, rekrutacji i eskort. Mowa strony sporu nie ustanawia jednej taryfy dla całej floty. |
| S14 | AIO, Lambert/Schuddeboom, [RO 37](https://www.atticinscriptions.com/inscription/RO/37) | 363/2, obowiązki i podział majątku/kosztów | Konkretny kontekst społeczny kultu; nie wzór na działanie każdej świątyni gry. |
| S15 | Pseudo-Apollodoros, tłum. J.G. Frazer (1921), [Biblioteka, Perseus/Tufts TEI](https://raw.githubusercontent.com/PerseusDL/canonical-greekLit/master/data/tlg0548/tlg001/tlg0548.tlg001.perseus-eng2.xml) | 2.4.2–3 oraz nagłówek wydania | Perseusz, odbicie gorgony w brązowej tarczy i ukrycie przez czapkę Hadesa. Późniejszy zapis tradycji; nie dowodzi jednego kanonu IV w. ani rzeczywistych skutków fizycznych. |

Otwieranie części stron Theoi zwracało 403. Dla Hymnów, Odysei i Pausaniasa odczytano wskazane wyżej wydania alternatywne. Nie przypisujemy niedostępnej stronie poziomu „przeczytana w całości”. Pełne monografie o rekonstrukcji triery, szczegółowe raporty wszystkich stanowisk i kompletny korpus mitologii nie zostały odczytane.

## 4. Karty potrzebne przed wykonaniem misji

| Misje | Właściwy punkt wyjścia | Nadal do sprawdzenia |
|---|---|---|
| K01/K03 Pireus/Zea | S01–S02 | Wycinek brzegu, głębokość, faza zabudowy w przypisanym roku. |
| K02 Laurion/Thorikos | S03 | Miejsce załadunku, trasa i zakres kompresji geografii. |
| K04 Salamina | S07 1.35 | Materialny brzeg sektora i datowanie obiektów; obecność herosa jest fikcją. |
| K05 Eleusis | S08 Hymn II, S14 jako porównawczy dokument kultowy | Konkretna infrastruktura wybrzeża i lokalne obowiązki w roku akcji. |
| K06 Sunion | S11 | Geometria portu, brzegów i stref obserwacji; moc Posejdona pozostaje fikcją. |
| K07 Brauron | S10 | Faza konkretnego obiektu, relacja brzegu i sanktuarium. |
| K08 Egina | S07 2.29 | Wycinek portu i dostępne obiekty IV w.; późniejszy opis nie wystarcza do ich odtworzenia. |
| K09 Delos | S08 Hymn III i S12 | Fazy zabudowy oraz rzeczywista rola konkretnej przystani w roku misji. |
| K10–K13 sektory mityczne | S09; S15 2.4.2–3; S08 Hymn XX; S07 1.19.5 | Podstawowe motywy syreny, gorgony, Hefajstosa i Boreasza sprawdzono. Nadal potrzebne karty wariantów harpii, cyklopa i drakona oraz szczegółowej fabuły. |
| K14 finał | pełny manifest kampanii | Chronologia, strony konfliktu i wykonalność wszystkich alternatywnych celów. |

Powyższe braki nie blokują napisania silnika zdolności ani zaprojektowania jawnie fikcyjnego sektora. Blokują twierdzenie, że konkretną rekonstrukcję geograficzną lub epizod fabularny zatwierdzono już naukowo.

## 5. Szablon raportu

```markdown
# Weryfikacja świata: <zadanie>
Data i strefa: <Europe/Warsaw>
Commit i stan roboczy: <wartości>
Zakres: <miejsce, rok, mechanika>
| Teza | Kategoria | Autor/instytucja | URL | Wydanie i lokalizator | Dostęp | Co wynika | Ograniczenie | Decyzja |
|---|---|---|---|---|---|---|---|---|
Źródła ponownie otwarte w tej sesji: <konkretne fragmenty>
Nieodczytane lub sporne: <wpływ na zadanie>
Parametry fikcji i balansu: <lista>
Status: <właściwy stan>
```

Nie zatwierdzaj reguły jako faktu naukowego na podstawie liczby linków. Ważne są jakość świadectwa, jego zakres i uczciwa interpretacja. Gotowy plan jest kontraktem projektu; każda realizowana część wymaga aktualnego sprawdzenia źródeł i właściwego odbioru działania.
