# Morza Bogów — środowisko i punkt techniczny

Stan sprawdzony 11 listopada 2026 na commitcie `81ec638`. Potwierdzaj go względem plików przy wznowieniu.

- Unity `6000.3.28f1`, rewizja `8f082d2fe476` — `ProjectSettings/ProjectVersion.txt`.
- HDRP, Shader Graph i VFX Graph `17.3.0`; Burst `1.8.31`; Input System `1.20.1`; Unity Test Framework `1.6.0` — `Packages/manifest.json`.
- Oddzielne assembly symulacji, prezentacji, ustawień, edytora i testów.
- Logika czystego C#, 30 Hz, D/S w setnych; wejście klawiatury i myszy przez `Presentation/Hud/GameInput.cs`.
- Zapis 18 obejmuje m.in. stan morza i rozszerzoną logistykę. Wpływ morza na ruch domyślnie wyłączony.
- Windows build: `Builds/M1/MorzaBogow.exe`; narzędzie edytorowe `BattleSceneBuilder`.
- Nie wykryto udostępnionego w tej sesji narzędzia Unity MCP; audyt wykonano na plikach i istniejących raportach.

Najnowsze odczytane dowody testów i buildu: [ANALIZA_STANU.md](ANALIZA_STANU.md). W tej pracy nie uruchamiano Unity ani pomiaru GPU. Sprzęt należy podawać dla konkretnego pomiaru, bez przypisywania wyników do niezweryfikowanej konfiguracji.

Kierunek mechanik: [NOWY_PLAN_GRY.md](NOWY_PLAN_GRY.md). Zasady i polecenia: [CLAUDE.md](../CLAUDE.md). Plan nie zmienia pakietów, ustawień, scen ani estetyki.
