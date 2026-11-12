# Morza Bogów — produkcja obrazu i dynamicznego środowiska

Data przygotowania: 12 listopada 2026, Europe/Warsaw. Autor: Codex. Status: **PROPOZYCJA DO ODBIORU, BEZ WDROŻENIA**. Odczytany HEAD: `7b747f7`, gałąź `balans-skala`, z cudzymi zmianami roboczymi. To identyfikator odczytu, nie polecenie cofnięcia projektu.

Dokument odpowiada na pytania o realistyczne materiały 4K, modele, shadery, animację, destrukcję, sztorm, deszcz, niebo, ptaki i automatyzację przez Claude Code. Nie jest raportem wykonania tych funkcji. Nie zmienia kodu, assetów, ustawień, planu mechanik ani bieżącej sesji agentów.

Dokumenty towarzyszące:

- [Kontrakty assetów, zmian wizualnych i odbioru](BRIEF_WIZUALNY_I_ODBIOR.md).
- [Prompt dla Claude Code](../PROMPT_TECH_ART_DLA_CLAUDE_CODE.md).
- [Rejestr odczytu źródeł](research/sessions/2026-11-12-0325-tech-art.md).

## 1. Decyzja techniczna

Rozwijać istniejącą architekturę: czysta symulacja C# → snapshot i zdarzenia → prezentacja HDRP. Istniejący pipeline Python/WSL/Blender/Meshy rozszerzyć o kontrolowane narzędzia produkcyjne i odbiór wizualny. Zachować jednego koordynatora, matrycę modeli, rezerwacje plików i jednego runnera Unity.

Docelowy warsztat: **Unity HDRP + istniejące Marine/ShipDynamics + Blender Python + materiały Substance + obecny pipeline generowania + kontrola sceny przez jeden most Unity MCP + powtarzalne testy obrazu i zachowania**. Houdini jest opcjonalnym narzędziem produkcji przygotowanej geometrii i efektów. Nie jest warunkiem uruchomienia całego planu.

„Premium”, „AAA” i „AAAA” traktujemy jako ambicję jakościową. Nie stanowią technicznego certyfikatu. Żaden generator ani prompt nie daje gwarancji najwyższej jakości każdego rezultatu. Można egzekwować poprawność danych, powtarzalność, progi techniczne, niezależne review i jawny odbiór artystyczny.

## 2. Potwierdzony punkt startowy

Poniższe ustalenia pochodzą z plików, nie z uruchomienia Edytora. Nie potwierdzają wyglądu aktualnego buildu ani wyników testów po późniejszych zmianach.

| Obszar | Odczytany stan | Konsekwencja |
|---|---|---|
| Silnik | Unity `6000.3.28f1`; HDRP, Shader Graph i VFX Graph `17.3.0` | Rozszerzać HDRP; brak uzasadnienia do migracji renderera. |
| Narzędzia | Burst `1.8.31`, Input System `1.20.1`, Unity Test Framework `1.6.0` | Wykorzystać istniejące podstawy. Sama obecność Burst nie oznacza przyspieszenia całego World. |
| Granica logiki | `MorzaBogow.Simulation.asmdef` ma `noEngineReferences: true` | Unity/PhysX/VFX nie mogą być nowym arbitrem reguł gry. |
| Zapis | `SaveGame.Version = 19`; zapisuje Marine | Wersję sprawdzić ponownie przed implementacją. Zmiana kontraktu zapisu wymaga wersjonowania. |
| Morze | `MarineState`, `WindField`, `WaveField`, `SeaSurface`, `ShipDynamics` | Nie tworzyć drugiego niezależnego modelu morza. |
| Widoczna tafla | `SeaSurfaceRenderer` i compute shader przenoszą składowe powierzchni symulacji do HDRP Water Decal | Zachować wspólną fazę fal widocznych i odczuwanych przez kadłub. |
| Ruch kadłuba | `SeakeepingDriver` wykorzystuje funkcje dynamiki, ale jest prezentacją | Nie mylić kołysania wizualnego z rozstrzyganiem nawigacji. |
| Pogoda | `WeatherController` interpoluje parametry wody, mgły, ekspozycji i słońca; opisuje się jako presentation-only | Potrzebne jest powiązanie z autorytatywnym stanem pogody; nie samo ściemnienie obrazu. |
| Wpływ na ruch | `World.EnableMarineForMatch()` przekazuje `windAffectsMovement = false` | Sztorm wpływający na ruch wymaga osobnego odbioru mechaniki. |
| Destrukcja | `DestructionModel`, `StructureGraph`, `DestructionBook`, testy destrukcji | Rozszerzyć istniejące zdarzenia, graf i zapis wyglądu. |
| Wygląd jednostek | `ShipLookLibrary`, `ShipLookComposer`, `ShipLookBook`, `ShipVisual` | Zachować trwałą tożsamość jednostki i jej punktów montażu. |
| Pipeline | `Tools/asset_pipeline`, `AIAssetImporter`, `AIAssetValidator` | Nowe narzędzia integrować jako etapy/adaptery tego potoku. |
| MCP projektu | W `.mcp.json` są `morza-bogow-assets` i `meshy`; nie wykryto pakietu mostu Unity MCP | Konfiguracja nie dowodzi dostępności usług. W tym czacie brak narzędzi live Unity. |
| Sceny | Włączona `M1_Battle`; `M0_Probe` i `Art_Integration_Coast` niewłączone w Build Settings | Scena artystyczna jest kandydatem na próbę; najpierw sprawdzić jej zawartość. |

Odczytane ścieżki: `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `ProjectSettings/GraphicsSettings.asset`, `ProjectSettings/EditorBuildSettings.asset`, assembly symulacji/prezentacji oraz wymienione klasy. Historyczne [environment.md](environment.md) opisuje starszy punkt dowodowy; nie zastępuje obecnego odczytu.

### 2.1. Konkretne ograniczenia jakości w obecnym potoku

1. `Tools/asset_pipeline/core/brief.py` ma `PROMPT_LIMIT = 800`. Prompt modelu kończy się przycięciem tekstu; przy długim opisie część stylu lub ograniczeń może się nie zmieścić. Prompt tekstury rezerwuje miejsce dla końcowego opisu zużycia. Potrzebna kontrola rzeczywistego payloadu.
2. `config/pipeline.json` ustawia wiele tekstur domyślnie na 1024, część na 512, stworzenia na 2048. Dopisanie „4K” w opisie nie zastępuje konfiguracji rozdzielczości i importera.
3. Globalny szablon weathering jest pusty. Stan i wykończenie powierzchni wynikają z konkretnego briefu, materiału i historii przedmiotu; nie należy ponownie narzucać jednego wieku wszystkim assetom.
4. Automatyczna walidacja geometrii i importu nie rozstrzyga, czy asset jest charakterystyczny, wiarygodny i artystycznie dobry.
5. Dokumentacja pipeline'u rozdziela gotowy prefab i jego podpięcie do gry. Dla aktualnego kodu trzeba sprawdzić faktyczne wiązanie runtime; wygenerowany plik sam nie dowodzi widocznej zmiany.
6. Obowiązuje ochrona estetyki. Nowe zlecenie wizualne musi jawnie określać dopuszczony zakres zmiany i zachowane niezmienniki. Ten dokument nie jest takim zleceniem wykonawczym.
7. Potok obsługuje także materiały `flat` dla brył i jawnych uproszczeń. Zadanie zamawiające pełne PBR nie może otrzymać odbioru końcowego po cichym zastąpieniu tego etapu materiałem płaskim. Blockout pozostaje dozwolonym, wyraźnie oznaczonym etapem wstępnym.

## 3. Narzędzia możliwe do obsługi przez Claude Code

Integracja oznacza udokumentowane CLI, API, Python lub C# wywoływane z narzędzi Claude Code. Nie każdy produkt ma oficjalny serwer MCP. Samo API nie oznacza, że adapter jest już w projekcie. Wersje i licencje sprawdzić przed instalacją; nie zakładamy zakupu ani zgody na kredyty.

| Zadanie | Wybór | Jak steruje nim Claude Code | Stan i granice |
|---|---|---|---|
| Unity, scena, prefab, Console | Jeden most Unity MCP, kandydat: CoplayDev/unity-mcp | MCP + jawne narzędzia edytorowe C# | Propozycja, nie zainstalowano. Autorzy deklarują Unity 6.x i Claude Code. Wymagany test z dokładną wersją projektu. |
| Modele, UV, retopologia, rig | Blender | `blender --background --python ...`, `bpy`, szablony Geometry Nodes | Kod przygotowania geometrii już istnieje. W tej pracy nie uruchamiano Blendera. |
| Generowanie brył i wariantów | Istniejący Meshy API/MCP | Obecny adapter i kolejka | Przydatny materiał wejściowy. Wynik wymaga korekty, kontroli topologii i historycznej. Koszt zatwierdzany dla zadania. |
| Materiały proceduralne i mapy 4K | Substance 3D Designer + Automation Toolkit | Pysbs, `sbscooker`, `sbsrender`, narzędzie bakingu | Proponowany adapter. Używać szablonów materiałowych z jawnymi parametrami. |
| Malowanie konkretnych modeli | Substance 3D Painter | Python API i remote scripting przez lokalne HTTP | Udokumentowana droga integracji; obecny etap ręczny można rozbudować. Aplikacja musi działać i być gotowa. Nie zakładać pełnego bezokiennego działania. |
| Koncepty i źródła tekstur | ComfyUI | HTTP/WebSocket, wersjonowane workflow JSON | Adapter istnieje; konfiguracja i działanie usługi wymagają próby. Dobór modelu i licencji jest oddzielny od ComfyUI. |
| Skanowane materiały i światło referencyjne | Poly Haven | Publiczne API HTTP i kontrolowany importer | Opcjonalny adapter. Weryfikować licencję assetu, zasady API, User-Agent i wymagane oznaczenie pochodzenia; nie pobrano nowych plików. |
| Proceduralne budowle, wybrzeże, prefabrykowana destrukcja | Houdini / Houdini Engine for Unity | `hython`, `hou`, HDA, cook i bake | Opcjonalne. Plugin dokumentuje obsługę Unity 6.5, ale konkretny zestaw wymaga próby. Baked wynik trafia do gry bez działającego Houdini Engine w playerze. |
| Animacja postaci i ptaków | Blender + Unity Animator; opcjonalnie Animation Rigging | Python → eksport → import C#; constrainty przez API | Animation Rigging nie występuje w odczytanym manifest.json. Nie dodawać pakietu bez zakresu i walidacji. |
| Woda, niebo, materiały i efekty | HDRP Water/Volumes, Shader Graph, VFX Graph | Własne adaptery C#, zweryfikowane szablony graphów, parametry VisualEffect | Pakiety są obecne. Claude powinien operować na publicznych API i szablonach, nie zgadywać serializacji graphów. |
| Regresje wydajności | ProfilerRecorder, testy Unity; opcjonalnie Performance Testing | C#, raporty JSON, polecenia runnera | Rozszerzenie istniejącej bramki po osobnym review; bez zmniejszania jej wymagań. |

Podstawy: [Unity MCP](https://github.com/CoplayDev/unity-mcp), [Blender CLI](https://docs.blender.org/manual/en/latest/advanced/command_line/), [Substance Toolkit](https://adobedocs.github.io/substance-automation-toolkit/), [Painter remote scripting](https://experienceleague.adobe.com/en/docs/substance-3d-dev/painter-python/tutorials/remote-control), [Houdini Engine](https://www.sidefx.com/docs/houdini/unity/about.html), [Houdini Python](https://www.sidefx.com/docs/houdini/hom/commandline), [ComfyUI API](https://docs.comfy.org/development/comfyui-server/comms_routes).

Nie rekomendujemy w tym planie produktu wyłącznie na podstawie jego materiałów promocyjnych lub nieoficjalnego MCP o nieznanych możliwościach. Nie przypisujemy narzędzi konkretnym hitowym grom bez potwierdzonego źródła produkcyjnego.

### 3.1. Minimalny sensowny zestaw

Najpierw doprowadzić do spójnego działania: obecny pipeline + Blender + HDRP + narzędzia odbioru. Następnie dodać Substance dla materiałów. Houdini włączyć, kiedy destrukcja lub proceduralne środowisko wykaże konkretną potrzebę. Nie ma potrzeby kupowania kilku systemów oceanu, kilku mostów MCP i kilku systemów pogody jednocześnie.

Most Unity MCP jest wygodnym transportem poleceń. Trwałe reguły wykonania pozostają w kodzie edytorowym, testach i manifestach, tak aby ta sama operacja była dostępna również z CLI. Połączenie potwierdzić przez odczyt wersji, sceny i Console, zanim dostanie polecenie mutujące.

## 4. Materiały 4K, które nie wyglądają jak plastik

### 4.1. Wymagany proces

1. Ustalić funkcję przedmiotu, konstrukcję, wymiary i materiały; zebrać referencje z różnych stron. Historyczna baza i artystyczny wybór mają osobne oznaczenie.
2. Najpierw zaakceptować bryłę i sylwetkę w neutralnym materiale. Detale tekstury nie naprawiają błędnej konstrukcji.
3. Przygotować geometrię high/low, UV, kierunki włókien i skalę detalu. Dobierać siatkę do wymaganych deformacji i zbliżenia.
4. Baking i materiały: albedo bez namalowanego światła, normal, roughness/smoothness, metallic, AO oraz uzasadnione height/detail.
5. Utworzyć master 4096×4096 tam, gdzie brief wymaga 4K. Osobno zweryfikować źródło, ustawienia importera i rozdzielczość faktycznie rezydentną podczas oględzin.
6. Sprawdzić model w neutralnym świetle, ostrym świetle bocznym, w porcie, na morzu i w deszczu. Dopiero potem stroić ostateczne światło sceny.

Rozdzielczość pliku nie dowodzi jakości. Rozciągnięta tekstura 1024 do 4096 nie odzyskuje automatycznie brakującej struktury materiału. Generatywne podnoszenie rozdzielczości może dodać szczegóły, lecz trzeba sprawdzić ich sens, ciągłość UV i zgodność map.

Jako materiał źródłowy można wykorzystać własną fotografię, skan lub zweryfikowaną bibliotekę. [Poly Haven](https://polyhaven.com/) opisuje skanowane, bezszwowe materiały PBR i [API](https://polyhaven.com/our-api) możliwe do obsługi przez pipeline. Skan współczesnej powierzchni jest źródłem cech materiału; nie dowodzi zastosowania tego materiału w konkretnym obiekcie starożytnym.

Obecna walidacja budżetów pozwala briefowi tylko obniżać limit kategorii. G2 wymaga więc jawnego, lokalnego profilu produkcyjnego dla zamówionego 4K i geometrii, a nie samego dopisania rozdzielczości w briefie. Nie podnosić globalnie limitów wszystkich zadań ani nie usuwać kontroli budżetu.

### 4.2. Diagnoza „plastikowości”

| Przyczyna | Co sprawdzić | Poprawka produkcyjna |
|---|---|---|
| Jednakowy połysk wszystkich powierzchni | Widoki roughness, metallic, normal | Oddzielić drewno, płótno, metal, kamień i powłoki; zmienność powiązać z materiałem. |
| Odwrócona roughness lub zły kanał | Import maski HDRP | Jawne role map, test importera i materiał kontrolny. |
| Światło namalowane w albedo | Obrót oświetlenia o 90° i 180° | Usunąć wypalone refleksy/cienie; oceniać w neutralnym świetle. |
| Brak struktury konstrukcyjnej | Sylwetka i widok bez tekstur | Osobne deski, połączenia, żebra, fałdy i krawędzie tylko tam, gdzie wynikają z budowy. |
| Całkowicie idealna geometria | Krawędzie, grubości, powtarzalność | Dodać kontrolowane ślady wykonania, nierównomierność i naprawy; zachować poprawne wymiary. |
| Brud na całej powierzchni | Maski wieku, ekspozycji i kontaktu | Powiązać zużycie z użytkowaniem; brud w zagłębieniach, przetarcia w miejscach kontaktu. |
| Brak historii jednostki | Powtarzalność floty | Trwały seed wyglądu, warsztat, naprawy, oznaczenia, zgodne z istniejącym ShipLookBook. |
| Błędny shader lub ekspozycja | Materiał neutralny, HDRP debug views | Najpierw kalibracja materiału i światła, potem grading. |

W modelu microfacet roughness opisuje statystykę mikrostruktury odbijającej światło. Nie jest ogólną regulacją „realizmu”. Podstawa: [PBRT, rozdział 9.6](https://pbr-book.org/4ed/Reflection_Models/Roughness_Using_Microfacet_Theory).

Drewno, len i kamień traktować jako dielektryki. Obszary czystego metalu odróżniać od tlenków, farby i brudu. Nie dodawać rdzy żelaznej do brązu ani rys metalu do całej powierzchni lnu. Dokładne materiały i techniki w konkretnym obiekcie wymagają źródeł; wartości shaderów są przyjętą rekonstrukcją lub kalibracją.

HDRP Mask Map: **R metallic, G ambient occlusion, B detail mask, A smoothness**. Dla mapy perceptual roughness zwykle stosuje się smoothness = 1 − roughness; konwencję źródła należy jawnie potwierdzić. Kolor albedo wymaga właściwego sRGB; maski danych nie mogą być przypadkowo przetwarzane jak kolor. Normal map wymaga prawidłowego typu, tangentów i orientacji kanałów. Sprawdzić obecny `HdrpMaterialConverter`, zanim powstanie drugi konwerter. [Dokumentacja HDRP](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.3/manual/Mask-Map-and-Detail-Map.html).

### 4.3. Co ma zapewniać shader

Zacząć od HDRP Lit, odpowiednich materiałów tkanin i sprawdzonych podgrafów. Dodać czytelnie rozdzielone warstwy: materiał bazowy, zabrudzenie, moknięcie, ślady napraw, przypalenie i uszkodzenia. Każda warstwa ma źródło danych, zakres działania i kontrolę artystyczną.

Moknięcie nie oznacza pokrycia wszystkich powierzchni jednakowym lakierem. Maskować ekspozycję na opad, poziom zanurzenia, osłonę i czas schnięcia; drewno, tkanina i metal reagują inaczej. Kałuże to lokalne zagłębienia, nie globalne lustro na każdym modelu. Odsłonięta krawędź ma pokazywać materiał wnętrza, nie tylko ciemniejszą naklejkę.

Nie edytować ręcznie nieudokumentowanego formatu `.shadergraph`/`.vfx` tylko dlatego, że plik jest tekstowy. Przygotować w Edytorze bazowe grafy i wystawić parametry używane przez narzędzia C#. Shader Graph i VFX Graph muszą przejść build docelowej ścieżki renderowania, a także kontrolę błędów wariantów i brakujących shaderów.

Deformacje żagli, roślinności i powierzchni muszą poprawnie współpracować z cieniami, bounding boxami i motion vectors używanej ścieżki HDRP. TAA nie naprawia błędnej informacji o ruchu. Odbiór obejmuje smugi za masztem, zanik detali przy ruchu kamery i migotanie piany; ostrość pojedynczego kadru nie wystarcza.

### 4.4. 4K i pamięć — konkretna skala

4096² = 16 777 216 texeli. Dla pełnej rezydencji, przed narzutami i bez streamingu:

| Format jednej mapy | Główny poziom | Z pełnym łańcuchem mipów, około |
|---|---:|---:|
| RGBA8 bez kompresji | 64 MiB | 85,33 MiB |
| BC7 lub BC5, 8 bitów na texel | 16 MiB | 21,33 MiB |

Kilka unikalnych map na każdy materiał mnoży tę wartość. Wybór kompresji, współdzielenia i streamingu ma być jawny; nie stanowi automatycznego pozwolenia na pogorszenie zamówionego obrazu. Osobno oceniać texel density i widoczność detalu. Tiling i trim sheets mogą zapewnić lepszy detal długiego kadłuba niż jedna tekstura obejmująca go w całości. Nie zakładać obsługi UDIM/virtual texturing bez prototypu konkretnej ścieżki importu i shaderów.

## 5. Modele z charakterem

Brief modelu musi zawierać konstrukcję, materiał, proporcje, zastosowanie, punkty funkcjonalne, referencje, historię zużycia i trzy rozpoznawalne cechy. Hasło „photorealistic, premium, 4K” jest niewystarczające.

Dla okrętu zachować katalogowe wymiary, dziób, wodnicę, stępkę, punkty uzbrojenia, załogi i olinowania. O wyglądzie decydują także profil dziobu, rozkład konstrukcji, ciężar wizualny rufy, sposób zamocowania żagla i wykonania poszycia. Przed generowaniem ustalić, co jest poświadczone, rekonstruowane i stylizowane.

Przebieg: referencje → bryła → wielowidokowy koncept → model → korekta Blender → UV/baking → materiały → rig → prefab → rzeczywiste podpięcie → odbiór. Warianty muszą różnić się zamówionymi cechami; zmiana losowego seeda nie gwarantuje odmiennego kierunku artystycznego.

Meshy pozostaje narzędziem wejściowym. Jego API riggingu dokumentuje przede wszystkim standardowe humanoidy; nie należy z tego wywodzić automatycznego rigowania żagli, statków, ptaków ani dowolnych stworzeń. Funkcje panelu WWW i API mogą się różnić. [Rigging API](https://docs.meshy.ai/en/api/rigging).

## 6. Automatyczna animacja assetów

Możliwy jest **automatyczny dobór, budowa i podpięcie znanych profili ruchu do rozpoznanych klas obiektów**. Nie można wiarygodnie zagwarantować poprawnego animowania dowolnej nieopisanej siatki na podstawie samej nazwy pliku.

Projektowany system składa się z: skanera semantycznego, manifestu assetu, rejestru profili ruchu, adaptera rigu, walidatora i biblioteki zweryfikowanych przykładów. Nieznany obiekt otrzymuje `NEEDS_CLASSIFICATION`; nie zostaje „ożywiony” losowym sinusoidem.

| Klasa | Wymagane dane | Ruch i integracja |
|---|---|---|
| Kadłub | Model kadłuba, wyporność/kalibracja, punkty próbne | Istniejące ShipDynamics i SeakeepingDriver, ta sama powierzchnia morza. |
| Żagiel | Maszt, reja, rogi, punkty mocowania, orientacja tkaniny | Proceduralne ugięcie/rig blisko kamery; wypieczone animacje lub prostszy shader daleko. Wspólny wiatr. |
| Wiosło | Dulka, kierunek osi, zasięg, faza napędu | Cykl wiosłowania z napędu i stanu obsady, z zanurzeniem i unikaniem kadłuba. |
| Lina i proporzec | Końce, długość spoczynkowa, kotwienie | Uproszczony łańcuch/spline; szczegółowy solver tylko w uzasadnionym zbliżeniu. |
| Roślinność | Pień, gałęzie, korzenie, sztywność, maski wierzchołków | Wspólny kierunek wiatru i opóźniona odpowiedź gałęzi; zachować istniejący MarineTreeWind. |
| Ptak | Rozpoznana anatomia, rig skrzydeł, klipy, parametry lotu | Lot, szybowanie, zakręt, lądowanie; zachowania stada niezależne od RNG rozgrywki. |
| Człowiek | Szkielet, klipy, punkty kontaktu na pokładzie | Animator i opcjonalne constrainty; podłoże uwzględnia przechył kadłuba. |
| Odłamki | Materiał, masa przybliżona, stan zdarzenia | Ograniczona fizyka prezentacyjna; pływanie/tonięcie zgodne z przyjętym profilem. |
| Stworzenie mitologiczne | Opis anatomii i jawnie fikcyjnych zdolności | Dedykowany rig. Profil mitu rozdzielony od biologicznej rekonstrukcji. |

### 6.1. Co znaczy weryfikacja naukowa ruchu

Każdy profil ma kartę źródłową: twierdzenie, autor/instytucja, data, lokalizator, odczytany zakres, niepewność, uproszczenie i test. Parametry wymyślone do strojenia opisuje się jako kalibrację. Obliczenia działają lokalnie z zatwierdzonych danych; runtime nie pyta internetu, jak ma poruszać się asset.

Mit opisuje tradycję, funkcję i cechy nadprzyrodzone. Nie jest źródłem masy, siły aerodynamicznej ani biologii nieistniejącego gatunku. Dla stworzenia: wiarygodna masa i kontakty jako artystyczna konstrukcja + osobno jawne odstępstwa fikcyjne.

### 6.2. Kryteria animacji

Sprawdzić kontakty, ślizganie stóp, penetrację siatek, zakresy stawów, objętość przy deformacji, ciągłość pętli, synchronizację wioseł, reakcję na zmianę wiatru, pauzę, tempo gry i zmianę LOD. Bliski rig nie powinien nagle przeskakiwać przy przejściu do dalekiego profilu. Widoczny ruch nie uruchamia obrażeń, przejęcia ani zużycia zasobów.

## 7. Sztorm wpływający na fizykę i obraz

**Tak, da się go zrealizować w Unity i zintegrować z Claude Code.** Projekt ma już istotną część fundamentów. Potrzebny jest jeden kontrakt danych i jego odbiór.

### 7.1. Jeden stan i jeden czas

```text
Zdarzenia scenariusza + seed + tick
              ↓
MarineState / stan pogody
    ├─ WindField → napęd, dryf, siły na żaglu
    ├─ WaveField / SeaSurface → ShipDynamics
    ├─ SeaSurfaceRenderer → HDRP Water
    └─ snapshot środowiska
         ├─ chmury, światło, mgła, deszcz, moknięcie
         ├─ żagle, liny, roślinność, załoga, ptaki
         └─ piana, kilwater, rozbryzgi, dźwięk
```

Ostateczne typy i nazwy ustalić po audycie istniejących klas. WeatherController staje się odbiorcą stanu, a nie niezależnym autorem kierunku i intensywności świata. Kosmetyczne losowania mają oddzielny generator i nie zmieniają RNG walki.

Zegar gry steruje pogodą związaną z rozgrywką. Pauza zatrzymuje jej rozwój; UI i kamera mogą działać osobno. Jeżeli scenariusz ma pogodę niezależną od czasu gry, wymaga osobnego jawnego kontraktu, bez przypadkowego użycia Time.unscaledDeltaTime.

### 7.2. Parametry i przebieg sztormu

Projektowany stan powinien obejmować: wiatr średni i porywy, kierunek, intensywność turbulencji, stan energii fal, kierunkowość, opad, zachmurzenie, widzialność środowiskową, moknięcie, fazę przejścia, cele interpolacji i identyfikatory zdarzeń. Jednostki, osie i przestrzenie współrzędnych muszą być jawne. Obecny WeatherController używa km/h dla parametrów HDRP; model morski operuje wielkościami własnego kontraktu. Konwersje wykonywać na granicy, nie w losowych klasach.

Przejście: spokój → narastanie wiatru i chmur → rozwój fal → opad i pełny sztorm → ustępowanie opadu/wiatru → wolniejsze wygaszanie fal. Opóźnienie reakcji morza powinno wynikać z modelu albo jawnej, przetestowanej aproksymacji. Nie wystarczy natychmiast zmieniać wszystkich suwaków jednym parametrem.

Podstawowy model powierzchni można opisać jako sumę składowych η(x,t)=Σ aᵢ cos(kᵢ·x−ωᵢt+φᵢ). To opis przybliżenia falowego; obecny WaveField zawiera własne procesy i nie należy zastępować go tą formułą bez analizy. Znaczenie mają widmo, głębokość, długość rozpędzania i wiatr, a nie tylko wysokość jednej sinusoidy.

### 7.3. Odpowiedź okrętu

Zachować istniejące funkcje kadłuba i sił. Wypór zależy od objętości zanurzonej: Fᵦ=ρgV. Ruch wymaga także masy, bezwładności, tłumienia, oporów i momentów. Samo ustawienie Transform na wysokości próbki wody nie jest modelem dynamiki kadłuba.

Obecny `HullForm` sam oznacza część współczynników jako kalibrację, nie pomiary. Nie wolno przedstawiać ich jako naukowo ustalonych parametrów okrętów IV w. p.n.e. D/S nie są kilogramami; fizyczna masa ładunku wymaga osobnej, jawnej rekonstrukcji lub proxy, bez zmiany znaczenia walut.

Widoczne przechyły, unoszenie i trym powinny wynikać z tego samego morza. Nawigację, ruch poziomy i trafienia nadal rozstrzyga World. PhysX można wykorzystać dla lokalnych odłamków i elementów prezentacyjnych. Przekazanie mu pełnego rozstrzygania ruchu floty byłoby przebudową architektury wymagającą osobnej decyzji.

Włączanie wpływu wiatru na ruch podlega P07 i [WM01–WM03](KONTRAKTY_MORZA_I_WIATRU.md). Odczytane ograniczenia MarineState — udział zmiany prędkości ±25%, udział prądu do 20% prędkości bazowej — opisują zastaną implementację i wymagają jawnej interpretacji w kontrakcie. Nie są naukowym pomiarem osiągów. Wywracanie jednostek, zalewanie, łamanie masztów i nowe kary bojowe są dodatkowymi mechanikami, wymagającymi odrębnego zlecenia.

Weryfikacja odpowiedzi: fale regularne i nieregularne, różne kierunki kursu, warianty obciążenia, odpowiedź swobodna, ograniczenia modelu. [ITTC, Seakeeping Experiments 7.5-02-07-02.1](https://www.ittc.info/media/11888/75-02-07-021.pdf) jest wzorcem metodyki badań; nie certyfikuje naszego modelu ani jego historycznych parametrów.

### 7.4. Zgodność renderowania

Istniejący renderer przenosi składowe symulacji do HDRP i wyłącza własne duże pasmo wody, gdy używany jest decal. Nie włączać drugiej niezależnej dużej fali, która rozjedzie się z ruchem kadłuba. Drobny detal renderera może pozostać dekoracyjny, jeśli nie udaje fali rozstrzygającej ruch.

Piana i rozbryzgi muszą wynikać z prędkości względem wody i kontaktów z powierzchnią. Dla próbkowania wody w VFX używać potwierdzonego API 17.3. Dokumentacja opisuje wiązanie danych globalnych i ograniczenie do jednej próbkowanej powierzchni jednocześnie; uwzględnić to przy dodatkowych akwenach. [HDRP Water/VFX](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.3/manual/water-vfx-interaction.html).

Nie obiecujemy pełnej symulacji CFD całego morza. Dla szczególnych ujęć można przygotować wypieczone symulacje rozbryzgów; rozstrzygający stan gry pozostaje w modelu runtime.

## 8. Dynamiczna pogoda, deszcz, niebo i ptaki

### 8.1. Niebo i fronty

Rozszerzyć istniejący WeatherController o adaptery HDRP Volumes: chmury wolumetryczne lub warstwy chmur, mgła, słońce i ekspozycja. Zapisać wpływ frontu na konkretne parametry. Chmury powinny zmieniać kształt, przemieszczać się i rzucać cienie tam, gdzie dana funkcja jest włączona w profilu. Wymaga to ustawień HDRP Asset, Frame Settings i Volume — samo dodanie skryptu nie wystarcza. [HDRP Volumetric Clouds](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.3/manual/create-realistic-clouds-volumetric-clouds.html).

Oświetlenie i widzialność wizualną rozdzielić od mgły wojny. Zmniejszenie jasności lub włączenie wolumetrii nie może samodzielnie zmienić wiedzy AI. Jeśli pogoda ma ograniczać zasięgi wykrywania, należy zaprojektować i zapisać tę regułę dla gracza i AI.

### 8.2. Deszcz i moknięcie

VFX Graph: objętość opadu wokół obserwowanego obszaru, kierunek ze wspólnego wiatru, rozbryzgi na wodzie i pobliskich powierzchniach. Efekt ma pozostać zakotwiczony w świecie podczas ruchu kamery. Obsłużyć dachy, osłony, warstwy kolizji i brak emisji we wnętrzu zamkniętego hangaru.

Moknięcie to osobny stan materiałowy z narastaniem i wysychaniem; część pod dachem nie moknie identycznie jak pokład. Odległe krople mogą być uproszczone, ale kierunek deszczu i opowieść pogodowa muszą pozostać spójne. Detal VFX nie zmienia kosztów, celności ani prędkości statków.

### 8.3. Ptaki

Zaprojektować stada na trasach nad wybrzeżem, punktach żerowania i odpoczynku. Dla bliskich ptaków używać rigu i klipów lotu/szybowania; dla dalekich dobrać prostszy wariant po ocenie sylwetki. Sterowanie stadem może wykorzystywać lokalne oddzielanie, wyrównanie i spójność — [model Reynoldsa](https://www.red3d.com/cwr/boids/). To model animacyjny, nie dowód dokładnej biologii gatunku.

Nie stosować bezmyślnego all-pairs dla wielkich stad; rozważyć siatkę sąsiedztwa, a Burst/Jobs dopiero po profilu. Dodać unikanie masztów i skał, reakcję na pogodę i brak gwałtownego przenikania przez obiekty. Wybór gatunku, sezonu i zachowania wymaga osobnego źródła przyrodniczego; współczesna obecność gatunku nie jest automatycznym dowodem dla IV w. p.n.e.

Dla licznych dalekich dekoracji można zbadać GPU instancing i wypieczoną animację wierzchołków (VAT), z osobnym materiałem HDRP i testem przejścia do bliskiego rigu. VAT nie jest obowiązkowym nowym pakietem ani zamiennikiem animacji kontaktowej załogi na pokładzie.

Ptaki domyślnie dekoracyjne nie mogą ujawniać niewidocznego wroga przez reakcję na jego tajny stan. Mitologiczny zwiadowca lub omen jest jawną, oddzielnie zatwierdzoną mechaniką.

### 8.4. Mitologiczna warstwa środowiska

Motyw sztormu wywołanego przez Posejdona można oprzeć na tradycji literackiej: [Odyseja, księga V](https://www.gutenberg.org/files/1727/1727-h/1727-h.htm). W tłumaczeniu Butlera używane są łacińskie odpowiedniki imion; projekt stosuje nazwy greckie. Odczyt mitu nie dowodzi fizycznego działania bóstwa ani lokalizacji wydarzeń w Attyce IV w.

Zjawisko nadprzyrodzone ma własne ID, zasięg, zapowiedź, czas i zasady gry. Jego wizualna ekspresja może świadomie odbiegać od fizyki; odchylenie opisujemy jako fikcję. Nie obchodzimy przez VFX kontraktów mocy, odporności i widoczności.

## 9. Kompleksowa destrukcja środowiska

Docelowo połączyć cztery skale: uszkodzenie powierzchni, odspajanie elementów, utratę podparcia konstrukcji oraz końcowy stan ruin/wraku.

1. W Blenderze lub Houdini przygotować grupy materiałowe i segmenty konstrukcji: kamień, drewno, dach, maszt, olinowanie. Kierunek pękania drewna ma odnosić się do jego struktury, a nie być przypadkowym cięciem identycznym jak skała.
2. Przygotować warianty geometrii i powierzchni przekroju, ID fragmentów, graf podparcia, punkty łączeń i profile emisji pyłu/odłamków.
3. Rozszerzyć istniejący StructureGraph i DestructionBook. Jedno rozstrzygające zdarzenie logiczne może dać wiele efektów wizualnych, ale nie wielokrotne obrażenia.
4. W pobliżu kamery uruchamiać ograniczoną liczbę dynamicznych odłamków; dalej stosować wypieczone lub uproszczone sekwencje. Usypane rumowisko ma zostać widoczne, nie natychmiast znikać bez uzasadnienia.
5. Jeśli gruz blokuje ruch, przeszkoda musi mieć reprezentację w World, ID, wpływ na trasę i zapis. Losowy collider odłamka prezentacyjnego nie jest wiarygodnym autorem nowej reguły nawigacji.
6. Po wczytaniu odtworzyć stan uszkodzeń, bez odtwarzania wszystkich eksplozji z historii. Prezentacyjny plik `.destruction.json` nie zastępuje zapisu obiektu mającego znaczenie dla rozgrywki.

Houdini dokumentuje materiałowe pękanie, constrainty i przygotowanie destrukcji. W tym projekcie proponujemy wykorzystanie ich do authoringu i bakingu; nie przenosimy dowolnej symulacji RBD do każdego obiektu w runtime. [Dokumentacja destrukcji](https://www.sidefx.com/docs/houdini/destruction/index.html).

Woda, ogień i dym wymagają również kontroli przenikania, widoczności pod mgłą wojny, przepełnienia puli i odtworzenia po zapisie. Nie dodawać automatycznie rozległego niszczenia topografii wyspy: zmiana dna i linii brzegowej wpływa na Bathymetry, WaveField, nawigację i zapis.

## 10. Projektowane skrypty i narzędzia

**Wszystkie nazwy w tabeli są propozycjami odpowiedzialności. Pliki nie zostały utworzone.** Gdy odpowiedzialność jest już realizowana, rozszerzyć istniejącą klasę zamiast dodawać równoległy system.

| ID | Narzędzie / odpowiedzialność | Integracja z tym, co istnieje | Wynik odbioru |
|---|---|---|---|
| T01 | `InspectArtCapabilities.ps1` | Odczyt pakietów, wersji, adapterów i narzędzi; wykorzystać istniejące capabilities | Raport bez sekretów i bez deklaracji „działa” na podstawie samej ścieżki. |
| T02 | `ArtRequestCompiler.py` | Rozszerzenie `core/brief.py` | Pełny brief + payload dla konkretnego dostawcy + raport pominięć/limitów. |
| T03 | `ArtChangeScopeValidator.py` | Kontrakt zmiany z dokumentu odbioru | Dokładne dozwolone pliki/kategorie, chronione elementy, zgodność z poleceniem użytkownika. |
| T04 | `BuildAssetVariant.py` | Istniejąca kolejka i etapy geometrii/tekstur | Odtwarzalny wariant z wersją modelu, seedem, źródłami i kosztem. |
| T05 | `MaterialEvidenceValidator.cs` | Rozszerzenie HdrpMaterialConverter i AIAssetValidator | Role map, kanały, kolory danych, rozdzielczość i materiał testowy. |
| T06 | `AssetSemanticScanner.cs` + rejestr profili ruchu | Manifest komponentów i skrypty Blender | Rozpoznana klasa lub NEEDS_CLASSIFICATION; brak arbitralnych animacji. |
| T07 | `AutomaticRigAndMotion.py` | Etapy rigging/animating obecnego potoku | Rig/klipy/parametry dla obsługiwanej klasy, z walidacją kontaktów. |
| T08 | `EnvironmentStateAdapter.cs` | MarineState, WeatherController, MarineTreeWind, renderer i VFX | Jeden spójny stan i czas, bez drugiego RNG rozgrywki. |
| T09 | `WeatherScenarioRunner.cs` | Kontrolowany scenariusz przejść pogodowych | Powtarzalny przebieg pogody oraz próby pauzy i zapisu. |
| T10 | `VisualBindingAudit.cs` | AIAssetImporter, ShipVisual i ShipLook | Dowód GUID/ID rzeczywiście renderowanego modelu, shaderów i materiałów. |
| T11 | `CaptureVisualReview.cs` / `RunVisualReview.ps1` | Rozszerzenie istniejących screenshotów/showcase | Powtarzalne pary obrazów, wideo, widoki debug i metadane. |
| T12 | `ValidateVisualChange.py` | Manifesty i obrazy T11 | Kontrola zakresu zmiany, braków oraz materiał do oceny człowieka/reviewera. |
| T13 | `BakeStructuralDestruction.py` | Houdini/Blender → StructureGraph | Przygotowane fragmenty, graf, przekroje i raport. |
| T14 | `EnvironmentRegressionSuite.cs` | Istniejące testy Marine/Weather/Destruction/Looks | Zachowanie, zapis, spójność obrazu i pomiary dla wskazanego snapshotu. |

Narzędzia powinny obsługiwać planowanie bez mutacji, jawny manifest zmian, kody błędu, odtwarzanie i raporty maszynowe. Ponowienie nie może samowolnie składać płatnego zlecenia ani produkować kolejnej kopii materiału/prefabu. Zachować politykę dostawcy i istniejące mechanizmy odzyskiwania zadań.

## 11. Jak wymuszać duże, rzeczywiste zmiany wizualne

Nie ma przełącznika zwiększającego „siłę” dowolnego promptu. Skrypt może egzekwować realizację konkretnego zakresu, uruchamiać odpowiednie narzędzia i dostarczać dowody.

Procedura:

1. Użytkownik wskazuje obszary przeprojektowania i to, co zachować. Jawne zlecenie w tym zakresie ma pierwszeństwo przed starszą ogólną ochroną estetyki; pozostałe obszary pozostają chronione.
2. Powstaje `ArtChangeRequest` z testowalnym opisem: np. zmienić geometrię poszycia, materiały drewna i żagla, rig oraz pianę; zachować obrys kolizji, wymiary, sloty, UI i reguły.
3. Narzędzie sprawdza, czy proponowany patch dotyka wymaganych domen, a nie wyłącznie ekspozycji lub koloru. Nie żąda zbędnych edycji, gdy domena rzeczywiście nie wymaga zmiany.
4. Przygotowuje się warianty zgodnie z aktualnym limitem potoku, kosztami i zakresem. Odczytany limit to dwa warianty na zlecenie; trzeci wymaga odrębnej zmiany zakresu/budżetu.
5. Sprawdza się faktyczne podpięcie w grze. Ten krok eliminuje sytuację „nowe assety są na dysku, lecz renderuje się stary model”.
6. Obraz przed/po ma ten sam kadr, seed, pogodę, porę, rozdzielczość i profil. Osobne ujęcie pokazuje pełny efekt nowego kierunku.
7. Różnica obrazu jest sygnałem zmiany, nie dowodem poprawy. Nie wolno zaliczać przerysowanej ekspozycji, szumu, rozmycia lub ukrycia obiektu tylko dlatego, że zwiększa różnicę pikseli.

Pełny kontrakt i rubryka odbioru są w [BRIEF_WIZUALNY_I_ODBIOR.md](BRIEF_WIZUALNY_I_ODBIOR.md). Nie zmieniamy globalnej ochrony projektu, aby uzyskać jedną mocną zmianę.

## 12. Kontrola jakości produkcji

### 12.1. Cztery niezależne rodzaje odbioru

- Techniczny: poprawny import, brak brakujących referencji, walidacja geometrii, kanałów, rigu i zapisu.
- Zachowania: zgodność z kontraktem fizyki/animacji, pauza, tempo, widoczność i brak wpływu kosmetyki na wynik.
- Wizualny: referencje, charakter, materiały, czytelność, stabilność w ruchu; wymaga obejrzenia wyniku.
- Produkcyjny: artefakty, pochodzenie, licencje, koszt i możliwość ponowienia/rollbacku.

Odbiór jednego rodzaju nie zastępuje pozostałych. Jeden zbiorczy wynik liczbowy nie może ukryć krytycznej wady w którymkolwiek z nich.

### 12.2. Pomiary i profile

Mierzyć CPU, GPU, czas ticku, pamięć tekstur, koszt przezroczystości, liczbę aktywnych efektów, skalowanie floty i spójność LOD. Pomiar odbywa się dla wskazanego urządzenia, API, buildu, sceny i profilu; nie staje się nowym wymaganiem sprzętowym gry.

Zachować istniejące profile obrazu i brak automatycznego obniżania jakości. Wygląd i detale mogą się różnić między profilami, ale reguły i informacje taktyczne muszą pozostać zgodne. Wybór SRP Batcher, GPU instancing i MaterialPropertyBlock należy sprawdzić pomiarem; nie zakładać, że każda z tych technik działa jednocześnie dla danego materiału.

Renderowe próby muszą mieć działającą ścieżkę grafiki. `-nographics` nie daje miarodajnego dowodu wyglądu. Nie uruchamiać drugiego Edytora ani runnera dla tego samego projektu. Wideo kontroluje błędy temporalne, których pojedynczy PNG nie ujawni.

### 12.3. Ochrona istniejącej gry

Przed integracją wyznaczyć snapshot, przydzielić pliki i spisać cudze zmiany. Podczas bramki zamrozić chroniony zakres. Raporty bramki pochodzą z runnera; nie powstają z ręcznego JSON PASS. Zmiana kodu po walidacji daje nowy snapshot.

Wykorzystać istniejące testy, w tym MarineTests, WeatherControllerTests, SeakeepingDriverTests, ShipLookTests i testy destrukcji. Nazwy sprawdzić w bieżącym repozytorium i wybierać pełne nazwy testów potrzebne planowi. Nie interpretować obecności pliku testu jako wykonanego odbioru.

## 13. Etapy przyszłego wdrożenia

Te etapy nie zastępują P01–P12. `G` oznacza pracę techniczno-artystyczną. Zależna zmiana mechaniki podlega nadal właściwemu P, Q/J i katalogowi.

| Etap | Zakres | Warunek wyjścia |
|---|---|---|
| G0 — audyt wykonalności | Aktualny kod, pakiety, źródła, usługi, payloady, wiązania assetów, stan sesji | Raport AVAILABLE/UNAVAILABLE/NOT_VERIFIED; konkretny pierwszy przyrost i zakres. Bez zmian produktu. |
| G1 — warsztat odbioru | T01/T02/T10/T11, neutralna scena i powtarzalne ujęcia | Widać rzeczywiście użyty asset, jego źródła, payload, ustawienia i obraz przed/po. |
| G2 — pionowy przykład jakości | Jeden istniejący typ okrętu, materiał bazowy 4K, uzasadnione detale i rig | Odbiór sylwetki, PBR, animacji i działającego podpięcia; brak regresji gry. |
| G3 — wspólny stan pogody | Adapter Marine/Weather; zegar i zapis | Ta sama pogoda w tafli, kadłubie, wietrze i prezentacji; stabilny zapis/odczyt. |
| G4 — fizyczny wpływ sztormu | Istniejące siły i odbiór wpływu na ruch | P07 i WM01–WM03, uczciwość AI i brak podwójnego naliczania sił. |
| G5 — pełna prezentacja pogody | Chmury, deszcz, moknięcie, piana, spray, dźwięk | Powtarzalne przejście pogody i materiał dowodowy w ruchu. |
| G6 — biblioteka automatycznej animacji | Profile żagli, wioseł, roślinności, ptaków i załogi | Obsługiwane klasy mają rig/manifest/test; nieznane są jawnie blokowane. |
| G7 — destrukcja | Przekroje, fragmenty, podparcie, ruiny i wraki | Stan logiczny, wygląd, nawigacja i zapis pozostają spójne. |
| G8 — skalowanie i utrwalenie | Zestawy scen, różne profile obrazu, pełna integracja | Właściwa bramka, build, playtest, pomiary i dokument przekazania. |

G4 nie jest warunkiem kosmetycznego deszczu z G5; można realizować niezależne podzadania po ustaleniu kontraktu. Nie można odebrać „sztormu wpływającego na rozgrywkę” po wykonaniu wyłącznie G5. Nie certyfikować nowej kampanii przez ukończenie sceny artystycznej.

## 14. Co oznacza zakończenie planowania

Gotowy materiał określa punkt startowy, narzędzia, granice automatyzacji, zakres przyszłych skryptów, zależności i dowody odbioru. Nie oznacza instalacji narzędzi ani potwierdzenia ich działania na tym komputerze.

W zakresie planu odczytano pliki i źródła oraz napisano dokumenty. Oddzielne, wyraźne polecenie użytkownika dotyczyło usunięcia domyślnego opisu zużycia powierzchni z pipeline'u; nie uruchamia ono pozostałych etapów tego planu. Unity, import nowych assetów, generowanie, testy gry, build, pomiary i połączenie live MCP: **NOT_RUN**. Sesji, modeli agentów i hooków nie zmieniano.
