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
| **Meshy API** | Text-, Image- i Multi-image-to-3D, retekstura, remesh, szkielet, animacje | dokumentacja docs.meshy.ai sprawdzona 2026-11-10; **bez próby na żywo** (brak klucza) | `needs_configuration`: `MESHY_API_KEY` |
| **ComfyUI** | koncept, tekstury | trasy serwera z dokumentacji (`/prompt`, `/history`, `/queue`, `/view`); **bez próby na żywo** | `needs_configuration`: `COMFYUI_URL` i własny workflow |
| **Blender** (headless) | geometria: osie, skala, punkt zaczepienia, czyszczenie, UV, decymacja, LOD, eksport FBX i GLB | wywołanie CLI z podręcznika 5.2; **skrypt `blender/prepare.py` nigdy nie był uruchomiony** (Blender niezainstalowany) | `needs_configuration`: `BLENDER_PATH` |
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

- **Python:** `cd Tools && python3 -m unittest discover -s asset_pipeline/tests -t .` (w WSL). 42 testy:
  - brief;
  - kolejka i worker: etapy, czekanie, ponowienia, `Retry-After`, konfiguracja, dzierżawy, anulowanie, błąd wewnętrzny;
  - odzyskiwanie Meshy;
  - ComfyUI;
  - inspekcja eksportu okrętu (łańcuch LOD) i konwersja OBJ → GLB;
  - ocena raportu;
  - MCP i CLI;
  - lokalna ścieżka do importu z atrapą Unity.
- **Unity EditMode** (`MorzaBogow.Tests.AIAssets`: 19 testów potoku i 5 testów `GreekShipsTests`, na obiektach w pamięci, bez zapisu assetów; do tego 7 testów PlayMode `ShipLookTests`):
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
- Prefab: `Assets/MorzaBogow/Art/Generated/ship_ram_blockout/ship_ram_blockout.prefab`.

## Ograniczenia

- **Okręt z generatora to nie jest grafika docelowa.** To ta sama geometria, którą gra rysuje w bitwie (`GreekShips`), z płaską paletą bez tekstur PBR. Jest wejściem dla retekstury Meshy (`ArtWorkspace/briefs/ship_ram_retexture.json`, 10 kr.). Gra buduje okręty w locie z tego samego generatora, więc prefab nie musi być podpięty do `ShipVisual`.
- **Meshy, ComfyUI i Blender** nie zostały uruchomione na żywo: brak klucza, serwera i programu. Kod odpowiada ich dokumentacji z 2026-11-10. Pierwsze prawdziwe uruchomienie może wymagać poprawek. Pola wyników szkieletu i animacji Meshy są odczytywane ogólnie: wszystkie linki do FBX i GLB w odpowiedzi.
- **Kierunek dziobu** modelu z Meshy jest nieznany, gdy brief ma `forward: auto`. Unity bierze dłuższą oś poziomą z dodatnim zwrotem, a raport ostrzega o konieczności sprawdzenia podglądu.
- **Bez Blendera** model z Meshy idzie jako jeden poziom LOD z mesh LOD importera Unity. Naprawa przekroczonej geometrii wymaga Blendera.
- **Substance, DeepMotion i Cascadeur** działają tylko przez przekazanie ręczne.
- **Podgląd** wymaga urządzenia graficznego: batch bez `-nographics`.
