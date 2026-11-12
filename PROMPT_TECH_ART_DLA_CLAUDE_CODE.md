# Claude Code — produkcja obrazu i dynamicznego środowiska

Jedynym nadrzędnym planem produktu jest [docs/NOWY_PLAN_GRY.md](docs/NOWY_PLAN_GRY.md), wraz z §0. Rama: mitologiczna Grecja IV w. p.n.e. (400–301 p.n.e., z 400 r. włącznie). Przed każdą mutacją, po niej i przy odbiorze stosuj [kartę zgodności](docs/KARTA_ZGODNOSCI_Z_PLANEM.md). Propozycja techniczno-artystyczna nie ustanawia innych założeń i nie może obchodzić planu.

Pracuj po polsku w `C:\Users\noirp\MorzaBogow`. Przyjmij odpowiedzialność techniczną studia produkującego grę: rozwijaj istniejące systemy, podejmuj decyzje na podstawie kodu, źródeł, obrazu i testów oraz jawnie oddzielaj plan, implementację i odbiór.

**Samo odczytanie tego promptu nie jest zgodą na wdrożenie zmian wyglądu, instalację pakietów, generowanie płatnych zasobów ani rekonfigurację sesji.** Domyślny tryb to ANALIZA. Polecenie użytkownika ma pierwszeństwo. Nie aktywuj ponownie całego studia i nie przerywaj trwających operacji innych wykonawców.

## Parametry wykonania

```text
TRYB = ANALIZA
ETAP = G0
ZAKRES = audyt istniejącej produkcji obrazu i środowiska
ASSET_LUB_SCENA = do wskazania dla konkretnego wdrożenia
ZGODA_NA_PŁATNE_ZLECENIA = brak
ZGODA_NA_INSTALACJE = brak
```

ANALIZA: odczyt, źródła, propozycje, dokumenty i plan dowodów. Bez zmian produktu, konfiguracji, agentów, hooków i działającej sesji. Nie inicjalizuj ani nie przepisuj istniejącego dziennika na potrzeby samego audytu.

WDROŻENIE: dopuszczalne dopiero po wyraźnym poleceniu użytkownika, z określonym etapem i zakresem. Taka zgoda obejmuje rutynowe, odwracalne prace konieczne w tym zakresie, bez ponawiania pytań o każdy krok. Nie jest zgodą na nieokreślone koszty, nowe usługi, publikację, usuwanie cudzej pracy ani objęcie zmianą całej gry.

## 1. Odczyt i ustalenie rzeczywistego punktu pracy

Przeczytaj:

1. [CLAUDE.md](CLAUDE.md), [AGENTS.md](AGENTS.md) i [protokół komunikacji](PROMPT_KOMUNIKACJA_STUDIA.md).
2. [Plan techniczno-artystyczny](docs/TECH_ART_I_DYNAMICZNE_SRODOWISKO.md) oraz [kontrakty produkcji i odbioru](docs/BRIEF_WIZUALNY_I_ODBIOR.md).
3. [Specyfikację obrazu](VISUAL_TECH_SPEC.md), [istniejący pipeline](docs/ASSET_PIPELINE.md), [plan gry](docs/NOWY_PLAN_GRY.md), [integrację P01–P12](docs/PLAN_WDROZENIA.md), [kontrakty grywalności](docs/GRYWALNOSC_I_ODBIOR.md) i [protokół źródłowy](docs/HISTORIA_I_ZRODLA.md).
4. [Organizację studia](PROMPT_STUDIO_PREMIUM.md) i [matrycę modeli](PROMPT_STUDIO_FABLE.md), bez ponownego instalowania lub przepisywania ich konfiguracji.

Zweryfikuj HEAD, gałąź, cudze zmiany, aktualne pakiety, wersję zapisu, najnowsze raporty, aktywnych autorów i stan bramki. Odczyt z dokumentu jest punktem odniesienia, nie poleceniem cofnięcia kodu. Stare raporty nie certyfikują dzisiejszego buildu.

Sprawdź istniejące: MarineState/WindField/WaveField/SeaSurface/ShipDynamics, SeaSurfaceRenderer, SeakeepingDriver, WeatherController, ShipVisual/ShipLookBook, DestructionModel/StructureGraph/DestructionBook oraz Tools/asset_pipeline. Ustal, co działa, co jest wyłączone, a co tylko opisane. Nie buduj drugiego systemu o tej samej odpowiedzialności.

## 2. Weryfikacja źródeł

Samodzielnie otwórz w tej sesji wiarygodne źródła dla podstaw historycznych i dla zależnych od zadania twierdzeń o konstrukcji, materiałach, ruchu, przyrodzie i mitologii. [Załączony rejestr](docs/research/sessions/2026-11-12-0325-tech-art.md) jest wskazaniem źródeł, nie Twoim osobistym odczytem.

Rozdziel: fakt w zakresie źródła, rekonstrukcję, kalibrację modelu, przybliżenie animacyjne, abstrakcję gry i fikcję mitologiczną. Parametr wybrany dla jakości ruchu nie staje się pomiarem antycznej jednostki. Mit nie potwierdza fizycznej prawdziwości mocy. Współczesna dokumentacja narzędzia nie potwierdza jego lokalnego działania.

Zapisz zakres dostępu i ograniczenia bez fikcyjnych stron ani cytatów. BLOCKED_RESEARCH dotyczy zależnego zakresu; kontynuuj niezależne prace techniczne. Nie wprowadzaj internetowych zapytań o naukę do runtime gry.

## 3. Kontrakt zmiany

Przed wdrożeniem przygotuj konkretny ArtChangeRequest i AssetProductionBrief według dokumentu odbioru. Podaj:

- co ma się rzeczywiście zmienić w geometrii, materiałach, animacji lub środowisku;
- referencje, cechy charakterystyczne i cel widoczny z kamery RTS;
- dokładne pliki, dopuszczony zakres i zachowane niezmienniki;
- wymagane mapy 4K, profile ruchu, punkty mocowania i podpięcia;
- aktualne ograniczenia pipeline'u i potrzebne, lokalne modyfikacje;
- źródła, koszt, zależności, testy i sposób odbioru.

Starsza ochrona estetyki pozostaje regułą domyślną. Wyraźne późniejsze zlecenie wizualne użytkownika ma pierwszeństwo tylko w zleconym obszarze. Nie znoś globalnie ochrony projektu. Przy braku zleconego zakresu wykonaj analizę i przygotuj konkretną propozycję.

Globalny szablon weathering jest pusty. Stan i wykończenie powierzchni wynikają z briefu. Nie przywracaj automatycznego dopisywania wieku i zużycia wszystkim assetom. Dla konkretnego zamówionego obiektu można zaprojektować odpowiedni stan materiału.

## 4. Modele i współpraca

Zachowaj przypisania: Opus — koordynacja/architektura; Fable — kod, skrypty, refaktoryzacja, testy i naprawy; odrębny Opus — niezależne review; Haiku — proste wyszukiwanie. Nie zmieniaj modeli ani ustawień globalnych w ramach tego promptu.

W trybie WDROŻENIE sprawdź konfigurację istniejącym Sprawdz-StudioPremium.ps1 oraz rzeczywiste modele potrzebnych ról w metadanych klienta. Nazwa agenta nie jest dowodem modelu. Nie generuj kodu na innym modelu i nie obchodź reguły przez ręczne zastosowanie patcha. Przy niedostępnym Fable oznacz zależne programowanie BLOCKED_MODEL; niezależna analiza może trwać.

Każdy autor dostaje cel, P/ID/Q/J albo uzasadnione N/A, etap G, pliki, baseline, kontrakt i odbiór. Przed edycją wymagaj rzeczywistego ACK. Jeden autor na plik, jeden koordynator dziennika, jeden właściciel Unity. Nie uruchamiaj zbędnych ról. Reviewer odczytuje rezultat i dowody, a poprawki kodu wracają do Fable.

## 5. Kolejność pracy

W ANALIZA wykonaj tylko G0: raport możliwości i ograniczeń, odczyt payloadów, wiązań, źródeł oraz propozycję najmniejszego kompletnego przyrostu. Nie uruchamiaj G1–G8 ani usług generacyjnych tylko dlatego, że przeczytałeś plan.

Po jawnym zleceniu WDROŻENIA realizuj wskazany etap i wykonane zależności:

- G1: kontrola narzędzi, kompilacja briefu, audyt podpięcia i powtarzalne ujęcia.
- G2: jeden istniejący typ okrętu jako pionowy przykład jakości modelu, PBR 4K i ruchu.
- G3: wspólny stan pogody, zegar i zapis.
- G4: odbiór wpływu sztormu na ruch zgodnie z P07 i docs/KONTRAKTY_MORZA_I_WIATRU.md (WM01–WM03).
- G5: chmury, deszcz, moknięcie, piana, rozbryzgi i dźwięk.
- G6: klasyfikowana biblioteka ruchu dla żagli, wioseł, roślinności, ptaków i załogi.
- G7: przygotowana destrukcja, graf podparcia, ruiny i wraki.
- G8: skalowanie, integracja, testy, build i odbiór.

Nie odtwarzaj wykonanej funkcji. Nie traktuj etapów G jako zastępstwa planu mechanik. Dużą zmianę artystyczną można realizować przez małe techniczne przyrosty, zachowując jej uzgodniony efekt docelowy.

## 6. Zasady produkcji assetów

Używaj istniejącej kolejki i adapterów. Preferuj udokumentowane CLI/API: Blender Python, Substance Automation Toolkit, Painter Python/remote scripting, obecne Meshy/ComfyUI i narzędzia C# Unity. Houdini jest opcjonalnym etapem authoringu/bakingu. Każde nowe narzędzie ma osobną próbę kompatybilności. Nie instaluj drugiego mostu MCP obok działającego.

Przed wysłaniem do generatora pokaż lub zapisz finalny payload bez sekretów oraz raport skróceń. 4K w tekście nie zastępuje rozdzielczości pliku i importera. Dostosuj zatwierdzony profil budżetu, jeśli obecny limit kategorii blokuje wymagane 4K; nie wyłączaj walidacji.

Modele: najpierw konstrukcja i sylwetka, potem topologia, UV, baking, materiały i rig. Rozdziel właściwości drewna, tkaniny, kamienia, metalu i powłok. Sprawdź kanały HDRP, roughness/smoothness, sRGB danych, normal mapy, skalę detalu i brak namalowanego światła. Obserwuj wygląd w kilku warunkach oświetlenia.

Automatyczna animacja wymaga rozpoznanej klasy, poprawnego rigu lub kotew i zweryfikowanego profilu. Nieopisane assety mają NEEDS_CLASSIFICATION/NEEDS_RIG. Nie obiecuj poprawnej fizyki każdego dowolnego modelu. Animator/klip/VFX nie rozstrzyga mechanik gry.

## 7. Zasady morza i środowiska

Zachowaj jeden rozstrzygający model morza. Obraz tafli, ruch kadłuba, wiatr w żaglach i opad mają wspólne źródło danych i czas. Nie dodawaj drugiego sinusoidalnego kołysania ani niezależnego dużego falowania HDRP ponad istniejącą powierzchnię symulacji.

WeatherController ma odzwierciedlać stan świata. Nie włączaj wpływu wiatru na ruch przy okazji samego zlecenia kosmetycznego. Nie dodawaj zalewania, wywracania, nowych obrażeń lub kar celności bez kontraktu mechaniki. D/S nie są kilogramami.

Deszcz respektuje dachy i maski powierzchni; moknięcie i schnięcie zależą od materiału. Ptaki korzystają z osobnego generatora i nie zdradzają ukrytego stanu przeciwnika. Destrukcja wizualna jest sterowana zdarzeniami; istotny dla rozgrywki gruz ma stan w World i zapisie, a nie tylko losowy collider PhysX.

Mitologiczny wyjątek ma oznaczoną fikcję, źródło tradycji, własne ID i zasady. Zachowaj mgłę wojny, pauzę, wersjonowanie zapisu i tożsamość jednostek.

## 8. Odbiór i ponowna kontrola

Przed bramką: zintegrowany snapshot, FREEZE/freezeId, rzeczywiste ACK autorów i brak równoległych zapisów. Nie zmieniaj hooków, polityki ani wyników, żeby uzyskać PASS. Nie uruchamiaj drugiego Edytora/runnera tego projektu.

Odbierz osobno: technikę, zachowanie, obraz i produkcyjne pochodzenie. Wymagaj faktycznego podpięcia modelu, materiału i efektu w grze. Ujęcia before/after muszą mieć zgodne parametry; duża różnica pikseli nie jest automatycznie lepszą jakością. Obejrzyj również ruch, kontakty, LOD i stabilność cienkich elementów.

Testuj odpowiednie przypadki E01–E22 oraz istniejące kontrakty. Dla C# wykonaj wymaganą kontrolę kompilacji. Renderowe dowody pozyskuj przez działającą ścieżkę grafiki; nie certyfikuj obrazu przez -nographics. Pomiar dotyczy konkretnego buildu i konfiguracji, bez dopisywania wymagań sprzętowych gry lub cichego obniżania jakości Ultra.

Po wykonaniu autoryzowanego przyrostu ponownie sprawdź spójność z zastaną pracą, zakresem estetycznym, mechanikami, źródłami, zapisem, widocznością i dowodami. Raport zawiera: co wykonano, własne pliki, snapshot, rzeczywiste modele, źródła, artefakty, review, wyniki, ograniczenia i najbliższy krok. NOT_RUN/BLOCKED/FAIL nie oznaczają PASS.

**Zacznij od G0 w trybie ANALIZA, chyba że bieżące polecenie użytkownika wyraźnie zleca konkretny etap WDROŻENIA. Nie ingeruj w trwającą sesję ani nie rozpoczynaj pozostałych etapów samym odczytem tego pliku.**
