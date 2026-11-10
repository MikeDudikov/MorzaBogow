# Morza Bogów — potok assetów (brief → prefab HDRP)

Data: 2026-11-10. Kod: `Tools/asset_pipeline` (Python 3.11+ w WSL, tylko biblioteka standardowa) i `Assets/MorzaBogow/Editor/AIAssets` (Unity 6000.3.28f1, HDRP 17.3).

## Zakres

Przebieg:
1. brief tekstowy lub obrazowy;
2. koncept;
3. model 3D;
4. geometria, UV i LOD;
5. tekstury PBR;
6. szkielet i animacje;
7. materiały HDRP i prefab Unity;
8. walidacja.

Potok niczego nie dodaje do rozgrywki: nie ma w nim scenariuszy, map, celów misji ani nowych mechanik. Gotowy prefab leży w `Assets/MorzaBogow/Art/Generated/<assetId>/`. Gra go jeszcze nie używa: podpięcie modeli okrętów do `ShipVisual` to osobny krok.

## Macierz możliwości

Stan na 2026-11-10. Polecenie `ap capabilities` pokazuje bieżący stan konfiguracji: zmienne środowiskowe tylko jako „ustawiona / brak”, bez wartości.

| Usługa | Etapy | Weryfikacja | Stan |
|---|---|---|---|
| **Okręt z generatora gry** (`GreekShips` eksportowany przez Unity, `providers/blockout.py`) | generowanie | uruchomiony: testy i przebieg E2E | dostępny |
| **Unity 6000.3.28f1** (batch, `AIAssetBatch`) | import, materiały HDRP, prefab, LOD, walidacja, podgląd | uruchomione: 19 testów EditMode i przebieg E2E | dostępne |
| **Meshy API** | Text-, Image- i Multi-image-to-3D, retekstura, remesh, szkielet, animacje | dokumentacja docs.meshy.ai sprawdzona 2026-11-10. Na żywo 2026-11-10 tylko retekstura: 2 zadania po 10 kr. Pozostałe tryby bez próby na żywo | dostępny po ustawieniu `MESHY_API_KEY` |
| **Meshy MCP** (oficjalny `@meshy-ai/meshy-mcp-server@0.6.10`, 28 narzędzi) | wszystkie zlecenia Meshy, w tym `text_to_image`, `uv_unwrap`, `text_to_motion`, `check_balance` | Działa 2026-11-10: `initialize`, `tools/list`, `meshy_check_balance`. Serwer `meshy` jest w `.mcp.json` projektu i w konfiguracji użytkownika. Klucz wczytuje PowerShell ze zmiennej użytkownika Windows, więc nie ma go w konfiguracji. Sesja bez wczytanych narzędzi woła serwer przez `Tools/meshy_mcp/meshy-mcp.ps1 -Tool <nazwa> -Arguments '<json>'` | dostępny |
| **ComfyUI** | koncept, tekstury | trasy serwera z dokumentacji (`/prompt`, `/history`, `/queue`, `/view`); **bez próby na żywo** | `needs_configuration`: `COMFYUI_URL` i własny workflow |
| **Blender** (headless) | geometria: osie, skala, punkt zaczepienia, czyszczenie, UV, decymacja, LOD, eksport FBX i GLB | Blender 5.2.3 LTS portable (`C:\Users\noirp\Tools\blender-5.2.3-windows-x64`, SHA-256 zgodny z blender.org). Próba `prepare.py` 2026-11-10: model źródłowy w cm, dziobem w −X → 12,00 m, stępka −0,70 m, dziób +Z, LOD 1904/665/228. Import tej próby do Unity nieuruchomiony | dostępny po ustawieniu `BLENDER_PATH` |
| **Substance 3D Painter** | tekstury | **nieweryfikowane** (brak dostępnej dokumentacji automatyzacji) | przekazanie ręczne |
| **DeepMotion / SayMotion** | animacje | **nieweryfikowane** (dokumentacja API nieosiągalna) | przekazanie ręczne |
| **Cascadeur** | szkielet, animacje | **nieweryfikowane** (skrypty działają tylko w aplikacji) | przekazanie ręczne |

**Przekazanie ręczne** wygląda tak:
1. Etap zapisuje `ArtWorkspace/jobs/<zadanie>/HANDOFF_<etap>.txt` z oczekiwanymi nazwami plików.
2. Zadanie czeka w stanie `needs_review`.
3. Człowiek wrzuca pliki do `ArtWorkspace/jobs/<zadanie>/inbox/<etap>/` i uruchamia `ap resume <zadanie>`.

Szczegóły Meshy z dokumentacji, użyte w kodzie:
- Utworzenie zadania zwraca `{"result": "<id>"}`. Stan zadania to `PENDING`, `IN_PROGRESS`, `SUCCEEDED`, `FAILED` albo `CANCELED`, z polem `progress` 0–100.
- Listy zadań przyjmują `page_num`, `page_size` i `sort_by=-created_at`. Pole `created_at` jest w milisekundach Unix.
- `model_url` (retekstura, remesh, szkielet) przyjmuje data URI `application/octet-stream`, do 100 MB. `image_url` przyjmuje data URI obrazu, do 20 MB.
- Kody błędów:
  - 429 przychodzi z `Retry-After`;
  - 429 `NoMorePendingTasks` oznacza pełną kolejkę równoległych zadań;
  - 402 oznacza brak kredytów albo plan bez API.
- Brak klucza idempotencji, dlatego potok nigdy sam nie ponawia płatnego utworzenia zadania.
- Ceny z dokumentacji, w kredytach: podgląd 20, refine 10, retekstura 10, remesh 5, szkielet 5, animacja 3 na akcję. Koszt image-to-3D (30) jest szacunkiem z konfiguracji. Rzeczywiste zużycie zapisuje pole `consumed_credits`.

## Brief (`morza-bogow/asset-brief@1`)

Wymagane pola to `id`, `category` i `description`, a dla okrętów także `shipClass`. Resztę uzupełniają wartości domyślne projektu. Szablon: `ap template ship --ship-class Ram`. Przykłady są w `ArtWorkspace/briefs/`.

```json
{
  "schema": "morza-bogow/asset-brief@1",
  "id": "ship_ram_meshy",
  "name": "Taranowiec (Meshy)",
  "category": "ship",
  "shipClass": "Ram",
  "description": "A lean Aegean warship with a low dark hull, a painted eye at the bow ...",
  "style": "hand-painted, readable from a high strategy camera",
  "references": ["ArtWorkspace/refs/ram_side.png"],
  "generation": { "provider": "meshy", "mode": "text", "forward": "auto" },
  "concept": { "provider": "none" },
  "texturing": { "provider": "generation", "size": 1024 },
  "rigging": { "provider": "none" },
  "animation": { "provider": "none", "actions": [] },
  "budget": { "triangles": 6000 },
  "lods": 3,
  "variants": 1
}
```

Najważniejsze zasady:
- **`id`**: małe litery, cyfry i `_`. Wyznacza folder `Generated/<id>`.
- **`category`**: `ship`, `ship_equipment`, `building`, `ruin`, `rock`, `vegetation`, `character`, `creature`, `material` albo `effect`. Kategoria wyznacza budżet z `config/pipeline.json`, np. okręt: 8000 trójkątów, 6 materiałów (paleta: smoła, ochra, farba, drewno, brąz, żagiel), 1024 px, 60 rendererów.
- **Wymiary okrętu** pochodzą z katalogu symulacji: długość = 2 × `Radius`, burta i zanurzenie z `ShipCatalog`. Brief nie może ich zmienić. Kopia katalogu to `config/project_dimensions.json`. Generuje ją menu *Morza Bogów → Assety AI → Eksportuj wymiary okrętów*, a test `AIAssets_DimensionsFileMatchesTheShipCatalogue` pilnuje zgodności.
- **`budget`** może budżet kategorii tylko obniżyć.
- **`generation.provider`**:
  - `meshy`: tryby `text`, `image`, `multi_image`;
  - `blockout`: okręt klasy z generatora gry (`GreekShips`), eksportowany przez Unity do OBJ (LOD 0–2) i GLB, tylko okręty;
  - `source`: własny plik FBX, OBJ albo GLB w `generation.model`.
- **`texturing.provider`**:
  - `generation`: mapy PBR z Meshy;
  - `meshy_retexture`: retekstura modelu z zachowaniem jego UV;
  - `comfyui`;
  - `substance`: przekazanie ręczne;
  - `provided`: `texturing.maps` po rolach;
  - `flat`: płaskie kolory materiałów.
- **Szkielet i animacje** tylko dla kategorii `character` i `creature`.
- **`variants`**: najwyżej 2. Każdy wariant to osobne zadanie z osobnym kosztem.

Osie i jednostki projektu: metry, oś Y w górę, dziób albo przód w +Z. Okręt ma linię wody na Y = 0 i stępkę na −zanurzenie. Inne assety stoją na Y = 0.

## Etapy i stany

Etapy: `queued → generating → preparing_geometry → texturing → rigging → animating → importing → validating → ready`.

Stany zatrzymania (`blocked_stage` zapamiętuje etap, od którego wznawia `ap resume`):
- **`needs_configuration`**: brakuje programu, zmiennej albo pliku. Pole `needs` podaje, czego.
- **`needs_review`**: decyzja człowieka. Powody:
  - zatwierdzenie kosztu;
  - przekazanie ręczne;
  - wynik walidacji;
  - zadanie zdalne o nieznanym stanie.
- **`failed`**: wyczerpane próby, błąd dostawcy albo anulowanie.

| Etap | Co robi | Bez narzędzi |
|---|---|---|
| `generating` | koncept (ComfyUI, opcjonalnie), model 3D (Meshy, bryła albo plik), pobranie wyników | Meshy: `needs_configuration` |
| `preparing_geometry` | Blender: osie, skala do wymiarów, punkt zaczepienia, scalenie wierzchołków, normalne, trójkąty, UV, decymacja do budżetu, LOD 1.0/0.35/0.12, eksport FBX i GLB. Bryła spełnia założenia od razu (sprawdza to inspekcja OBJ/GLB). | Bez Blendera model w budżecie i z UV idzie jako jeden LOD: osie i skalę ustawia Unity, a mesh LOD generuje importer Unity. W pozostałych przypadkach `needs_configuration`. |
| `texturing` | mapy z generowania, retekstura Meshy, ComfyUI, Substance (ręcznie), mapy z briefu albo kolory płaskie | jak wyżej |
| `rigging`, `animating` | Meshy (szkielet, akcje) albo ręczny eksport FBX; dla okrętów i obiektów statycznych pomijane | — |
| `importing` | Unity batch `AIAssetBatch.Import` (opis poniżej) | Unity: `needs_configuration` |
| `validating` | ocena raportu Unity: budżet, LOD, wymiary klasy, punkty, fizyka; przy przekroczonej geometrii i dostępnym Blenderze najwyżej 2 naprawy | `needs_review` |

**Import w Unity** (`AIAssetImporter`):
1. Kopiuje pliki do `Generated/<id>/{Source,Textures,Animations}`.
2. Ustawia importery:
   - modele: metry, bez kamer, świateł i colliderów;
   - tekstury: sRGB tylko dla koloru, normalne jako *Normal map*, rozmiar z budżetu.
3. Składa maskę HDRP z map metal/AO/roughness.
4. Robi jeden materiał `HDRP/Lit` na materiał źródłowy i przemapowuje na niego modele.
5. Zapisuje prefab:
   - korzeń logiczny z `GeneratedAsset`, a dla okrętów także z `ShipModel`: punkty wody `Water/*`, stanowiska `Mounts/Slot*`, wyloty `Muzzles/Slot*`, kolor strony na żaglu;
   - korzeń wizualny `Visual` z `LODGroup` nad `Model/LOD*`.

   Okręty i assety nie mają Rigidbody ani colliderów, bo ruch i trafienia liczy symulacja.
6. Przy ponownym imporcie usuwa nieaktualne pliki we własnych podfolderach. Folder i prefab zostają, więc GUID prefabu się nie zmienia.

Progi LOD to 0.25 i 0.08 wysokości ekranu. Ostatni poziom znika dopiero przy 0.002 (`lodCullScreenSize`), więc okręty zostają widoczne z najwyższej kamery strategicznej.

**Walidator** (`AIAssetValidator`):
- mierzy:
  - trójkąty na poziom LOD;
  - materiały (tylko HDRP, bez pustych slotów);
  - tekstury i ich rozmiary;
  - UV i normalne;
  - komponenty fizyki;
  - odbicia lustrzane w transformacjach;
  - granice LOD0 w przestrzeni prefabu;
  - gęstość tekseli;
- dla okrętów sprawdza punkty, liczbę stanowisk klasy, dziób w +Z i kolor strony;
- zapisuje `validation.json` i podgląd PNG 512 px z ukośnej kamery.

Decyzję podejmuje `validation/report.py` z tolerancjami: długość okrętu ±3%, stępka ±8% zanurzenia, szerokość nie mniejsza niż 0,8 burty i nie większa niż burta z wiosłami.

## Ponowienia, limity, koszty

Limity są w `config/pipeline.json`:
- `maxAttemptsPerStage` 4;
- `maxRepairAttempts` 2;
- `maxVariants` 2;
- `maxCreditsPerJob` 120 i `maxCreditsPerDay` 400 (doba krocząca);
- `maxDownloadBytes` 200 MB.

**Ponowienia.** Błąd chwilowy ponawia ten sam krok:
- odstępy są wykładnicze: 10 s × 2^(n−1), najwyżej 120 s, z deterministycznym rozrzutem ±20%;
- przy `Retry-After` serwera czeka tyle, ile każe serwer;
- `NoMorePendingTasks` czeka 60 s.

**Odpytywanie** trwającego zadania zdalnego nie zużywa prób. Odstęp to 10–120 s, rośnie z czasem trwania.

**Zatwierdzanie kosztu.**
1. Przy tworzeniu zadanie dostaje szacunek kredytów.
2. Każdy płatny krok najpierw sprawdza:
   - `zatwierdzone ≥ użyte + koszt kroku`;
   - limit na zadanie;
   - limit dobowy.
3. Bez zgody zadanie stoi w `needs_review` z gotowym poleceniem `ap approve <zadanie> --credits N`.

Zatwierdza wyłącznie człowiek w terminalu. Serwer MCP nie ma narzędzia do zatwierdzania. Skill zabrania Claude'owi używania `approve` i `--approve-credits` bez zgody użytkownika na kwotę w czacie.

## Przerwanie i wznowienie bez ponownego płatnego wysyłania

Kolejka to SQLite w trybie WAL: `ArtWorkspace/pipeline.db`, tabele `jobs`, `provider_tasks` i `events`. Worker bierze zadanie na dzierżawę (30 min). Przejmuje je dopiero po wygaśnięciu dzierżawy albo gdy proces właściciela na tym komputerze już nie istnieje.

1. **Zapis przed wysłaniem.** Każde żądanie zdalne dostaje wiersz `submitting` z podsumowaniem: prompt, typ, czas. Wiersz powstaje, zanim żądanie opuści komputer. Po odpowiedzi zapisuje się identyfikator zadania (`submitted`).
2. **Utracona odpowiedź.** Dotyczy przerwania procesu, timeoutu po wysłaniu albo 5xx przy tworzeniu. Wiersz ma wtedy stan `submitting` albo `unknown`. Następny krok nie wysyła żądania ponownie, tylko szuka zadania na liście dostawcy:
   - po typie i prompcie;
   - z `created_at` nie wcześniej niż 120 s przed zapisem.
   - Jedno trafienie: zadanie zostaje przyjęte i potok dalej je odpytuje.
   - Brak trafienia: przez 180 s czeka, bo lista może się opóźniać. Potem przechodzi do `needs_review`.
   - Kilka trafień: `needs_review` z identyfikatorami do wyboru.
3. **Ponowne wysłanie** następuje tylko na polecenie `ap resume <zadanie> --resubmit`:
   - stare zadania przechodzą w stan `orphaned`;
   - nadal liczą się do wydanych kredytów;
   - nowe wymaga zatwierdzonego budżetu.
4. **Pobieranie** zapisuje do pliku `.part` i dopiero potem zmienia nazwę. Pobrane pliki nie są pobierane drugi raz. Wygasłe wyniki (`expires_at`) dają `needs_review`.
5. **ComfyUI** jest lokalne i darmowe. `prompt_id` to uuid5 z zadania i kroku, więc utraconą odpowiedź łatwo znaleźć po identyfikatorze. Zadanie, które nie dotarło, potok wysyła ponownie.

Test `tests/test_meshy_recovery.py` sprawdza to na lokalnej atrapie API. Atrapy istnieją tylko w testach. Przypadki:
- proces zabity po wysłaniu;
- timeout po wysłaniu;
- zadanie niewidoczne na liście;
- `--resubmit`;
- 429 z `Retry-After`;
- 402;
- brak klucza;
- zatwierdzanie kosztu.

## Użycie

### CLI

Windows: `Tools\asset_pipeline\ap.ps1 <polecenie>`. WSL: `python3 Tools/asset_pipeline/ap.py <polecenie>`.

```text
ap capabilities                          macierz możliwości i konfiguracja
ap template ship --ship-class Ram        szablon briefu
ap create ArtWorkspace/briefs/x.json [--run] [--approve-credits N]
ap list [--state needs_review]
ap status <zadanie> [--json]             stan, zadania zdalne, artefakty, zdarzenia
ap approve <zadanie> --credits N         zgoda człowieka na koszt
ap prepare|texture|rig|animate|import|validate <zadanie>   ręczne uruchomienie etapu
ap resume <zadanie> [--resubmit]         wznowienie od zapamiętanego etapu
ap cancel <zadanie>
ap worker [--until-idle] [--job J] [--max-seconds N]
ap mcp                                   serwer MCP na stdio
```

### MCP

Serwer jest zarejestrowany w `.mcp.json` jako `morza-bogow-assets` i uruchamiany przez `wsl.exe -e python3 …/ap.py mcp`. Mówi JSON-RPC 2.0, jedna wiadomość na linię.

Narzędzia:
- `assets_capabilities`, `assets_brief_template`;
- `assets_create_job`, `assets_list_jobs`, `assets_job_status`;
- `assets_run`: worker w tle, wraca od razu;
- `assets_run_stage`, `assets_resume_job`, `assets_cancel_job`.

Narzędzia do zatwierdzania kosztu celowo nie ma.

### Skill

Skill `.claude/skills/asset-pipeline/SKILL.md` zawiera przykłady próśb i zasady pracy z kosztami.

### Zmienne środowiskowe

Wartości kluczy nigdy nie trafiają do repozytorium, manifestów, promptów ani logów. Logi przechodzą przez filtr, który ukrywa klucze i podpisane parametry linków.

| Zmienna | Znaczenie |
|---|---|
| `MESHY_API_KEY` | klucz API Meshy |
| `COMFYUI_URL` | adres serwera ComfyUI, np. `http://127.0.0.1:8188` |
| `BLENDER_PATH` | `blender.exe` (4.2 LTS lub nowszy) |
| `UNITY_PATH` | domyślnie `C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe` |

Python działa w WSL, a zmienne ustawia się w Windows. `ap.ps1` i `.mcp.json` przekazują je przez `WSLENV=MESHY_API_KEY/u:COMFYUI_URL/u:BLENDER_PATH/p:UNITY_PATH/p`. Flaga `/p` tłumaczy ścieżki na `/mnt/c/...`. Unity i Blender z Windows są uruchamiane przez interop WSL ze ścieżkami Windows.

Import Unity czeka (ponowienie co 120 s), gdy:
- projekt jest otwarty w innym edytorze;
- działa bramka jakości (`quality-gate.ps1`), bo ona porównuje migawkę `Assets`.

## Testy

- **Python:** `cd Tools && python3 -m unittest discover -s asset_pipeline/tests -t .` (w WSL). 44 testy:
  - brief;
  - kolejka i worker: etapy, czekanie, ponowienia, `Retry-After`, konfiguracja, dzierżawy, anulowanie, błąd wewnętrzny;
  - odzyskiwanie Meshy;
  - ComfyUI;
  - inspekcja eksportu okrętu (łańcuch LOD) i konwersja OBJ → GLB;
  - ocena raportu;
  - MCP i CLI;
  - lokalna ścieżka do importu z atrapą Unity.
- **Unity EditMode** (`MorzaBogow.Tests.AIAssets`: 19 testów potoku i 8 testów `GreekShipsTests`, na obiektach w pamięci, bez zapisu assetów; do tego 7 testów PlayMode `ShipLookTests`):
  - nazwy map i ich role;
  - maska HDRP;
  - konwersja materiału z jednym krokiem Undo;
  - prefab okrętu z LOD, punktami i bez fizyki;
  - pomiary walidatora i wykrycie fizyki oraz materiału spoza HDRP;
  - asset ogólny na ziemi w wysokości z briefu;
  - odczyt zlecenia;
  - odmowa zapisu poza `Generated`;
  - zgodność wymiarów z katalogiem.

## Przebieg end-to-end: taranowiec (bryła)

Brief: `ArtWorkspace/briefs/ship_ram_blockout.json` (bryła i kolory płaskie, bez usług płatnych). Polecenie:

```text
python3 Tools/asset_pipeline/ap.py create ArtWorkspace/briefs/ship_ram_blockout.json --run
```

**Pierwsze przebiegi** (bryła pythonowa, później usunięta): wszystkie etapy z prawdziwym Unity. Walidacja zatrzymała pierwsze zadanie przez 7 materiałów przy budżecie 4. Ponowny import usunął nieaktualne materiały, a GUID prefabu się nie zmienił.

**Przebieg okrętu z generatora gry:**
- Zadanie `ship_ram_blockout-20261110-083728-25b5` zatrzymało się w `needs_review`: długość 21,61 m przy 20 m klasy, bo wiosła sterowe wystawały za rufę. Generator poprawiono.
- **Przebieg rozstrzygający:** zadanie `ship_ram_blockout-20261110-084350-0a8e`, stan `ready`, 0 ostrzeżeń.
  - LOD: 2564, 1458 i 1046 trójkątów, progi 0,25 / 0,08 / 0,002 wysokości ekranu.
  - 5 materiałów HDRP/Lit (smoła, ochra, farba, drewno, żagiel) i 3 renderery.
  - Granice 16,02 × 12,55 × 20,02 m; stępka na −1,21 m (zanurzenie klasy 1,2 m).
  - Wszystkie kontrole walidatora spełnione.
- Prefab: `Assets/MorzaBogow/Art/Generated/ship_ram_blockout/ship_ram_blockout.prefab`. Odświeżony 2026-11-10 po zmianach generatora (wiosła z rękojeścią, stery z rumplem, pas burty): zob. niżej.

## Przebieg end-to-end: wszystkie klasy (bryła)

Briefy: `ArtWorkspace/briefs/ship_{cutter,crewdestroyer,boarder,merchant,shipkiller,bomber}_blockout.json`, w tym samym schemacie co taranowiec (generator gry, kolory płaskie, 3 poziomy LOD, bez usług płatnych). Polecenia:

```text
python3 Tools/asset_pipeline/ap.py create ArtWorkspace/briefs/ship_<klasa>_blockout.json
python3 Tools/asset_pipeline/ap.py worker --until-idle
```

**Przebiegi:**
1. **Pierwszy:** 6 zadań `failed` po 4 próbach („Projekt zajęty”), bo Unity z projektem miały otwarte inne sesje. Po zwolnieniu projektu: `ap resume <zadanie> --no-run` i `worker --until-idle`, 6 zadań w 333 s.
2. **Drugi:** Boarder, Merchant, ShipKiller i Bomber `ready`. Cutter i CrewDestroyer `needs_review`:
   - Cutter: „stępka na −0,82 m, zanurzenie klasy 0,7 m”;
   - CrewDestroyer: „−1,54 m przy 1,3 m”.

   Kadłuby kończyły się dokładnie na zanurzeniu. Głębiej sięgały pióra wioseł sterowych: od obrócenia pióra w płaszczyznę wzdłużną trzonu jego krawędź opadała ok. 0,26 m poniżej końca trzonu.

   Poprawka w `GreekShips.BuildSteering`: tam, gdzie pióro zeszłoby poniżej stępki, trzon kończy się wyżej. Czopy, osie, rumple i stanowiska sternika są bez zmian. Pilnuje tego test `Ships_SteeringOarsReachNoDeeperThanTheKeel`.
3. **Rozstrzygający:** po poprawce nowe zadania dla Cuttera i CrewDestroyera oraz dla taranowca (jego prefab był sprzed zmian generatora). Dwa wcześniejsze zadania anulowano.

Wynik: 7 klas `ready`, 0 ostrzeżeń, wszystkie kontrole walidatora spełnione; 3 renderery, materiały HDRP/Lit.

| Klasa | Zadanie | LOD0/1/2 (trójkąty) | Materiały | Granice (m) | Najniższy punkt / zanurzenie |
|---|---|---|---|---|---|
| Cutter | `ship_cutter_blockout-20261110-134832-f0ae` | 1904/902/486 | 5 | 9,68 × 7,46 × 11,97 | −0,70 / 0,7 |
| Ram | `ship_ram_blockout-20261110-134833-e00a` | 2976/1762/1302 | 5 | 16,04 × 12,53 × 20,39 | −1,20 / 1,2 |
| CrewDestroyer | `ship_crewdestroyer_blockout-20261110-134833-f1c8` | 3032/1818/1358 | 5 | 17,67 × 13,30 × 22,46 | −1,30 / 1,3 |
| Boarder | `ship_boarder_blockout-20261110-130502-9345` | 4120/2426/1926 | 5 | 24,49 × 15,29 × 26,38 | −2,20 / 2,2 |
| Merchant | `ship_merchant_blockout-20261110-130504-726a` | 4924/2038/1134 | 6 (także brąz) | 22,17 × 16,14 × 24,22 | −2,00 / 2,0 |
| ShipKiller | `ship_shipkiller_blockout-20261110-130509-ad64` | 3016/1802/1342 | 5 | 16,45 × 12,18 × 20,03 | −1,70 / 1,7 |
| Bomber | `ship_bomber_blockout-20261110-130513-dc1c` | 3052/1838/1338 | 5 | 23,99 × 16,20 × 28,12 | −2,80 / 2,8 |

Prefaby: `Assets/MorzaBogow/Art/Generated/ship_<klasa>_blockout/`. Podglądy: `ArtWorkspace/jobs/<zadanie>/preview.png`. Stanowiska broni są punktami `ShipModel`, bez modeli machin.

## Budowle portu (IV w. p.n.e.): kod zamiast Meshy

Budowle portu buduje kod (`Runtime/Presentation/PortBuildings.cs`). Podglądy z Meshy odrzucono: kolumna z bazą i ząbkami, potem jońska; dach jak blacha falista; ściana bez detalu (`docs/art/MeshyLedger.json`).

- **Szkoła Mędrców (gimnazjon):**
  - krepidoma z ciosów;
  - perystyl dorycki 4 × 6: trzon z 20 płytkimi żłobkami i entazą, echinus z tokarki, abakus; smukłość 6,3 średnicy dolnej (R-DORIC-PROPORTIONS);
  - hala w warstwach (ortostaty z kamienia, wyżej tynk) z wysokimi drzwiami ku przystani;
  - belkowanie: architraw z czerwoną tenią i błękitnymi regulami, fryz z błękitnymi tryglifami nad każdą kolumną i między nimi, gzyms z błękitnymi mutulami;
  - dach z dachówek o nachyleniu 15°, błękitne tympanony, czerwona sima i akroteria.

  Błękitne tryglify potwierdza fasada grobowców w Werginie (R-VERGINA-PELLA). Czerwona tenia, sima i błękitne tympanony to interpretacja.
- **Magazyn i domy:** cokół z kamienia, mury w 6 warstwach, ościeżnica i nadproże drzwi, okna. Dach: płaskie dachówki, kalyptery na stykach, kalenica, antefiksy na okapach, końce krokwi (R-OLYNTHUS).
- **Mury miasta:** kurtyna z regularnych ciosów z blankami; kwadratowe baszty ze strzelnicami w górnej izbie i dachem dwuspadowym (R-MESSENE). Blanki i dachówki na basztach to interpretacja.
- **Przed magazynem:** pithoi wkopane do ramion, amfory oparte o ścianę, dwie kotwice jednoramienne z drewnianym poprzecznikiem wypełnionym ołowiem (R-ANCHORS). Nie są częścią obrysu budynku (`Footprint`).
- **Budowa:** kolejne warstwy (kolumny jednej strony, warstwa muru, architraw, fryz z gzymsem) pojawiają się według postępu projektu, bez skalowania. Stojący budynek to jedna scalona siatka zamiast kilkunastu rendererów.
- **Ruina Szkoły:** kikuty kolumn z nierównym wierzchem, leżące bębny, zwęglone belki, gruz.
- **Zawalenie przy upadku osady** (budynki, które stały w zasięgu wzroku; inne od razu leżą w ruinie), 2,6 s czasu gry, pauza je zatrzymuje:
  - ściany osiadają w gruz i przechylają się, dach zapada się pierwszy i szybciej;
  - pierwszy obłok pyłu przy pęknięciu, drugi, gdy dach uderza: wtedy przez pył wyłania się ruina i lecą dachówki;
  - na końcu elementy wracają, ukryte, na swoje miejsca, więc odbudowa stawia budynek tam, gdzie stał.

  Pył to kopia materiału rozbryzgu sceny z własnym kolorem wapiennego pyłu; chroniony materiał wody zostaje nietknięty. Test `Port_AFallenTownCrumblesIntoItsRuinsInGameTime`.
- **Materiały:** `Editor/BuildingTextureBaker.cs` wypieka deterministycznie, bez pobierania:
  - ciosy wapienne 2,4 × 1,6 m (warstwy 0,4 m, bloki 1,2 m, co druga warstwa przesunięta o pół bloku; gładka obwódka wokół szpicowanego lica jako interpretacja);
  - tynk 2 × 2 m.

  Każda tekstura ma mapy koloru, normalnych i maski HDRP. Materiały `Ashlar.mat` i `Stucco.mat` leżą w `Assets/MorzaBogow/Art/Buildings/Resources/Buildings`, z tymi samymi słowami kluczowymi co głazy i kora oliwki. Tynk barwią farby: biel, błękit egipski, czerwona ochra. Na pionowych ścianach UV biegną poziomo i w górę (`MeshBuilder.Level`), więc warstwy ciosów schodzą się między blokami.
- **Podgląd** (batch z urządzeniem graficznym, bez okna gry): `-executeMethod MorzaBogow.Editor.BuildingPreview.PreviewPort`, obrazy w `artifacts-log/buildings/`.
- **Testy:** `PortMasonryTests` (4, EditMode) i dotychczasowe `PortPresentationTests`.

## Gotowe modele w grze: zestaw `AegeanArt`

Gra bierze gotowe prefaby potoku z jednego zasobu w Resources: `Assets/MorzaBogow/Art/Resources/AegeanArt.asset` (`Runtime/Presentation/AegeanArt.cs`). Dzięki temu build je zawiera, a scena bitwy nie musi być przebudowywana (jej bloki wody są chronione).

- **Wypełnienie** po każdym gotowym zadaniu: `-executeMethod MorzaBogow.Editor.AegeanArtSet.RefreshBatch` (menu: Morza Bogów → Zasoby → Odśwież zestaw egejski). Bierze `Art/Generated/rock_*` (skały) i `prop_timber_*` (stosy kłód) w porządku ścieżek, więc ta sama wyspa dostaje te same skały.
- **Brzegi:** skały z zestawu dochodzą do głazów sceny w `BattleController.ScatterBoulders`. Scena bez własnych głazów (testowa) zostaje bez nich.
- **Scalanie głazów:** `ModelOf` brał wszystkie poziomy LOD, więc każdy głaz z Poly Haven był rysowany czterokrotnie. Teraz bierze tylko najdokładniejszy poziom (`AegeanArt.FinestMeshes`).
- **Składy drewna:** stosy jodłowych kłód z potoku (po trzy obok siebie i jeden na wierzchu w każdej z dwóch sągów) zamiast walców. Sągi maleją z zasobem warstwami, wierzchnie pierwsze. Bez zestawu zostają walce.
- **Drzewa składów:** sosna alepska (`veg_pinus_halepensis_a`) i cyprys kolumnowy (`veg_cupressus_sempervirens`) z Meshy, tej samej wysokości co dawne bryły, przechylają się i padają przy ścince jak dotąd. Nie kołyszą się na wietrze (dawne też nie); kołysze się tylko gaj oliwny z generatora środowiska. Kępy przybrzeżne zostają przy `TreeModels`.
- **Kopalnia srebra:** skały z zestawu przyciemnione do łupku zamiast sześcianów za sztolnią; kosze wiklinowe z rudą przy hałdzie i moździerzach.
- **Rekwizyty** (`AegeanArt.Props`, szukane po początku nazwy, rozstawiane przez `AegeanArt.Place`): zwój liny na pomostach miejsc surowców i przy magazynie portu, kosze z rudą w Laurionie. Wszystkie `prop_*` z potoku oprócz stosów kłód.
- **Seria 3 Meshy:** kosz, lina i trzeci głaz wapienny zostały. Odrzucone: sieć (wyszła klatka z patyków) i herma (popiersie na okrągłej kolumnie z cokołem, forma rzymska; herma to czworokątny filar).
- **Testy:** `AegeanArtTests` (EditMode): zestaw zgodny z prefabami potoku, scalanie tylko LOD0.

## Ograniczenia

- **Najniższy punkt modelu jest stępką.** Przygotowanie w Blenderze stawia ten punkt na −zanurzenie, a walidator porównuje go z zanurzeniem klasy (tolerancja 8%). Wiosła sterowe, taran i inne części nie mogą schodzić poniżej kadłuba.
- **Zajęty projekt:** gdy inna sesja trzyma Unity z projektem dłużej niż 4 próby co 120 s, zadanie kończy się `failed`. Wznowienie: `ap resume <zadanie>`. Błąd kompilacji w cudzym kodzie zatrzymuje eksport tak samo.

- **Okręt z generatora to nie jest grafika docelowa.** To ta sama geometria, którą gra rysuje w bitwie (`GreekShips`), z płaską paletą bez tekstur PBR. Jest wejściem dla retekstury Meshy (`ArtWorkspace/briefs/ship_ram_retexture.json`, 10 kr.). Gra buduje okręty w locie z tego samego generatora, więc prefab nie musi być podpięty do `ShipVisual`.
- **Blender:** pierwsze uruchomienie znalazło błąd ścieżek. Blender z Windows zwraca `C:\...`, a Python w WSL doklejał je do katalogu zadania. Poprawione w `blender/__init__.py`; test `test_blender.py`.
- **Retekstura Meshy okrętu z generatora** (`ArtWorkspace/briefs/ship_ram_retexture.json`), 2026-11-10:
  - **Pierwsze zadanie** `ship_ram_retexture-20261110-144306-18fa` (10 kr.): ready, ale cały okręt w jednolitym jasnym drewnie. Generator ma UV w metrach, powtarzane i nakładające się, a Meshy przy `enable_original_uv` malował wszystkie części w te same teksele.
  - **Poprawka:** opcja briefu `texturing.originalUv: false`. Meshy rozkłada wtedy własne UV, a potok bierze zwrócony model i robi z niego LOD w Blenderze. Test `test_meshy_retexture.py`.
  - **Drugie zadanie** `ship_ram_retexture-20261110-145054-4d03` (10 kr.): ready. Ciemny kadłub, ochra wzdłuż burty, lniany żagiel, spiż na taranie; LOD 2976/1040/357; stępka −1,20 m; długość 20 m.
  - **Ostrzeżenia:** jeden materiał, więc żagiel nie przyjmie barwy strony; 32 px/m przy celu 64. Biały zygzak nieczytelny.
  - Prefab: `Assets/MorzaBogow/Art/Generated/ship_ram_retexture`. Gra go nie używa.
- **Modele z Meshy MCP przez potok** (2026-11-10):
  - `text_to_3d` w trybie smart-topology `meshy-t2.5`: 5 kr. za podgląd;
  - `text_to_3d_refine` z PBR: 10 kr.;
  - `meshy_download_model` do `ArtWorkspace/meshy/<asset>/`;
  - potem brief z `generation.provider=source` i `texturing.provider=provided`.

  Przykłady: `prop_amphora_rhodian` (LOD 1470/514/176) i `rock_attica_limestone_a` (1959/684/234), oba ready, 0 ostrzeżeń.
  - **Ocena podglądów:** Meshy dobrze robi formy organiczne (skały, ceramika). Architekturę o kanonicznych proporcjach daje błędnie: kolumnę dorycką wydał raz z bazą i ząbkami, raz jako jońską. Takie elementy powstają proceduralnie.
  - **Druga seria** (6 podglądów, 30 kr.): zostały marmur pentelicki (wychodnia ze śladami klinów) i sągi jodłowych kłód, dopracowane z PBR (20 kr.). Odrzucone:
    - wapienny słup: wyglądał jak rozbity komin;
    - łupek z Laurionu: kopuła bez warstw;
    - pithos: beczka z obręczami (anachronizm);
    - kotwica: współczesna kotwica admiralicji.

    Pithos i kotwica powstały w kodzie (`PortBuildings.AddStoreYard`). Surowe podglądy (bez tekstur, przed dopracowaniem) renderuje obok siebie Blender w trybie Workbench: `blender -b --factory-startup -P Tools/asset_pipeline/blender/preview_glb.py -- wynik.png a.glb b.glb …`.
  - **Mapy większe niż budżet** (Meshy daje 2048 px) Blender zmniejsza w kopii zadania do `textureSize` budżetu (`blender/resize_maps.py`, `core/images.py`). Repozytorium nie trzyma map, których asset nigdy nie użyje: amfora i skała razem 5 MB zamiast 25 MB.
  - **Budżet skały:** 3 renderery, bo każdy z 3 poziomów LOD to osobny renderer.
  - **LOD roślinności:** korona z dziesiątków drobnych kęp igieł nie daje się zdziesiątkować (sosna utknęła na ok. 935 trójkątach w LOD1 i LOD2, walidator: „LOD2 nie upraszcza LOD1”). Dla kategorii `vegetation` `prepare.py` odrzuca wtedy najmniejsze rozłączne części (największa, pień, zostaje zawsze), aż poziom jest lżejszy od poprzedniego: sosna 2449/932/293. Inne kategorie bez zmian: wiosła i liny okrętów muszą zostać na każdym poziomie.
  - **Wydatki:** `docs/art/MeshyLedger.json`.
- **ComfyUI** nie zostało uruchomione na żywo: brak serwera. Text-, Image-to-3D, szkielet i animacje Meshy też bez próby na żywo. Kod odpowiada ich dokumentacji z 2026-11-10. Pierwsze prawdziwe uruchomienie może wymagać poprawek. Pola wyników szkieletu i animacji Meshy są odczytywane ogólnie: wszystkie linki do FBX i GLB w odpowiedzi.
- **Kierunek dziobu** modelu z Meshy jest nieznany, gdy brief ma `forward: auto`. Unity bierze dłuższą oś poziomą z dodatnim zwrotem, a raport ostrzega o konieczności sprawdzenia podglądu.
- **Bez Blendera** model z Meshy idzie jako jeden poziom LOD z mesh LOD importera Unity. Naprawa przekroczonej geometrii wymaga Blendera.
- **Substance, DeepMotion i Cascadeur** działają tylko przez przekazanie ręczne.
- **Podgląd** wymaga urządzenia graficznego: batch bez `-nographics`.
