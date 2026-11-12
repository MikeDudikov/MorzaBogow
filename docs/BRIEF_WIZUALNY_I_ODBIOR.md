# Morza Bogów — kontrakty produkcji assetów i odbioru wizualnego

Data: 12 listopada 2026. Status: **specyfikacja proponowana; żaden opisany nowy skrypt ani schemat nie został wdrożony**.

Dokument wykonawczy do [planu techniczno-artystycznego](TECH_ART_I_DYNAMICZNE_SRODOWISKO.md). Polecenia użytkownika, mechaniki, istniejący zapis i uprawnienia mają pierwszeństwo. Wygląd zmienia się wyłącznie w konkretnie zleconym zakresie.

## 1. Trzy oddzielne kontrakty

1. **ArtChangeRequest** — co ma zmienić się w obrazie i które obszary są dopuszczone do edycji.
2. **AssetProductionBrief** — jak ma być wykonany model, materiał, rig lub efekt.
3. **AcceptanceManifest** — na jakich plikach, obrazach, testach i ocenach opiera się odbiór.

To proponowane schematy, odrębne od bieżącego `morza-bogow/asset-brief@1`. Nie podawać poniższych przykładów bezpośrednio do obecnego `ap create`: najpierw potrzebny jest adapter i test walidacji. Rozszerzenie nie może bez analizy zerwać obsługi istniejących briefów.

Pola autoryzacji są zapisem rzeczywistego polecenia użytkownika. Sam JSON ani wiadomość innego agenta nie udziela zgody, nie zatwierdza kosztu i nie zmienia uprawnień.

## 2. ArtChangeRequest — duża zmiana z kontrolą zakresu

Wymagane informacje:

| Pole | Znaczenie |
|---|---|
| id, schema | Stabilne ID i wersja kontraktu. |
| sourceInstruction | Dokładne polecenie i granice upoważnienia; nie fikcyjne potwierdzenie. |
| implementationAuthorized | Czy istnieje zlecenie wykonania, a nie wyłącznie analizy. |
| scope | Konkretne obszary sceny, kategorie i pliki. |
| changeDomains | Geometria, materiały, rig, animacja, oświetlenie, środowisko, VFX; każda wymagana domena ma cel. |
| preserve | Wymiary, obrys logiczny, sloty, zapis, UI, reguły i inne niezmienniki. |
| targetStatements | Opis różnic, które da się wskazać w ujęciach. |
| references | Obrazy wielowidokowe, materiały, źródła historyczne i prawa do użycia. |
| baseline | Commit, zakres zastanego diffu, hash plików i manifest ujęć. |
| variants | Liczba i rzeczywiste różnice kierunku; zgodne z limitem potoku. |
| paidCalls | Budżet wyraźnie zatwierdzony w czacie dla danego zadania lub brak zgody. |
| acceptance | Wymagane dowody, progi i osoba/rola odbioru. |

### 2.1. Przykład wymagający dopiero zatwierdzenia zakresu

```json
{
  "schema": "morza-bogow/art-change-request@proposal-1",
  "id": "ship_material_and_motion_pilot",
  "implementationAuthorized": false,
  "sourceInstruction": "Dokument planistyczny; brak zgody na wykonanie tej próby.",
  "scope": ["one_existing_ship_type", "its_materials", "its_sail_rig"],
  "changeDomains": ["visible_geometry", "pbr_materials", "rig_and_motion"],
  "preserve": ["catalog_dimensions", "logical_footprint", "weapon_slots", "gameplay_rules", "save_contract", "ui_style"],
  "targetStatements": [
    "Poszycie ma rozpoznawalną strukturę konstrukcyjną w zbliżeniu i czytelną sylwetkę w kamerze RTS.",
    "Drewno, płótno i metal reagują odmiennie na światło i moknięcie.",
    "Żagiel reaguje na wspólne pole wiatru, z zachowaniem mocowań."
  ],
  "variants": 2,
  "paidCalls": {"approved": false},
  "baseline": {"status": "TO_CAPTURE"},
  "acceptance": {"technical": "REQUIRED", "visual": "REQUIRED", "gameplay_regression": "REQUIRED"}
}
```

Nie wystarczy zmienić gradingu, aby zaliczyć zamówioną zmianę poszycia i żagla. Nie wystarczy duża różnica pikseli: można ją uzyskać także błędem ekspozycji. Odbiór sprawdza semantyczne cele i rzeczywiście używane assety.

## 3. AssetProductionBrief — charakter przed ozdobnikami

Przed rozpoczęciem produkcji opisać:

- przeznaczenie, sylwetkę i sposób rozpoznania obiektu z kamery strategicznej;
- co najmniej trzy cechy charakterystyczne, wynikające z budowy, użycia lub świadomie wybranego kierunku artystycznego;
- materiały i ich rozmieszczenie, technikę łączenia, grubości, kierunek włókien, funkcjonalne części;
- stan obiektu: nowy, użytkowany, naprawiony, uszkodzony; wieku nie zastępuje losowy brud;
- docelowe odległości kamery i rodzaj zbliżenia;
- wymagane części ruchome, rig, punkty kontaktu, mocowania i punkty montażowe;
- źródła oraz poziom pewności dla realiów; odrębne odstępstwa mitologiczne;
- geometrię, LOD, materiały, mapy, rozdzielczości, eksport i testy;
- obszary, w których generatorowi wolno proponować warianty.

### 3.1. Karta referencji

Każda referencja ma: ID, źródło, autora/instytucję, datę odczytu, prawa do wykorzystania, przedstawiony obiekt/okres, widok i zakres, który rzeczywiście uzasadnia. Zdjęcie nowoczesnej rekonstrukcji nie jest automatycznie źródłem historycznej polichromii, a obraz koncepcyjny nie jest dowodem archeologicznym.

Dla wyglądu poświadczonego i rekonstruowanego używać oddzielnych pól. Rekonstrukcja może być spójna i przekonująca, bez udawania dokładnej wiedzy o nieznanej barwie lub wymiarze. Szczegóły sprawdzić według [protokołu źródłowego](HISTORIA_I_ZRODLA.md).

### 3.2. Przykład briefu materiału

Zamiast „ultra realistic wooden ship 4K” opisać: gatunek lub przyjęty typ drewna po weryfikacji źródłowej; kierunek cięcia i włókien; osobne powierzchnie czołowe; miejsca łączeń; użyte powłoki; naprawione fragmenty; ślady kontaktu i słońca; linię moknięcia; różnicę między drewnem suchym i mokrym; oświetlenie neutralne bez namalowanych refleksów. Nie wpisywać domniemanej antycznej techniki jako faktu bez źródła.

Tekstura ma wspierać bryłę. Jeżeli sylwetka wymaga wystającej deski lub wyraźnego łączenia, nie zastępować go fotografią namalowaną na płaskiej powierzchni.

### 3.3. Karta jakości tekstur

Wymagane osobne pola:

| Pole | Kontrola |
|---|---|
| sourceResolution | Rzeczywiste wymiary pliku każdej mapy. |
| requestedResolution | Zamówiony master, np. 4096×4096. |
| importerMaxSize | Faktyczna konfiguracja Unity. |
| residentResolution | Rozdzielczość używana podczas ujęcia odbiorowego, gdy streaming ma znaczenie. |
| mapRole | Albedo, normal, metallic, roughness/smoothness, AO, height, detail. |
| encoding | sRGB lub dane linear; normal convention; bit depth. |
| uvAndDensity | Rozkład UV, texele/metr, szwy, kierunki wzoru, rozciągnięcia. |
| provenance | Narzędzie/model, wersja lub brak informacji, seed, parametry, prawa. |

Próg 4K dotyczy map wymienionych w briefie; nie wymusza bezsensownego powiększania stałej maski zawierającej wyłącznie jedną wartość. Użycie takiej maski i jej faktyczna rola muszą być jawne. Wymagań na detale nie obniża się cicho przez ustawienie importera na 1024.

## 4. Kontrakt automatycznej animacji

### 4.1. Semantyka

Proponowany manifest przechowuje: semanticType, części, osie lokalne, punkty mocowania, dopuszczalne deformacje, sztywność lub skalibrowany profil, rig i klipy, źródła, stopień pewności oraz funkcję w grze.

Przykład opisowy: `sail_main` jest żaglem z kotwami przy reji i rogach; `oar_port_03` ma punkt obrotu w dulce i zadaną stronę; `bird_01` ma szkielet skrzydeł i profile lotu. Nazwa bez weryfikacji geometrii nie wystarcza. Niespójne kotwy, brak kości albo nieznana anatomia dają NEEDS_CLASSIFICATION lub NEEDS_RIG.

### 4.2. Źródło ruchu

| Wielkość | Właściciel |
|---|---|
| Ruch poziomy okrętu, legalność, trafienia | World. |
| Wiatr i rozstrzygający stan morza | MarineState oraz zatwierdzony stan pogody. |
| Faza wioseł | Stan napędu i obsady, nie sama prędkość przesuwania się modelu. |
| Widoczny ruch kadłuba | Istniejący SeakeepingDriver, bez dodawania drugiego kołysania. |
| Żagiel, lina, gałąź, opad | Adapter wspólnego wiatru, odpowiedni profil i ten sam czas gry. |
| Animacja ataku/przejęcia | Prezentacja potwierdzonego zdarzenia, bez rozstrzygania wyniku eventem klipu. |
| Losowanie dekoracji | Oddzielny seed i generator, bez wpływu na RNG walki. |

Wizualne solver-y mogą być przybliżone i zależne od LOD. Zmiana profilu jakości nie zmienia logicznego wyniku. Bitowej zgodności symulacji zmiennoprzecinkowej między wszystkimi platformami nie wywodzi się z samego użycia seeda — wymaga osobnej weryfikacji.

### 4.3. Profile i granice weryfikacji

Profil ma wersję i kartę źródłową. Rozdziela pomiar, rekonstrukcję, przybliżenie animacyjne i fikcję. Brak danych nie jest powodem wymyślania masy, częstotliwości pracy skrzydeł lub oporów i oznaczania ich jako prawdy naukowej. Można zastosować jawnie artystyczny profil pilotażowy, jeżeli użytkownik zlecił taki zakres; nie otrzymuje on etykiety naukowo zweryfikowanego.

Automatyzacja może zastosować zatwierdzony profil do setek poprawnie opisanych assetów. Nie gwarantuje prawdziwej biomechaniki nieznanego stworzenia ani odpowiedniego rigu nieopisanej siatki.

## 5. Kompilacja promptów i odtwarzalność

Obecne 800 znaków to obserwowane ograniczenie kodu projektu, nie wspólna specyfikacja wszystkich dostawców. Nowy kompilator ma:

1. zachować pełny brief jako źródło;
2. sprawdzić limit i możliwości konkretnego endpointu w bieżącej dokumentacji;
3. priorytetyzować twarde ograniczenia, konstrukcję, materiał i cechy charakterystyczne;
4. pokazać finalny prompt, długość, obrazy i parametry przed wysłaniem;
5. wykazać, co zostało skrócone lub pominięte;
6. blokować wysłanie, jeśli zniknął wymóg konieczny;
7. wysyłać negative prompt tylko do dostawcy rzeczywiście obsługującego taki parametr;
8. zapisać ID zadania, payload bez sekretów, wynik, wersję dostawcy/modelu, jeśli raportowana, i faktyczny koszt;
9. nie obiecywać identycznego wyniku przy nieprzypiętej wersji zdalnego modelu.

Własne poprawki, retry i ponowne płatne generowanie są oddzielnymi operacjami. Wybranie „latest” w konfiguracji utrudnia ścisłą odtwarzalność; jeśli API nie pozwala przypiąć wersji, odnotować ograniczenie i zachować wszystkie wyniki wejściowe/wyjściowe.

Globalny weathering jest pusty. Zachować wybór stanu materiału wynikający z briefu, bez dopisywania wszystkim assetom tego samego wieku i wykończenia. To nie usuwa tożsamości artystycznej ani możliwości wykonania assetu o konkretnie zamówionym stanie.

## 6. Twarde bramki

Wynik jest odrzucany, jeśli choć jedna wymagana bramka nie przeszła:

- GATE-SCOPE: zmieniono chroniony zakres lub nie da się wskazać zgody na zmianę produktu.
- GATE-IMPORT: błędy kompilacji, niepoprawny import, brak shadera, materiału, kości lub referencji.
- GATE-BINDING: prezentacja używa niewłaściwego modelu lub materiału; brak dowodu rzeczywistego podpięcia.
- GATE-GEOMETRY: naruszone wymiary, osie, obrys, sloty lub wymagane kotwy.
- GATE-PBR: nieprawidłowe role map, utracony detal przez importer, błędny kolor danych lub inne naruszenie kontraktu.
- GATE-MOTION: poważne penetracje, niepoprawne kotwy, podwójne kołysanie lub brak wymaganej reakcji.
- GATE-GAMEPLAY: kosmetyka zmieniła nieautoryzowane reguły, widoczność lub RNG gry.
- GATE-SAVE: utrata nowego stanu lub niejawna zmiana formatu pod starym numerem wersji.
- GATE-EVIDENCE: obrazy/testy pochodzą z innego snapshotu albo nie wykonano wymaganej próby.
- GATE-RIGHTS-COST: brak wymaganych praw lub zgody na konkretny koszt.

Przykłady te nie zastępują ani nie osłabiają istniejącej `.claude/qa/policy.json`. Ich przyszłe egzekwowanie wymaga implementacji i niezależnego review.

## 7. Rubryka artystyczna

Proponowana skala pilotażu: 0 — brak; 1 — poważnie nietrafione; 2 — częściowo spełnione; 3 — zgodne z uzgodnioną referencją i kontraktem; 4 — ponad uzgodniony poziom, bez utraty spójności.

Oceniane osobno: sylwetka/konstrukcja, zachowanie materiałów, charakter/tożsamość, wiarygodność ruchu, czytelność z kamery RTS, stabilność obrazu w ruchu. W pilotażu proponuje się co najmniej 3 w każdej wymaganej kategorii i brak wad krytycznych. Progi trzeba uzgodnić przed uznaniem ich za obowiązujące. Średnia nie kompensuje nieprawidłowego rigu albo plastikowego materiału.

Ocenę przypisać do konkretnego wykonawcy i ujęć. Jeśli obrazu nie obejrzał niezależny reviewer lub człowiek, wynik wizualny pozostaje NIEODEBRANY. Ocena modelu językowego wspiera przegląd, ale nie daje obiektywnego certyfikatu najwyższej jakości artystycznej.

## 8. Pakiet dowodowy

AcceptanceManifest powinien obejmować:

- runId, commit, własny diff, hashe zależnych assetów i ustawień;
- wersję Unity/pakietów, API renderowania, profil, rozdzielczość i konfigurację konkretnego pomiaru;
- ID assetu, GUID prefabu, rzeczywiście użyte renderery, materiały i shadery;
- źródła, licencje, narzędzia, seed, payloady bez sekretów;
- wyniki walidacji geometrii, map, animacji i zachowania;
- obrazy kontrolne i docelowe, wideo, liczby z pomiarów i wyniki bramki;
- review, nierozwiązane ograniczenia i dokładny następny krok.

Planowane foldery wyników mogą wykorzystywać `ArtWorkspace/jobs/<job>/` dla produkcji i `work/visual-review/<run>/` dla prób. Nie tworzyć ich w ramach samego czytania dokumentu. Przeniesienie ostatecznych raportów do docs odbywa się poza bramką. Zapisane wyniki nie stają się nową prawdą o całym projekcie.

### 8.1. Ujęcia wymagane w pilotażu

1. Neutralne światło i obrót assetu: przód, bok, tył, góra, trzy czwarte, zbliżenia kontaktów materiałów.
2. Kamera RTS: jedna jednostka, grupa i tło portowe; czytelność właściciela i roli.
3. Światło boczne, słońce za żaglem, cień i sztorm.
4. Powierzchnia sucha, mokra i wysychająca.
5. Animacja: praca, spoczynek, zmiana kierunku wiatru, przejście LOD.
6. Uszkodzenia: przed zdarzeniem, w trakcie i po nim; stan po wczytaniu.
7. Wideo co najmniej jednej reprezentatywnej sekwencji z ruchem kamery, wodą, cienkimi elementami i VFX. Długość oraz progi ustalić w G0; pojedyncza klatka nie zalicza tego wymagania.

Parę before/after wykonywać z identycznymi parametrami. Osobno pokazać pełny zamówiony kierunek obrazu. Zmierzyć zmianę tylko w odpowiednim obszarze; wynik SSIM/MAE lub innej metryki jest pomocniczy. Nie ustawiać odbioru na zasadzie „im większa różnica, tym lepiej”.

## 9. Macierz testów środowiska

Poniższe nazwy są ID wymagań, nie istniejącymi nazwami testów Unity. Implementer przypisuje je do faktycznych testów i artefaktów.

| ID | Próba | Oczekiwanie |
|---|---|---|
| E01 | Spokój → sztorm → spokój | Ciągłość stanu i opóźniona odpowiedź fal według modelu. |
| E02 | Zmiana kierunku wiatru | Tafla, żagiel, roślinność i opad korzystają z jednego źródła danych. |
| E03 | Pauza w połowie frontu | Brak ticków, faz i kosztów rozgrywki; zgodna polityka wizualna. |
| E04 | Zapis w przejściu pogodowym | Odtworzone cele interpolacji, stan i RNG; brak resetu do spokoju. |
| E05 | Zmiana tempa | Brak niezależnego przyspieszenia chmur/deszczu względem świata. |
| E06 | Różne profile obrazu | Ten sam wynik symulacji i dostęp do informacji taktycznej. |
| E07 | Ślad za niewidocznym wrogiem | Brak wycieku przez pianę, dym, ptaki, cienie i reakcje efektów. |
| E08 | Fale z różnych kierunków i ładunek | Odpowiedź kadłuba zgodna z wybranym modelem i jego ograniczeniami. |
| E09 | Ruch względem prądu | Kilwater i opory nie wynikają wyłącznie z prędkości względem lądu. |
| E10 | Deszcz pod dachem i na pokładzie | Poprawne osłony, moknięcie i wysychanie. |
| E11 | Powtórzone ID zdarzenia | Jedna emisja/rozliczenie, bez powielania efektów. |
| E12 | Odtworzenie destrukcji | Ten sam stan ruin i wraku; brak ponownego odgrywania historycznych uderzeń. |
| E13 | Gruz jako przeszkoda | Spójność World, nawigacji, AI i zapisu, jeśli ta funkcja została zlecona. |
| E14 | Ptak przy maszcie i brzegu | Unikanie przeszkód i brak skoków animacji lub przenikania. |
| E15 | Nieznany asset | Jawna klasyfikacja do uzupełnienia, bez pozornej „naukowej” animacji. |
| E16 | Przejście LOD animacji | Ciągłość fazy, mocowań i sylwetki. |
| E17 | Powrót niewidocznego obiektu | Poprawne odtworzenie wyglądu i stanu, bez zaległych efektów. |
| E18 | Wariant 4K w grze | Prawidłowe mapy źródłowe, import, rzeczywista rezydencja i detal. |
| E19 | Zmiana wyłącznie prezentacyjna | Brak nieautoryzowanych różnic w stanie World i RNG. |
| E20 | Wyczerpanie puli VFX | Kontrolowane zachowanie bez naruszenia logiki i zapisu. |
| E21 | Zanik usługi/API podczas produkcji | Bezpieczne wznowienie bez automatycznego podwójnego kosztu. |
| E22 | Nowy prefab w gotowym buildzie | Faktyczne wiązanie, brak efektu „zmieniono tylko pliki nieużywane”. |

W testach numerycznych tolerancje wynikają z rozdzielczości siatki, interpolacji i metody całkowania. Niewyznaczony próg nie może otrzymać PASS. Różne platformy i API mają własne dowody; nie deklarować uniwersalnej deterministyczności z jednego urządzenia.

## 10. Przekazanie do Claude Code

Zadanie powinno podawać: MODE, etap G, powiązane P/ID/Q/J, AssetProductionBrief, zakres i niezmienniki, bieżący baseline, model wymagany dla roli, pliki, koszty, źródła, testy i sposób odbioru. Brak upoważnienia do implementacji pozostawia tryb analizy.

Dokumentacja nie tworzy nowych agentów ani nie zmienia działającej sesji. Gotowy sposób zlecenia zawiera [PROMPT_TECH_ART_DLA_CLAUDE_CODE.md](../PROMPT_TECH_ART_DLA_CLAUDE_CODE.md).
