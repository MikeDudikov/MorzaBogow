# Morza Bogów: raport stanu projektu i założeń (11.11.2026)

**Do czego służy:** do analizy i ewentualnego przeorganizowania założeń gry (np. przez ChatGPT). Raport opisuje, co gra deklaruje, co faktycznie istnieje i gdzie dokumenty przeczą sobie albo kodowi. Ścieżki są względne wobec katalogu repozytorium.

**Jak powstał:** z trzech niezależnych przeglądów repozytorium tylko do odczytu (dokumenty projektowe, kod z testami i pomiarami, zgodność dokumentacji z kodem) oraz z wiedzy sesji środowiska. Liczby i ścieżki pochodzą z plików repozytorium. Pomiarów wydajności nie powtarzano. To migawka: równoległe sesje dalej zmieniają kod.

## 1. Projekt w skrócie

- **Gatunek:** RTS o bitwach morskich w starożytnej Grecji. Kamera ukośna, produkcja premium, PL i EN.
- **Inspiracja:** mechaniki Ancient Conquest (1998). Nazwy, liczby, grafika i fabuła mają być własne.
- **Technologia:** Unity 6000.3.28f1 (HDRP 17.3), Windows 11. Logika gry to czysta, deterministyczna symulacja C# (30 Hz, liczby double, losowość z ziarna), oddzielona od warstwy obrazu.
- **Sposób pracy:**
  - kod pisze kilka równoległych sesji Claude Code w jednym folderze, koordynowanych wiadomościami;
  - każda praca kończy się lokalnym commitem;
  - automatyczna bramka jakości „M1” uruchamia testy Unity EditMode i PlayMode;
  - repozytorium ma 107 commitów z 3 dni, bieżąca gałąź to `balans-skala`.
- **Odbiór i źródła:**
  - nie było żadnego playtestu z człowiekiem ani zewnętrznego CI;
  - macierze odbioru w `qa/*.csv` mają wszystkie pozycje w stanie NOT_RUN;
  - książki źródłowe nie zostały przeczytane, źródłami historycznymi są strony WWW i Wikipedia (`docs/art/HistoricalConstraints.md`).

## 2. Deklarowana wizja

Źródła: `GAME_VISION.md`, `GAME_DESIGN.md`, `ECONOMY_DESIGN.md`, `MYTHIC_SCENARIOS.md`, `VISUAL_TECH_SPEC.md`, wszystkie z 09.11.

- **Fantazja gracza:** dowódca floty. Rutynę (zbiór, dostawy, kolejki, eskortę) robi automatyka, a ponad 70% czasu ma przypadać na działania na wodzie. Gospodarka tylko „daje powody do wypłynięcia”, bez potrzeb mieszkańców i armii lądowej.
- **Świat:** 8 biomów, w tym mityczne (Wyspy Syren, Morze Zapomnianych). Frakcje to Hellenowie, Persowie i barbarzyńcy.
- **Tryby:**
  - kampania z 14 misjami (Eneasz, Odyseusz, Dionizos, Andromeda);
  - 40 potyczek: 8 biomów × 5 warunków zwycięstwa;
  - „docelowo” multiplayer do 8 graczy i edytor map.
- **Rozgrywka według specyfikacji:**
  - 12 klas okrętów z podsystemami, limit 100 okrętów na stronę;
  - 12 zaklęć, 9 bohaterów, stworzenia morskie;
  - pogoda wpływająca na grę: wiatr ±25% dla żagli, prąd, sztorm;
  - mecz 15–25 min, pierwszy kontakt po 1:45–3:00.
- **Zasady stałe:**
  - pełny determinizm i zapis;
  - obraz nie zmienia wyniku symulacji;
  - SI ma te same rozkazy i tę samą mgłę wojny co gracz, bez ukrytych premii;
  - każde zagrożenie ma zapowiedź co najmniej 2 s;
  - historyczna wiarygodność z poziomami pewności;
  - assety tylko własne albo CC0, z rejestrem licencji.
- **Etapy produkcji:**
  - M0: infrastruktura;
  - M1: pętla gry, 4 klasy, jedna mapa, SI, zapis;
  - M2: pełna zawartość jednostek i ekonomii;
  - M3: magia, bohaterowie, pogoda, wycinek kampanii;
  - M4: całość.

**Późniejsze decyzje użytkownika** (10–11.11, zapisane w `docs/`) zmieniły wiele z powyższego:
- mecz trwa 30, 60 albo 90 minut (trzy profile);
- „mało lądu”, okręty zawsze widoczne;
- złoża się odnawiają;
- kadłub naprawia tylko dok, tylko za srebro;
- 4 zawody załóg zamiast 3;
- kampania nieliniowa, z flotą przenoszoną między misjami;
- wiatr i fale liczone w symulacji, ale bez wpływu na ruch w meczu;
- kotwica estetyczna to **Grecja IV w. p.n.e. (Attyka: Pireus, hangary w Zei, Laurion)**. Najpierw był XII w., potem krótko VII w.

Głównych dokumentów nie zaktualizowano. `CLAUDE.md` mówi tylko, że nowsze decyzje mają pierwszeństwo.

## 3. Stan faktyczny

Kod ma 183 pliki C# i ok. 59 tys. linii. Runtime to 111 plików i ok. 41 tys. linii, z tego symulacja ok. 8,7 tys., a prezentacja ok. 32 tys.

### 3.1 Rozgrywka (symulacja)

| Obszar | Stan |
|---|---|
| Tryb gry | Tylko potyczka 1v1 z SI na jednej symetrycznej mapie, w 3 profilach długości. Brak menu głównego: gra startuje od razu w bitwie. |
| Okręty | 7 klas: kuter, handlowiec, taranowiec, niszczyciel załóg, abordażowiec, niszczyciel okrętów, bombowiec |
| Broń | 9 typów: stanowiska łuczników i oszczepników, skorpion, gastrafetes, balista, katapulta, ciężkie machiny. Pociski mają lot, rozrzut i obszar rażenia. |
| Taran | Zapowiedź szarży; obrażenia zależą od prędkości, kąta i masy |
| Abordaż | Haki, rundy walki, morale, kapitulacja, przejęcie okrętu z bronią |
| Załogi | Żeglarze, szermierze, oszczepnicy, łucznicy i ranni. Port werbuje, leczy i przeszkala. |
| Ekonomia | Drewno i srebro, złoża odnawialne, magazyny lokalne, handlowce, przyczółki, eskorta, wraki, złom, naprawa w doku. Limit floty 100. |
| Budowle | Warsztat → Miasto → Szkoła Mędrców → broń ciężka, do tego Dok. Brak fortów i świątyń. |
| SI | Wydaje te same rozkazy przez mgłę wojny. Zna jednak pozycję startową wroga, broni się dopiero, gdy wróg jest 250 m od portu, i wysyła całą flotę naraz. |
| Mgła wojny | Siatka 104×104 pól po 25 m |
| Zwycięstwo | Utrata portu macierzystego albo floty bez możliwości odbudowy; jest remis |
| Morze i wiatr | Pełny model fizyczny (wiatr, fale, siły na żagiel i kadłub), deterministyczny i w zapisie. W meczu przełącznik wpływu na ruch jest zawsze wyłączony, więc służy tylko obrazowi. |
| Zapis | Wersja 17, jeden slot szybkiego zapisu, bez migracji starszych wersji |
| Brak całkiem | Kampania i misje (są tylko dokumenty i dane JSON), druga cywilizacja, bohaterowie, magia, potwory, multiplayer |

### 3.2 Obraz i dźwięk

- **Okręty:** proceduralny generator 7 klas z wariantami wyglądu, teksturami PBR, uszkodzeniami i łamanym masztem.
- **Załogi:** każdy człowiek ze stanu gry jest rysowany. Instancjonowane, z LOD, wiosłują w rytmie, walczą i przechodzą przez burty.
- **Port:** budowle (magazyn, warsztat, domy, mury, szkoła dorycka, dok typu Zea, obóz) pękają element po elemencie, a ruiny przetrwają zapis.
- **Środowisko (zestaw próbny):**
  - oliwki, makia i frygana poruszane wiatrem meczu;
  - rośliny klifów;
  - notch pływowy, beachrock, plaża żwirowa;
  - ruda typu Laurion z pyłem przy pracy;
  - sosny i cyprysy z Meshy, bez wiatru.
- **Woda i interfejs:** woda HDRP z falami z symulacji. Interfejs w UI Toolkit: minimapa, kolejki, plan załogi i broni, zmienialne skróty.
- **Dźwięk:** brak. W projekcie nie ma ani jednego `AudioSource` ani pliku audio.
- **Efekty:** częściowo. Są cząsteczki z kodu, nie ma VFX Graph ani ognia i dymu z prawdziwego zdarzenia.

### 3.3 Testy i jakość

- **Bramka M1 z 11.11:** PASS, EditMode 345/345, PlayMode 146/146.
- **Charakter testów:** przeważają testy prezentacji i generatorów (woda, assety, drzewa, zniszczenia). Testów reguł gry jest ok. 30–90, zależnie od tego, co liczyć.
- **Czego bramka nie mierzy:** GPU, wyglądu ani grywalności.

### 3.4 Wydajność

Pomiary na laptopie z RTX 3050 Ti, 1080p, z dokumentów w `docs/`:

| Profil | Klatka | Uwagi |
|---|---|---|
| Ultra | ok. 36–47 ms | Ogranicza GPU (28–42 ms), więc 60 FPS nie jest osiągalne |
| Low | ok. 13–23 ms | 60 FPS osiągalne tylko na części pomiarów |

- **Załogi:** w bitwie 40 okrętów kosztują 9–11 ms CPU wobec budżetu 1 ms.
- **Ograniczenia pomiarów:** testy dymne obejmują tylko pierwsze 60–90 s meczu, bez walki. Pomiary dla 100, 200 i 400 okrętów nie były robione.
- **Sprzęt docelowy jest opisany niespójnie:**
  - i5, 16 GB, RTX 3050 Ti (`CLAUDE.md`);
  - i7, 32 GB, RTX 5060 (`docs/art/ZALOGI.md`);
  - „brak wymagań” (`VISUAL_TECH_SPEC.md`).

### 3.5 Na czym skupiała się ostatnio praca

Z 40 ostatnich commitów:

| Obszar | Commity |
|---|---|
| Środowisko | 12 |
| Budowle i zniszczenia | 10 |
| Potok assetów (Meshy) | 8 |
| Zmiana epoki | 4 |
| Załogi | 4 |
| Woda | 2 |
| Symulacja (reguły gry) | 0 (ostatnia zmiana 10.11) |
| Interfejs | 1 |

## 4. Najważniejsze sprzeczności i napięcia

1. **Epoka.**
   - Obowiązuje IV w. p.n.e., ale `docs/art/ArtDirection.md`, generowana macierz załóg `ShipCrewCoverage.json` i profil wyglądu okrętów `aegean-12c` wciąż opisują XII w. (LH IIIC).
   - Ruda i stocznia mają formy z XII w., choć działa profil IV w.
   - Okręty mają jeden rząd wioseł i 12–28 m długości, a flota ateńska IV w. to triery: trzy rzędy wioseł, ok. 37 m.
   - Dane gry zawierają onager i „ogień grecki”, oba późniejsze niż IV w. p.n.e.
2. **Mit kontra realizm.**
   - Deklaracje: historyczny realizm, fizyka morza według norm IMO, źródła z poziomami pewności.
   - Jednocześnie mityczne biomy, potwory, zaklęcia, kampanie Eneasza i Odyseusza (czasy heroiczne) obok Persów i kultury IV w.
   - W tekstach misji pojawiają się rzymskie imiona bogów (Jowisz, Kybele).
3. **Pogoda.**
   - Specyfikacja: wpływa na grę, a misje M09 i M12 od niej zależą.
   - Decyzja: liczona, ale bez wpływu w meczu i bez sztormu.
4. **Długość meczu.**
   - Specyfikacja: 15–25 min. Decyzja: 30–90 min.
   - Mecze SI kontra SI trwają 13–38 min. Decyzja o tym czeka.
5. **Podział czasu gracza.** Ponad 70% na wodzie (wizja) wobec 50% bitew, 35% gospodarki i 15% osad (`docs/BALANS_I_SKALA.md`). Żaden z podziałów nie został zmierzony.
6. **Zasoby.**
   - Skończone (wizja, macierz odbioru) wobec odnawialnych (decyzja). `BALANS_I_SKALA.md` mówi o obu naraz.
   - Naprawa i złom zamieniają srebro na drewno, czyli łamią niezmiennik ekonomii.
7. **Kampania.** Liniowa, bez przenoszenia floty (specyfikacja) wobec nieliniowej, z flotą i weteranami (decyzja). Nie wiadomo, czy 14 opisanych misji nadal obowiązuje.
8. **Mapa.** Cieśniny jako oś rozgrywki (wizja, ekonomia) wobec decyzji „mało lądu”, która je usunęła.
9. **Filar walki.**
   - Założenie: ostrzał, taran i abordaż współdziałają, każdy ma kontrę.
   - Pomiar: dwa taranowce w lustrzanym ustawieniu nie zadają sobie obrażeń przez 300 s.
   - Pomiar: abordażowiec wygrywa każdy pojedynek 1:1, gdy przeciwnik do niego podpływa.
10. **Uczciwość SI.** Wymóg „SI nie wie nic spod mgły” wobec SI znającej pozycję startową wroga.
11. **Zakres M1.** Specyfikacja: 4 klasy i jedna mapa. Bramka M1 obejmuje już formacje, wiatr, pogodę i dużo testów sztuki. Rośnie warstwa wizualna, a dźwięku, menu i misji nadal nie ma.
12. **Interfejs.** Wymagane linie rozkazów i cztery wskaźniki stanu okrętu wobec paska HP widocznego tylko po zaznaczeniu i braku linii rozkazów.
13. **Wygląd materiałów.**
    - `CLAUDE.md` każe dodawać „rust, oil stains” do każdego opisu tekstury, a kierunek artystyczny zabrania jednego filtra brudu na wszystko.
    - `ArtDirection.md` wciąż powołuje się na Diablo II, choć to odwołanie wycofano.

## 5. Stan dokumentacji

- **Nieaktualne dokumenty uznane za obowiązujące.**
  - `docs/environment.md` (z 09.11) jest na liście obowiązujących w `CLAUDE.md`, ale twierdzi m.in., że:
    - jedyna broń to łucznicy;
    - zapis ma wersję 6;
    - pogoda nie należy do zapisu;
    - modele są zastępcze.
  - `CLAUDE.md` mówi o starym Input Managerze, choć przejście na Input System jest zakończone, i podaje sprzeczny z innym dokumentem numer karty graficznej.
- **Nieaktualne statusy w manifestach** (`ArtProductionManifest`, `VisualReworkManifest`, `AssetManifest`, część `EnvironmentAssetManifest`):
  - wiele zrobionych rzeczy ma status „planned” albo „placeholder”: oliwki, makia, frygana, płuczka, dok Zea, obóz, załogi na lądzie;
  - część manifestów jest generowana z kodu i pilnowana testami, więc poprawka wymaga zmiany generatora, nie pliku JSON.
- **Sprzeczności wewnątrz dokumentów.**
  - `ZALOGI.md` jednocześnie opisuje i zaprzecza przejściu abordażystów na wrogi pokład.
  - `MORZE_I_WIATR.md` ma nieaktualne zdania obok aktualnych.
- **Wspólny plik planu sesji** gromadzi plany 7 sesji. Część jest zrobiona, część rozjechała się z kodem, część przeczy sobie (epoka, sprzęt, rola keleustesa).
- **Brakujące artefakty**, których wymaga specyfikacja: `docs/requirements.csv`, `docs/rendering/compatibility.md`, sceny testów wizualnych.

## 6. Decyzje, które czekają na człowieka

- długość meczów SI;
- odległość podejścia zależna od broni;
- czy handlowiec wymaga Miasta;
- reguły przenoszenia w kampanii;
- klawisz pauzy;
- architektura multiplayera albo rezygnacja z niego;
- wiatr w rozgrywce;
- tożsamość drugiej cywilizacji;
- bohaterowie, zaklęcia i potwory;
- zakup 4 paczek assetów;
- licencja jednego modelu skał;
- pomiar w oknie gry, odłożony przez użytkownika.

## 7. Pytania do analizy

1. **Tożsamość produktu.** Historyczny RTS morski osadzony w IV w. p.n.e. czy mityczny RTS z bogami i potworami? Jeśli oba, gdzie leży granica i jak ją pokazać graczowi bez sprzeczności z ambicją wiarygodności?
2. **Epoka i skala.** Czy IV w. p.n.e. ma oznaczać triery (trzy rzędy wioseł, ok. 170 wioślarzy, ok. 37 m)? Gra ma dziś okręty 12–28 m i jeden rząd wioseł. Co wtedy z onagrem, ogniem greckim i wątkiem Eneasza i Odyseusza?
3. **Najbliższy cel.** Co jest minimalnym grywalnym „pionowym wycinkiem” przed dalszą pracą nad grafiką? Proponowane kandydatury: menu główne, dźwięk, jedna misja, poprawki balansu walki, uczciwa SI, testy z ludźmi.
4. **Pętla rozgrywki.** Który podział czasu (woda, gospodarka, osady) i która długość meczu (15–25 czy 30–90 min) lepiej służy fantazji „dowódcy floty”? Czy zasoby powinny być skończone, czy odnawialne?
5. **Wiatr i pogoda.** Model fizyczny jest gotowy. Warto go włączyć do rozgrywki (żagiel, dryf, okna pogodowe w misjach), czy zostawić jako tło?
6. **Sprzęt i wydajność.** Jaki sprzęt i jaki FPS przyjąć jako wiążące? Ultra nie osiąga 60 FPS na RTX 3050 Ti, a załogi przekraczają budżet CPU kilkukrotnie.
7. **Zarządzanie dokumentacją.** Jak ustanowić jedno źródło prawdy, czyli dokument decyzji wersjonowanych zamiast specyfikacji z 09.11 i nadpisujących ją notatek? Jak zsynchronizować statusy?
8. **Kampania.** 14 liniowych misji ze specyfikacji czy struktura nieliniowa z przenoszoną flotą? Ile realnie wnosi każda z nich przy obecnym stanie kodu?
