# Ochrona wody w odświeżeniu wizualnym

Data: 2026-11-10. Woda jest poza zakresem odświeżenia wizualnego (decyzja w poleceniu odświeżenia). Odświeżenie może:
- korzystać z istniejących zapytań do wody (`IWaterHeights`, `SeaSurface.Sample`);
- wyzwalać istniejące efekty przez ich zdarzenia.

Zmiana otoczenia może zmienić to, co odbija się w wodzie. To nie jest zmiana systemu wody.

## Co jest chronione

Strażnik `Tools/visual/water_guard.py` zapisuje odciski SHA-256 w `docs/art/WaterProtection.json` (76 części, baseline z 2026-11-10).

**Całe pliki:**
- **Symulacja morza:** `Runtime/Simulation/Marine/*`, `World.Marine.cs`.
- **Prezentacja tylko wodna:**
  - `SeaSurfaceRenderer`, `WeatherController`, `HullWaves`, `WaterBatchSampler`, `SeakeepingDriver`, `WaterDetail`;
  - `SeaSurfaceHeights.compute`, `SeaHeightsDecal.mat`;
  - dekale fali dziobowej i piany, rozprysk (`Placeholder_Spray`, `Placeholder_SprayDot`);
  - dno i brzeg widoczne przez wodę (`Shore_Seabed`, `Shore_Limestone`, `Placeholder_Sand`).
- **Ustawienia wspólne z resztą obrazu:**
  - `HDRP_*.asset` (woda, dekale, wykluczenia), `Volume_*.asset` (ekspozycja, tonemapping, kolor, mgła, niebo);
  - `DefaultSettingsVolumeProfile`, `HDRenderPipelineGlobalSettings`;
  - `QualitySettings`, `GraphicsSettings`.

  Baseline ujmuje je w stanie drzewa roboczego z 2026-11-10, z niezacommitowanymi zmianami innej sesji: flagi światła liniowego i temperatury barwowej oraz LOD bias Ultra.

**Fragmenty plików mieszanych:**
- **`BattleController`:**
  - całe metody: `ApplyMarineSea`, `StepSeakeeping`, `CompareRenderedSea`, `CompareHullWaterlines`, `KeepWaterOut`, `DryHullMesh`, `RenderSea`, `UpdateWaterPoses`, `BreakOnRock`;
  - tylko linie wodne: `Awake`, `StartMatch`, `BuildStaticViews`, `PresentFrame`.
- **`ShipVisual`:**
  - całe metody: `CreateSpray`, `WaterDecalAt`, `ApplyDraft`, `SetWaterPose`, `SetPhysicalPose`, `Splash`, `BreakingFor`, `EmitSpray`;
  - tylko linie wodne: `Create`, `ResetForReuse`, `Animate`.
- **`ShoreShape`:** `Shelf`, `Beach`, `SurfPoints`, `SurfSpacing`, `SurfSize`.
- **`BattleEffects`:** `WaterColumn`.
- **`BattleSceneBuilder`:**
  - blok słońca, globalnego Volume i oceanu (`Run`);
  - metody `WaterDecalMaterial`, `SprayMaterial`, `SprayTexture`.
- **`M0Bootstrap`:** `EnsurePipelineAsset`, `ConfigureQualityLevels`, `EnsureVolumeProfile`.

„Linie wodne” to linie metody zawierające słowa związane z wodą: woda, kilwater, piana, rozprysk, morze, fala, dekal, zanurzenie, wypór, wykluczenie, pogoda. Zwykła zmiana tej samej metody nie uruchamia strażnika.

## Zasady

- Przed każdym etapem i po nim: `python3 Tools/visual/water_guard.py check` (WSL).
  - Kod 1 wymienia zmienione części. Wtedy czytam ich diff.
  - Zmianę wody cofam albo zgłaszam. Uzasadnioną cudzą zmianę (np. sesji wiatru i fal) zapisuję w nowym baseline z opisem.
- `M0Bootstrap.Run` nie uruchamiać. Kasuje i tworzy na nowo `Volume_*` z nowymi GUID oraz zmienia listę scen.
- Bez zmian ekspozycji, gradingu, profili Volume, słońca i jakości tylko dla nastroju.
- Nowe efekty mają własne materiały. Nie biorą `Placeholder_Spray` ani dekali wody i oszczędzają limit 2048 dekali.
- **Kadłub zachowuje:**
  - `Model` z linią wody na y = 0;
  - wartości `Length`, `Beam`, `Draft` i `DeckHeight`, od których zależą wykluczenie wody, dekale i ruch kadłuba.
- **Testy wody zostają zielone:** PlayMode `WaterInteractionTests`, `WaterDetailTests`, `WaterBatchSamplerTests`, `SeakeepingDriverTests`, `WeatherControllerTests` oraz EditMode `WaterDecalMaterialTests` i `MarineTests`.
- **Raport dymny przed i po:** `seaCorrelation`, `hullWater*` i `seaHeightRatio` w kontrolowanych warunkach.

Testy samego strażnika: `python3 Tools/visual/tests/test_water_guard.py` (8 testów).
