---
name: asset-pipeline
description: Potok assetów Morza Bogów (generowanie modeli 3D, tekstur PBR, prefabów HDRP z LOD i ich walidacja; Meshy, ComfyUI, Blender, Unity). Użyj, gdy użytkownik prosi o nowy model, okręt, budynek, skałę, teksturę lub prefab z briefu, o stan zadania generowania assetów, wznowienie, reteksturę albo walidację wygenerowanego prefabu.
---

# Potok assetów Morza Bogów

Dokumentacja: `docs/ASSET_PIPELINE.md`. Kod: `Tools/asset_pipeline` (Python w WSL) i `Assets/MorzaBogow/Editor/AIAssets`.

## Zasady, których nie wolno obejść

- **Koszty:** nigdy nie uruchamiaj `ap approve` ani `ap create --approve-credits`, dopóki użytkownik w czacie nie zgodzi się na konkretną kwotę kredytów dla konkretnego zadania. Najpierw podaj szacunek: `ap status <zadanie>` → `kredyty`. Bez zgody zadanie stoi w `needs_review`, i tak ma zostać.
- **`--resubmit`** wysyła płatne zadanie ponownie. Używaj go tylko po zgodzie użytkownika i po sprawdzeniu w panelu dostawcy, że poprzednie zadanie nie istnieje.
- **Klucze:** nie wypisuj wartości zmiennych (`MESHY_API_KEY` i innych). Podawaj tylko, czy są ustawione: `ap capabilities`. Klucze nie trafiają do briefów, plików ani wiadomości.
- **Pobieranie programów** (np. Blendera) wymaga zgody użytkownika. Podaj nazwę pliku, źródło i rozmiar.
- **Zakres:** bez scenariuszy, map, celów misji i nowych mechanik. Własne nazwy i wygląd. Okręty mają wymiary z `ShipCatalog`.
- **Wspólny folder z innymi sesjami:** import Unity sam czeka, gdy działa bramka jakości albo inny edytor. Nie zmieniaj plików w `Assets`, `docs` i `.claude`, gdy działa cudza bramka.

## Polecenia

Windows: `Tools\asset_pipeline\ap.ps1 …`. WSL: `python3 Tools/asset_pipeline/ap.py …`. Serwer MCP `morza-bogow-assets` daje to samo bez zatwierdzania kosztów.

```text
ap capabilities                       co jest skonfigurowane
ap template ship --ship-class Ram     szablon briefu
ap create ArtWorkspace/briefs/x.json --run
ap status <zadanie>                   stan, zadania zdalne, kredyty, zdarzenia
ap resume <zadanie>                   po konfiguracji albo przeglądzie
ap import|validate <zadanie>          ponowny import lub walidacja
```

Stany:
- `ready`: gotowe;
- `needs_configuration`: brakuje programu albo zmiennej; pole `needs` mówi, czego;
- `needs_review`: decyzja człowieka (koszt, przekazanie ręczne, wynik walidacji);
- `failed`: błąd.

## Przykłady

**„Zrób bryłę bombowca do testów”**
1. Utwórz `ArtWorkspace/briefs/ship_bomber_blockout.json`: `category: ship`, `shipClass: Bomber`, `generation.provider: blockout`, `texturing.provider: flat`.
2. Uruchom `ap create … --run`. Koszt: 0 kr.
3. Podaj stan, liczby z `validation.json` i obejrzyj `preview.png`.

**„Wygeneruj w Meshy nowy taranowiec”**
1. Brief `ArtWorkspace/briefs/ship_ram_meshy.json`.
2. `ap create …` bez `--approve-credits`.
3. Powiedz użytkownikowi, ile kosztuje: 30 kr. (podgląd 20 + refine 10) i że Blender jest potrzebny do LOD i naprawy geometrii.
4. Zapytaj o zgodę. Dopiero po zgodzie na kwotę: `ap approve <zadanie> --credits 30`, potem `ap resume <zadanie>`.

**„Oteksturuj bryłę taranowca w Meshy”**
- Brief `ship_ram_retexture.json`: bryła i `meshy_retexture`, 10 kr. Dalej tak samo jak wyżej.

**„Mam własny model okrętu”**
- `generation.provider: source`, `generation.model: ArtWorkspace/incoming/x.fbx`, `forward` zgodny z dziobem modelu (`+Z`, `-Z`, `+X`, `-X`).
- Bez Blendera model musi już mieścić się w budżecie trójkątów i mieć UV.

**„Zadanie stoi w needs_review”**
1. Przeczytaj `ap status`.
2. Odpowiedni ruch:
   - koszt: zapytaj użytkownika o zgodę;
   - przekazanie ręczne: podaj folder `inbox/<etap>` i oczekiwane nazwy plików;
   - walidacja: pokaż niespełnione punkty raportu i podgląd;
   - nieznane zadanie zdalne: poproś o sprawdzenie panelu Meshy przed `--resubmit`.
